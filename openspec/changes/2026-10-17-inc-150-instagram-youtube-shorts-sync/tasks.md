# Plan de Tareas SDD: Sincronización Automática de Reels de Instagram a YouTube Shorts (INC-150)

## Bloque 1: Dominio y Contratos (`Ludeka.Core` y `Ludeka.Application`)
- [x] **1.1.** Crear enum `InstagramReelSyncStatus` (`Pending`, `Uploaded`, `Failed`, `Skipped`) en `src/Ludeka.Core/Enums/InstagramReelSyncStatus.cs`.
- [x] **1.2.** Crear entidad `InstagramReelSyncItem` en `src/Ludeka.Core/Entities/InstagramReelSyncItem.cs` con invariantes y transiciones de estado (`MarkUploaded`, `MarkFailed`, `MarkSkipped`, `UpdateAdaptedMetadata`).
- [x] **1.3.** Añadir DTOs (`InstagramAccountMediaDto`, `YouTubeAdaptedMetadataDto`, `ResolvedMentionDto`, `InstagramYouTubeSyncResultDto`, `InstagramReelSyncItemDto`) y ampliar `IInstagramApiClient` con `GetAccountReelsAsync`.
- [x] **1.4.** Crear contratos `IInstagramReelSyncRepository`, `IInstagramToYouTubeCaptionAdapter`, `IYouTubeShortsUploadClient` e `IInstagramYouTubeSyncService`.
- [x] **1.5.** Ampliar `YouTubeOptions` e `InstagramYouTubeSyncOptions` con parámetros OAuth 2.0 (`OAuthClientId`, `OAuthClientSecret`, `OAuthRefreshToken`, `MaxUploadsPerRun = 5`).

## Bloque 2: Motor Híbrido de Adaptación y Servicio de Sincronización (`Ludeka.Application` / `Ludeka.Infrastructure`)
- [x] **2.1.** Implementar `InstagramToYouTubeCaptionAdapter` con las 3 fases:
  1. Cruce de `@handles` contra `IPublisherRepository`, `IStoreRepository`, `ICreatorRepository` e `IMonitoredAccountRepository`.
  2. Adaptación con Google Gemini Flash + fallback determinista.
  3. Pie determinista hacia `https://ludeka.es` con enlaces a las fichas de Ludeka detectadas y hashtags `#Shorts`.
- [x] **2.2.** Ampliar `InstagramApiClient` con `GetAccountReelsAsync` (consulta a Graph API `/media` filtrando `REELS`/`VIDEO` + modo simulado con 5 Reels de prueba).
- [x] **2.3.** Implementar `YouTubeShortsUploadClient` con flujo OAuth 2.0 (`refresh_token` ➔ `access_token`) y subida *resumable* a `https://www.googleapis.com/upload/youtube/v3/videos`, más modo simulado seguro.
- [x] **2.4.** Implementar `InstagramYouTubeSyncService` con descubrimiento cronológico ascendente (para subir primero los más antiguos de los ~5 históricos y luego los nuevos diarios), control de límite `MaxUploadsPerRun = 5` y parada elegante ante `quotaExceeded`.

## Bloque 3: Persistencia Dual, Job Desatendido y Panel Admin (`Ludeka.Infrastructure`, `Ludeka.Jobs`, `Ludeka.Web`)
- [x] **3.1.** Configurar `InstagramReelSyncItems` en `LudekaDbContext`, crear migración EF Core, añadir creación defensiva en `SqliteSchemaMigrator` e implementar `SqliteInstagramReelSyncRepository`.
- [x] **3.2.** Crear `InstagramYouTubeSyncJobRunner` en `Ludeka.Jobs`, registrarlo en `JobNames.InstagramYouTubeSync` (`"instagram-youtube-sync"`), `JobRunnerServiceCollectionExtensions`, `LudekaServiceCollectionExtensions` y `.github/workflows/ci-cd.yml`.
- [x] **3.3.** Añadir en `/admin/instagram` (`InstagramModeration.razor`) el panel de «Sincronización de Reels a YouTube Shorts» con estado de conexión, botón «Sincronizar Reels a Shorts ahora» y listado de Reels sincronizados con sus títulos adaptados y enlaces a YouTube.

## Bloque 4: Suite de Pruebas Unitarias y de Integración (`Ludeka.UnitTests`)
- [x] **4.1.** Pruebas unitarias de `InstagramToYouTubeCaptionAdapterTests` (resolución de tienda/editorial con canal de YouTube en `SocialLinks`, resolución de tienda/editorial sin canal de YouTube pero con ficha en Ludeka, transformación de `@handle` desconocido y pie determinista con `https://ludeka.es`).
- [x] **4.2.** Pruebas unitarias de `InstagramApiClientReelsTests` y `YouTubeShortsUploadClientTests` (intercambio OAuth, protocolo *resumable upload* y modo simulado).
- [x] **4.3.** Pruebas unitarias de `InstagramYouTubeSyncServiceTests` y `InstagramYouTubeSyncJobRunnerTests` (carga inicial de 5 Reels en orden cronológico, idempotencia en ejecuciones diarias sucesivas, respeto del límite de 5 subidas por ejecución y manejo de error de cuota).
- [x] **4.4.** Actualizar el conteo de migraciones en `PostgresSchemaVerificationTests` y verificar que el 100% de la suite de pruebas de la solución pase en verde.
