using System;

namespace Ludeka.Core.Entities;

/// <summary>
/// Bitácora para auditar discrepancias entre el código de barras registrado en Ludeka (ej. BGG)
/// y el código comercial real provisto por el feed de una tienda física/online.
/// </summary>
public class AffiliateEanDiscrepancyLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public string GameTitle { get; set; } = string.Empty;
    public string GameSlug { get; set; } = string.Empty;
    public string? CurrentEan { get; set; }
    public string FeedEan { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public DateTimeOffset DetectedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public bool IsResolved { get; set; }
    public string? ResolutionNote { get; set; }

    public AffiliateEanDiscrepancyLog() { }

    public AffiliateEanDiscrepancyLog(
        Guid gameId,
        string gameTitle,
        string gameSlug,
        string? currentEan,
        string feedEan,
        string storeName)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException("El identificador del juego no puede estar vacío.", nameof(gameId));
        if (string.IsNullOrWhiteSpace(gameTitle))
            throw new ArgumentException("El título del juego no puede estar vacío.", nameof(gameTitle));
        if (string.IsNullOrWhiteSpace(feedEan))
            throw new ArgumentException("El EAN del feed no puede estar vacío.", nameof(feedEan));
        if (string.IsNullOrWhiteSpace(storeName))
            throw new ArgumentException("El nombre de la tienda no puede estar vacío.", nameof(storeName));

        GameId = gameId;
        GameTitle = gameTitle.Trim();
        GameSlug = gameSlug?.Trim() ?? string.Empty;
        CurrentEan = string.IsNullOrWhiteSpace(currentEan) ? null : currentEan.Trim();
        FeedEan = feedEan.Trim();
        StoreName = storeName.Trim();
        DetectedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkResolved(string note)
    {
        IsResolved = true;
        ResolutionNote = note?.Trim();
    }
}
