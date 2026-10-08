# INC-133: Saneamiento de Novedades Devir y Maldito, Galería Fotográfica y Job de Barrido de Catálogo

**Estado:** ✅ Archivado  
**Fecha de inicio:** 2026-10-08  
**Fecha de finalización:** 2026-10-08  
**Rama:** `inc/devir-galeria-filtros-novedades`  
**Worktree:** `F:\repos\ludeka-wt\devir-galeria-filtros-novedades`  

---

## 1. Motivación y Problema Detectado

En la sincronización de novedades editoriales oficiales de Devir y Maldito Games se identificaron tres incidencias:
1. **Duplicados espurios en Devir:** El parser de tarjetas capturaba fragmentos intermedios de texto HTML como títulos (`Autor:`, `Libro básico`), asignando precios erróneos por herencia indebida de EAN. Además, se incluían meses pasados (septiembre) y juegos de rol.
2. **Falta de galería fotográfica completa:** La sincronización solo guardaba la vista de la caja 3D (`face3d.jpg`), perdiendo las imágenes de componentes en mesa (`components1.jpg`) y contraportada (`backflat.jpg`) disponibles en la ficha del producto en `devir.es`.
3. **Bloqueo y desconexión en Maldito Games:** Al sincronizar secuencialmente con llamadas ilimitadas a Gemini AI para deducción de correspondencias BGG, el proceso acumuló varios minutos, provocando el cierre del circuito WebSocket (error 1006 / timeout de 5 minutos en Cloud Run) e impidiendo el registro de Maldito Games.

---

## 2. Alcance Técnico y Solución Entregada

1. **DevirReleasesExtractor:**
   - Descarte de fechas previas al mes en curso (`< DateOnly(currentYear, currentMonth, 1)`).
   - Filtrado de secciones y productos de rol (`juegos de rol`, `rol`, `rpg`, suplementos).
   - Evitación del doble parseo de tarjetas simples si la sección ya aportó productos detallados con precio.
   - Lista negra estricta de tokens de título (`Autor:`, `Ilustrador:`, `Libro básico`, etc.).
   - Extracción de la galería fotográfica completa (`face3d`, `components1.jpg` para mesa, `backflat.jpg` para contraportada) desde el bloque JSON `mage/gallery/gallery` de la URL del producto.
   - Métodos `ExtractCatalogPageAsync` y `ParseCatalogPageHtml` para la paginación del catálogo general de Devir (`https://devir.es/catalogo/juegos-de-mesa?p={page}`).

2. **EditorialReleasesSyncService:**
   - Timeout defensivo de 4 segundos con fallback inmediato al emparejador heurístico en llamadas a Gemini para proteger el circuito WebSocket en Blazor Server.
   - Aislamiento por editorial para garantizar la ejecución independiente y tolerante a fallos.
   - Enriquecimiento de `TableImageUrl` y `BackCoverImageUrl` en la entidad `Game` si están disponibles en la novedad.

3. **DevirImagesBackfillJobRunner (Ludeka.Jobs):**
   - Trabajo desatendido (`devir-images-backfill`) bajo concesión de ventana para recorrer el catálogo general de Devir, casar con los juegos de Ludeka y poblar masivamente sus imágenes de caja 3D, mesa y contraportada.
   - Optimización de red: si el juego ya cuenta con todas sus imágenes completas, omite la consulta individual a la ficha de producto.

---

## 3. Criterios de Aceptación y Verificación

- [x] Cero entradas de "Autor:" o "Libro básico" en la sincronización de Devir.
- [x] No se importan meses pasados ni productos de rol.
- [x] Las novedades con ficha de producto capturan foto de mesa y contraportada.
- [x] Maldito Games y Devir se ejecutan de manera aislada y resiliente sin caídas de WebSocket.
- [x] Job de barrido implementado y ejecutable en `Ludeka.Jobs` (`devir-images-backfill`).
- [x] Suite completa de pruebas unitarias en verde (2.705 pruebas superadas al 100%, 2.715 en total).
