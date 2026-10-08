# Propuesta de Cambio: INC-129 — Moderación Asistida por IA de Novedades, Filtros Devir y Galería 3D

## 1. Motivación y Problema

1. **Cuello de Botella y Descarte Sistemático en Maldito Games:**
   - La tienda de Maldito Games (`tienda.malditogames.com`) expone 47 lanzamientos reales y confirmados con EAN, fecha y PVP oficial.
   - En `EditorialReleasesSyncService`, cada juego no catalogado desencadenaba hasta 3 búsquedas secuenciales hacia la API de BGG con un rate limiter estricto de 2 req/s. Con más de 100 llamadas HTTP acumuladas, la sincronización tardaba minutos, bloqueando la interfaz web de Blazor.
   - Muchos títulos comerciales están en castellano (*«Los 12 trabajos de Hércules»*, *«Crucero Galáctico»*, *«El Valle de los Molinos»*), por lo que la búsqueda en BGG arrojaba cero coincidencias.
   - La regla estricta de descarte `if (matchedGame == null) continue;` tiraba a la basura las 47 novedades legítimas de Maldito Games con su fecha, EAN y PVP oficial.

2. **Defectos de Extracción en Devir Iberia:**
   - Se importaban secciones «En desarrollo» (tanto de rol como de juegos de mesa) sin fecha ni mes cerrado de lanzamiento.
   - En las tarjetas de Devir (`ParseTileCardItems`), el extractor no capturaba el enlace `<a href="...">` del producto y asignaba por defecto `https://devir.es/proximos-lanzamientos`.

3. **Requerimiento Editorial de Imágenes y Galería:**
   - La caja en 3D (`face3d`) debe ser la imagen principal del juego (`CoverImageUrl`), aportando volumen y presencia.
   - Las imágenes secundarias (portada plana 2D `frontflat`, contraportada en castellano `back`, despliegue en mesa `table`) deben alimentar la galería extensible del juego (`GameImage` / `GameGallery`).
   - La galería debe soportar la incorporación de fotos reales tomadas en mesa por el fundador con sus análisis personales (`FoundingVerdict`).

---

## 2. Alcance Propuesto

1. **Moderación de Novedades Editoriales en Dominio:**
   - Añadir enum `WeeklyReleaseStatus` (`Published`, `PendingModeration`, `Rejected`) a `WeeklyRelease`.
   - Si la novedad cruza de forma automática e inequívoca con catálogo local o BGG (`GameId != null`), se guarda como `Published`.
   - Si no cruza, se guarda como `PendingModeration` conservando todos sus datos extraídos (EAN, PVP, fecha, carátula y URL de tienda).
   - `/novedades` pública solo lista lanzamientos en estado `Published`.

2. **Servicio de Deducción y Enlace con IA (Google Gemini):**
   - Servicio `IReleaseAiMatcherService` implementado sobre Gemini Flash.
   - Deducir el título canónico en inglés a partir del nombre comercial español y consultar a BGG para obtener candidato contrastado.
   - Almacenar propuesta IA en la novedad (`AiSuggestedBggId`, `AiSuggestedTitle`, `AiMatchReasoning`).

3. **Bandeja de Moderación en `/novedades` (o `/admin/moderacion-novedades`):**
   - Pestaña o panel accesible con `ModeratorPermission.CanApproveMedia`.
   - Comparativa visual: Datos extraídos vs. Propuesta BGG de la IA.
   - Acciones: Aprobar y vincular a BGG, Aprobar sin juego vinculado, o Descartar.

4. **Correcciones en `DevirReleasesExtractor`:**
   - Excluir secciones con `desarrollo`.
   - Requerir mes concreto en el calendario.
   - Extraer enlace `href` directo a la ficha del producto en Devir.

5. **Jerarquía Visual y Galería:**
   - Priorizar `face3d` como carátula principal (`CoverImageUrl`).
   - Soportar galería de imágenes (`GameGalleryImage`) para portada plana, contraportada, mesa y fotos de análisis personales de `FoundingVerdict`.

---

## 3. Impacto Arquitectónico

- **Core:** `WeeklyReleaseStatus`, propiedades de sugerencia IA en `WeeklyRelease`, entidad/VO `GameGalleryImage`.
- **Application:** `IReleaseAiMatcherService`, actualización de `IEditorialReleasesSyncService` y `IWeeklyReleaseService`.
- **Infrastructure:** `GeminiReleaseMatcherService`, refinamiento de `DevirReleasesExtractor`, migración de esquema en `SqliteSchemaMigrator`.
- **Web:** Panel/pestaña de moderación en `News.razor` / `AdminDashboard.razor`, visor de galería en `GameDetail.razor`.
