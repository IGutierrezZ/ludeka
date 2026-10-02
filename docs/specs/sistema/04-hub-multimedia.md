# 04. Hub Multimedia en Español

## 1. Visión General y Propósito
El Hub Multimedia organiza el contenido audiovisual de cada juego de mesa segregándolo en cuatro formatos claramente diferenciados mediante pestañas horizontales limpias, evitando la mezcla de miniaturas panorámicas con vídeos verticales o imágenes cuadradas. Incorpora categorización editorial asistida por heurística semántica, herramientas de moderación directa desde la propia ficha de juego, reasignación asistida con autocompletado y trazabilidad integral en la bitácora de auditoría.

---

## 2. Segregación de Formatos y Taxonomía Editorial

La taxonomía editorial oficial se modela mediante el enum `MediaCategory`:

| Pestaña / Categoría | `MediaCategory` | Formato | Plataforma | Contenido | Requisitos Específicos |
|---|---|---|---|---|---|
| **⚡ Cómo Funciona** | `QuickOverview` (0) | 16:9 Panorámico | YouTube | Vistazo rápido de mecánicas | Duración breve (≤ 2–3 min) para entender el flujo lúdico sin ser un tutorial exhaustivo. |
| **🎬 Tutoriales** | `Tutorial` (1) | 16:9 Panorámico | YouTube | Explicación de reglas completas | Canal, duración estimada (8–25 min), miniatura oficial. |
| **🎲 Partidas Completas** | `Gameplay` (2) | 16:9 Panorámico | YouTube | Partida jugada de principio a fin | **Badge obligatorio de comensales** (ej. *"Partida a 2"*, *"En solitario"*). |
| **💬 Opiniones y Redes** | `ReviewOpinion` (3) | Mixto (16:9, 1:1, 9:16) | YouTube / Instagram / TikTok | Reseñas, primeras impresiones, unboxings y reels | Subsección destacada para **Reseñas en Vídeo** panorámicas y carrusel social para reels/posts. |

---

## 3. Modelo de Dominio (`Ludeka.Core`)

### 3.1 Entidad `MediaItem`
Ubicación: [`src/Ludeka.Core/Entities/MediaItem.cs`](file:///c:/repos/Ludeka/src/Ludeka.Core/Entities/MediaItem.cs)

- `Id` (Guid), `GameId` (Guid?).
- `Type`: Enum `MediaType` (`QuickOverview`, `Tutorial`, `Playthrough`, `InstagramPost`, `ShortReel`).
- `Category`: Enum `MediaCategory` (`QuickOverview`, `Tutorial`, `Gameplay`, `ReviewOpinion`).
- `Platform`: Enum `MediaPlatform` (`YouTube`, `Instagram`, `TikTok`).
- `Title`, `Url`, `EmbedUrl`, `ThumbnailUrl`, `AuthorChannel`.
- `DurationSeconds` (int?), `PlayerCountBadge` (string?).
- `Status`: Enum `ModerationStatus` (`PendingApproval`, `Approved`, `Rejected`).
- `IsBroken`: booleano activado si el detector de enlaces rotos detecta HTTP 404 o contenido privado.
- **Métodos de Mutación:**
  - `ChangeCategory(MediaCategory newCategory)`: actualiza la categoría y sincroniza reactivamente `Type` y `PlayerCountBadge` cuando proceda.
  - `ReassignGame(Guid newGameId)`: reasocia el contenido a otro juego del catálogo con validación de no-vacío.
  - `Approve(MediaCategory? category = null)`: aprueba el ítem y permite recategorizarlo en un único paso atómico.

### 3.2 Clasificador Heurístico Inteligente
Ubicación: [`src/Ludeka.Core/Helpers/MediaClassifier.cs`](file:///c:/repos/Ludeka/src/Ludeka.Core/Helpers/MediaClassifier.cs)
- Evalúa patrones semánticos multivariante mediante expresiones regulares en español e inglés:
  - *QuickOverview:* "cómo funciona", "vistazo rápido", "en 2/3 minutos", "overview", "quick look", "resumen de mecánicas".
  - *Gameplay:* "partida", "gameplay", "jugando a", "playthrough", "let's play", "duelo a 2", "en solitario".
  - *ReviewOpinion:* "reseña", "opinión", "análisis", "primeras impresiones", "¿vale la pena?", "veredicto", "unboxing", "abriendo la caja", "review".
  - *Tutorial:* "cómo jugar", "tutorial", "aprende a jugar", "reglas", "explicación", "how to play".
  - *Fallback:* `MediaCategory.Tutorial` si el texto no contiene patrones inequívocos.

### 3.3 Extractor de Jugadores para Partidas
Ubicación: [`src/Ludeka.Core/Helpers/PlayerCountExtractor.cs`](file:///c:/repos/Ludeka/src/Ludeka.Core/Helpers/PlayerCountExtractor.cs)
- Analizador con expresiones regulares para detectar comensales en títulos y descripciones en español (`"a 2"`, `"a 3"`, `"en solitario"`), con fallback a la escalabilidad ideal del juego.

---

## 4. Servicios, Ingesta y Moderación Editorial (`Ludeka.Application` & `Ludeka.Infrastructure`)

- **Búsqueda Quirúrgica e Ingesta de YouTube:** [`IYouTubeSearchService`](file:///c:/repos/Ludeka/src/Ludeka.Application/Contracts/IYouTubeSearchService.cs) implementado en [`YouTubeSearchService.cs`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/YouTube/YouTubeSearchService.cs):
  - Ingesta automática aplicando clasificación heurística inicial mediante `MediaClassifier.Classify(request.Title)`.
  - Padrón oficial de canales con [`IChannelFocusProvider`](file:///c:/repos/Ludeka/src/Ludeka.Application/Contracts/IChannelFocusProvider.cs) para priorizar editoriales, divulgadores y tiendas hispanohablantes.
- **Servicio de Medios y Moderación:** [`MediaService`](file:///c:/repos/Ludeka/src/Ludeka.Application/Features/Media/MediaService.cs):
  - `GetGameMediaAsync`: segrega activamente en colecciones de `QuickOverviews`, `Tutorials`, `Playthroughs`, `InstagramPosts`, `ShortReels` y `ReviewsAndOpinions`.
  - `FlashIngestAsync`: ingesta flash directa de vídeos de YouTube mediante URL y categoría (INC-92). Valida formato de URL (`youtube.com`, `youtu.be`, `shorts`), previene duplicados (`ExistsByUrlAsync`), extrae metadatos mediante `ISocialMetadataExtractor` (título, duración, miniatura, autor/canal), infiere número de comensales para partidas (`PlayerCountExtractor`) y publica de forma inmediata con estado aprobado y auditoría (`AuditAction.Created`).
  - `UpdateMediaCategoryAsync`: cambio dinámico de categoría editorial.
  - `ReassignMediaGameAsync`: reasignación hacia otro juego del catálogo con validación de existencia.
  - `DeleteMediaAsync`: eliminación física de enlaces erróneos u obsoletos.
  - `ApproveMediaAsync`: aprobación individual con soporte de categoría explícita.
  - **Seguridad y Permisos Granulares:** Todas las mutaciones exigen `ModeratorPermission.CanApproveMedia` o pertenecer al rol `FoundingTeam`.
  - **Trazabilidad y Auditoría:** Cada mutación registra un evento en la bitácora central [`IAuditService`](file:///c:/repos/Ludeka/src/Ludeka.Application/Contracts/IAuditService.cs) (`AuditEntityType.Media`) detallando cambios de categoría, juegos origen/destino o eliminaciones.
- **Persistencia y Migración SQLite:**
  - Columna `Category` e índice en tabla `MediaItems` añadidos mediante el paso 15 en [`SqliteSchemaMigrator.cs`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/Data/SqliteSchemaMigrator.cs).

---

## 5. Componentes UI (`Ludeka.Web`)

- [`MultimediaHub.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/MultimediaHub.razor):
  - **Despeje Visual y Cabecera Compacta (INC-92):** Supresión de títulos y subtítulos estáticos redundantes («Hub Multimedia en Español»). Acceso frontal inmediato al selector de 4 pestañas interactivas: `⚡ Cómo Funciona`, `🎬 Tutoriales`, `🎲 Partidas`, `💬 Redes & Reseñas` con badges numéricos de contenido e iconografía Lucide normalizada (sin emojis crudos).
  - **Tarjetas Limpias y Sin Ruido (INC-92):** Eliminación de pies de tarjeta repetitivos («YouTube Tutorial», «Reproducir →», «Ver análisis →»). La reproducción se lanza directamente al pulsar la tarjeta o su miniatura (o mediante `Enter` / `Espacio`), ahorrando espacio vertical.
  - **Barra de Moderación Directa en Tarjetas (INC-92):** Barra inferior visible exclusivamente para moderadores (`IsModeratorUser`):
    - **`[🏷 Cambiar Categoría]`:** abre el modal enfocado en el selector de categoría y reasignación de juego.
    - **`[🗑 Eliminar]`:** activa el diálogo directo de confirmación y desvinculación sin pasos intermedios.
  - **Modal de Ingesta Flash (INC-92):** Botón `[⚡ Ingesta Flash]` para moderadores que despliega un modal reactivo para pegar la URL de YouTube, seleccionar categoría destino, indicar comensales opcionales para partidas y publicar al instante.
  - **Rediseño con Barra Segmentada y Carrusel Horizontal (INC-99):**
    - **Barra Segmentada Profesional:** Sustitución de píldoras amontonadas con fondos discordantes por un control segmentado estilo cápsula (`bg-[var(--bg-surface-elevated)] p-1 rounded-xl border border-[var(--border-subtle)]`) en fila única, con badges de conteo y estilos acordes a la paleta de Ludeka (`Todos`, `Tutoriales`, `Cómo Funciona`, `Partidas`, `Opiniones y Redes`).
    - **Carrusel Horizontal de Vídeos:** Sustitución de la cuadrícula vertical de 2 columnas gigantescas por un carril horizontal deslizante (`id="media-carousel-rail"`, `snap-x snap-mandatory scrollbar-none`), con tarjetas compactas (260px a 310px) en proporción 16:9 que permiten ver 2 o 3 vídeos a la vez sin forzar scroll vertical en la página.
    - **Controles de Desplazamiento y Arrastre:** Flechas circulares prev/next en la barra superior conectadas con `window.ludekaScrollRail` en `rail-scroll.js`, manteniendo compatibilidad con arrastre por ratón o táctil.
    - **Integración Limpia:** Supresión del doble contenedor con borde y padding pesado (`p-6 bg-[var(--bg-card)] border`), adaptándose de forma natural a la pestaña de `GameDetail.razor`.
- [`MediaModeration.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Pages/MediaModeration.razor):
  - Accesible desde `/admin/multimedia` y `/moderacion-media`.
  - Bandeja centralizada de pendientes y huérfanos con selector de categoría preseleccionado por la heurística de `MediaClassifier`.
- [`YouTubeSearchModal.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/YouTubeSearchModal.razor):
  - Ingesta quirúrgica en 1 clic con previsualización embebida.
  - INC-82: Búsqueda reactiva de títulos del catálogo protegida con temporizador de retardo (debounce 250 ms), cancelación cooperativa con `CancellationTokenSource` por cada pulsación continua y ciclo de vida `IDisposable`. Erradica la saturación de eventos SignalR en Cloud Run y previene la desconexión del circuito («Reconectando con el servidor...») y el cierre accidental del modal.
  - Búsqueda manual desacoplada de la selección del catálogo (hacer clic en una sugerencia fija el título sin lanzar búsqueda no deseada), timeout de 10s en cliente HTTP y `CancellationTokenSource` con botón interactivo de «Cancelar Búsqueda», sincronización determinista del circuito Blazor Server mediante `InvokeAsync(StateHasChanged)` al inicio, finalización y cancelación, `ConfigureAwait(false)` en llamadas de infraestructura y deduplicación defensiva por `VideoId` en respuestas de YouTube Data API v3.
