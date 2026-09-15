using System;

namespace Ludeka.Application.DTOs;

/// <summary>
/// Entrada individual en el historial de evolución de precios de un juego.
/// </summary>
public record PriceHistoryEntryDto(
    DateTimeOffset RecordedAtUtc,
    string StoreName,
    decimal Price,
    string Currency,
    bool InStock,
    string AffiliateUrl
);

/// <summary>
/// DTO con información de una oferta destacada o bajada de precio activa.
/// </summary>
public record PriceDropAlertDto(
    Guid GameId,
    string GameTitle,
    string GameSlug,
    string? GameCoverUrl,
    string StoreName,
    string Country,
    string AffiliateUrl,
    decimal CurrentPrice,
    string Currency,
    decimal? ReferencePrice,
    double DiscountPercentage,
    bool IsAllTimeLow,
    DateTimeOffset DetectedAtUtc
)
{
    public string FormattedCurrentPrice => $"{CurrentPrice:0.00} {Currency}";
    public string? FormattedReferencePrice => ReferencePrice.HasValue ? $"{ReferencePrice.Value:0.00} {Currency}" : null;
    public string FormattedDiscount => DiscountPercentage > 0 ? $"-{DiscountPercentage:0.#}%" : "Oferta";
}
