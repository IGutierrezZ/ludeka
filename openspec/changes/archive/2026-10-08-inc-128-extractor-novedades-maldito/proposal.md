# Propuesta: INC-128 Extractor Determinista de Novedades y Reimpresiones de Maldito Games

## 1. Contexto y Diagnóstico
En la sincronización de novedades editoriales oficiales de Ludeka (`EditorialReleasesSyncService` y `MalditoReleasesExtractor`), Devir Iberia sincroniza correctamente sus lanzamientos mientras que Maldito Games arroja cero elementos vinculados al catálogo y a la cartelera de `/novedades`.

La investigación técnica con el DOM real de la tienda de Maldito Games (`tienda.malditogames.com`) ha revelado tres causas encadenadas:
1. **Extracción en secciones obsoletas/incompletas:** El extractor actual ignora los bloques con producto real (`<li class="product-item">`) bajo las secciones principales de la portada:
   - `A puntito de llegar` (próximos lanzamientos inmediatos con fecha).
   - `Volverán a estar disponibles en breve` (reimpresiones oficiales confirmadas).
   - `Últimas novedades` (lanzamientos recién salidos).
   En su lugar, solo leía los banners de `Lo que se viene` (imágenes JPG con fechas genéricas como 2027) y una llamada a `/juegos?product_list_order=creation_time` que en Magento ordena ascendentemente por defecto y trae juegos de 2016.
2. **Incompatibilidad de formato de fechas:** La tienda indica fechas como `22 de octubre` en `<span class="fecha_home">`. El extractor solo evaluaba enteros de 4 dígitos (`2027`), dejando `ReleaseDate` a `null`.
3. **Descarte implacable en el orquestador por ausencia de EAN y entidades HTML:**
   - Devir publica su código EAN en texto; Maldito no lo expone en el texto pero sí en el nombre de fichero de la imagen del CDN (`8436578818099-1200-face3d.jpg`).
   - Los títulos contenían entidades HTML sin decodificar (`Nemo&#039;s War`, `&amp;`) y coletillas comerciales (`- Edición Kickstarter`, `- Senderos`).
   - Al no cruzar por EAN y fallar la búsqueda literal en BGG, la regla de negocio introducida en `26c1e35` descartaba el 100% de los lanzamientos y purgaba registros huérfanos.

## 2. Enfoque Arquitectónico (Solución Determinista)
Frente a una aproximación opaca y costosa con LLM, se adopta una solución puramente determinista, rápida y reproducible al 100% en pruebas unitarias:
- Parsear las secciones reales de la portada (`A puntito de llegar`, `Volverán a estar disponibles en breve`, `Últimas novedades`).
- Parsear fechas en castellano (`"d 'de' MMMM"`, `"MMMM yyyy"`, `"yyyy"`).
- Extraer EAN de 13 dígitos desde la URL de imagen si está presente (`8436578818099`).
- Decodificar entidades HTML (`HtmlDecode`) y limpiar coletillas comerciales en el fallback de búsqueda de BGG.
- Reparar la reconciliación SQLite en `SqliteSchemaMigrator` para la columna `IsMonthOnly`.

## 3. Impacto y Criterios de Aceptación
- `MalditoReleasesExtractor` extrae todas las novedades y reimpresiones de la portada con título decodificado, fecha, precio, portada y EAN cuando esté disponible.
- Las reimpresiones de `Volverán a estar disponibles en breve` quedan marcadas con `IsReprint = true`.
- `EditorialReleasesSyncService` enlaza con éxito los juegos en el catálogo local y en BGG.
- 100% de la suite de pruebas unitarias en verde.
