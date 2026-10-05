using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Contrato para proveedores de datos de productos de Amazon (oficial PA-API o puente HTTP).
/// </summary>
public interface IAmazonProductProvider
{
    /// <summary>
    /// Resuelve el identificador ASIN de Amazon correspondiente a un código de barras EAN o UPC.
    /// </summary>
    Task<string?> LookupAsinByEanAsync(string ean, CancellationToken ct = default);

    /// <summary>
    /// Consulta el precio, disponibilidad y metadatos de un producto a partir de su ASIN.
    /// </summary>
    Task<AmazonProductPriceResult?> GetPriceAndStockAsync(string asin, CancellationToken ct = default);
}
