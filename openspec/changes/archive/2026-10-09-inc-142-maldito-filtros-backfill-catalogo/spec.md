# Especificación Funcional y Técnica: INC-142

## 1. Alcance de Cambios

### 1.1 `MalditoReleasesExtractor` y DTOs
- Se retira el procesamiento de «Últimas novedades», «Lo que se viene» y la llamada al catálogo reciente en `ExtractReleasesAsync`.
- Se añade el método `ExtractProductGalleryAsync(string productUrl, CancellationToken ct)` y `ParseProductGalleryHtml(string html)` a `IMalditoReleasesExtractor` / `MalditoReleasesExtractor`.
- Se define `MalditoProductGalleryDto(string? CoverImageUrl, string? TableImageUrl, string? BackCoverImageUrl, string? FrontFlatUrl, string? Ean, decimal? Pvp)`.
- Se define `MalditoCatalogItemDto(string ProductUrl, string? Title, string? Ean, string? CoverImageUrl)` y `MalditoCatalogPageResultDto(IReadOnlyList<MalditoCatalogItemDto> Items, bool HasNextPage, bool Success = true)`.
- Se implementa `ExtractCatalogPageAsync(int page, CancellationToken ct)` para paginar sobre `https://tienda.malditogames.com/juegos?p={page}`.

### 1.2 `DevirImagesBackfillJobRunner`
- Se actualiza la lógica de evaluación de juegos coincidentes para que un juego se procese si:
  - Le faltan imágenes (no tiene 3D, mesa o trasera).
  - O le falta el código EAN.
  - O le falta la oferta de compra de «Devir» en `PurchaseLinks` con su precio.
- Al obtener la galería con `gallery.Pvp`, si tiene precio:
  - Se añade o actualiza el enlace en `matchedGame.PurchaseLinks` con `StoreName = "Devir"`, `AffiliateUrl = item.ProductUrl`, `Price = gallery.Pvp.Value`, `InStock = true`.
- Si el juego no tenía EAN y `gallery.Ean` está disponible, se actualiza con `matchedGame.UpdateEan(gallery.Ean)`.

### 1.3 `MalditoImagesBackfillJobRunner`
- Nuevo runner de trabajo implementando `IJobRunner` con nombre de trabajo `maldito-images-backfill`.
- Registrado en `JobNames.MalditoImagesBackfill` y `JobRunnerServiceCollectionExtensions.cs`.
- Flujo de ejecución:
  - Cargar todos los juegos locales en memoria e indexar por EAN y por título normalizado.
  - Recorrer páginas de catálogo de Maldito Games (`ExtractCatalogPageAsync`).
  - Para cada producto del catálogo:
    - Buscar coincidencia en la base de datos por EAN o por título normalizado.
    - Si coincide, evaluar si requiere imágenes, EAN o registrar la oferta de compra de «Maldito Games».
    - En caso afirmativo, consultar `ExtractProductGalleryAsync(item.ProductUrl)`.
    - Actualizar imágenes (`UpdateImages` / `UpdateMediaUrls`), EAN (`UpdateEan`) y `PurchaseLinks` (oferta de «Maldito Games» con su PVP y URL).
    - Persistir mediante `_gameRepository.UpdateAsync(matchedGame)`.
    - Pausa de cortesía para respetar el servicio web.

## 2. Invariantes Arquitectónicos
- No mutar identificadores inmutables de juegos.
- La caja 3D (`face3d`) se prioriza sobre portadas planas 2D como `CoverImageUrl`.
- Los precios deben ser no negativos y formateados en euros (€).
- El código EAN debe respetar el formato estándar EAN-13 si es válido.
