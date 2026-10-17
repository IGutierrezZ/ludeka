# 21. Generador y Publicador Directo de Posts para Instagram y Sincronización de Reels a YouTube Shorts

> **Incrementos Asociados:** INC-28 (`change-28-instagram-direct-publisher`), INC-150 (`inc-150-instagram-youtube-shorts-sync`)  
> **Estado:** Implementado, Verificado y Documentado  
> **Módulo:** Moderación, Comunidad y Difusión Social Omnicanal

---

## 1. Visión General y Propósito

El módulo de **Publicador Directo de Instagram y Sincronización a YouTube Shorts** dota al equipo de moderación y a la Mesa Fundadora de Ludeka de dos capacidades editoriales complementarias:
1. Generar, maquetar, previsualizar y publicar directamente en la cuenta oficial de Instagram de la plataforma cualquier sorteo activo (`Giveaway`), novedad semanal (`WeeklyRelease`) o ficha del catálogo (`Game`) en formato cuadrado 1:1 (1080x1080 píxeles).
2. Sincronizar de forma desatendida y retroactiva los **Reels de Instagram** (de entre 1 y 3 minutos) publicados en la cuenta oficial de Ludeka hacia el canal oficial de **YouTube de Ludeka** como **YouTube Shorts**, adaptando el texto en 3 fases (resolución de menciones `@handle` contra el directorio de editoriales, tiendas y creadores de Ludeka + reescritura con Google Gemini Flash + pie determinista con enlace a `https://ludeka.es`) y operando 100% dentro de la cuota gratuita de YouTube Data API v3 (`MaxUploadsPerRun = 5`).

---

## 2. Modelo de Dominio (`Ludeka.Core`)

### 2.1. Entidad `InstagramPostDraft`
Representa el ciclo de vida editorial de una publicación para Instagram.

- **Identificadores y Origen:**
  - `Id`: Identificador único (GUID).
  - `SourceType`: Tipología de contenido origen (`InstagramPostSourceType`: `Giveaway`, `WeeklyRelease`, `Game`, `Manual`).
  - `SourceId`: Identificador de la entidad de origen vinculada (clave foránea lógica tipo string).
- **Contenido Visual y Textual:**
  - `Title`: Título editorial del post.
  - `Caption`: Texto enriquecido (copy) para Instagram (hasta 2.200 caracteres según límites de la plataforma).
  - `ImageUrl`: URL pública de la imagen de fondo o carátula de alta resolución.
  - `SvgContent`: Código vectorial SVG generado para la tarjeta cuadrada de marca 1080x1080.
  - `Theme`: Variante visual aplicada (`"Dark"` u `"Light"`).
- **Estado y Trazabilidad:**
  - `Status`: Estado del ciclo (`InstagramPostDraftStatus`: `Draft`, `Publishing`, `Published`, `Failed`).
  - `InstagramMediaId`: Identificador del medio asignado por la API de Meta tras publicar.
  - `InstagramPermalink`: Enlace permanente directo a la publicación en Instagram.
  - `ErrorMessage`: Detalle del fallo en caso de error de red o rechazo de la API de Meta.
  - `CreatedByUserId`, `CreatedByUserName`: Moderador responsable de la composición.
  - `CreatedAt`, `PublishedAt`, `UpdatedAt`: Marcas temporales UTC.

### 2.2. Enriquecimiento de Entidades Existentes
- **`Giveaway` (Sorteos):**
  - Campos `InstagramMediaId` y `InstagramPermalink`.
  - Propiedad calculada `IsPublishedOnInstagram => !string.IsNullOrWhiteSpace(InstagramPermalink)`.
  - Método de dominio `MarkPublishedOnInstagram(mediaId, permalink)`.
- **`WeeklyRelease` (Novedades Semanales):**
  - Campos `InstagramMediaId` y `InstagramPermalink`.
  - Propiedad calculada `IsPublishedOnInstagram => !string.IsNullOrWhiteSpace(InstagramPermalink)`.
  - Método de dominio `MarkPublishedOnInstagram(mediaId, permalink)`.
- **`ModeratorPermission`:**
  - Nuevo flag bitwise granular `CanPublishInstagram = 1 << 7` (128).
  - Actualización de la máscara acumulativa `All = 4095`.
- **`AuditEntityType` y `AuditAction`:**
  - `AuditEntityType.InstagramPost` para la bitácora inmutable.
  - `AuditAction.Published` para registrar el lanzamiento oficial en redes sociales.

### 2.3. Entidad `InstagramReelSyncItem` y Enum `InstagramReelSyncStatus` (INC-150)
Representa el estado de sincronización e idempotencia de cada Reel de la cuenta oficial de Instagram de Ludeka hacia YouTube Shorts:
- `Id` (`Guid`), `InstagramMediaId` (índice único `UX_InstagramReelSyncItems_InstagramMediaId`), `InstagramPermalink`, `OriginalCaption`, `DurationSeconds`, `InstagramPublishedAt`.
- `AdaptedTitle`, `AdaptedDescription`, `ResolvedMentionsJson`.
- `Status` (`InstagramReelSyncStatus`: `Pending = 0`, `Uploaded = 1`, `Failed = 2`, `Skipped = 3`).
- `YouTubeVideoId`, `YouTubeShortUrl` (`https://www.youtube.com/shorts/{id}`), `AttemptCount`, `LastError`, `CreatedAt`, `SyncedAt`.
- Transiciones de dominio: `SetAdaptedMetadata(...)`, `MarkUploaded(youTubeVideoId)`, `MarkFailed(errorMessage)` y `MarkSkipped(reason)`.

---

## 3. Arquitectura de Casos de Uso y Contratos (`Ludeka.Application`)

### 3.1. Compositor Visual y Textual (`IInstagramComposerService` y `InstagramComposerService`)
Genera el diseño vectorial y el copy optimizado sin dependencias de canvas o librerías pesadas en servidor:
- **`ComposeSvg(sourceType, sourceEntity, theme)`:** Retorna el documento SVG 1080x1080 con jerarquía visual:
  - Cabecera de marca (`Ludeka.` con punto naranja de marca y badge de sección).
  - Marco de carátula 800x450 con esquinas redondeadas (`rx="24"`) y sombras.
  - Píldoras flotantes dinámicas (ej. `🎁 Sorteo Activo`, `📰 Novedad Editorial`, `🔄 Reimpresión`, bandera del país o ámbito internacional).
  - Bloque tipográfico con título truncado elegantemente, organizador o editorial, PVP estimado y fecha límite.
  - Pie de marca con llamada a la acción hacia `ludeka.app`.
  - Soporte de temas: fondo oscuro profundo (`#0B0F17` a `#131B2A`) o editorial claro (`#FFFFFF` a `#F8FAFC`).
- **`GenerateCaption(sourceType, sourceEntity)`:** Compone el texto de la publicación estructurado con emojis, llamadas a la acción en la bio, mención social normalizada con prefijo `@` (`@malditogames`, `@asmodee_es`) y etiquetas temáticas (`#sorteojuegos`, `#novedadesludicas`, `#juegosdemesa`, `#ludeka`).

### 3.2. Cliente de Publicación y Lectura de Reels de Instagram (`IInstagramApiClient`)
Encapsula la comunicación HTTP con la **Meta Graph API v19.0**:
- `CreateMediaContainerAsync(imageUrl, caption)`: POST a `/{ig-user-id}/media` para generar el contenedor `creation_id`.
- `PublishMediaAsync(creationId)`: POST a `/{ig-user-id}/media_publish` para publicar el contenedor en el feed oficial.
- `GetPermalinkAsync(mediaId)`: GET a `/{media-id}?fields=permalink` para obtener la URL pública final.
- `ListAccountReelsAsync(maxItems, ct)` (INC-150): GET paginado a `/{ig-user-id}/media?fields=id,caption,media_type,media_product_type,media_url,permalink,timestamp,video_duration`, filtrando exclusivamente vídeos/Reels (`media_product_type == "REELS"` o `media_type == "VIDEO"`).
- `DownloadReelVideoAsync(mediaUrl, destination, ct)` (INC-150): Descarga en streaming (`HttpCompletionOption.ResponseHeadersRead`) el binario MP4 desde la URL firmada de Meta hacia un archivo temporal.
- **Modo Simulado (`Simulate = true`):** Permite ejecutar pruebas completas en entornos de desarrollo o integración sin credenciales reales de Meta, devolviendo identificadores deterministas y 5 Reels simulados con menciones de editoriales y tiendas reales del directorio.

### 3.3. Orquestador de Publicación (`IInstagramPublisherService` e `InstagramPublisherService`)
Coordina todo el flujo editorial:
1. Valida los permisos del moderador o pertenencia a la Mesa Fundadora.
2. Si se crea desde un sorteo o novedad, verifica si ya existe un borrador previo o crea uno nuevo a partir del compositor.
3. Al publicar (`PublishDraftAsync`):
   - Cambia el estado a `Publishing`.
   - Obtiene o compone la URL pública de la imagen (apuntando al endpoint vectorial `/api/instagram/card/{draftId}.svg` o imagen de carátula).
   - Ejecuta las dos fases contra `IInstagramApiClient`.
   - Actualiza el borrador a `Published` con su `mediaId` y `permalink`.
   - Sincroniza la entidad de origen (`Giveaway` o `WeeklyRelease`) invocando `MarkPublishedOnInstagram`.
   - Registra una entrada inmutable en `IAuditService` (`AuditEntityType.InstagramPost`, `AuditAction.Published`) con el diff de cambios.
   - En caso de excepción, captura el fallo, lo persiste en `draft.ErrorMessage`, marca el estado como `Failed` y no audita como publicado.

### 3.4. Adaptador Híbrido de Texto y Menciones (`IInstagramToYouTubeCaptionAdapter` / `InstagramToYouTubeCaptionAdapter`) (INC-150)
Transforma el caption original de un Reel de Instagram en un título y descripción optimizados para YouTube Shorts en tres fases:
1. **Fase 1 (Cruce Determinista con el Directorio de Ludeka):** Extrae las menciones `@handle` y las cruza (insensible a mayúsculas) contra `IPublisherRepository`, `IStoreRepository`, `ICreatorRepository` e `IMonitoredSocialAccountRepository`.
   - Si la entidad tiene canal de YouTube registrado en `SocialLinks` (`GetYouTubeLink()`), sustituye la mención de Instagram por `NombreOficial (@HandleYouTube)`.
   - Si está en el directorio de Ludeka pero no tiene canal de YouTube, sustituye por su `NombreOficial` limpio y registra el enlace canónico a su ficha en Ludeka (`https://ludeka.es/editoriales/{slug}`, `/tiendas/{slug}` o `/creadores/{slug}`).
2. **Fase 2 (Adaptación con Google Gemini Flash + Fallback Determinista):**
   - Si hay `Gemini:ApiKey` activa, solicita en JSON estructurado un título atractivo ($\le 90$ caracteres + `#Shorts` $\le 100$ caracteres) y la reescritura del cuerpo aplicando el mapa de menciones resueltas, transformando cualquier `@cuenta` externa desconocida en su nombre legible sin `@`, y eliminando referencias exclusivas de Instagram (*«link en bio»*, *«enlace en el perfil»*).
   - Si Gemini no está disponible o falla, aplica un motor determinista que limpia `@handles` externos, suprime frases de Instagram y extrae la primera línea como título.
3. **Fase 3 (Pie Editorial Determinista de Ludeka):** Añade al final de la descripción los enlaces a las fichas de Ludeka detectadas en el vídeo, el bloque de llamada a la acción hacia `https://ludeka.es` y los hashtags `#Shorts #JuegosDeMesa #BoardGames #Ludeka`.

### 3.5. Cliente de Subida a YouTube Shorts (`IYouTubeShortsUploadClient` / `YouTubeShortsUploadClient`) y Orquestador (`InstagramYouTubeSyncService`) (INC-150)
- **Subida OAuth 2.0 Resumable:** Intercambia `YouTube:OAuthRefreshToken` (con fallback a `Authentication:Google:ClientId/ClientSecret` en web) contra `https://oauth2.googleapis.com/token` y ejecuta una subida resumable en dos pasos a `https://www.googleapis.com/upload/youtube/v3/videos?uploadType=resumable&part=snippet,status` (`categoryId = "20"`, `privacyStatus` configurable, `selfDeclaredMadeForKids = false`). Si detecta un error HTTP 403/429 con `quotaExceeded`, lanza `YouTubeQuotaExceededException`.
- **Orquestación y Cuota Gratuita (`InstagramYouTubeSyncService`):**
  - Filtra Reels fuera del rango configurable $[60\text{s}, 180\text{s}]$ marcándolos como `Skipped` (si Meta omite `video_duration`, se acepta el Reel).
  - Ordena los pendientes cronológicamente (`InstagramPublishedAt ASC`, del más antiguo al más reciente) y sube un máximo de `MaxUploadsPerRun = 5` vídeos por ejecución ($5 \times 1.600 = 8.000$ unidades $\le 10.000$ unidades/día gratuitas de YouTube Data API v3).
  - Ante `YouTubeQuotaExceededException`, detiene el lote limpiamente dejando el resto en `Pending` para el día siguiente.

---

## 4. Persistencia, Jobs e Infraestructura (`Ludeka.Infrastructure` y `Ludeka.Jobs`)

- **Entity Framework Core (`LudekaDbContext`):**
  - Mapeo de las tablas `InstagramPostDrafts` e `InstagramReelSyncItems` (migración `20261017120000_AddInstagramReelSyncItems` y script DDL `docs/database/supabase_schema.sql`).
  - Mapeo de columnas `InstagramMediaId` e `InstagramPermalink` en `Giveaways` y `WeeklyReleases`.
- **Reconciliación Automática de Base de Datos (`SqliteSchemaMigrator`):**
  - Creación idempotente de `InstagramPostDrafts` e `InstagramReelSyncItems` y sus índices en SQLite.
- **Repositorios Específicos:** `SqliteInstagramPostDraftRepository` y `SqliteInstagramReelSyncRepository`.
- **Runner Desatendido (`InstagramYouTubeSyncJobRunner`):**
  - Registrado en `Ludeka.Jobs` bajo el nombre canónico `instagram-youtube-sync` (`JobNames.InstagramYouTubeSync`), con control de concurrencia mediante lease en `JobExecutionCoordinator` e inyección de `YouTube__OAuthClientId`, `YouTube__OAuthClientSecret` y `YouTube__OAuthRefreshToken` en `.github/workflows/ci-cd.yml`.

---

## 5. Experiencia de Usuario y Capa Web (`Ludeka.Web`)

### 5.1. Panel de Moderación de Instagram (`/admin/instagram`)
- **Acceso Restringido:** Solo accesible por usuarios con rol `FoundingTeam` o moderadores con permiso explícito `CanPublishInstagram`.
- **Conmutador de Pestañas:**
  1. **Compositor y Borradores:** Bandeja lateral por estado, simulador de feed 1:1 en vivo, editor con selector de tema Oscuro/Claro, nube de hashtags y despacho directo a Meta Graph API.
  2. **Sincronización YouTube Shorts (INC-150):** Tarjetas de resumen (`Subidos a Shorts`, `Pendientes`, `Omitidos`, `Fallidos`), botón interactivo «Sincronizar ahora (máx. 5)» protegido por `IStaffWriteGuard` (`CanPublishInstagram`) y tabla de historial con título adaptado, menciones resueltas del directorio de Ludeka, estado y enlaces directos al Reel en Instagram y al Short en YouTube.

### 5.2. Puntos de Entrada Contextuales y Endpoint Vectorial
- **Tarjeta de Sorteo (`GiveawayCard.razor`) y Vista de Novedades (`News.razor`):** Botones contextuales para crear post de Instagram y badges de publicación.
- **Endpoint Minimal API:** `GET /api/instagram/card/{draftId:guid}.svg`.

---

## 6. Verificación Automatizada

La funcionalidad completa se encuentra validada dentro de las **2.916 pruebas unitarias y 10 de integración en verde (2.926 totales)**:
1. **`InstagramPostDraftTests`, `InstagramComposerServiceTests`, `InstagramApiClientTests`, `InstagramPublisherServiceTests`:** Cobertura integral del compositor SVG, borradores, publicación y auditoría.
2. **`InstagramYouTubeSyncTests` (INC-150):**
   - Resolución de menciones de editoriales con canal de YouTube (`@deviriberia` $\to$ `Devir Iberia (@DevirTV)`), tiendas sin YouTube con enlace a su ficha en `ludeka.es` (`@zacatrus` $\to$ `Zacatrus` + `https://ludeka.es/tiendas/zacatrus`) y limpieza de `@handles` externos y frases *«link en bio»*.
   - Filtrado por duración ($60\text{s}\text{–}180\text{s}$), aceptación de Reels sin metadato de duración, ordenación cronológica ascendente, tope de `MaxUploadsPerRun = 5`, idempotencia y parada segura ante `YouTubeQuotaExceededException`.

