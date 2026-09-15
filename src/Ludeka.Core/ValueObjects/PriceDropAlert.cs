using System;

namespace Ludeka.Core.ValueObjects;

/// <summary>
/// Representa una alerta u oportunidad de compra detectada cuando el precio de un juego desciende de forma destacada o marca un mínimo histórico.
/// </summary>
public record PriceDropAlert
{
    public Guid GameId { get; init; }
    public string GameTitle { get; init; } = string.Empty;
    public string GameSlug { get; init; } = string.Empty;
    public string? GameCoverUrl { get; init; }
    public string StoreName { get; init; } = string.Empty;
    public string Country { get; init; } = "España";
    public string AffiliateUrl { get; init; } = string.Empty;
    public decimal CurrentPrice { get; init; }
    public string Currency { get; init; } = "€";
    public decimal? ReferencePrice { get; init; }
    public double DiscountPercentage { get; init; }
    public bool IsAllTimeLow { get; init; }
    public DateTimeOffset DetectedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string FormattedCurrentPrice => $"{CurrentPrice:0.00} {Currency}";
    public string? FormattedReferencePrice => ReferencePrice.HasValue ? $"{ReferencePrice.Value:0.00} {Currency}" : null;
    public string FormattedDiscount => DiscountPercentage > 0 ? $"-{DiscountPercentage:0.#}%" : "Oferta";
}
