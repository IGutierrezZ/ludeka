# Checklist de Tareas: change-24-nightly-game-discovery-cataloging

- [x] **1. Dominio y Modelado (`Ludeka.Core`)**
  - [x] 1.1 Crear enum `CatalogQueueOrigin` (`UserImport`, `NewsDiscovery`, `TopBggBackfill`).
  - [x] 1.2 Actualizar entidad `PendingBggImport` con propiedades `Origin` y `ExtractedTitle`.
  - [x] 1.3 Añadir método `LinkGame(Guid gameId)` a `WeeklyRelease`.
  - [x] 1.4 Crear entidad `NightlyCatalogingExecutionLog` con métodos `Complete` y `Fail`.

- [x] **2. Contratos y DTOs (`Ludeka.Application`)**
  - [x] 2.1 Crear DTOs: `BggTopGameDto`, `NightlyCatalogingResultDto`, `NightlyCatalogingExecutionLogDto`, `NightlyCatalogingOptions`.
  - [x] 2.2 Actualizar `CatalogQueueItemDto` con `Origin` y `ExtractedTitle`.
  - [x] 2.3 Actualizar interfaz `IBggClient` incorporando `FetchTopGamesAsync`.
  - [x] 2.4 Definir interfaz `INewsGameExtractor` y DTOs asociados.
  - [x] 2.5 Definir interfaz `INightlyCatalogingService`.
  - [x] 2.6 Definir interfaz `INightlyCatalogingLogRepository`.

- [x] **3. Servicios de Aplicación (`Ludeka.Application`)**
  - [x] 3.1 Implementar `NewsGameExtractor` con heurísticas, patrones regex y verificación contra `IGameRepository` y `IBggClient`.
  - [x] 3.2 Implementar `NightlyCatalogingService` orquestando las 4 fases: detección en novedades, procesamiento de cola, relleno con Top BGG y bitácora de ejecución, con rate limiting respetuoso.

- [x] **4. Infraestructura y Persistencia (`Ludeka.Infrastructure`)**
  - [x] 4.1 Extender `BggSimulationDataset` y `SimulatedBggClient` implementando `FetchTopGamesAsync`.
  - [x] 4.2 Implementar `FetchTopGamesAsync` en `BggXmlApiClient` consultando `/xmlapi2/hot?type=boardgame`.
  - [x] 4.3 Añadir `DbSet<NightlyCatalogingExecutionLog>` en `LudekaDbContext`.
  - [x] 4.4 Implementar `SqliteNightlyCatalogingLogRepository`.
  - [x] 4.5 Actualizar `SqliteSchemaMigrator` para columnas de `PendingBggImports` y tabla `NightlyCatalogingExecutionLogs`.
  - [x] 4.6 Implementar `NightlyCatalogingHostedService` para ejecución programada en segundo plano.

- [x] **5. Presentación Web (`Ludeka.Web`)**
  - [x] 5.1 Registrar servicios, repositorios, opciones y hosted service en `Program.cs`.
  - [x] 5.2 Crear la vista Razor `/admin/cola-catalogacion` (`CatalogQueueAdmin.razor`) con KPIs, tabla de cola con filtros por origen y tabla de historial.
  - [x] 5.3 Añadir enlace de navegación en `MainLayout.razor` para moderadores y Mesa Fundadora.

- [x] **6. Suite de Pruebas Automatizadas (`Ludeka.UnitTests`)**
  - [x] 6.1 Pruebas unitarias para `NewsGameExtractor`: detección por patrones, vinculación a juego existente y encolado en BGG.
  - [x] 6.2 Pruebas unitarias para `NightlyCatalogingService`: procesamiento de cola, relleno con Top BGG hasta cupo de 20, límite estricto y rate limiting.
  - [x] 6.3 Pruebas unitarias para `PendingBggImport`, `WeeklyRelease.LinkGame` y `NightlyCatalogingExecutionLog`.
  - [x] 6.4 Ejecución completa de `dotnet test` asegurando 100% de tests en verde (600/600 superados).
