# 48. Listado Diario de Juegos en Tendencia (BGG Hotness), Ingesta Inmediata Satélite y Conmutador de Portada y Catálogo

> **Estado:** Implementado y Verificado  
> **Fecha:** 2026-10-01  
> **Incremento Asociado:** INC-93 (`inc/tendencias-bgg`) e INC-95 (`inc/pantalla-tendencias`)  
> **Pruebas Verificadas:** 2.249 unitarias en verde (2.259 totales con integración)  

---

## 1. Propósito y Contexto del Dominio

Hasta estos incrementos, la consulta a las tendencias mundiales de BoardGameGeek (*BGG Hotness* en `/xmlapi2/hot?type=boardgame`) se utilizaba únicamente en el lote nocturno para descubrir títulos no catalogados y depositarlos en una cola de espera. Este enfoque presentaba carencias arquitectónicas y funcionales:
1. **Pérdida de la serie temporal diaria:** Las posiciones del top 50 de tendencias de cada día se desechaban tras el análisis, impidiendo consultar qué juegos eran los más populares o comentados en una fecha concreta.
2. **Postergación innecesaria de títulos de moda:** Los títulos presentes en el top 50 de tendencias que no existían en la base de datos se sometían al cupo diario de catalogación (`DailyCatalogingLimit = 20`), retrasando días su disponibilidad para los usuarios en lugar de ingestarse de forma prioritaria.
3. **Rigidez editorial en portada y catálogo:** El carril principal de la portada mostraba únicamente el histórico de títulos mejor valorados sin alternativa dinámica de tendencias.
4. **Desacople de Catálogo vs Tendencias (INC-95):** Inicialmente en INC-93 se introdujo `orden=tendencia` en el catálogo general, pero al ser una ordenación y no un filtro exclusivo de 50 títulos, causaba confusión de volumen (500 títulos mostrados) y apertura automática indeseada de los filtros avanzados en `Home.razor`. En INC-95 se desacopla por completo, creando una pantalla dedicada `/tendencias` y limpiando el catálogo general.

---

## 2. Modelo de Dominio y Persistencia

### 2.1 Entidad `DailyTrendingGame`
Ubicada en `Ludeka.Core.Entities.DailyTrendingGame`:
- `Id` (`Guid`, PK): Identificador único inmutable de la entrada.
- `DateUtc` (`DateOnly`): Fecha UTC de la fotografía diaria de tendencias.
- `Rank` (`int`, 1..50): Posición del juego en el ranking de tendencias de ese día.
- `BggId` (`int`): Identificador del juego en BoardGameGeek.
- `Title` (`string`): Título original reportado por BGG Hotness.
- `ThumbnailUrl` (`string?`): URL de la miniatura reportada por BGG.
- `YearPublished` (`int?`): Año de publicación reportado.
- `GameId` (`Guid?`, FK opcional): Enlace a la entidad `Game` en el catálogo de Ludeka una vez catalogado.
- `CreatedAt` (`DateTimeOffset`): Marca temporal UTC de registro.
- `Game` (`Game?`): Propiedad de navegación hacia el catálogo de Ludeka.

Métodos de mutación de dominio:
- `LinkToGame(Guid gameId)`: Asocia o reasocia atómicamente la entrada con un juego catalogado.
- `UnlinkGame()`: Desvincula la entrada del juego si este es retirado del catálogo.

### 2.2 Índices y Restricciones en Base de Datos
Configurado en `LudekaDbContext`:
```csharp
modelBuilder.Entity<DailyTrendingGame>(entity =>
{
    entity.ToTable("DailyTrendingGames");
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => new { e.DateUtc, e.Rank }).IsUnique();
    entity.HasIndex(e => new { e.DateUtc, e.BggId }).IsUnique();
    entity.HasIndex(e => e.BggId);
    entity.HasOne(e => e.Game)
        .WithMany()
        .HasForeignKey(e => e.GameId)
        .OnDelete(DeleteBehavior.SetNull);
});
```

### 2.3 Contrato de Repositorio e Implementación
- **Contrato (`IDailyTrendingGameRepository`):**
  - `GetByDateAsync(DateOnly dateUtc, CancellationToken ct)`: Obtiene los 50 puestos del día ordenados por `Rank ASC` incluyendo la navegación a `Game`.
  - `GetLatestAvailableDateAsync(CancellationToken ct)`: Devuelve la fecha más reciente con datos registrados.
  - `GetPreviousDateAsync(DateOnly dateUtc, CancellationToken ct)`: Devuelve la fecha anterior más reciente a una dada con datos (incorporado en INC-95 para cálculo resiliente de deltas).
  - `GetLatestTrendingGamesAsync(int limit, CancellationToken ct)`: Devuelve los títulos en tendencia más recientes asociados a un juego en catálogo (`GameId != null`).
  - `UpsertDailyTrendingBatchAsync(IEnumerable<DailyTrendingGame> entries, CancellationToken ct)`: Inserción o actualización idempotente del lote diario en base de datos.
  - `GetTrendingRankMapAsync(DateOnly? dateUtc, CancellationToken ct)`: Diccionario en memoria `IReadOnlyDictionary<Guid, int>` (`GameId -> Rank`) para ordenación rápida y decoración de tarjetas.
- **Implementación (`SqliteDailyTrendingGameRepository`):**
  Persistencia transaccional para SQLite en entornos locales y pruebas, y esquema DDL simétrico en `docs/database/supabase_schema.sql` para PostgreSQL en producción.

### 2.4 Enum `RankMovement` (INC-95)
Ubicado en `Ludeka.Core.Enums.RankMovement`:
- `Same = 0`: Mantiene la misma posición que en el snapshot del día anterior.
- `Up = 1`: Sube puestos en el ranking (posición numérica menor).
- `Down = 2`: Baja puestos en el ranking (posición numérica mayor).
- `New = 3`: Nueva entrada en el Top 50 que no figuraba en el día anterior.

---

## 3. Ingesta Inmediata y Orquestación Nocturna

### 3.1 Pipeline de Ingesta Inmediata (`SyncDailyTrendingAsync`)
Implementado en `BggDiscoveryService` e invocado en el trabajo programado:
1. **Descarga de Hotness:** Consume `/xmlapi2/hot?type=boardgame` a través de `IBggClient.GetHotGamesAsync`.
2. **Persistencia de la Fotografía Diaria:** Mapea los ítems a instancias de `DailyTrendingGame` para la fecha `DateTime.UtcNow.Date`. Si el título ya existe en catálogo (por `BggId`), se enlaza inmediatamente asignando su `GameId`. Se persiste el lote completo mediante `UpsertDailyTrendingBatchAsync`.
3. **Ingesta Inmediata de Títulos Ausentes:**
   Para cada título del top 50 ausente en `Games`:
   - **Snapshot Satélite (`EnsureSnapshotAsync`):** Invoca a `IBggRawSnapshotSyncService.EnsureSnapshotAsync(bggId, ct)` para descargar el XML completo de BGG, convertirlo a JSON y guardarlo de forma inmutable en `BggRawSnapshots`.
   - **Descarga de Metadatos:** Obtiene los datos detallados mediante `IBggClient.GetGameByIdAsync(bggId)`.
   - **Síntesis con IA:** Genera el veredicto y resumen inteligente mediante `IAiGameSummaryService.SummarizeGameAsync`.
   - **Inserción Atómica en Catálogo:** Registra el juego en `IGameRepository.AddAsync(newGame)` y vincula la entrada en `DailyTrendingGame.LinkToGame(newGame.Id)`.
4. **Respeto de Límites de Tasa:** Incorpora pausas defensivas entre llamadas a la API externa para garantizar el cumplimiento de las políticas de BoardGameGeek.

### 3.2 Integración en el Lote Nocturno (`NightlyCatalogingService`)
En la Fase 1.5 del ciclo nocturno (`ExecuteFase1_5_DiscoveryAsync`), se ejecuta en primer lugar `RunDailyTrendingSyncAsync(ct)`. Solo una vez asegurada la ingesta de las tendencias del día se continúa con el descubrimiento secundario de lanzamientos del año y la catalogación de la cola comunitaria.

---

## 4. Servicio de Aplicación: `ITrendingService` / `TrendingService` (INC-95)

Implementa la comparativa temporal diaria:
- `GetTrendingComparisonAsync(CancellationToken ct)`:
  1. Consulta la fecha de tendencias más reciente (`GetLatestAvailableDateAsync`).
  2. Consulta la fecha previa más reciente con datos (`GetPreviousDateAsync`), garantizando resiliencia ante saltos de fecha.
  3. Mapea y calcula las variaciones de posición para cada uno de los 50 títulos.
  4. Retorna el DTO `TrendingComparisonDto` con la lista de `TrendingGameItemDto` y las fechas de referencia.

---

## 5. Experiencia de Usuario: Portada, Pantalla Dedicada y Catálogo

### 5.1 Conmutador Editorial en Portada (`HomeDashboard.razor`)
El Carril 1 de la portada incorpora un conmutador de pestañas accesible conforme a WCAG 2.2 AA (`role="tablist"`, `aria-selected`):
- **«En tendencia» (activo por defecto):** Muestra los títulos del último snapshot de tendencias diario que están disponibles en catálogo. Cada tarjeta muestra el distintivo visual con la llama y el puesto (`#1`, `#2`...).
- **«Mejor valorados»:** Muestra los 20 juegos con mayor puntuación media histórica del catálogo.
- **Enlace Directo:** El botón «Ver todas las tendencias» navega a la pantalla dedicada `/tendencias`.

### 5.2 Distintivo Visual en Tarjetas (`HomeGameCard.razor`)
Se extiende `GameSummaryDto` con la propiedad opcional `TrendingRank`. Cuando está presente, la tarjeta renderiza un badge compacto de alto contraste con el icono `flame` de Lucide y el número de ranking (`#1` a `#50`).

### 5.3 Pantalla Dedicada `/tendencias` (`TrendingGames.razor`, INC-95)
- **Top 50 Estricto:** Lista vertical editorial con los 50 títulos del snapshot del día.
- **Deltas de Movimiento:** Cada fila renderiza el indicador de variación (🔺 `+X`, `=` mantiene, 🔻 `-X`, ✨ `Entra`).
- **Navegación:** Botón superior prominente «Ir al catálogo» hacia `/catalogo`.
- **Fichas y Metadatos:** Enlace directo a `/juegos/{slug}` para títulos catalogados, miniatura, año y valoración BGG.

### 5.4 Catálogo General (`Home.razor`, INC-95)
- Retirado el botón de ordenación por tendencia del panel.
- Desacoplado `ActiveAdvancedFiltersCount` de cualquier ordenación, evitando auto-aperturas indeseadas del panel colapsable de filtros.
- Redirección defensiva de accesos heredados a `/catalogo?orden=tendencia` hacia `/tendencias`.

---

## 6. Verificación y Calidad

- **Pruebas de Dominio y Persistencia:** `DailyTrendingGameTests.cs`, `SqliteDailyTrendingGameRepositoryTests.cs`.
- **Pruebas de Ingesta y Orquestación:** `BggDiscoveryServiceTrendingTests.cs`, `NightlyCatalogingServiceTests.cs`.
- **Pruebas de Servicio de Tendencias:** `TrendingServiceTests.cs` (cálculo exhaustivo de deltas y resiliencia temporal).
- **Pruebas de Contrato Web:** `TrendingPageContractTests.cs`, `CatalogFilterContractTests.cs`, `HomeDashboardServiceTests.cs`.
- **Métricas:** 2.249 pruebas unitarias pasando al 100% (2.259 totales con integración) en la suite global de Ludeka.
