using System;

namespace Ludeka.Core.Entities;

/// <summary>
/// Registro de auditoría y métrica de clics en enlaces de compra y afiliación.
/// </summary>
public class AffiliateClickLog
{
    public Guid Id { get; private set; }
    public Guid? GameId { get; private set; }
    public string GameTitle { get; private set; } = string.Empty;
    public string GameSlug { get; private set; } = string.Empty;
    public string StoreName { get; private set; } = string.Empty;
    public string TargetUrl { get; private set; } = string.Empty;
    public string? Country { get; private set; }
    public DateTimeOffset ClickedAtUtc { get; private set; }

    private AffiliateClickLog() { }

    public AffiliateClickLog(
        Guid? gameId,
        string gameTitle,
        string gameSlug,
        string storeName,
        string targetUrl,
        string? country = null,
        DateTimeOffset? clickedAtUtc = null,
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(storeName))
            throw new ArgumentException("El nombre de la tienda no puede estar vacío.", nameof(storeName));

        if (string.IsNullOrWhiteSpace(targetUrl))
            throw new ArgumentException("La URL de destino no puede estar vacía.", nameof(targetUrl));

        Id = id ?? Guid.NewGuid();
        GameId = gameId;
        GameTitle = gameTitle?.Trim() ?? string.Empty;
        GameSlug = gameSlug?.Trim() ?? string.Empty;
        StoreName = storeName.Trim();
        TargetUrl = targetUrl.Trim();
        Country = string.IsNullOrWhiteSpace(country) ? null : country.Trim();
        ClickedAtUtc = clickedAtUtc ?? DateTimeOffset.UtcNow;
    }
}
