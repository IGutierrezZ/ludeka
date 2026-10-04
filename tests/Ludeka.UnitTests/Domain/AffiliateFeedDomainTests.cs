using System;
using Ludeka.Core.Entities;
using Xunit;

namespace Ludeka.UnitTests.Domain;

public class AffiliateFeedDomainTests
{
    [Fact]
    public void AffiliateFeedSource_ValidArguments_InitializesCorrectly()
    {
        var source = new AffiliateFeedSource(
            storeName: "Zacatrus",
            feedUrl: "https://zacatrus.es/feeds/google.xml",
            format: FeedFormat.GoogleShoppingXml,
            affiliateTag: "ludeka",
            country: "España",
            syncIntervalHours: 4);

        Assert.Equal("Zacatrus", source.StoreName);
        Assert.Equal("https://zacatrus.es/feeds/google.xml", source.FeedUrl);
        Assert.Equal(FeedFormat.GoogleShoppingXml, source.Format);
        Assert.Equal("ludeka", source.AffiliateTag);
        Assert.Equal("España", source.Country);
        Assert.Equal(4, source.SyncIntervalHours);
        Assert.True(source.IsEnabled);
        Assert.Equal(0, source.MatchedProductsCount);
        Assert.Null(source.LastSyncUtc);
    }

    [Theory]
    [InlineData("", "https://valid.com")]
    [InlineData("   ", "https://valid.com")]
    [InlineData("Tienda", "")]
    [InlineData("Tienda", "   ")]
    public void AffiliateFeedSource_InvalidArguments_ThrowsArgumentException(string storeName, string feedUrl)
    {
        Assert.Throws<ArgumentException>(() => new AffiliateFeedSource(storeName, feedUrl));
    }

    [Fact]
    public void AffiliateFeedSource_RecordSyncResult_UpdatesMetrics()
    {
        var source = new AffiliateFeedSource("Zacatrus", "https://zacatrus.es/feeds/google.xml");
        
        source.RecordSyncResult(success: true, matchedCount: 142);

        Assert.NotNull(source.LastSyncUtc);
        Assert.Equal("Success", source.LastSyncStatus);
        Assert.Equal(142, source.MatchedProductsCount);

        source.RecordSyncResult(success: false, matchedCount: 0, statusNote: "HTTP 500");

        Assert.Equal("HTTP 500", source.LastSyncStatus);
        Assert.Equal(142, source.MatchedProductsCount); // Preserva el conteo previo exitoso
    }

    [Fact]
    public void AffiliateEanDiscrepancyLog_ValidArguments_InitializesCorrectly()
    {
        var gameId = Guid.NewGuid();
        var log = new AffiliateEanDiscrepancyLog(
            gameId: gameId,
            gameTitle: "Wingspan",
            gameSlug: "wingspan",
            currentEan: "0012345678905",
            feedEan: "8435407629616",
            storeName: "Zacatrus");

        Assert.Equal(gameId, log.GameId);
        Assert.Equal("Wingspan", log.GameTitle);
        Assert.Equal("wingspan", log.GameSlug);
        Assert.Equal("0012345678905", log.CurrentEan);
        Assert.Equal("8435407629616", log.FeedEan);
        Assert.Equal("Zacatrus", log.StoreName);
        Assert.False(log.IsResolved);
        Assert.Null(log.ResolutionNote);

        log.MarkResolved("Promovido a principal por admin");

        Assert.True(log.IsResolved);
        Assert.Equal("Promovido a principal por admin", log.ResolutionNote);
    }
}
