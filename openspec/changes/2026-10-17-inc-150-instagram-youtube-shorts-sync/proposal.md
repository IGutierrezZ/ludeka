# Propuesta SDD: Sincronización Automática de Reels de Instagram a YouTube Shorts con Adaptación Híbrida IA + Directorio (INC-150)

## 1. Intención y Propósito
Automatizar la sindicación de los **Reels** publicados en la cuenta oficial de Instagram de Ludeka hacia el **canal oficial de YouTube de Ludeka** como **YouTube Shorts** (soportando vídeos verticales de hasta 3 minutos), cubriendo tanto la carga inicial del historial existente (~5 Reels actuales) como la sincronización diaria desatendida dentro de los límites de la capa gratuita de YouTube Data API v3 (10.000 unidades/día; 1.600 unidades por subida = máximo seguro de 5 vídeos/día).

El flujo transformará inteligentemente el texto de Instagram (`caption`) al formato de YouTube mediante un **motor híbrido en tres fases**:
1. **Fase 1 — Cruce determinista con el Directorio de Ludeka:** Detección de menciones `@handle` de Instagram en el texto y resolución contra las entidades registradas en Ludeka (`Publisher`, `Store`, `Creator` y `MonitoredSocialAccount`). Si la entidad tiene canal de YouTube registrado en sus `SocialLinks`, se obtiene su `@handle` o canal de YouTube; si no tiene canal de YouTube pero está en el directorio, se resuelve su nombre comercial oficial y el enlace a su ficha en `ludeka.es`.
2. **Fase 2 — Adaptación asistida con Google Gemini Flash (con fallback determinista):** Generación de un **Título gancho** ($\le 90$ caracteres + `#Shorts`) y reescritura limpia de la **Descripción** para YouTube, aplicando las equivalencias resueltas del directorio de Ludeka y transformando cualquier `@cuenta` no registrada en su nombre comercial legible (eliminando `@` huérfanas y expresiones exclusivas de Instagram como *"link en bio"*).
3. **Fase 3 — Pie editorial determinista de Ludeka:** Adición automática de un bloque de cierre estructurado con el enlace principal a `https://ludeka.es` (y enlaces directos a las fichas de Ludeka detectadas en el post) más los hashtags esenciales (`#Shorts #JuegosDeMesa #Ludeka`).

---

## 2. Alcance y Áreas Afectadas

### 2.1. Dominio (`src/Ludeka.Core`)
- **`InstagramReelSyncItem` (Nueva entidad):** Registro de control e idempotencia para cada Reel de la cuenta de Instagram de Ludeka:
  - `Id` (`Guid`), `InstagramMediaId` (`string`, índice único), `InstagramPermalink` (`string`), `Caption` (`string?`), `MediaPublishedAtUtc` (`DateTimeOffset`).
  - `Status` (`InstagramReelSyncStatus`: `Pending`, `Uploaded`, `Failed`, `Skipped`).
  - `GeneratedYouTubeTitle` (`string?`), `GeneratedYouTubeDescription` (`string?`), `YouTubeVideoId` (`string?`), `YouTubeUrl` (`string?`).
  - `AttemptCount` (`int`), `LastError` (`string?`), `CreatedAtUtc` (`DateTimeOffset`), `SyncedAtUtc` (`DateTimeOffset?`).
- **`InstagramReelSyncStatus` (Nuevo enum):** Estados del ciclo de vida de sincronización.

### 2.2. Aplicación (`src/Ludeka.Application`)
- **Ampliación de `IInstagramApiClient`:**
  - Nuevo método `GetAccountReelsAsync(int limit = 25, CancellationToken ct = default)` que consulta `GET /{InstagramAccountId}/media?fields=id,media_type,media_product_type,media_url,permalink,caption,timestamp` y filtra los elementos de vídeo/Reels (`media_product_type == "REELS"` o `media_type == "VIDEO"`).
- **`IYouTubeShortsUploadClient` (Nuevo contrato):**
  - Autenticación OAuth 2.0 mediante `ClientId`, `ClientSecret` y `RefreshToken` del canal oficial de Ludeka para obtener un `access_token` válido (`https://oauth2.googleapis.com/token`).
  - Subida por *streaming* (`videos.insert` multiparte/resumable en `https://www.googleapis.com/upload/youtube/v3/videos?part=snippet,status`) descargando el flujo binario MP4 desde la `media_url` temporal de Instagram sin almacenar ficheros pesados permanentes en disco.
- **`IInstagramToYouTubeCaptionAdapter` / `InstagramToYouTubeCaptionAdapter`:**
  - **Cruce con Directorio:** Escanea menciones `@handle` en el `caption` y busca coincidencias insensibles a mayúsculas en `IPublisherRepository`, `IStoreRepository`, `ICreatorRepository` e `ISocialInboxRepository` (`MonitoredSocialAccount`). Extrae el `@handle` de YouTube (si la entidad posee `SocialPlatform.YouTube` en `SocialLinks`), el nombre oficial (`Name`) y la ruta canónica en Ludeka (`/editoriales/{slug}`, `/tiendas/{slug}`, `/creadores/{slug}`).
  - **Síntesis con Gemini Flash:** Invoca a Gemini con el texto original y la tabla de menciones resueltas para generar `{ title, cleanBody }`. Incluye fallback determinista (primera línea limpia + sustitución directa de `@handles`) si Gemini no está disponible.
  - **Construcción del pie determinista:** Ensambla la descripción final añadiendo el bloque promocional bonito hacia `https://ludeka.es` y los enlaces a las fichas de tiendas/editoriales detectadas.
- **`IInstagramYouTubeSyncService` / `InstagramYouTubeSyncService`:**
  - Orquesta el descubrimiento de Reels en Instagram (ordenados cronológicamente de más antiguo a más reciente para respetar el orden del historial en la carga inicial), registra los nuevos en `InstagramReelSyncItems` y sube hasta `MaxUploadsPerRun` (por defecto `5` por ejecución para proteger la cuota gratuita de 10.000 unidades/día de YouTube).

### 2.3. Infraestructura y Jobs (`src/Ludeka.Infrastructure` y `src/Ludeka.Jobs`)
- **`YouTubeOptions` / `InstagramYouTubeSyncOptions`:**
  - Nuevas propiedades de configuración OAuth para subida a YouTube (`OAuthClientId`, `OAuthClientSecret`, `OAuthRefreshToken`, `DefaultPrivacyStatus = "public"`, `CategoryId = "20"` Gaming) y opciones del sincronizador (`MaxUploadsPerRun = 5`, `MaxFetchReels = 50`).
- **Persistencia EF Core Dual (SQLite + PostgreSQL):**
  - Tabla `InstagramReelSyncItems` con índice único sobre `InstagramMediaId`, migración EF Core y reconciliación defensiva en `SqliteSchemaMigrator`.
- **Job desatendido `instagram-youtube-sync` (`InstagramYouTubeSyncJobRunner`):**
  - Nuevo runner registrado en `Ludeka.Jobs` (`JobNames.InstagramYouTubeSync = "instagram-youtube-sync"`) y añadido al bucle de despliegue de Cloud Run Jobs en `.github/workflows/ci-cd.yml`.
- **Panel de Administración / Backoffice (`/admin/instagram`):**
  - Sección o pestaña en la página administrativa de Instagram (`InstagramModeration.razor`) para visualizar el estado de los Reels sincronizados a YouTube Shorts y botón de sincronización manual bajo demanda, además de un asistente o guía rápida para obtener el `RefreshToken` OAuth de YouTube en 1 clic si aún no está configurado.

---

## 3. Riesgos y Mitigaciones
- **Agotamiento de la cuota gratuita de YouTube Data API v3 (10.000 uds/día):** Cada `videos.insert` consume 1.600 unidades. El parámetro `MaxUploadsPerRun = 5` (8.000 unidades máximo) garantiza que jamás se supere la cuota diaria gratuita. Si YouTube devuelve `403 quotaExceeded`, el servicio detiene limpiamente el lote actual dejando los restantes en `Pending` para el día siguiente.
- **Caducidad de `media_url` en Instagram Graph API:** Las URLs firmadas del CDN de Meta caducan al cabo de unas horas. Por ello, `media_url` no se guarda de forma estática para días posteriores, sino que se obtiene en fresco desde la API de Instagram en el momento exacto de realizar el *streaming* hacia YouTube.
- **Modo simulación seguro:** Si no se ha configurado aún `YouTube:OAuthRefreshToken` o `Simulate = true`, el cliente opera en modo simulado registrando la traza completa sin fallar en desarrollo o CI.

---

## 4. Criterio de Aprobación
Por favor, revisa esta propuesta para el **INC-150**. Si estás de acuerdo con el enfoque, procederemos con la especificación, diseño, plan de tareas e implementación TDD en el worktree.
