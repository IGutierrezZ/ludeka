using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Contrato para el parsing y extracción de catálogos comerciales de tiendas basadas en Shopify (/products.json).
/// Extrae identificadores EAN-13 multinivel desde barcode, sku y nombres de archivo de imágenes de producto.
/// </summary>
public interface IShopifyJsonCatalogParser : IFeedParser
{
    /// <summary>
    /// Procesa un flujo de datos JSON con el esquema estándar de Shopify {"products": [...]}.
    /// </summary>
    /// <param name="stream">Flujo con el payload JSON de la página.</param>
    /// <param name="storeBaseUrl">URL base de la tienda para construir las URLs canónicas completas (/products/{handle}).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Secuencia asíncrona de productos normalizados.</returns>
    IAsyncEnumerable<FeedProductItem> ParseStreamAsync(
        Stream stream,
        string? storeBaseUrl = null,
        CancellationToken ct = default);

    /// <summary>
    /// Recorre de forma paginada y controlada el endpoint /products.json de una tienda Shopify produciendo ítems.
    /// </summary>
    /// <param name="storeFeedUrl">URL base o de feed de la tienda (ej. https://cuartodejuegos.es o https://cuartodejuegos.es/products.json).</param>
    /// <param name="httpClient">Cliente HTTP para realizar las peticiones paginadas.</param>
    /// <param name="maxPages">Límite defensivo de páginas a consultar (por defecto 100, equivalente a 25.000 productos a 250/página).</param>
    /// <param name="delayBetweenPagesMs">Retardo de cortesía de red entre páginas consecutivas.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Secuencia asíncrona de productos de todas las páginas de la tienda.</returns>
    IAsyncEnumerable<FeedProductItem> ParsePaginatedAsync(
        string storeFeedUrl,
        HttpClient httpClient,
        int maxPages = 100,
        int delayBetweenPagesMs = 150,
        CancellationToken ct = default);
}
