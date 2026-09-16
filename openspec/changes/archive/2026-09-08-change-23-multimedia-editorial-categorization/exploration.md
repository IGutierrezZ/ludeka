# Exploración Técnica y Análisis de Dominio: change-23-multimedia-editorial-categorization

## 1. Contexto y Objetivos del Incremento

El Incremento 23 tiene como objetivo dotar a Ludeka de una clasificación taxonómica formal de contenidos audiovisuales y una suite de herramientas de moderación editorial en vivo sobre los vídeos vinculados a las fichas de juego.

Los 4 ejes fundamentales son:
1. **Taxonomía Formal de Contenidos Multimedia (`MediaCategory`):**
   - Incorporación del enum `MediaCategory` en el dominio:
     - `Tutorial`: Explicaciones de reglas y guías paso a paso de cómo jugar.
     - `Gameplay`: Partidas completas o demostrativas con número de participantes explícito.
     - `ReviewOpinion`: Críticas, análisis de sensaciones y veredictos de creadores.
     - `Unboxing`: Aperturas de caja, componentes y primeras impresiones de producción.
2. **Heurística de Clasificación Editorial:**
   - Analizador heurístico inteligente (`MediaClassifier`) que deduce la categoría sugerida en base a patrones semánticos en el título y descripción del contenido.
3. **Categorización en el Panel de Moderación (`/admin/multimedia` y alias):**
   - Selector desplegable obligatorio de categoría para moderadores antes de aprobar vídeos encolados o huérfanos, con preselección heurística en 1 clic.
4. **Gestión Multimedia Directa en la Ficha de Juego (`GameDetail.razor` / `MultimediaHub.razor`):**
   - Para usuarios con rol `FoundingTeam` o `Moderator` con permiso `CanApproveMedia`:
     - Menú contextual de moderación rápida `[ ⚙️ Moderar Vídeo ]` en cada tarjeta audiovisual.
     - **Cambio Inmediato de Categoría:** Mueve el vídeo entre pestañas en tiempo real.
     - **Reasignación de Juego Asociado:** Modal con buscador asistido para transferir vídeos asociados erróneamente a otro juego.
     - **Eliminación / Desvinculación:** Eliminación segura con confirmación.
   - Trazabilidad completa mediante la bitácora universal de auditoría (`AuditLogEntry`, INC-20) registrando acciones `Updated` y `Deleted` para `AuditEntityType.Media`.

---

## 2. Diagnóstico del Estado Actual del Código

### 2.1 Dominio (`Ludeka.Core`)
- **`MediaItem.cs`:**
  - Actualmente posee `MediaType Type` (`Tutorial`, `Playthrough`, `InstagramPost`, `ShortReel`, `QuickOverview`), `MediaPlatform Platform`, `Status`, `GameId`, `DurationSeconds`, `PlayerCountBadge`, etc.
  - Carece de la propiedad de categoría editorial tipada `MediaCategory Category`.
  - Debe incorporarse el enum `MediaCategory` y métodos de mutación en la entidad:
    - `ChangeCategory(MediaCategory newCategory)`
    - `ReassignGame(Guid newGameId)`
  - Se debe garantizar compatibilidad de constructores y valores predeterminados (por ejemplo, infiriendo `Category` a partir de `MediaType` si no se especifica explícitamente).
- **`PlayerCountExtractor.cs`:**
  - Ya existe y extrae con éxito badges de comensales para `Playthrough` / `Gameplay`.
- **Nuevo `MediaClassifier.cs`:**
  - Se creará un analizador heurístico puro en `Ludeka.Core/Helpers/MediaClassifier.cs` para clasificar títulos en `Tutorial`, `Gameplay`, `ReviewOpinion` y `Unboxing`.

### 2.2 Aplicación (`Ludeka.Application`)
- **`IMediaService.cs` y `MediaService.cs`:**
  - Métodos existentes: `GetGameMediaAsync`, `GetPendingModerationAsync`, `GetOrphansAsync`, `GetApprovedAsync`, `ApproveMediaAsync`, `RejectMediaAsync`, `AssignOrphanMediaAsync`, `CheckBrokenLinksAsync`, `CreateMediaItemAsync`.
  - Métodos a añadir / extender:
    - `ApproveMediaAsync(Guid id, MediaCategory? category = null, CancellationToken ct = default)`: Permite especificar o ajustar la categoría al aprobar.
    - `UpdateMediaCategoryAsync(Guid id, MediaCategory newCategory, CancellationToken ct = default)`: Cambia la categoría de un vídeo existente y registra auditoría.
    - `ReassignMediaGameAsync(Guid id, Guid newGameId, CancellationToken ct = default)`: Transfiere el vídeo a otro juego y registra auditoría.
    - `DeleteMediaAsync(Guid id, CancellationToken ct = default)`: Elimina el elemento audiovisual del catálogo y registra auditoría.
  - Validación de seguridad: Todos los métodos de moderación deben invocar `EnsurePermission()`, verificando `IsFoundingTeam` o el permiso granular `ModeratorPermission.CanApproveMedia`.
  - Auditoría: Inyectar `IAuditService` para registrar las entradas con `AuditAction.Updated` y `AuditAction.Deleted`.
- **DTOs (`MediaDtos.cs`):**
  - `MediaItemDto`: Añadir propiedad `MediaCategory Category`.
  - `GameMediaHubDto`: Añadir soporte para contenidos de tipo `ReviewOpinion` y `Unboxing`, permitiendo su visualización y filtrado ordenado en las pestañas del componente.

### 2.3 Infraestructura (`Ludeka.Infrastructure`)
- **`LudekaDbContext`:**
  - Añadir índice sobre `MediaItem.Category`.
- **`SqliteSchemaMigrator`:**
  - Añadir reconciliación defensiva de esquema SQLite para la tabla `MediaItems`:
    - Comprobar si existe la columna `Category`.
    - Ejecutar `ALTER TABLE "MediaItems" ADD COLUMN "Category" INTEGER NOT NULL DEFAULT 0;` si no existe.
- **`CatalogSeeder`:**
  - Actualizar semillado inicial de contenidos multimedia asignando explícitamente sus categorías editoriales.
- **`YouTubeSearchService`:**
  - Utilizar `MediaClassifier` para inferir tanto la categoría como el tipo al ingestar nuevos vídeos.

### 2.4 Interfaz de Usuario Blazor (`Ludeka.Web`)
- **`MediaModeration.razor`:**
  - Añadir ruta `@page "/admin/multimedia"`.
  - En la lista de pendientes y huérfanos: incorporar un desplegable interactivo `<select>` para que el moderador confirme o modifique la `MediaCategory` sugerida por la heurística antes de pulsar "Aprobar".
- **`MultimediaHub.razor`:**
  - Integrar menú contextual accesible `[ ⚙️ Moderar ]` en cada tarjeta de vídeo, visible solo si `CurrentUserService.IsFoundingTeam || (CurrentUserService.IsInRole("Moderator") && CurrentUserService.HasPermission(ModeratorPermission.CanApproveMedia))`.
  - Acciones del menú:
    1. **Cambiar Categoría:** Desplegable con confirmación inmediata que actualiza la UI al instante.
    2. **Reasignar Juego:** Modal con buscador asistido de juegos del catálogo (`SearchGamesAsync`).
    3. **Eliminar de la ficha:** Modal de confirmación para retirar el vídeo.
- **`GameDetail.razor`:**
  - Pasar eventos o delegar la recarga fluida del `_mediaHub` tras operaciones de moderación directa para actualizar la vista sin recargar la página web.

---

## 3. Matriz de Riesgos y Mitigaciones

| Riesgo | Impacto | Mitigación |
|---|---|---|
| Incompatibilidad retroactiva en bases de datos SQLite existentes sin la columna `Category` | Medio | Reconciliación automática e idempotente en `SqliteSchemaMigrator.cs` (`ADD COLUMN Category INTEGER NOT NULL DEFAULT 0`). |
| Falsos positivos en la clasificación heurística de títulos | Bajo | La heurística solo propone un valor predeterminado; el moderador tiene siempre la última palabra mediante el selector desplegable. |
| Intento de acceso no autorizado a operaciones de moderación | Alto | Doble barrera de seguridad: UI condicional en Blazor (`AuthorizeView` / comprobación de permisos) y validación defensiva en `MediaService` lanzando `UnauthorizedAccessException`. |
| Desincronización visual al reasignar o cambiar categoría en `GameDetail` | Bajo | Disparo de eventos `EventCallback` desde `MultimediaHub.razor` hacia `GameDetail.razor` para invalidar y refrescar `_mediaHub` en memoria de forma reactiva. |
