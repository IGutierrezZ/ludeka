using System;

namespace Ludeka.Core.Entities;

public enum FeedFormat
{
    GoogleShoppingXml = 1,
    GenericCsv = 2
}

/// <summary>
/// Representa una fuente de datos estructurados (feed comercial) provista por una tienda de juegos.
/// </summary>
public class AffiliateFeedSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StoreName { get; set; } = string.Empty;
    public string FeedUrl { get; set; } = string.Empty;
    public FeedFormat Format { get; set; } = FeedFormat.GoogleShoppingXml;
    public string? AffiliateTag { get; set; }
    public string Country { get; set; } = "España";
    public bool IsEnabled { get; set; } = true;
    public int SyncIntervalHours { get; set; } = 6;
    public DateTimeOffset? LastSyncUtc { get; set; }
    public string? LastSyncStatus { get; set; }
    public int MatchedProductsCount { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public AffiliateFeedSource() { }

    public AffiliateFeedSource(
        string storeName,
        string feedUrl,
        FeedFormat format = FeedFormat.GoogleShoppingXml,
        string? affiliateTag = null,
        string country = "España",
        int syncIntervalHours = 6)
    {
        if (string.IsNullOrWhiteSpace(storeName))
            throw new ArgumentException("El nombre de la tienda no puede estar vacío.", nameof(storeName));
        if (string.IsNullOrWhiteSpace(feedUrl))
            throw new ArgumentException("La URL del feed no puede estar vacía.", nameof(feedUrl));

        StoreName = storeName.Trim();
        FeedUrl = feedUrl.Trim();
        Format = format;
        AffiliateTag = string.IsNullOrWhiteSpace(affiliateTag) ? null : affiliateTag.Trim();
        Country = string.IsNullOrWhiteSpace(country) ? "España" : country.Trim();
        SyncIntervalHours = syncIntervalHours > 0 ? syncIntervalHours : 6;
    }

    public void RecordSyncResult(bool success, int matchedCount, string? statusNote = null)
    {
        LastSyncUtc = DateTimeOffset.UtcNow;
        LastSyncStatus = success ? "Success" : (statusNote ?? "Failed");
        if (success)
        {
            MatchedProductsCount = matchedCount;
        }
    }
}
