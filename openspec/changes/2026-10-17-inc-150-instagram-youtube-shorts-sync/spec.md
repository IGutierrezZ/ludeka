# Especificación SDD: Sincronización Automática de Reels de Instagram a YouTube Shorts con Adaptación Híbrida IA y Directorio (INC-150)

## 1. Requisitos Funcionales

### RF-01: Descubrimiento de Reels en la Cuenta Oficial de Instagram (`IInstagramApiClient`)
- **RF-01.1:** El cliente `IInstagramApiClient` debe exponer el método `GetAccountReelsAsync(int limit = 25, CancellationToken ct = default)` que devuelva una lista de `InstagramAccountMediaDto`.
- **RF-01.2:** Cuando `InstagramOptions.IsConfigured` sea `true`, consultará el endpoint oficial de Instagram Graph API:
  `GET {BaseUrl}/{InstagramAccountId}/media?fields=id,media_type,media_product_type,media_url,permalink,caption,timestamp&limit={limit}&access_token={AccessToken}`
- **RF-01.3:** Solo se seleccionarán aquellas publicaciones cuyo `media_product_type` sea `"REELS"` o cuyo `media_type` sea `"VIDEO"`, y que dispongan de una `media_url` no vacía.
- **RF-01.4:** Cuando `InstagramOptions.IsConfigured` sea `false` (modo simulado), `GetAccountReelsAsync` devolverá un conjunto determinista de Reels simulados de Ludeka (con menciones reales como `@deviriberia` y `@zacatrus`) para permitir pruebas locales y de integración sin credenciales de Meta.

### RF-02: Motor Híbrido de Adaptación de Texto y Menciones (`IInstagramToYouTubeCaptionAdapter`)
- **RF-02.1 (Fase 1 — Resolución determinista contra el Directorio de Ludeka):**
  - El adaptador extraerá todas las menciones `@handle` presentes en el `caption` de Instagram mediante expresión regular (`@([a-zA-Z0-9._]+)`).
  - Para cada `@handle`, buscará coincidencias insensibles a mayúsculas en:
    1. `IPublisherRepository.GetAllAsync()`
    2. `IStoreRepository.GetAllAsync()`
    3. `ICreatorRepository.GetAllAsync()`
    4. `IMonitoredAccountRepository.GetAllAsync()`
  - Una entidad coincide si posee un `SocialNetworkLink` de plataforma `Instagram` cuyo `Handle` (sin `@`) o segmento final de `Url` coincida con el handle buscado, o si su `Slug` normalizado coincide exactamente.
  - Si la entidad coincide:
    - Se comprueba si tiene un `SocialNetworkLink` con `Platform == SocialPlatform.YouTube` (o una cuenta monitorizada homónima de YouTube). Si tiene handle de YouTube (`@CanalYT`), se registra como reemplazo preferente para el cuerpo del texto (`@CanalYT (Nombre Oficial)` o `Nombre Oficial (@CanalYT)`).
    - Si no tiene canal de YouTube registrado, se registra su `Name` comercial oficial (ej. `"Devir Iberia"`, `"Zacatrus"`) como reemplazo del `@handle` de Instagram.
    - Además, si tiene ficha pública en Ludeka (`/editoriales/{slug}`, `/tiendas/{slug}` o `/creadores/{slug}`), se guarda su enlace canónico (`{PublicBaseUrl}/{ruta}`) para incluirlo en el bloque de referencias de la descripción.
- **RF-02.2 (Fase 2 — Adaptación con Google Gemini Flash y Fallback Determinista):**
  - El adaptador construirá un prompt estructurado para Google Gemini Flash (`GeminiOptions`) entregándole:
    - El `caption` original del Reel de Instagram.
    - El mapa de menciones resueltas desde el directorio de Ludeka.
  - Gemini devolverá un JSON con `{ "title": "...", "cleanDescription": "..." }` cumpliendo:
    - `title`: Título gancho en español de máximo 88 caracteres, añadiendo `#Shorts` al final (total $\le 100$ caracteres exigidos por YouTube).
    - `cleanDescription`: Texto adaptado para YouTube donde las menciones del directorio usan su equivalencia resuelta, las menciones `@cuenta` desconocidas (no presentes en la base de datos de Ludeka) se transforman en su nombre comercial legible sin la `@` de Instagram, y se limpian expresiones exclusivas de Instagram (*"link en bio"*, *"enlace en el perfil"*).
  - Si Gemini está en modo simulado, sin `ApiKey` o devuelve error/timeout, el adaptador ejecutará un **fallback determinista**:
    - Generará el título a partir de la primera línea/frase limpia del `caption` + `#Shorts` ($\le 100$ caracteres).
    - Sustituirá en el cuerpo cada `@handle` conocido por su equivalencia del directorio de Ludeka, y humanizará cualquier `@handle` desconocido quitando la `@` y convirtiendo puntos/guiones bajos en espacios con formato Title Case.
- **RF-02.3 (Fase 3 — Pie Editorial Determinista de Ludeka):**
  - Al final de `cleanDescription`, el adaptador concatenará de forma determinista el bloque oficial de Ludeka:
    - Si se detectaron fichas del directorio de Ludeka (tiendas, editoriales o creadores), una lista con sus enlaces directos en `ludeka.es`.
    - El bloque principal de llamada a la acción hacia `https://ludeka.es`:
      ```text
      🎲 Descubre más juegos de mesa, compara precios en tiendas, sorteos y eventos en tu mesa:
      👉 https://ludeka.es
      ```
    - Garantizará la presencia de los hashtags `#Shorts #JuegosDeMesa #Ludeka`.

### RF-03: Subida de Shorts a YouTube Data API v3 (`IYouTubeShortsUploadClient`)
- **RF-03.1:** `YouTubeOptions` incorporará las propiedades OAuth 2.0 necesarias para publicar en el canal de Ludeka: `OAuthClientId`, `OAuthClientSecret`, `OAuthRefreshToken`, `DefaultPrivacyStatus` (por defecto `"public"`) y `DefaultCategoryId` (por defecto `"20"` — Gaming).
- **RF-03.2:** Cuando `YouTubeOptions.IsUploadConfigured` sea `true` (`!Simulate` y con `OAuthClientId`, `OAuthClientSecret` y `OAuthRefreshToken` definidos):
  1. Intercambiará el `OAuthRefreshToken` en `POST https://oauth2.googleapis.com/token` para obtener un `access_token` Bearer vigente.
  2. Descargará en *streaming* el archivo de vídeo desde la `media_url` del Reel de Instagram.
  3. Iniciará una subida *resumable* en `POST https://www.googleapis.com/upload/youtube/v3/videos?uploadType=resumable&part=snippet,status` enviando el JSON de metadatos (`title`, `description`, `categoryId = "20"`, `privacyStatus`, `selfDeclaredMadeForKids = false`) y a continuación transmitirá el cuerpo binario `video/mp4` a la URL de sesión devuelta en la cabecera `Location`.
  4. Extraerá el `id` del vídeo creado en YouTube y devolverá `https://www.youtube.com/shorts/{videoId}`.
- **RF-03.3:** En modo simulado (`!IsUploadConfigured`), generará un identificador simulado (`sim_yt_{guid}`) y la URL `https://www.youtube.com/shorts/sim_yt_{guid}` sin realizar llamadas externas.

### RF-04: Persistencia, Cola Cronológica y Job Diario (`IInstagramYouTubeSyncService` + `InstagramYouTubeSyncJobRunner`)
- **RF-04.1:** La entidad `InstagramReelSyncItem` persistida en `InstagramReelSyncItems` garantizará idempotencia estricta por `InstagramMediaId` (índice único).
- **RF-04.2:** En cada ciclo de `SyncReelsToYouTubeAsync(int? maxUploads = null, CancellationToken ct = default)`:
  1. Consultará `IInstagramApiClient.GetAccountReelsAsync(limit: 50)` y ordenará los Reels descubiertos por fecha de publicación ascendente (`Timestamp` de más antiguo a más reciente) para que la carga inicial del historial (~5 Reels) y los posteriores se publiquen en el mismo orden cronológico en que salieron en Instagram.
  2. Registrará en base de datos como `Pending` cualquier Reel cuyo `InstagramMediaId` aún no exista en `InstagramReelSyncItems`.
  3. Seleccionará hasta `maxUploads ?? options.MaxUploadsPerRun` (por defecto `5` por ejecución, equivalente a 8.000 unidades de las 10.000 diarias gratuitas de YouTube) elementos en estado `Pending` (o `Failed` con menos de 3 intentos), ordenados por `MediaPublishedAtUtc` ascendente.
  4. Para cada elemento seleccionado, adaptará el texto con `IInstagramToYouTubeCaptionAdapter`, subirá el vídeo con `IYouTubeShortsUploadClient` usando la `media_url` fresca del Reel y marcará el registro como `Uploaded` con su `YouTubeVideoId` y `YouTubeUrl`.
  5. Si un vídeo falla por cuota agotada (`quotaExceeded` / HTTP 403), registrará el error y detendrá inmediatamente el resto del lote actual para reintentarlo al día siguiente.
- **RF-04.3:** El runner `InstagramYouTubeSyncJobRunner` (`JobNames.InstagramYouTubeSync = "instagram-youtube-sync"`) se ejecutará bajo coordinación de ventana diaria UTC (`JobWindowKeyCalculator.DailyUtc`) y se desplegará en el pipeline de Cloud Run Jobs (`.github/workflows/ci-cd.yml`).
- **RF-04.4:** En `/admin/instagram` (`InstagramModeration.razor`), se mostrará una sección dedicada **«Sincronización de Reels a YouTube Shorts»** con el estado de conexión OAuth de YouTube, el botón **«Sincronizar Reels ahora»** y la tabla/listado de los Reels sincronizados con su título adaptado, estado y enlace directo al Short en YouTube.
