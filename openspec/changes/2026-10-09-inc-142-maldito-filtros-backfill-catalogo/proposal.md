# Propuesta SDD: INC-142 — Filtro Estricto de Novedades Maldito Games, Enriquecimiento de Imágenes, EAN y Precios en Catálogo (Maldito y Devir)

## 1. Contexto y Motivación
Actualmente, el extractor de novedades de Maldito Games (`MalditoReleasesExtractor`) procesa indiscriminadamente varias secciones de la tienda oficial (`tienda.malditogames.com`), incluyendo «Últimas novedades» (juegos ya comercializados) y «Lo que se viene» (banners con solo un año tipo 2027 sin fecha cerrada ni precio), además de una consulta adicional al catálogo reciente. Esto genera ruido en la cola de moderación con juegos que ya han salido o anuncios lejanos sin ficha ni presencia en BGG.

Por otro lado, tanto Devir como Maldito Games disponen de fotografías de alta resolución en sus tiendas online (con caja 3D `face3d`, contraportada `backflat` y componentes de mesa), así como códigos EAN-13 oficiales y precios PVP. En el incremento anterior se habilitó el runner `devir-images-backfill` para el catálogo general de Devir, pero se omitía la actualización del PVP en las ofertas de compra (`PurchaseLinks`) y se ignoraba la asignación de EAN si el juego ya disponía de imágenes.

Adicionalmente, debido a la protección WAF/Cloudflare en los sitios web de las editoriales frente a IPs de centros de datos de Google Cloud Run, se requiere asegurar que los trabajos de barrido masivo de catálogo de ambas editoriales puedan ser ejecutados de forma limpia, robusta y desasistida desde el entorno local.

## 2. Objetivos
1. **Filtro estricto en novedades de Maldito Games**:
   - Limitar la extracción de novedades exclusivamente a las secciones «A puntito de llegar» y «Volverán a estar disponibles en breve» (reimpresiones).
   - Descartar «Últimas novedades», «Lo que se viene» y la lectura redundante del catálogo reciente.
2. **Extracción de galería y metadatos de producto en Maldito Games**:
   - Extraer galería completa (`CoverImageUrl` con caja 3D `face3d`, `TableImageUrl` y `BackCoverImageUrl`), EAN-13 y PVP para las novedades inminentes.
3. **Corrección de actualización de PVP y EAN en Devir (`DevirImagesBackfillJobRunner`)**:
   - Registrar o actualizar la oferta de Devir en `PurchaseLinks` con su PVP oficial y enlace a la tienda.
   - No descartar juegos que ya tienen imágenes si todavía carecen de EAN o de la oferta de Devir con precio.
4. **Nuevo trabajo de barrido para el catálogo de Maldito Games (`maldito-images-backfill`)**:
   - Recorrer la paginación del catálogo general (`/juegos?p={page}`) de Maldito Games.
   - Cruzar por EAN y por título normalizado con los juegos de Ludeka.
   - Enriquecer imágenes (caja 3D como portada principal, trasera y mesa), asignar EAN si faltaba y añadir/actualizar la oferta en `PurchaseLinks` con su PVP oficial.
5. **Ejecución local garantizada**:
   - Configuración de cabeceras de navegación humana, pausas de cortesía y tolerancia a fallos para ejecución local mediante CLI (`dotnet run --project src/Ludeka.Jobs -- devir-images-backfill` y `maldito-images-backfill`).

## 3. Criterios de Aceptación
- Las pruebas automáticas de extracción de Maldito Games verifican que solo se capturen juegos de «A puntito de llegar» y «Volverán a estar disponibles», ignorando «Últimas novedades» y «Lo que se viene».
- La galería de producto de Maldito Games extrae correctamente la caja 3D, mesa, contraportada, EAN y PVP desde el bloque JSON `mage/gallery/gallery` y el SKU del formulario.
- El runner de Devir enriquece `PurchaseLinks` con la tienda «Devir» y su PVP, y actualiza el EAN incluso en juegos que ya tenían portada.
- El nuevo runner `maldito-images-backfill` está registrado bajo `JobNames.MalditoImagesBackfill`, compila limpiamente y dispone de pruebas unitarias de extremo a extremo.
- Toda la suite de pruebas unitarias del repositorio se mantiene en verde al 100%.
