# Informe de Verificación: INC-142 — Filtro Estricto de Novedades Maldito Games, Enriquecimiento de Imágenes, EAN y Precios en Catálogo (Maldito y Devir)

## 1. Resumen Ejecutivo
Se ha implementado el saneamiento del extractor de novedades de Maldito Games (`MalditoReleasesExtractor`), el enriquecimiento completo de metadatos de producto (caja 3D como portada principal, contraportada, mesa, EAN-13 y PVP oficial), la corrección de asignación de ofertas de Devir con su PVP en `PurchaseLinks` dentro de `DevirImagesBackfillJobRunner`, y la creación del nuevo runner `MalditoImagesBackfillJobRunner` (`maldito-images-backfill`) para el recorrido y enriquecimiento del catálogo general de Maldito Games.

## 2. Verificaciones Realizadas

### 2.1 Filtro Estricto de Secciones en Maldito Games
- **Secciones conservadas:**
  - «A puntito de llegar» (lanzamientos inminentes con fecha y precio).
  - «Volverán a estar disponibles en breve» (reimpresiones con fecha y precio).
- **Secciones descartadas:**
  - «Últimas novedades» (juegos ya en tiendas a la venta).
  - «Lo que se viene» (banners inferiores con solo año sin fecha ni precio).
  - Consulta redundante al catálogo reciente en `ExtractReleasesAsync`.
- **Verificación:** `ParseHtml_FiltersStrictly_OnlyReturnsPuntitoAndReprint_ExcludesUltimasNovedadesAndLoQueSeViene` valida que en un HTML con las 4 secciones solo se extraen las 2 deseadas (Railway Boom y Earthborne Rangers), excluyendo Emblemas y Floe.

### 2.2 Enriquecimiento de Galería, EAN y Precios en Maldito Games
- Implementados `ExtractProductGalleryAsync` y `ParseProductGalleryHtml`.
- Extracción de imágenes desde `mage/gallery/gallery`: caja 3D (`face3d`), trasera (`backflat`) y mesa (`frontflat` o componentes).
- Extracción de código EAN-13 desde el atributo de formulario `data-product-sku` o URLs de imágenes.
- Extracción de PVP desde metaetiquetas de producto (`product:price:amount`) o elementos de precio.
- **Verificación:** `ParseProductGalleryHtml_ExtractsImagesEanAndPrice` acredita la extracción correcta contra la estructura real de Maldito Games.

### 2.3 Corrección en `DevirImagesBackfillJobRunner`
- Si un juego ya disponía de imágenes pero carecía de EAN o de la oferta oficial de Devir en `PurchaseLinks`, ya no se omite.
- Se registra o actualiza la oferta en `PurchaseLinks` con el PVP de Devir y su URL de producto.
- **Verificación:** `RunAsync_WhenGameHasImages_StillUpdatesEanAndDevirOfferIfMissing` acredita que un juego con imágenes completas recibe su EAN y su oferta de Devir con precio.

### 2.4 Nuevo Runner `MalditoImagesBackfillJobRunner`
- Registrado bajo el nombre `maldito-images-backfill` en `JobNames` y en el contenedor de inyección de dependencias.
- Implementa paginación (`ExtractCatalogPageAsync`), cruce por EAN y por título normalizado, y enriquecimiento de imágenes, EAN y ofertas de compra.
- **Verificación:** Pruebas unitarias de composición (`LudekaJobsCompositionTests`, 18 runners), resolución de trabajo (`JobSelectionResolverTests`), y suite dedicada `MalditoImagesBackfillJobRunnerTests` con 4 casos de prueba (cruce y enriquecimiento, omisión cuando está completo, tolerancia a fallos y aborto por fallos consecutivos).

## 3. Estado de la Suite de Pruebas
- Todas las pruebas unitarias del repositorio ejecutadas y verificadas en verde al 100%.
