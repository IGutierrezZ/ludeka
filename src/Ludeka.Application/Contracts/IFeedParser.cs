using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Representa un ítem o producto extraído de un feed comercial (Google Shopping XML, CSV, etc.).
/// </summary>
/// <param name="Sku">Identificador o código único de producto en la tienda.</param>
/// <param name="Title">Título comercial del producto.</param>
/// <param name="RawBarcode">Código de barras tal como viene en el feed (sin validar).</param>
/// <param name="NormalizedEan">Código EAN-13 normalizado de 13 dígitos numéricos válidos, o null si es inválido.</param>
/// <param name="Price">Precio numérico normalizado.</param>
/// <param name="Currency">Divisa normalizada (por defecto "EUR").</param>
/// <param name="InStock">Indica si la tienda tiene disponibilidad de stock inmediata.</param>
/// <param name="ProductUrl">URL de la ficha del producto en la tienda.</param>
public record FeedProductItem(
    string Sku,
    string Title,
    string? RawBarcode,
    string? NormalizedEan,
    decimal Price,
    string Currency,
    bool InStock,
    string ProductUrl);

/// <summary>
/// Contrato para el parsing en streaming continuo de feeds de catálogo comercial.
/// </summary>
public interface IFeedParser
{
    /// <summary>
    /// Procesa el flujo de datos del feed produciendo productos de forma asíncrona y con consumo de memoria constante.
    /// </summary>
    /// <param name="stream">Flujo de entrada del feed.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Secuencia asíncrona de ítems de producto procesados.</returns>
    IAsyncEnumerable<FeedProductItem> ParseStreamAsync(Stream stream, CancellationToken ct = default);
}
