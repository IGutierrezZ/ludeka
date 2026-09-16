# Especificación de Requerimientos: change-23-multimedia-editorial-categorization

## 1. Introducción y Propósito
El Incremento 23 dota a Ludeka de una clasificación taxonómica formal de contenidos audiovisuales y una suite de herramientas de moderación editorial en vivo sobre los vídeos vinculados a las fichas de juego.
Garantiza que los contenidos se agrupen de forma coherente en las pestañas del Hub Multimedia y permite a moderadores autorizados reasignar categorías, transferir vídeos mal vinculados o eliminar enlaces sin salir de la ficha del juego, con trazabilidad completa en la bitácora universal de auditoría.

---

## 2. Requerimientos Funcionales (RF)

### RF-1: Taxonomía Formal de Contenidos Multimedia (`MediaCategory`)
- **RF-1.1:** Se define el enum formal `MediaCategory` en el dominio con 4 categorías clave:
  - `QuickOverview` (0): Vídeos breves ("Cómo Funciona") que resumen en 2 minutos las mecánicas principales y dinámica de juego sin constituir un tutorial completo.
  - `Tutorial` (1): Explicaciones detalladas de reglas completas y guías paso a paso de cómo jugar.
  - `Gameplay` (2): Partidas completas o demostrativas con número explícito de participantes (requiere badge de comensales).
  - `ReviewOpinion` (3): Reseñas críticas, análisis de sensaciones, veredictos y primeras impresiones de creadores.
- **RF-1.2:** Cada entidad `MediaItem` debe almacenar su `Category` tipada y garantizar compatibilidad retroactiva con el atributo existente `MediaType Type`.
- **RF-1.3:** La entidad `MediaItem` expone métodos de mutación de dominio:
  - `ChangeCategory(MediaCategory newCategory)`: actualiza la categoría y refresca la fecha de modificación.
  - `ReassignGame(Guid newGameId)`: transfiere el vídeo a otro juego y refresca la fecha de modificación.

### RF-2: Clasificador Heurístico Inteligente (`MediaClassifier`)
- **RF-2.1:** Componente de dominio puro `MediaClassifier.Classify(string title, string? description = null)` que evalúa patrones semánticos:
  - Patrones para `QuickOverview`: "cómo funciona", "como funciona", "vistazo rápido", "vistazo rapido", "en 2 minutos", "en 3 minutos", "overview", "quick look", "resumen de mecánicas".
  - Patrones para `Tutorial`: "cómo jugar", "como jugar", "tutorial", "aprende a jugar", "reglas", "explicación", "how to play", "rules".
  - Patrones para `Gameplay`: "partida", "gameplay", "jugando a", "a 2", "a 3", "a 4", "duelo", "en solitario", "playthrough", "let's play".
  - Patrones para `ReviewOpinion`: "reseña", "resena", "opinión", "opinion", "análisis", "analisis", "primeras impresiones", "vale la pena", "merece la pena", "review", "crítica", "critica", "unboxing", "abriendo".
- **RF-2.2:** Si ningún patrón coincide, se asigna `Tutorial` como fallback predeterminado para vídeos horizontales de YouTube.

### RF-3: Categorización en el Panel de Moderación (`/admin/multimedia` y alias)
- **RF-3.1:** El panel de moderación multimedia responde a las rutas `/admin/multimedia`, `/moderacion/multimedia` y `/moderacion-media`.
- **RF-3.2:** En la cola de elementos pendientes y huérfanos, cada tarjeta presenta un selector interactivo `<select>` con las opciones de `MediaCategory`.
- **RF-3.3:** El selector se inicializa con la categoría sugerida por el `MediaClassifier`.
- **RF-3.4:** Al pulsar "Aprobar", se persiste la categoría seleccionada por el moderador.

### RF-4: Gestión Multimedia Directa en la Ficha del Juego (`MultimediaHub.razor`)
- **RF-4.1: Control de Acceso:** Los controles de moderación directa solo son visibles para usuarios con rol `FoundingTeam` o rol `Moderator` con el permiso granular `ModeratorPermission.CanApproveMedia`.
- **RF-4.2: Menú Contextual:** Cada tarjeta de vídeo en cualquiera de las pestañas del Hub incluye un botón flotante `[ ⚙️ Moderar ]`.
- **RF-4.3: Cambio Inmediato de Categoría:**
  - El moderador puede seleccionar una nueva categoría (`Cómo Funciona`, `Tutorial`, `Partida`, `Opinión/Reseña`).
  - La tarjeta desaparece de la pestaña actual y pasa a renderizarse en la pestaña destino de inmediato.
- **RF-4.4: Reasignación Asistida de Juego:**
  - Modal accesible con buscador en vivo por título de juego.
  - Al confirmar, el vídeo se desvincula del juego actual y queda asignado al nuevo juego.
  - El componente local refresca la vista eliminando la tarjeta de la ficha actual.
- **RF-4.5: Eliminación / Desvinculación de la Ficha:**
  - Botón de borrado con confirmación modal.
  - Al confirmar, el vídeo se elimina o desvincula del catálogo.

### RF-5: Integración con la Bitácora Universal de Auditoría (`IAuditService`)
- **RF-5.1:** Toda operación de recategorización registra una entrada en `AuditLogEntry` con `Action = AuditAction.Updated`, `EntityType = AuditEntityType.Media`, detallando el cambio de valor anterior y nuevo en el campo `Category`.
- **RF-5.2:** Toda reasignación de juego registra una entrada con `Action = AuditAction.Updated`, `EntityType = AuditEntityType.Media`, documentando el cambio de juego en el campo `GameId`.
- **RF-5.3:** Toda eliminación de vídeo registra una entrada con `Action = AuditAction.Deleted`, `EntityType = AuditEntityType.Media`.
- **RF-5.4:** Todas las entradas identifican el `UserId` y `UserName` del moderador responsable.

---

## 3. Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Clasificación heurística inteligente de un vídeo sugerido
  Dado un vídeo con título "Wingspan — Cómo jugar y reglas completas"
  Cuando se evalúa mediante MediaClassifier.Classify
  Entonces la categoría inferida es "Tutorial"

Escenario: Clasificación de vídeo corto de mecánicas
  Dado un vídeo con título "Catan — Vistazo rápido y cómo funciona en 2 minutos"
  Cuando se evalúa mediante MediaClassifier.Classify
  Entonces la categoría inferida es "QuickOverview"

Escenario: Moderador clasifica y aprueba un vídeo en el panel central
  Dado un moderador autenticado con permiso "CanApproveMedia" en "/admin/multimedia"
  Y un vídeo pendiente sugerido inicialmente como "Tutorial"
  Cuando el moderador cambia el selector a "ReviewOpinion" y pulsa "Aprobar"
  Entonces el vídeo adquiere el estado "Approved" con categoría "ReviewOpinion"
  Y se genera una entrada de auditoría para la entidad "Media"

Escenario: Moderador cambia la categoría de un vídeo desde la ficha de juego
  Dado un moderador autenticado con permiso "CanApproveMedia" en la ficha de "Wingspan"
  Y un vídeo clasificado en la pestaña "Opiniones y Redes" que explica cómo jugar
  Cuando pulsa en el menú contextual del vídeo y selecciona la categoría "Tutorial"
  Entonces el vídeo desaparece de la pestaña "Opiniones y Redes"
  Y pasa a renderizarse automáticamente en la pestaña "Tutoriales"
  Y se genera una entrada de auditoría con la acción "Updated" para la entidad "Media"

Escenario: Moderador reasigna un vídeo a otro juego distinto
  Dado un moderador en la ficha de "Ark Nova" visualizando un vídeo de la expansión "Ark Nova: Mundos Marinos"
  Cuando pulsa "Reasignar Juego" y busca "Ark Nova: Mundos Marinos"
  Y confirma el cambio
  Entonces el vídeo se desvincula de "Ark Nova"
  Y queda visible exclusivamente en la ficha de "Ark Nova: Mundos Marinos"
  Y se genera una entrada de auditoría con la acción "Updated" para la entidad "Media"

Escenario: Moderador elimina un vídeo de la ficha de juego
  Dado un moderador en la ficha de "Catan" visualizando un vídeo con enlace roto o inapropiado
  Cuando pulsa "Eliminar de la ficha" y confirma en el diálogo modal
  Entonces el vídeo se elimina del catálogo
  Y deja de mostrarse en la ficha de "Catan"
  Y se genera una entrada de auditoría con la acción "Deleted" para la entidad "Media"

Escenario: Usuario comunitario no visualiza herramientas de moderación
  Dado un usuario con rol comunitario navegando por la ficha de "Catan"
  Cuando visualiza las tarjetas del Hub Multimedia
  Entonces puede reproducir los vídeos con normalidad
  Pero no visualiza ningún botón o menú "Moderar", selector de categoría ni opción de borrado

Escenario: Denegación de acceso no autorizado en capa de aplicación
  Dado un usuario sin permiso "CanApproveMedia" ni rol "FoundingTeam"
  Cuando invoca UpdateMediaCategoryAsync o ReassignMediaGameAsync
  Entonces la operación es rechazada con UnauthorizedAccessException
  Y no se altera ningún registro en la base de datos
```
