# INC-129: Moderación Asistida por IA (Gemini) de Novedades Editoriales, Filtro de Calendario en Devir y Jerarquía de Imágenes 3D

- **ID del Incremento:** `INC-129`
- **Estado:** `✅ Archivado`
- **Fecha de Inicio:** `2026-10-08`
- **Fecha de Cierre:** `2026-10-08`
- **Rama:** `inc/novedades-moderacion-ia-devir`
- **Worktree:** `F:\repos\ludeka-wt\novedades-moderacion-ia-devir`
- **Directorio SDD:** `openspec/changes/2026-10-08-inc-129-novedades-moderacion-ia-devir/`
- **Pruebas Verificadas:** `2.675 pruebas unitarias verificadas al 100% en verde`

---

## 1. Contexto y Diagnóstico del Problema

1. **Cuello de Botella y Descarte Sistemático en Maldito Games:**
   - `MalditoReleasesExtractor` extrae 47 productos reales de `tienda.malditogames.com`.
   - `EditorialReleasesSyncService` ejecutaba búsquedas secuenciales hacia la API de BGG con un rate limiter estricto (2 req/s). Con ~100-140 peticiones necesarias para 47 títulos, el bucle tardaba minutos y bloqueaba la interfaz web.
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

## 2. Solución Arquitectónica

1. **Estado de Moderación en Novedades Editoriales (`WeeklyRelease`):**
   - Incorporar estado formal de publicación/moderación: `Published` (aprobado y visible en `/novedades`), `PendingModeration` (oculto al público general, visible en bandeja de moderación) y `Rejected`.
   - Si la novedad cruza automáticamente de forma inequívoca con catálogo o BGG (`GameId != null`), pasa a `Published`.
   - Si no cruza de inmediato, se registra en `PendingModeration` conservando todos sus datos extraídos (EAN, PVP, fecha, portada y URL de tienda).

2. **Asistente de Enlace con IA (Google Gemini):**
   - Servicio `IReleaseAiMatcherService` / `GeminiReleaseMatcherService` que analiza títulos comerciales en castellano y sugiere el título canónico original en inglés y su candidato BGG.
   - Enriquecimiento con consulta a BGG para recuperar portada, año y enlace BGG verificado.
   - Almacenamiento de la sugerencia en la novedad: `AiSuggestedBggId`, `AiSuggestedTitle`, `AiMatchReasoning`.

3. **Bandeja de Moderación en `/novedades` (o `/admin/moderacion-novedades`):**
   - Vista comparativa para moderadores (`ModeratorPermission.CanApproveMedia`):
     - Datos extraídos de la editorial (portada, título, precio, fecha, enlace oficial).
     - Candidato propuesto por la IA (título original, carátula BGG, año y razonamiento).
   - Acciones de 1 clic:
     - **Aprobar con enlace BGG:** asocia el juego (importándolo si es necesario) y publica la novedad.
     - **Aprobar sin juego:** publica la novedad como legítima aunque no exista en BGG.
     - **Descartar:** elimina o marca rechazada la entrada si es merchandising o accesorio.

4. **Corrección de Extracción en Devir (`DevirReleasesExtractor`):**
   - Excluir explícitamente secciones que contengan `desarrollo`.
   - Exigir que la novedad pertenezca a un mes específico del calendario.
   - Capturar el enlace `href` real a la ficha del juego en Devir.

5. **Jerarquía de Imágenes y Galería Extensible:**
   - Priorizar `face3d` como imagen principal (`CoverImageUrl`).
   - Almacenar portada plana, contraportada y foto en mesa optimizadas en WebP vía Cloudflare R2.
   - Conectar las fotos de análisis personales de `FoundingVerdict` a la vitrina fotográfica del juego.

---

## 3. Criterios de Aceptación y Verificación

1. La sincronización de Maldito Games procesa las 47 novedades en segundos sin congelar la aplicación.
2. Ningún juego legítimo de Maldito se descarta silenciosamente: los no vinculados pasan a moderación con propuesta de IA.
3. Devir no incluye productos de secciones «En desarrollo» y enlaza correctamente a la ficha de producto.
4. Las carátulas 3D presiden la imagen principal cuando están disponibles.
5. Suite completa de pruebas unitarias al 100% en verde.
