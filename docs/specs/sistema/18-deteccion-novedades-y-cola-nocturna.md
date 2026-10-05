# 18. Detección Automática de Juegos en Novedades y Cola Nocturna Inteligente BGG/Gemini

> **Estado del Módulo:** ✅ Implementado y Verificado  
> **Incrementos Asociados:** [INC-24](file:///c:/repos/Ludeka/docs/increments/archive/inc-24-nightly-game-discovery-cataloging.md), [INC-107](file:///c:/repos/Ludeka/docs/increments/archive/inc-107-ingesta-expansiones-lotes-ia.md), [INC-110](file:///c:/repos/Ludeka/docs/increments/archive/inc-110-desacoplo-batch-nocturno.md)  
> **Pruebas Automatizadas:** 2.451 pruebas globales en verde (100% de la suite)  

---

## 1. Propósito y Filosofía
Este módulo conecta la monitorización de publicaciones y noticias editoriales en español con el motor de ingesta y catalogación de Ludeka. Cuando se registra un anuncio o preventa editorial (`WeeklyRelease`), el sistema extrae automáticamente el juego o expansión involucrado, verificando si ya existe en Ludeka para vincularlo de inmediato, o consultando BoardGameGeek (BGG Search API) para encolarlo automáticamente con el origen `NewsDiscovery`.

Durante las horas de menor tráfico (03:00 UTC), el servicio de catalogación nocturna (`NightlyCatalogingService`) procesa la cola de espera de usuarios y novedades, y si el volumen es inferior al cupo diario configurado (20 títulos), completa el cupo restante hasta alcanzar 20 juegos con los títulos mejor rankeados de BoardGameGeek que todavía no forman parte del catálogo, respetando una cadencia estricta de 2.5 segundos entre llamadas para prevenir bloqueos por HTTP 429 en BGG y saturación de tokens en Google Gemini.

---

## 2. Componentes de Dominio (`Ludeka.Core`)

### 2.1 Enumerado `CatalogQueueOrigin`
```csharp
namespace Ludeka.Core.Enums;

public enum CatalogQueueOrigin
{
    UserImport = 0,     // Solicitado por usuarios en su importación de BGG
    NewsDiscovery = 1,  // Detectado y extraído automáticamente desde una novedad editorial
    TopBggBackfill = 2  // Relleno automático nocturno con el Top de BGG
}
```

### 2.2 Entidad `PendingBggImport`
- Incorpora las propiedades `Origin` (`CatalogQueueOrigin`) y `ExtractedTitle` (`string?`).
- Métodos de ciclo de vida: `MarkAsProcessing()`, `MarkAsCompleted()`, `MarkAsFailed(string error)`, `ResetToPending()`.

### 2.3 Entidad `WeeklyRelease`
- Incorpora el método de mutación `LinkGame(Guid gameId)` que permite asignar retrospectivamente la clave foránea hacia la entidad `Game` una vez identificado o catalogado.

### 2.4 Entidad `NightlyCatalogingExecutionLog`
- Modela la bitácora estructurada de cada lote nocturno o bajo demanda:
  - `StartedAt` y `CompletedAt`: Marcas temporales UTC de inicio y finalización.
  - `QueueProcessedCount`: Títulos procesados de la cola comunitaria y novedades.
  - `NewsDiscoveryCount`: Títulos descubiertos a partir de publicaciones editoriales.
  - `TopBackfillCount`: Títulos completados procedentes del Top mundial de BGG.
  - `TotalCatalogedCount`: Total de títulos nuevos incorporados al catálogo.
  - `FailedCount`: Número de fallos registrados durante el lote.
  - `CatalogedTitlesJson`: Serialización JSON de los títulos catalogados.
  - `Status`: `"Running"`, `"Completed"`, `"Failed"`.

---

## 3. Capa de Aplicación (`Ludeka.Application`)

### 3.1 Extractor de Novedades (`INewsGameExtractor` y `NewsGameExtractor`)
- Analiza sintáctica y semánticamente titulares mediante expresiones regulares ordenadas por especificidad:
  - `"anuncia (la edición en castellano de|el lanzamiento de|la llegada de|la reimpresión de) (?<title>[^.]+)"`
  - `"publicará (?<title>[^.]+) en español"`
  - `"lanzamiento de (?<title>[^.]+)"`
  - Citas explícitas: `"Apiary"`, `«Harmonies»`, `“Wingspan”`.
  - Descarte inteligente de palabras vacías (*stop-words*) como "Lanzamiento", "Novedad", "Juego".
- **Comprobación local:** Si el título extraído existe en `IGameRepository`, vincula `WeeklyRelease.LinkGame(game.Id)`.
- **Comprobación remota:** Si no existe en Ludeka, invoca `IBggClient.SearchGamesAsync`. Si BGG devuelve coincidencia, encola el juego en `PendingBggImports` con `Origin = CatalogQueueOrigin.NewsDiscovery` y su `ExtractedTitle`.

### 3.2 Orquestador Nocturno (`INightlyCatalogingService` y `NightlyCatalogingService`)
- **Auto-recuperación y Saneamiento Pre-vuelo (INC-110):** Antes de iniciar la catalogación, el servicio invoca `IPendingBggImportRepository.RecoverStaleProcessingToPendingAsync` para devolver a `Pending` cualquier título retenido en `Processing` por caídas previas del proceso, y `INightlyCatalogingLogRepository.FailStaleRunningLogsAsync` para marcar como `Failed` los registros huérfanos con estado `Running`. Si ocurre una cancelación o error fatal, la persistencia del fallo y la recuperación de la cola se ejecutan de forma blindada mediante `CancellationToken.None`.
- Ejecuta en 4 fases secuenciales:
  1. **Fase 1 (Detección en Novedades):** Escanea lanzamientos sin vincular (`GameId == null`) y encola títulos nuevos en BGG.
  2. **Fase 2 (Cola Prioritaria con Lotes x20 BGG y x10 IA - INC-107):** Extrae hasta `DailyCatalogingLimit` (400) elementos pendientes, consulta BGG en bloques de hasta 20 IDs mediante `FetchGamesByBggIdsAsync(chunk, includeVersions: true)`, genera resúmenes estructurados en bloques de 10 juegos con `IAiGameSummaryService.GenerateBatchSummariesAsync` (o fallback heurístico), los añade al catálogo y promueve colecciones de usuarios en espera.
  3. **Fase 3 (Relleno con Top de BGG en Lotes - INC-107):** Si el total de catalogados es inferior a `DailyCatalogingLimit`, consulta `IBggClient.FetchTopGamesAsync`, filtra los ya existentes en Ludeka o en cola, y descarga los mejores en bloques de 20 juegos con síntesis en lotes de 10 hasta completar exactamente el cupo diario con `Origin = CatalogQueueOrigin.TopBggBackfill`.
  4. **Fase 4 (Bitácora):** Registra el resultado en `INightlyCatalogingLogRepository`.
- **Protección de tasa:** Pausa configurable entre bloques hacia BGG y Gemini, con una drástica reducción del número de llamadas de red (ahorro >90% en latencia y cuota).

---

## 4. Infraestructura y Persistencia (`Ludeka.Infrastructure`)

- **`IBggClient`:**
  - `FetchGamesByBggIdsAsync(IEnumerable<int> bggIds, bool includeVersions = true, CancellationToken ct)`:
    - En producción (`BggXmlApiClient`): Agrupa identificadores separados por comas y consulta `/xmlapi2/thing?id={ids}&stats=1&versions=1`, parseando todos los ítems devueltos en un único documento XML unificado.
    - En pruebas (`SimulatedBggClient`): Implementación concurrente simulada.
  - `FetchTopGamesAsync(int limit, CancellationToken ct)`:
    - En producción (`BggXmlApiClient`): Consulta `https://boardgamegeek.com/xmlapi2/hot?type=boardgame` y parsea los ítems con su ranking BGG.
    - En pruebas offline (`SimulatedBggClient`): Extrae títulos de `BggSimulationDataset` ordenados por `BggRank ?? int.MaxValue`.
- **`LudekaDbContext`:**
  - `DbSet<NightlyCatalogingExecutionLog> NightlyCatalogingExecutionLogs` con índice sobre `StartedAt`.
- **`SqliteSchemaMigrator`:**
  - Agrega columnas `Origin` y `ExtractedTitle` a `PendingBggImports` si faltan, y genera la tabla `NightlyCatalogingExecutionLogs` con sus índices.
- **`NightlyCatalogingHostedService`:**
  - Hereda de `BackgroundService`, despierta a la hora configurada (03:00 UTC) e invoca al orquestador nocturno con apagado limpio ante cancelación.

---

## 5. Interfaz de Usuario (`Ludeka.Web`)

- **Ruta de Administración:** `/admin/cola-catalogacion` (`CatalogQueueAdmin.razor`).
- **Control de Acceso:** Exclusivo para la Mesa Fundadora o moderadores con permiso `ModeratorPermission.CanEditGames`.
- **Desacoplo en Segundo Plano y Selector de Cupo (INC-110):**
  - El botón de disparo manual se desacopla del circuito SignalR de Blazor Server mediante `Task.Run` con un ámbito efímero `IServiceScopeFactory` y `CancellationTokenSource`.
  - El proceso de catalogación sobrevive a desconexiones de red, recargas de página o expiraciones del timeout de 300 segundos de Google Cloud Run.
  - La interfaz dispone de un selector de tamaño de lote (40, 50, 100, 200 o 400 títulos), banner de estado activo con animación pulsante y botón de detención segura (`StopNightlyBatch`).
  - La sincronización y actualización reactiva de métricas en el panel se realiza mediante un `PeriodicTimer` no bloqueante que refresca los datos cada 3 segundos mientras haya tareas en curso.
- **Elementos UI:**
  - **Tarjetas KPI:** Cupo diario (400), Títulos pendientes en cola, Títulos descubiertos en novedades, Estado del último lote.
  - **Filtros por Origen:** Botones de alternancia rápida para ver todos, solo procedentes de novedades (`📰`), de usuarios (`👤`) o de relleno Top BGG (`🏆`).
  - **Tabla de Historial:** Auditoría de cada ejecución nocturna con fecha, estado, detalle por orígenes y títulos incorporados.
  - **Saneamiento Editorial (INC-107):** Purgados botones de lote rápido de 20 redundantes y acciones amortizadas, manteniendo controles continuos y de descubrimiento.
- **Navegación:** Enlace directo "🌙 Cola BGG" visible en la cabecera superior (`MainLayout.razor`) para moderadores autorizados.
