# 48. Listado Diario de Juegos en Tendencia (BGG Hotness), Ingesta Inmediata Satélite y Conmutador de Portada y Catálogo

> **Estado:** Implementado y Verificado  
> **Fecha:** 2026-10-01  
> **Incremento Asociado:** INC-93 (`inc/tendencias-bgg`)  
> **Pruebas Verificadas:** 2.215 unitarias en verde (2.225 totales con integración)  

---

## 1. Propósito y Contexto del Dominio

Hasta este incremento, la consulta a las tendencias mundiales de BoardGameGeek (*BGG Hotness* en `/xmlapi2/hot?type=boardgame`) se utilizaba únicamente en el lote nocturno para descubrir títulos no catalogados y depositarlos en una cola de espera. Este enfoque presentaba tres carencias arquitectónicas y funcionales:
1. **Pérdida de la serie temporal diaria:** Las posiciones del top 50 de tendencias de cada día se desechaban tras el análisis, impidiendo consultar qué juegos eran los más populares o comentados en una fecha concreta.
2. **Postergación innecesaria de títulos de moda:** Los títulos presentes en el top 50 de tendencias que no existían en la base de datos se sometían al cupo diario de catalogación (`DailyCatalogingLimit = 20`), retrasando días su disponibilidad para los usuarios en lugar de ingestarse de forma prioritaria.
3. **Rigidez editorial en portada y catálogo:** El carril principal de la portada mostraba únicamente el histórico de títulos mejor valorados sin alternativa dinámica de tendencias, y el catálogo no disponía de ordenación por popularidad actual.

El incremento **INC-93** dota a Ludeka de persistencia histórica de tendencias diarias, ingesta inmediata desacoplada con volcado en la tabla satélite de snapshots crudos e IA Gemini, conmutador accesible en portada (con «En tendencia» activo por defecto) y ordenación por tendencia en el catálogo lúdico.

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
  - `GetLatestTrendingGamesAsync(int limit, CancellationToken ct)`: Devuelve los títulos en tendencia más recientes asociados a un juego en catálogo (`GameId != null`).
  - `UpsertDailyTrendingBatchAsync(IEnumerable<DailyTrendingGame> entries, CancellationToken ct)`: Inserción o actualización idempotente del lote diario en base de datos.
  - `GetTrendingRankMapAsync(DateOnly? dateUtc, CancellationToken ct)`: Diccionario en memoria `IReadOnlyDictionary<Guid, int>` (`GameId -> Rank`) para ordenación rápida y decoración de tarjetas.
- **Implementación (`SqliteDailyTrendingGameRepository`):**
  Persistencia transaccional para SQLite en entornos locales y pruebas, y esquema DDL simétrico en `docs/database/supabase_schema.sql` para PostgreSQL en producción.

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

## 4. Experiencia de Usuario: Portada Editorial y Catálogo

### 4.1 Conmutador Editorial en Portada (`HomeDashboard.razor`)
El Carril 1 de la portada incorpora un conmutador de pestañas accesible conforme a WCAG 2.2 AA (`role="tablist"`, `aria-selected`):
- **«En tendencia» (activo por defecto):** Muestra los títulos del último snapshot de tendencias diario que están disponibles en catálogo. Cada tarjeta muestra el distintivo visual con la llama y el puesto (`#1`, `#2`...).
- **«Mejor valorados»:** Muestra los 20 juegos con mayor puntuación media histórica del catálogo.
- **Fallback Defensivo:** Si el listado de tendencias aún no se ha sincronizado en una base de datos nueva, el carril conmuta automáticamente a los mejores valorados para evitar estados vacíos indeseados.

### 4.2 Distintivo Visual en Tarjetas (`HomeGameCard.razor`)
Se extiende `GameSummaryDto` con la propiedad opcional `TrendingRank`. Cuando está presente, la tarjeta renderiza un badge compacto de alto contraste con el icono `flame` de Lucide y el número de ranking (`#1` a `#50`).

### 4.3 Ordenación por Tendencia en Catálogo (`/catalogo`)
- **Criterio de Ordenación (`GameSortOrder.Trending = 8`):** Nuevo valor en el enum de dominio para ordenar títulos según su posición en el ranking de tendencias diario más reciente.
- **Soporte Dual en `SqliteGameRepository.SearchAsync`:**
  - **Vía SQL Puro:** Realiza un `LEFT JOIN` hacia `DailyTrendingGames` filtrando por la fecha más reciente y ordenando por `coalesce(Rank, 999999) ASC`.
  - **Vía Índice en Memoria (con filtros facetados de escalabilidad o fundas):** Hidrata el mapa de rangos más reciente y ordena deterministamente los identificadores antes de paginar.
- **Interfaz de Catálogo (`Home.razor`):** Botón selector «En tendencia» con icono de llama y sincronización bidireccional con el parámetro de URL `?orden=tendencia`.

---

## 5. Verificación y Calidad

- **Pruebas de Dominio y Persistencia:** `DailyTrendingGameTests.cs`, `SqliteDailyTrendingGameRepositoryTests.cs`.
- **Pruebas de Ingesta y Orquestación:** `BggDiscoveryServiceTrendingTests.cs`, `NightlyCatalogingServiceTests.cs`.
- **Pruebas de Portada y Catálogo:** `HomeDashboardServiceTests.cs`, `SqliteGameRepositoryTests.cs`.
- **Métricas:** 2.215 pruebas unitarias pasando al 100% sin regresiones en la suite global de Ludeka.
