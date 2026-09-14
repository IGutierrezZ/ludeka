using System;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Xunit;

namespace Ludeka.UnitTests.Domain;

public class BggCatalogStagingItemTests
{
    [Fact]
    public void Constructor_WithValidArguments_InitializesStagingItemCorrectly()
    {
        // Act
        var item = new BggCatalogStagingItem(
            bggId: 224517,
            originalTitle: "Brass: Birmingham",
            yearPublished: 2018,
            bggRank: 1,
            usersRated: 45000,
            bayesAverage: 8.42,
            averageRating: 8.61,
            spanishTitle: "Brass: Birmingham"
        );

        // Assert
        Assert.Equal(224517, item.BggId);
        Assert.Equal("Brass: Birmingham", item.OriginalTitle);
        Assert.Equal("Brass: Birmingham", item.SpanishTitle);
        Assert.Equal(2018, item.YearPublished);
        Assert.Equal(1, item.BggRank);
        Assert.Equal(45000, item.UsersRated);
        Assert.Equal(8.42, item.BayesAverage);
        Assert.Equal(8.61, item.AverageRating);
        Assert.Equal(StagingFetchStatus.Pending, item.FetchStatus);
        Assert.Equal(StagingImagesStatus.Pending, item.ImagesStatus);
        Assert.Equal(StagingAiStatus.Pending, item.AiStatus);
        Assert.Equal(StagingPromotionStatus.Pending, item.PromotionStatus);
        Assert.Null(item.ErrorMessage);
        Assert.Null(item.ProcessedAt);
        Assert.True(item.CreatedAt <= DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithInvalidBggId_ThrowsArgumentOutOfRangeException(int invalidId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BggCatalogStagingItem(invalidId, "Title"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidTitle_ThrowsArgumentException(string? invalidTitle)
    {
        Assert.Throws<ArgumentException>(() => new BggCatalogStagingItem(123, invalidTitle!));
    }

    [Fact]
    public void MarkFetched_UpdatesFieldsAndSetsStatusToFetched()
    {
        // Arrange
        var item = new BggCatalogStagingItem(174430, "Gloomhaven");

        // Act
        item.MarkFetched(
            rawXml: "<item id=\"174430\"><description>Epic</description></item>",
            spanishTitle: "Gloomhaven en español",
            designer: "Isaac Childres",
            publisher: "Cephalofair Games",
            description: "Juego de mazmorreo táctico.",
            minPlayers: 1,
            maxPlayers: 4,
            playingTimeMinutes: 120,
            minAge: 14,
            bggRating: 8.7
        );

        // Assert
        Assert.Equal(StagingFetchStatus.Fetched, item.FetchStatus);
        Assert.Equal("Gloomhaven en español", item.SpanishTitle);
        Assert.Equal("Isaac Childres", item.Designer);
        Assert.Equal("Cephalofair Games", item.Publisher);
        Assert.Equal("Juego de mazmorreo táctico.", item.Description);
        Assert.Equal(1, item.MinPlayers);
        Assert.Equal(4, item.MaxPlayers);
        Assert.Equal(120, item.PlayingTimeMinutes);
        Assert.Equal(14, item.MinAge);
        Assert.Equal(8.7, item.AverageRating);
    }

    [Fact]
    public void MarkImagesCompleted_SetsUrlsAndStatus()
    {
        // Arrange
        var item = new BggCatalogStagingItem(174430, "Gloomhaven");

        // Act
        item.MarkImagesCompleted(
            coverImageUrl: "https://cdn.ludeka.com/games/174430/cover.webp",
            thumbnailUrl: "https://cdn.ludeka.com/games/174430/cover_thumb.webp",
            backCoverImageUrl: "https://cdn.ludeka.com/games/174430/back.webp",
            tableImageUrl: "https://cdn.ludeka.com/games/174430/table.webp"
        );

        // Assert
        Assert.Equal(StagingImagesStatus.Completed, item.ImagesStatus);
        Assert.Equal("https://cdn.ludeka.com/games/174430/cover.webp", item.CoverImageUrl);
        Assert.Equal("https://cdn.ludeka.com/games/174430/cover_thumb.webp", item.ThumbnailUrl);
        Assert.Equal("https://cdn.ludeka.com/games/174430/back.webp", item.BackCoverImageUrl);
        Assert.Equal("https://cdn.ludeka.com/games/174430/table.webp", item.TableImageUrl);
    }

    [Fact]
    public void MarkAiCompleted_And_QuotaExceeded_ManagesStateTransitions()
    {
        // Arrange
        var item = new BggCatalogStagingItem(174430, "Gloomhaven");

        // Act 1: Quota exceeded
        item.MarkAiQuotaExceeded();
        Assert.Equal(StagingAiStatus.QuotaExceeded, item.AiStatus);

        // Act 2: Reset to pending
        item.ResetAiQuotaToPending();
        Assert.Equal(StagingAiStatus.Pending, item.AiStatus);

        // Act 3: Completed
        string json = "{\"generalVerdict\": \"Imprescindible\", \"scalabilitySummary\": \"Brilla a 2-3\"}";
        item.MarkAiCompleted(json);
        Assert.Equal(StagingAiStatus.Completed, item.AiStatus);
        Assert.Equal(json, item.AiSummaryJson);
    }

    [Fact]
    public void MarkPromoted_SetsStatusAndProcessedAt()
    {
        // Arrange
        var item = new BggCatalogStagingItem(174430, "Gloomhaven");

        // Act
        item.MarkPromoted();

        // Assert
        Assert.Equal(StagingPromotionStatus.Promoted, item.PromotionStatus);
        Assert.NotNull(item.ProcessedAt);
    }

    [Fact]
    public void MarkStageFailed_IncrementsRetryCountAndSetsError()
    {
        // Arrange
        var item = new BggCatalogStagingItem(174430, "Gloomhaven");

        // Act
        item.MarkStageFailed("images", "Timeout al descargar foto");

        // Assert
        Assert.Equal(StagingImagesStatus.Failed, item.ImagesStatus);
        Assert.Equal(1, item.RetryCount);
        Assert.Contains("Timeout al descargar foto", item.ErrorMessage);
    }

    [Fact]
    public void Game_UpdateMediaUrls_UpdatesBackCoverAndTableImages()
    {
        // Arrange
        var game = new Game(
            bggId: 342942,
            originalTitle: "Ark Nova",
            spanishTitle: "Ark Nova",
            designer: "Mathias Wigge",
            publisher: "Feuerland Spiele",
            yearPublished: 2021,
            coverImageUrl: "https://example.com/cover.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            description: "Zoológico moderno",
            bggRating: 8.5,
            bggRank: 4,
            ludistRating: 8.8,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: true,
            age: new Ludeka.Core.ValueObjects.AgeRating(14, 12),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new Ludeka.Core.ValueObjects.GameDuration(90, 150, 45)
        );

        // Act
        game.UpdateMediaUrls(
            coverImageUrl: "https://cdn.ludeka.com/games/342942/cover.webp",
            thumbnailUrl: "https://cdn.ludeka.com/games/342942/cover_thumb.webp",
            backCoverImageUrl: "https://cdn.ludeka.com/games/342942/back.webp",
            tableImageUrl: "https://cdn.ludeka.com/games/342942/table.webp"
        );

        // Assert
        Assert.Equal("https://cdn.ludeka.com/games/342942/cover.webp", game.CoverImageUrl);
        Assert.Equal("https://cdn.ludeka.com/games/342942/cover_thumb.webp", game.ThumbnailUrl);
        Assert.Equal("https://cdn.ludeka.com/games/342942/back.webp", game.BackCoverImageUrl);
        Assert.Equal("https://cdn.ludeka.com/games/342942/table.webp", game.TableImageUrl);
    }
}
