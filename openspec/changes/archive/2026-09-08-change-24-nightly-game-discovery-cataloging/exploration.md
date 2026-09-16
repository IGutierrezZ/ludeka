# Fase de Exploración: change-24-nightly-game-discovery-cataloging

## 1. Contexto y Objetivos del Incremento
El **Incremento 24** tiene como propósito automatizar el descubrimiento e ingesta de juegos de mesa dentro del ecosistema de Ludeka conectando dos pipelines fundamentales:
1. **Detección Automática en Novedades Editoriales:**
   - Analizar las publicaciones de novedades y lanzamientos (`WeeklyRelease`) capturadas de editoriales (como Devir, Asmodee, Tranjis, Maldito Games, etc.).
   - Extraer el título del juego o expansión mencionado.
   - Si el juego ya está en el catálogo de Ludeka, vincularlo inmediatamente (`WeeklyRelease.GameId = game.Id`).
   - Si no está en Ludeka, verificar su existencia en BoardGameGeek (BGG Search API).
   - Si existe en BGG, encolarlo automáticamente en la cola de auto-catalogación (`PendingBggImport`) con origen `NewsDiscovery` y el título extraído.
2. **Orquestador Nocturno Inteligente de Ingesta (`NightlyCatalogingService`):**
   - Tarea programada en segundo plano (*BackgroundService*) con límite diario configurable (`DailyCatalogingLimit = 20`).
   - Procesa los juegos pendientes de la cola comunitaria y de novedades.
   - Si la cola tiene menos de 20 juegos pendientes, rellena el cupo restante hasta 20 con los mejores títulos del ranking de BGG aún no catalogados en Ludeka.
   - Aplica pausas de cortesía y rate limiting (mínimo 2.5s entre llamadas externas) protegiendo las cuotas de BGG XMLAPI2 y Google Gemini.
3. **Panel de Supervisión en Administración (`/admin/cola-catalogacion`):**
   - Vista para moderadores y Mesa Fundadora con métricas de la cola, desglose por origen, historial de ejecuciones por lote y trigger manual.

---

## 2. Hallazgos en el Código Fuente Existente

### 2.1 Dominio (`Ludeka.Core`)
- **`PendingBggImport`:** Entidad que modela la cola de catalogación (`BggId`, `Title`, `YearPublished`, `ThumbnailUrl`, `RequestedCount`, `Status`, `ErrorMessage`, `CreatedAt`, `ProcessedAt`).
  - *Gaps detectados:* No tiene campo para identificar el origen (`CatalogQueueOrigin`) ni el título originalmente extraído (`ExtractedTitle`).
- **`WeeklyRelease`:** Entidad que modela los lanzamientos de novedades (`Title`, `Publisher`, `ReleaseDate`, `GameId`, `CoverImageUrl`, `EstimatedPvp`, `IsReprint`, `Notes`).
  - *Gaps detectados:* No dispone de método de dominio `LinkGame(Guid gameId)` para vincular un juego identificado posteriormente.
- **Nueva entidad necesaria:** `NightlyCatalogingExecutionLog` para persistir la bitácora de ejecuciones nocturnas (fecha de inicio, fin, procesados de cola, descubiertos de novedades, relleno de ranking, errores y estado).

### 2.2 Aplicación (`Ludeka.Application`)
- **`BggCatalogQueueService`:** Procesa la cola comunitaria pero solo procesa lotes directos de `PendingBggImports` sin distinción de cuota nocturna ni relleno automático con Top de BGG.
- **`IBggClient`:**
  - Métodos actuales: `FetchGameByBggIdAsync`, `FetchUserCollectionAsync`, `SearchGamesAsync`.
  - *Gap:* Carece de un método para obtener juegos destacados o por ranking (`FetchTopGamesAsync(int limit, CancellationToken ct)`).
- **Extracción de juegos:** No existe actualmente un extractor sintáctico/semántico para novedades editoriales (`INewsGameExtractor`).

### 2.3 Infraestructura (`Ludeka.Infrastructure`)
- **`SimulatedBggClient` y `BggSimulationDataset`:** El dataset simulado cuenta con 40 juegos con `BggRank` predefinido, lo que permite implementar `FetchTopGamesAsync` de forma offline y determinista para pruebas automatizadas.
- **`BggXmlApiClient`:** Puede consultar el endpoint `/xmlapi2/hot?type=boardgame` o ranking de BGG mapeando a `BggTopGameDto`.
- **`SqliteSchemaMigrator`:** Reconcilia columnas en SQLite en caliente sin necesidad de migraciones de EF Core destructivas. Necesita actualizar `PendingBggImports` (`Origin`, `ExtractedTitle`) y crear `NightlyCatalogingExecutionLogs`.
- **Servicios en segundo plano:** `CommunityNotificationDispatcherHostedService` sirve de plantilla probada para implementar `NightlyCatalogingHostedService`.

### 2.4 Presentación (`Ludeka.Web`)
- Existe `CatalogQueuePanel.razor` en `MyLibrary.razor` para importaciones personales, pero no existe la vista administrativa `/admin/cola-catalogacion` para supervisar el pipeline nocturno y la tasa de consumo.
