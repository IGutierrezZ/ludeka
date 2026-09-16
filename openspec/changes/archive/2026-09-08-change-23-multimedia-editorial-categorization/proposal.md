# Propuesta de Cambio: change-23-multimedia-editorial-categorization

## 1. Resumen Ejecutivo
El **Incremento 23** dota a Ludeka de un sistema integral de clasificación taxonómica de contenidos audiovisuales y herramientas de moderación editorial directa desde la propia ficha de juego:
1. **Taxonomía Formal de Vídeos (`MediaCategory`):**
   - Incorporación del enum `MediaCategory` con 4 valores esenciales: `Tutorial` (explicaciones de reglas), `Gameplay` (partidas completas o demostrativas con número de comensales), `ReviewOpinion` (análisis, sensaciones y críticas) y `Unboxing` (apertura de componentes y primeras impresiones).
2. **Clasificador Heurístico Inteligente (`MediaClassifier`):**
   - Detección automática en base al título y descripción para sugerir la categoría por defecto con alta precisión.
3. **Categorización en el Panel de Moderación (`/admin/multimedia` y alias):**
   - Selector desplegable obligatorio de categoría para moderadores antes de aprobar vídeos encolados o huérfanos, con preselección heurística en 1 clic.
4. **Gestión Multimedia Directa en Fichas de Juego (`GameDetail.razor` / `MultimediaHub.razor`):**
   - Para moderadores (`CanApproveMedia`) y Mesa Fundadora:
     - Menú contextual flotante `[ ⚙️ Moderar Vídeo ]` en cada tarjeta de vídeo.
     - **Cambio Inmediato de Categoría:** Selector rápido para reubicar el vídeo entre pestañas en tiempo real (ej. de Opiniones a Tutoriales).
     - **Reasignación de Juego:** Buscador asistido por título para transferir vídeos asociados erróneamente a otra ficha del catálogo.
     - **Eliminación / Desvinculación:** Retirada segura de vídeos de la ficha con modal de confirmación.
5. **Trazabilidad Universal con Bitácora de Auditoría:**
   - Registro automático en `AuditLogEntry` (INC-20) con `AuditEntityType.Media` y acciones `Updated` / `Deleted` documentando el usuario, el juego original, los campos alterados y el motivo.

---

## 2. Justificación y Valor para el Ecosistema
1. **Fichas Lúdicas Rigurosas y Ordenadas:** Actualmente los vídeos se agregan sin una taxonomía estricta de 4 categorías, lo que puede provocar que una partida se clasifique como reseña o que un tutorial quede oculto. La taxonomía formal garantiza que el jugador encuentre exactamente lo que necesita: aprender a jugar, ver una partida a su número de jugadores o escuchar una opinión sincera.
2. **Eficiencia para la Moderación (Cero Fricción):** El moderador no debe estar obligado a navegar hasta el panel de administración central para corregir un vídeo mal vinculado o mal clasificado. Poder hacerlo directamente en la ficha del juego en 2 clics reduce el tiempo de resolución editorial a menos de 5 segundos.
3. **Mantenimiento del Catálogo y Prevención de Errores:** En juegos con múltiples expansiones (ej. *Ark Nova* vs. *Ark Nova: Mundos Marinos* o *Wingspan* vs. *Wingspan: Oceanía*), es frecuente que los bots o usuarios sugieran un vídeo de la expansión en la ficha del juego base. La reasignación asistida resuelve esto limpiamente sin perder las miniaturas ni estadísticas del vídeo.
4. **Seguridad y Auditoría:** Al igual que en la edición de fichas y resolución de reportes (INC-18 e INC-20), cada acción queda firmada en la auditoría editorial, evitando vandalismo y asegurando total transparencia para la Mesa Fundadora.

---

## 3. Alcance de la Propuesta por Capas

### 3.1 Dominio (`Ludeka.Core`)
- **`MediaCategory`:** Nuevo enum en `Ludeka.Core/Enums/MediaCategory.cs`:
  - `Tutorial = 0`: Explicación de reglas y cómo jugar.
  - `Gameplay = 1`: Partidas completas o demostrativas.
  - `ReviewOpinion = 2`: Reseñas, críticas, análisis y primeras impresiones.
  - `Unboxing = 3`: Apertura de caja y componentes físicos.
- **`MediaItem`:**
  - Nueva propiedad `Category`: `public MediaCategory Category { get; private set; }`.
  - Métodos de mutación:
    - `ChangeCategory(MediaCategory newCategory)`
    - `ReassignGame(Guid newGameId)`
  - Actualización defensiva de constructores infiriendo `Category` desde `Type` cuando sea nula.
- **`MediaClassifier`:**
  - Componente de lógica pura en `Ludeka.Core/Helpers/MediaClassifier.cs` con expresiones regulares para clasificar títulos en `Tutorial`, `Gameplay`, `ReviewOpinion` y `Unboxing`.

### 3.2 Aplicación (`Ludeka.Application`)
- **`IMediaService` y `MediaService`:**
  - `ApproveMediaAsync(Guid id, MediaCategory? category = null, CancellationToken ct = default)`
  - `UpdateMediaCategoryAsync(Guid id, MediaCategory newCategory, CancellationToken ct = default)`
  - `ReassignMediaGameAsync(Guid id, Guid newGameId, CancellationToken ct = default)`
  - `DeleteMediaAsync(Guid id, CancellationToken ct = default)`
  - Verificación estricta de permisos (`EnsurePermission()` con `ModeratorPermission.CanApproveMedia` o `IsFoundingTeam`).
  - Registro de cambios estructurado en `IAuditService`.
- **`MediaDtos`:**
  - `MediaItemDto`: inclusión de `Category` (y nombre para mostrar en español).
  - `GameMediaHubDto`: enriquecimiento para servir colecciones de reseñas/opiniones y unboxings o estructuración en las pestañas correspondientes.
  - `UpdateMediaCategoryRequest`, `ReassignMediaGameRequest`.

### 3.3 Infraestructura (`Ludeka.Infrastructure`)
- **`LudekaDbContext`:**
  - Mapeo de índice en `MediaItems` sobre la columna `Category`.
- **`SqliteSchemaMigrator`:**
  - Migración idempotente para SQLite: comprobación y ejecución de `ALTER TABLE "MediaItems" ADD COLUMN "Category" INTEGER NOT NULL DEFAULT 0;`.
- **`YouTubeSearchService`:**
  - Integración de `MediaClassifier` para inferir `Category` en ingestas automáticas.
- **`CatalogSeeder`:**
  - Precarga de contenidos multimedia con su correspondiente `MediaCategory`.

### 3.4 Presentación Web (`Ludeka.Web`)
- **`MediaModeration.razor`:**
  - Añadir ruta `@page "/admin/multimedia"`.
  - En la vista de elementos pendientes y huérfanos: selector desplegable obligatorio de `MediaCategory` con preselección heurística en base al título antes de confirmar aprobación.
- **`MultimediaHub.razor`:**
  - Menú contextual `[ ⚙️ Moderar ]` en cada tarjeta audiovisual, visible exclusivamente para moderadores autorizados (`CanApproveMedia` o Mesa Fundadora).
  - Acciones integradas:
    - Selector rápido de categoría para mover el vídeo entre pestañas.
    - Modal de reasignación asistida con autocompletado/búsqueda de juegos.
    - Diálogo de confirmación para eliminar/desvincular el vídeo.
  - Actualización reactiva de estado (sin refresco forzado del navegador completo).

---

## 4. Estrategia de Pruebas y Verificación
- Pruebas unitarias de dominio para `MediaCategory`, `MediaItem.ChangeCategory`, `MediaItem.ReassignGame` y validaciones de invariantes.
- Pruebas unitarias para `MediaClassifier` cubriendo títulos reales de tutoriales ("cómo jugar", "aprende las reglas"), partidas ("partida a 2", "gameplay en solitario"), reseñas ("análisis y opinión", "vale la pena") y unboxings ("abriendo la caja", "unboxing y componentes").
- Pruebas unitarias en `MediaServiceTests` para:
  - Cambio de categoría con verificación de auditoría `AuditAction.Updated` y `AuditEntityType.Media`.
  - Reasignación de juego con verificación de desvinculación y nuevo juego.
  - Eliminación con verificación de auditoría `AuditAction.Deleted`.
  - Denegación de acceso para usuarios no autorizados (`UnauthorizedAccessException`).
- Verificación visual y de renderizado en componentes Blazor.
