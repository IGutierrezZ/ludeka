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
  - 4 pestañas interactivas: `⚡ Cómo Funciona`, `🎬 Tutoriales`, `🎲 Partidas`, `💬 Redes & Reseñas`.
  - Pestaña social dividida en subsección destacada para **Reseñas en Vídeo** (16:9) y cuadrícula de redes sociales.
  - Botón contextual `[ ⚙️ Moderar ]` visible en cada tarjeta exclusivamente para usuarios con permiso de moderación o Mesa Fundadora:
    - **Selector de Categoría en Vivo:** permite reclasificar el vídeo instantáneamente sin salir de la ficha.
    - **Buscador Asistido de Reasignación:** caja de texto con autocompletado en tiempo real para mover el vídeo a la ficha correcta en caso de catalogación errónea.
    - **Eliminación Segura:** modal de confirmación con advertencia de irreversibilidad.
- [`MediaModeration.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Pages/MediaModeration.razor):
  - Accesible desde `/admin/multimedia` y `/moderacion-media`.
  - Bandeja centralizada de pendientes y huérfanos con selector de categoría preseleccionado por la heurística de `MediaClassifier`.
- [`YouTubeSearchModal.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/YouTubeSearchModal.razor):
  - Ingesta quirúrgica en 1 clic con previsualización embebida.
  - INC-82: Búsqueda reactiva de títulos del catálogo protegida con temporizador de retardo (debounce 250 ms), cancelación cooperativa con `CancellationTokenSource` por cada pulsación continua y ciclo de vida `IDisposable`. Erradica la saturación de eventos SignalR en Cloud Run y previene la desconexión del circuito («Reconectando con el servidor...») y el cierre accidental del modal.
  - Búsqueda manual desacoplada de la selección del catálogo (hacer clic en una sugerencia fija el título sin lanzar búsqueda no deseada), timeout de 10s en cliente HTTP y `CancellationTokenSource` con botón interactivo de «Cancelar Búsqueda» y reporte explícito de errores HTTP de YouTube API (403 cuota / 401 clave).
