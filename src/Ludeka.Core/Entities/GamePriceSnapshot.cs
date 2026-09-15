using System;

namespace Ludeka.Core.Entities;

/// <summary>
/// Representa una lectura histórica inmutable del precio y disponibilidad de un juego en una tienda asociada.
/// </summary>
public class GamePriceSnapshot
{
    public Guid Id { get; private set; }
    public Guid GameId { get; private set; }
    public string StoreName { get; private set; } = string.Empty;
    public string AffiliateUrl { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = "€";
    public bool InStock { get; private set; }
    public DateTimeOffset RecordedAtUtc { get; private set; }

    // Constructor sin parámetros para EF Core / Dapper
    private GamePriceSnapshot() { }

    public GamePriceSnapshot(
        Guid gameId,
        string storeName,
        string affiliateUrl,
        decimal price,
        bool inStock = true,
        string currency = "€",
        DateTimeOffset? recordedAtUtc = null,
        Guid? id = null)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException("El identificador del juego no puede ser vacío.", nameof(gameId));

        if (string.IsNullOrWhiteSpace(storeName))
            throw new ArgumentException("El nombre de la tienda no puede estar vacío.", nameof(storeName));

        if (string.IsNullOrWhiteSpace(affiliateUrl))
            throw new ArgumentException("La URL de afiliado no puede estar vacía.", nameof(affiliateUrl));

        if (price < 0)
            throw new ArgumentOutOfRangeException(nameof(price), "El precio registrado no puede ser negativo.");

        Id = id ?? Guid.NewGuid();
        GameId = gameId;
        StoreName = storeName.Trim();
        AffiliateUrl = affiliateUrl.Trim();
        Price = Math.Round(price, 2);
        Currency = string.IsNullOrWhiteSpace(currency) ? "€" : currency.Trim();
        InStock = inStock;
        RecordedAtUtc = recordedAtUtc ?? DateTimeOffset.UtcNow;
    }
}
