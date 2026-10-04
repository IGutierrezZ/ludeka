# Propuesta: Ingesta de Catálogos Shopify para Tiendas de Juegos de Mesa

## Motivación y Contexto
Tras la puesta en producción del motor de sincronización de feeds de afiliados (INC-86), Ludeka cuenta con la infraestructura para procesar feeds en streaming, cruzar por código de barras EAN-13, auto-asignar EANs por título normalizado, detectar discrepancias y actualizar ofertas y el histórico de precios (`PriceRadarService`).

Sin embargo, muchas de las tiendas físicas y online más reconocidas de España (como **Cuarto de Juegos** en Madrid, **Ludus Belli** en Móstoles o **Mi Juego Bonito**) operan sobre la plataforma **Shopify**.
Shopify expone de forma estándar y nativa su catálogo público paginado en JSON a través de `/products.json?limit=250&page={N}` sin necesidad de autenticación, WAF restrictivo ni scraping de HTML. Este endpoint contiene:
- Título del producto y slug (`handle`) para construir la URL canónica de compra directa.
- Variantes con precio actual (`price`), precio de referencia (`compare_at_price`) y disponibilidad en inventario en vivo (`available: true/false`).
- Código SKU (que en tiendas como Ludus Belli coincide directamente con el código de barras EAN-13) o nombre de imagen que referencia el EAN.
- Fabricante o editorial (`vendor`).

Incorporar un parser específico para Shopify en el pipeline existente permite a Ludeka:
1. Empezar a registrar precios reales, histórico de precios y disponibilidad verificada en tiempo real de comercios españoles sin requerir acuerdos técnicos previos ni scraping frágil.
2. Capturar y contrastar EAN-13 físicos de las ediciones españolas comercializadas.
3. Permitir a los usuarios comprar directamente en tiendas nacionales con enlaces contextuales exactos a la ficha del producto.

## Alcance Propuesto

1. **Extensión del Formato de Feeds (`FeedFormat.ShopifyJson`):**
   - Ampliar la enumeración `FeedFormat` en `Ludeka.Core.Entities.AffiliateFeedSource` con el valor `ShopifyJson = 3`.
   - Adaptar el panel administrativo en `AffiliatesAdmin.razor` para permitir registrar fuentes con formato `Shopify Catálogo JSON (/products.json)`.

2. **Parser de Catálogo Shopify (`ShopifyJsonCatalogParser`):**
   - Implementar `IShopifyJsonCatalogParser` (o `IFeedParser`) en `Ludeka.Application.Features.Affiliates` con lectura paginada o en streaming mediante `System.Text.Json` (`Utf8JsonReader` o `JsonDocument` por página).
   - Extraer ítems normalizados a `AffiliateFeedItem`:
     - Título y URL canónica (`https://{store_domain}/products/{handle}`).
     - Precio (`decimal`), divisa (`EUR` / `€`), disponibilidad (`InStock`).
     - Extracción inteligente del EAN-13: primero desde la propiedad `variant.sku` (si supera `BarcodeValidator.IsValidEan13`), secundariamente desde `variant.barcode` si viniera informado, o desde patrones numéricos en las URLs de imágenes (`8436625611079-...`).
     - Fabricante / Editorial (`vendor`).

3. **Integración con `CatalogFeedSyncService`:**
   - Inyectar el parser de Shopify en el despachador de formatos de `CatalogFeedSyncService`.
   - Soporte para recorrer páginas sucesivas (`page=1`, `page=2`, ...) con retardo cooperativo para respetar la cortesía de red con las tiendas.
   - Cruce automático por EAN-13, auto-asignación por título unívoco y registro en cola de discrepancias.

4. **Siembra Inicial de Tiendas Shopify Españolas:**
   - Alta preconfigurada en `seed-directory.json` o panel `/admin/afiliados` para:
     - **Cuarto de Juegos** (`cuartodejuegos.es`)
     - **Ludus Belli** (`ludusbelli.com`)
     - **Mi Juego Bonito** (`mijuegobonito.com`)

5. **Pruebas y Verificación:**
   - Batería de pruebas unitarias xUnit con payloads reales de Shopify JSON (con y sin EAN en SKU, con variantes múltiples, productos agotados y paginación).
   - Pruebas de integración del cruce en `CatalogFeedSyncServiceTests`.
