# Especificación: Ingesta de Feeds Comerciales, Auto-asignación de EAN y Panel de Afiliados

## Requisitos Funcionales

### RF-01: Configuración de Fuentes de Feeds
- El sistema debe permitir registrar fuentes de feeds comerciales (`AffiliateFeedSource`) con los campos:
  - `StoreName`: Nombre de la tienda comercial (ej. "Zacatrus").
  - `FeedUrl`: URL absoluta HTTPS del feed.
  - `FeedFormat`: Formato del feed (`GoogleShoppingXml` o `GenericCsv`).
  - `AffiliateTag`: Parámetro o código de afiliado para los enlaces.
  - `Country`: País de distribución principal (por defecto "España").
  - `IsEnabled`: Estado activo o pausado.
  - `LastSyncUtc`: Marca temporal de la última sincronización.
  - `LastSyncStatus`: Resultado de la última ejecución (`Success`, `Failed`, etc.).
  - `MatchedProductsCount`: Número de productos del feed cruzados exitosamente con el catálogo.

### RF-02: Parsing en Streaming de Feeds
- El parser de Google Shopping XML (`GoogleShoppingFeedParser`) debe procesar elementos `<item>` sin cargar el XML completo en memoria:
  - Extraer `g:id` o identificador SKU.
  - Extraer `title` del producto en tienda.
  - Extraer `g:gtin` / `g:barcode` y validar mediante `BarcodeValidator.TryNormalizeEan13`.
  - Extraer `g:price` y normalizar a valor numérico decimal y divisa (`EUR` / `€`).
  - Extraer `g:availability` (mapear `in stock` a `InStock = true`, `out of stock` a `false`).
  - Extraer `link` y aplicar `AffiliateUrlResolver.ResolveAffiliateUrl` para inyectar los parámetros de afiliación de la tienda.

### RF-03: Cruce Determinista y Auto-asignación de EAN
- **Paso 1 (Cruce por EAN):** Si el producto del feed contiene un EAN válido que coincide con `game.Ean` o con algún elemento de `game.AdditionalBarcodes`, se actualiza la oferta en `game.PurchaseLinks`.
- **Paso 2 (Auto-asignación en juego sin EAN):** Si el producto del feed contiene un EAN válido, el juego en Ludeka tiene `Ean == null` y el título en español o el slug normalizado coincide exactamente con el título del producto, se asigna el EAN del feed a `game.Ean`.
- **Paso 3 (Detección de discrepancia):** Si el producto del feed coincide por título o slug pero su EAN difiere de `game.Ean`:
  - Se añade el EAN del feed a `game.AdditionalBarcodes` para permitir el cruce comercial futuro.
  - Se crea un registro en `AffiliateEanDiscrepancyLog` con el juego, el EAN actual, el EAN del feed y la tienda de origen.
  - Se actualiza la oferta en `game.PurchaseLinks`.

### RF-04: Bitácora y Cola de Discrepancias
- La entidad `AffiliateEanDiscrepancyLog` almacena:
  - `GameId`, `GameTitle`, `GameSlug`.
  - `CurrentEan`: Código actualmente asignado como principal en el juego.
  - `FeedEan`: Código comercial proveniente del feed de la tienda.
  - `StoreName`: Tienda que reportó la discrepancia.
  - `DetectedAtUtc`: Fecha y hora de detección.
  - `IsResolved`: Estado de resolución.
  - `ResolutionNote`: Comentario de resolución (ej. "Promovido a principal por admin").

### RF-05: Runner en Ludeka.Jobs (`feed-sync`)
- El runner `CatalogFeedSyncJobRunner` debe:
  - Implementar `IJobRunner` para el trabajo con identificador `feed-sync`.
  - Obtener las fuentes habilitadas (`IsEnabled == true`).
  - Adquirir concesión distribuida vía `IJobExecutionCoordinator` para evitar ejecuciones concurrentes duplicadas.
  - Procesar cada feed y reportar telemetría de sincronización (productos leídos, ofertas actualizadas, EANs auto-asignados, discrepancias detectadas).

### RF-06: Panel de Administración Web (`/admin/afiliados`)
- Vista interactiva accesible para administradores:
  - Tabla de fuentes de feeds con estado, fecha de última sincronización, productos vinculados y conmutador para activar/desactivar.
  - Botón para "Sincronizar ahora" que dispara la ingesta bajo demanda.
  - Pestaña o sección de "Discrepancias de EAN detectadas" con botón de acción: **«Promover EAN de la tienda como principal»**.

## Requisitos No Funcionales
- **RNF-01 (Memoria Constante):** La ingesta debe operar con procesamiento en streaming (`XmlReader`), asegurando un consumo de memoria inferior a 60 MB sin importar el tamaño del feed.
- **RNF-02 (Idempotencia y Resiliencia):** Las ejecuciones repetidas no deben duplicar enlaces de compra en `game.PurchaseLinks` ni crear discrepancias duplicadas si ya están pendientes de revisión.
