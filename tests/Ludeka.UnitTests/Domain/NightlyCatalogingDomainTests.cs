using System;
using System.Collections.Generic;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Xunit;

namespace Ludeka.UnitTests.Domain;

public class NightlyCatalogingDomainTests
{
    [Fact]
    public void PendingBggImport_WithOriginAndExtractedTitle_SetsPropertiesProperly()
    {
        // Act
        var item = new PendingBggImport(
            bggId: 370132,
            title: "Apiary",
            yearPublished: 2023,
            thumbnailUrl: "https://example.com/apiary.jpg",
            coverImageUrl: null,
            origin: CatalogQueueOrigin.NewsDiscovery,
            extractedTitle: "Apiary: Edición en Castellano"
        );

        // Assert
        Assert.Equal(CatalogQueueOrigin.NewsDiscovery, item.Origin);
        Assert.Equal("Apiary: Edición en Castellano", item.ExtractedTitle);
        Assert.Equal("Apiary", item.Title);
        Assert.Equal(370132, item.BggId);
        Assert.Equal(CatalogQueueStatus.Pending, item.Status);
    }

    [Fact]
    public void PendingBggImport_DefaultOrigin_IsUserImport()
    {
        // Act
        var item = new PendingBggImport(13, "Catan");

        // Assert
        Assert.Equal(CatalogQueueOrigin.UserImport, item.Origin);
        Assert.Null(item.ExtractedTitle);
    }

    [Fact]
    public void WeeklyRelease_LinkGame_WithValidGuid_AssignsGameId()
    {
        // Arrange
        var release = new WeeklyRelease("Devir anuncia Apiary", "Devir", new DateOnly(2026, 9, 15));
        var gameId = Guid.NewGuid();

        // Act
        release.LinkGame(gameId);

        // Assert
        Assert.Equal(gameId, release.GameId);
    }

    [Fact]
    public void WeeklyRelease_LinkGame_WithEmptyGuid_ThrowsArgumentException()
    {
        // Arrange
        var release = new WeeklyRelease("Devir anuncia Apiary", "Devir", new DateOnly(2026, 9, 15));

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => release.LinkGame(Guid.Empty));
        Assert.Contains("El identificador del juego vinculado no puede ser vacío", ex.Message);
    }

    [Fact]
    public void NightlyCatalogingExecutionLog_Lifecycle_CompleteAndFail_WorkCorrectly()
    {
        // Arrange
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var log = new NightlyCatalogingExecutionLog(startedAt);

        Assert.NotEqual(Guid.Empty, log.Id);
        Assert.Equal("Running", log.Status);
        Assert.Null(log.CompletedAt);

        // Act - Complete
        var titles = new List<string> { "Apiary", "Wingspan", "Brass: Birmingham" };
        log.Complete(
            queueProcessed: 1,
            newsDiscovery: 2,
            topBackfill: 2,
            totalCataloged: 3,
            failed: 0,
            titles: titles,
            bggDiscovery: 4
        );

        // Assert Complete
        Assert.Equal("Completed", log.Status);
        Assert.NotNull(log.CompletedAt);
        Assert.Equal(1, log.QueueProcessedCount);
        Assert.Equal(2, log.NewsDiscoveryCount);
        Assert.Equal(4, log.BggDiscoveryCount);
        Assert.Equal(2, log.TopBackfillCount);
        Assert.Equal(3, log.TotalCatalogedCount);
        Assert.Equal(0, log.FailedCount);
        Assert.Contains("Apiary", log.CatalogedTitlesJson);
        Assert.Contains("Brass: Birmingham", log.CatalogedTitlesJson);
        Assert.Null(log.ErrorMessage);

        // Act - Fail
        log.Fail("Error de conexión a BGG XMLAPI2");
        Assert.Equal("Failed", log.Status);
        Assert.Equal("Error de conexión a BGG XMLAPI2", log.ErrorMessage);
    }

    [Theory]
    [InlineData(CatalogQueueOrigin.BggNewReleases)]
    [InlineData(CatalogQueueOrigin.BggHotness)]
    public void PendingBggImport_WithBggDiscoveryOrigins_SetsOriginProperly(CatalogQueueOrigin origin)
    {
        var item = new PendingBggImport(4001, "Test Discovery", 2026, origin: origin);
        Assert.Equal(origin, item.Origin);
    }
}
