# Tareas de Implementación: change-41-ingesta-bgg-catalogo (Incremento 41)

- [x] **1. Dominio y Modelado de Datos (`Ludeka.Core`)**
  - [x] 1.1 Crear entidad `BggCatalogStagingItem` con estados y transiciones (`FetchStatus`, `ImagesStatus`, `AiStatus`, `PromotionStatus`).
  - [x] 1.2 Crear enums de estado de staging (`StagingFetchStatus`, `StagingImagesStatus`, `StagingAiStatus`, `StagingPromotionStatus`).
  - [x] 1.3 Extender `Game` para soportar `BackCoverImageUrl` y `TableImageUrl` y método `UpdateMediaUrls`.
  - [x] 1.4 Pruebas unitarias de dominio para `BggCatalogStagingItem` y `Game`.

- [x] **2. Contratos y DTOs de Aplicación (`Ludeka.Application`)**
  - [x] 2.1 Crear `IBggCatalogStagingRepository` y DTOs (`BggStagingMetricsDto`, `BggRanksDumpRowDto`).
  - [x] 2.2 Crear `IGeekDoImagesClient` y `GeekDoGalleryImagesDto`.
  - [x] 2.3 Extender `IAiGameSummaryService` con `GenerateBatchSummariesAsync` y DTOs de batching (`AiGameBatchInputDto`, `AiBatchResultDto`).
  - [x] 2.4 Crear `IBggMassIngestionService` con métodos de ingesta de dump, procesamiento de lotes y promoción a catálogo.
  - [x] 2.5 Pruebas unitarias de contratos y lógica de orquestación en Application.

- [x] **3. Persistencia y Base de Datos (`Ludeka.Infrastructure`)**
  - [x] 3.1 Añadir `DbSet<BggCatalogStagingItem> BggCatalogStaging` y mapeo en `LudekaDbContext` (SQLite y PostgreSQL compatible).
  - [x] 3.2 Implementar `SqliteBggCatalogStagingRepository`.
  - [x] 3.3 Crear migración EF Core / script SQL para PostgreSQL/Supabase.
  - [x] 3.4 Pruebas de integración para persistencia de staging (inserción masiva, consultas por estado, contadores).

- [x] **4. Clientes de Integración Externa (`Ludeka.Infrastructure`)**
  - [x] 4.1 Implementar `BggDumpParser` para leer CSV/GZ de ranks aplicando filtro `UsersRated >= 30`.
  - [x] 4.2 Implementar `GeekDoImagesClient` (real con HTTP y simulado para tests) para extraer las 3 fotos comunitarias más votadas.
  - [x] 4.3 Integrar descarga de imágenes y subida a R2 vía `IImageStorageService` (INC-40) con rutas deterministas.
  - [x] 4.4 Extender `GeminiGameSummaryService` para soportar el procesamiento en lotes de 5-10 juegos y control de 429 / QuotaExceeded.
  - [x] 4.5 Pruebas unitarias para `GeekDoImagesClient`, `BggDumpParser` y `GeminiGameSummaryService` batching.

- [x] **5. Orquestador de Ingesta y Servicio Nocturno (`Ludeka.Application` & `Ludeka.Infrastructure`)**
  - [x] 5.1 Implementar `BggMassIngestionService` orquestando las fases de dump, detalles, imágenes, IA y promoción.
  - [x] 5.2 Reingeniería de `NightlyCatalogingService` para drenar staging progresivamente junto a novedades y peticiones de usuarios.
  - [x] 5.3 Actualizar `NightlyCatalogingHostedService` con los nuevos flujos.
  - [x] 5.4 Pruebas unitarias y de integración para el orquestador nocturno y drenaje de lotes.

- [x] **6. Monitorización Web y Ajustes de Presentación (`Ludeka.Web`)**
  - [x] 6.1 Añadir sección/panel de estado de Ingesta Masiva y métricas de staging en la zona de administración.
  - [x] 6.2 Actualizar componentes de ficha de juego para renderizar contraportada y foto en mesa si están disponibles.

- [x] **7. Verificación, Pruebas y Cierre SDD (`sdd-verify` & `sdd-archive`)**
  - [x] 7.1 Ejecutar `dotnet test` (100% de tests en verde sin regresiones).
  - [x] 7.2 Crear `docs/specs/sistema/27-ingesta-masiva-bgg-galeria-geekdo-ia-lotes.md` y actualizar `docs/specs/sistema/README.md`.
  - [x] 7.3 Archivar incremento en `docs/increments/archive/inc-41-ingesta-bgg-catalogo.md` y actualizar `ROADMAP.md`.
  - [x] 7.4 Preparar PR con `scripts/sdd-worktree.ps1 pr ingesta-bgg-catalogo`.
