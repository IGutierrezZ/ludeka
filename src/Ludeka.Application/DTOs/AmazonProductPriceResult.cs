using System;

namespace Ludeka.Application.DTOs;

/// <summary>
/// Representa el resultado obtenido de consultar un producto en un proveedor de Amazon.
/// </summary>
public record AmazonProductPriceResult
{
    public string Asin { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string Currency { get; init; } = "€";
    public bool InStock { get; init; } = true;
    public string? Title { get; init; }
    public string? ProductUrl { get; init; }
    public DateTimeOffset FetchedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public AmazonProductPriceResult() { }

    public AmazonProductPriceResult(
        string asin,
        decimal price,
        string currency = "€",
        bool inStock = true,
        string? title = null,
        string? productUrl = null,
        DateTimeOffset? fetchedAtUtc = null)
    {
        Asin = asin ?? throw new ArgumentNullException(nameof(asin));
        Price = price;
        Currency = string.IsNullOrWhiteSpace(currency) ? "€" : currency;
        InStock = inStock;
        Title = title;
        ProductUrl = productUrl;
        FetchedAtUtc = fetchedAtUtc ?? DateTimeOffset.UtcNow;
    }
}
