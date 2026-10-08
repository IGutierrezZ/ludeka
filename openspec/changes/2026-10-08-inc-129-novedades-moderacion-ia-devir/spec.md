# Especificación de Requerimientos: INC-129

## 1. Requerimientos Funcionales (RF)

### RF-01: Estado de Moderación y Publicación en Novedades Editoriales
- La entidad `WeeklyRelease` debe disponer de un estado formal `Status` (`WeeklyReleaseStatus`):
  - `Published` (1): Visible en el calendario público de `/novedades`.
  - `PendingModeration` (2): Oculto en la vista pública, disponible en la bandeja de moderación.
  - `Rejected` (3): Descartado por moderación.
- Al sincronizar editoriales oficiales:
  - Si el producto cruza inequívocamente con el catálogo local (`GameId != null`), su estado inicial será `Published`.
  - Si el producto no cruza de inmediato con el catálogo local, se creará o actualizará en estado `PendingModeration` preservando título, editorial, fecha, PVP estimado, EAN, carátula y URL oficial de la tienda.
  - En ningún caso se descartará silenciosamente un lanzamiento proveniente de la web oficial de Maldito Games o Devir.

### RF-02: Filtro Estricto de Calendario en Devir Iberia
- `DevirReleasesExtractor` debe descartar explícitamente cualquier sección o encabezado que contenga la palabra `desarrollo` (tanto en juegos de mesa como en juegos de rol).
- Solo se considerarán válidos aquellos lanzamientos que cuenten con un mes específico del calendario asignado (ej. *«Octubre 2026»*, *«Noviembre 2026»*).
- Las tarjetas de producto deben capturar el enlace directo a la ficha del juego en Devir (`https://devir.es/<slug>`). Solo si el HTML carece absolutamente de etiqueta `<a>`, se usará la URL de próximos lanzamientos como fallback.

### RF-03: Asistencia de Enlace con IA (Google Gemini)
- Se introduce `IReleaseAiMatcherService` para resolver ambigüedades en títulos traducidos al español:
  - Recibe: título en español, editorial, PVP estimado y notas de extracción.
  - Consulta a Gemini estructuradamente para predecir el título canónico en inglés y posibles identificadores BGG.
  - Ejecuta una búsqueda dirigida en `IBggClient` con el término en inglés inferido.
  - Si localiza un candidato idóneo, registra en la novedad:
    - `AiSuggestedBggId`: Identificador BGG del candidato.
    - `AiSuggestedTitle`: Título en inglés/original.
    - `AiMatchReasoning`: Justificación concisa de la inferencia.
- Dispone de simulación determinista para entornos de desarrollo y pruebas automatizadas offline sin depender de credenciales externas.

### RF-04: Bandeja de Moderación de Novedades
- En `/novedades` (y/o accesible desde el panel de administración para usuarios con `ModeratorPermission.CanApproveMedia`):
  - Una pestaña o vista dedicada: *«Pendientes de moderación (N)»*.
  - Cada tarjeta presenta:
    - **Datos Oficiales Extraídos:** portada, título, editorial, fecha, PVP estimado, EAN y enlace a la tienda oficial.
    - **Propuesta IA:** juego BGG sugerido con su título original, año, portada de BGG, enlace directo a BoardGameGeek y razonamiento.
  - **Acciones Disponibles:**
    - *Aprobar y Vincular:* importa la ficha del juego desde BGG (si no existe localmente), enlaza `GameId` y pasa el estado a `Published`.
    - *Aprobar sin Juego:* pasa el estado a `Published` manteniendo `GameId = null`.
    - *Descartar:* marca la entrada como `Rejected` o la elimina.

### RF-05: Jerarquía de Imágenes y Galería Extensible
- La imagen principal del juego (`CoverImageUrl`) debe priorizar la representación tridimensional de la caja (`face3d`), aportando mayor presencia editorial.
- La portada plana 2D (`frontflat`), la contraportada (`BackCoverImageUrl`) y la fotografía en mesa (`TableImageUrl`) se preservan y exponen de forma complementaria.
- La ficha del juego en `GameDetail.razor` permite visualizar la galería de recursos del juego, integrando de forma natural las fotografías de análisis reales provenientes de `FoundingVerdict`.

---

## 2. Requerimientos No Funcionales (RNF)

- **RNF-01: Rendimiento y Ausencia de Bloqueos en UI:** La sincronización de 47 productos de Maldito Games debe completarse en menos de 5 segundos, eliminando llamadas HTTP masivas bloqueantes en la solicitud inicial.
- **RNF-02: Compatibilidad de Persistencia Dual:** Soporte completo en SQLite (desarrollo/local) y PostgreSQL (producción/Supabase) mediante `SqliteSchemaMigrator`.
- **RNF-03: Cobertura de Pruebas:** 100% de la suite de pruebas unitarias en verde con pruebas específicas de moderación, extracción de Devir y deducción con IA.
