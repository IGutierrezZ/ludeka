using System;
using System.Linq;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Xunit;

namespace Ludeka.UnitTests.Domain;

public class GameEditorDomainTests
{
    private static Game CreateSampleGame()
    {
        return new Game(
            bggId: 13,
            originalTitle: "Catan",
            spanishTitle: "Catán",
            designer: "Klaus Teuber",
            publisher: "Devir",
            yearPublished: 1995,
            coverImageUrl: "/images/games/catan.jpg",
            thumbnailUrl: "/images/games/catan.jpg",
            description: "Juego de colonización y comercio de la isla de Catán.",
            bggRating: 7.1,
            bggRank: 450,
            ludistRating: 7.5,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: false,
            age: new AgeRating(10, 10),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(60, 90, 25),
            scalability: new[]
            {
                new ScalabilityEntry(3, "3", ScalabilityStatus.Recommended),
                new ScalabilityEntry(4, "4", ScalabilityStatus.MustPlay)
            }
        );
    }

    [Fact]
    public void UpdateCatalogInformation_WithValidData_UpdatesAllFieldsCorrectly()
    {
        // Arrange
        var game = CreateSampleGame();

        // Act
        game.UpdateCatalogInformation(
            spanishTitle: "Catán: El Clásico",
            originalTitle: "The Settlers of Catan",
            designer: "Klaus Teuber & Benjamin Teuber",
            publisher: "Kosmos / Devir",
            yearPublished: 1996,
            description: "Nueva sinopsis editorial revisada.",
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: true,
            age: new AgeRating(8, 8),
            language: LanguageDependence.None,
            footprint: TableFootprint.TableMonster,
            duration: new GameDuration(45, 75, 20),
            minPlayers: 2,
            maxPlayers: 5
        );

        // Assert
        Assert.Equal("Catán: El Clásico", game.SpanishTitle);
        Assert.Equal("The Settlers of Catan", game.OriginalTitle);
        Assert.Equal("Klaus Teuber & Benjamin Teuber", game.Designer);
        Assert.Equal("Kosmos / Devir", game.Publisher);
        Assert.Equal(1996, game.YearPublished);
        Assert.Equal("Nueva sinopsis editorial revisada.", game.Description);
        Assert.True(game.IsOfficialSolo);
        Assert.Equal(8, game.Age.BoxAge);
        Assert.Equal(TableFootprint.TableMonster, game.Footprint);
        Assert.Equal(45, game.Duration.MinMinutes);
        Assert.Equal(75, game.Duration.MaxMinutes);

        // Scalability adjusted from 2 to 5
        Assert.Equal(4, game.Scalability.Count);
        Assert.Contains(game.Scalability, s => s.PlayerCount == 2);
        Assert.Contains(game.Scalability, s => s.PlayerCount == 3 && s.Status == ScalabilityStatus.Recommended);
        Assert.Contains(game.Scalability, s => s.PlayerCount == 4 && s.Status == ScalabilityStatus.MustPlay);
        Assert.Contains(game.Scalability, s => s.PlayerCount == 5);
    }

    [Fact]
    public void UpdateCatalogInformation_InvalidRanges_ThrowsExceptions()
    {
        // Arrange
        var game = CreateSampleGame();

        // Act & Assert: SpanishTitle empty
        Assert.Throws<ArgumentException>(() => game.UpdateCatalogInformation(
            "", "Original", "Designer", "Publisher", 2000, "Desc",
            ConfrontationType.Competitive, GameStyle.Eurogame, false,
            new AgeRating(10, 10), LanguageDependence.None, TableFootprint.SmallTable,
            new GameDuration(30, 60, 15), 2, 4));

        // Act & Assert: MinPlayers > MaxPlayers
        Assert.Throws<ArgumentException>(() => game.UpdateCatalogInformation(
            "Título", "Original", "Designer", "Publisher", 2000, "Desc",
            ConfrontationType.Competitive, GameStyle.Eurogame, false,
            new AgeRating(10, 10), LanguageDependence.None, TableFootprint.SmallTable,
            new GameDuration(30, 60, 15), 5, 4));

        // Act & Assert: Year published out of range (< -5000 o > año actual + 10)
        Assert.Throws<ArgumentOutOfRangeException>(() => game.UpdateCatalogInformation(
            "Título", "Original", "Designer", "Publisher", -6000, "Desc",
            ConfrontationType.Competitive, GameStyle.Eurogame, false,
            new AgeRating(10, 10), LanguageDependence.None, TableFootprint.SmallTable,
            new GameDuration(30, 60, 15), 2, 4));

        Assert.Throws<ArgumentOutOfRangeException>(() => game.UpdateCatalogInformation(
            "Título", "Original", "Designer", "Publisher", DateTime.UtcNow.Year + 50, "Desc",
            ConfrontationType.Competitive, GameStyle.Eurogame, false,
            new AgeRating(10, 10), LanguageDependence.None, TableFootprint.SmallTable,
            new GameDuration(30, 60, 15), 2, 4));
    }

    [Theory]
    [InlineData(-2200)] // Go / Weiqi
    [InlineData(-3500)] // Senet
    [InlineData(1475)]  // Ajedrez moderno
    [InlineData(1876)]  // Crokinole
    [InlineData(0)]     // Sin año formal asignado en BGG
    [InlineData(2026)]  // Año contemporáneo
    public void UpdateCatalogInformation_HistoricalAndSpecialYears_Succeeds(int year)
    {
        // Arrange
        var game = CreateSampleGame();

        // Act
        game.UpdateCatalogInformation(
            "Catán", "Catan", "Klaus Teuber", "Devir", year, "Desc",
            ConfrontationType.Competitive, GameStyle.Eurogame, false,
            new AgeRating(10, 10), LanguageDependence.None, TableFootprint.SmallTable,
            new GameDuration(30, 60, 15), 2, 4);

        // Assert
        Assert.Equal(year, game.YearPublished);
    }

    [Fact]
    public void UpdateDna_UpdatesStyleAndConfrontationCorrectly()
    {
        // Arrange
        var game = CreateSampleGame();

        // Act
        game.UpdateDna(GameStyle.Ameritrash, ConfrontationType.Cooperative, true);

        // Assert
        Assert.Equal(GameStyle.Ameritrash, game.Style);
        Assert.Equal(ConfrontationType.Cooperative, game.Confrontation);
        Assert.True(game.IsOfficialSolo);
    }

    [Fact]
    public void UpdateCatalogInformation_PreservesScalabilityWithCommunityVotes()
    {
        // Arrange
        var game = CreateSampleGame(); // sample game has 3J (Recommended) and 4J (MustPlay) with votes
        game.UpdateScalability([
            new ScalabilityEntry(3, "3J", ScalabilityStatus.MustPlay, 100, 20, 5),
            new ScalabilityEntry(4, "4J", ScalabilityStatus.MustPlay, 200, 10, 2)
        ]);

        // Act: update catalog info with range 1 to 4
        game.UpdateCatalogInformation(
            "Catán", "Catan", "Klaus Teuber", "Devir", 1995, "Desc",
            ConfrontationType.Competitive, GameStyle.Eurogame, false,
            new AgeRating(10, 10), LanguageDependence.None, TableFootprint.SmallTable,
            new GameDuration(30, 60, 15), 1, 4);

        // Assert: scalability with community votes was preserved, not overwritten with dummy recommended
        Assert.Equal(2, game.Scalability.Count);
        Assert.Contains(game.Scalability, s => s.PlayerCount == 3 && s.Status == ScalabilityStatus.MustPlay && s.BestVotes == 100);
        Assert.Contains(game.Scalability, s => s.PlayerCount == 4 && s.Status == ScalabilityStatus.MustPlay && s.BestVotes == 200);
    }

    [Fact]
    public void UpdateImages_NormalizesAndUpdatesProperties()
    {
        // Arrange
        var game = CreateSampleGame();

        // Act
        game.UpdateImages("  /images/games/catan-hd.jpg  ", "  /images/games/catan-thumb.jpg  ");

        // Assert
        Assert.Equal("/images/games/catan-hd.jpg", game.CoverImageUrl);
        Assert.Equal("/images/games/catan-thumb.jpg", game.ThumbnailUrl);
    }

    [Fact]
    public void GameEditLog_Constructor_ValidatesAndInitializesCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        // Act
        var log = new GameEditLog(
            gameId,
            "mod-1",
            "Marta Moderadora",
            "Añadido soporte a 2 jugadores y actualizada carátula.",
            reportId
        );

        // Assert
        Assert.NotEqual(Guid.Empty, log.Id);
        Assert.Equal(gameId, log.GameId);
        Assert.Equal("mod-1", log.EditorUserId);
        Assert.Equal("Marta Moderadora", log.EditorName);
        Assert.Equal("Añadido soporte a 2 jugadores y actualizada carátula.", log.SummaryOfChanges);
        Assert.Equal(reportId, log.AssociatedReportId);
        Assert.True((DateTimeOffset.UtcNow - log.EditedAt).TotalSeconds < 5);
    }

    [Fact]
    public void GameEditLog_EmptyGameIdOrUser_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new GameEditLog(Guid.Empty, "user", "name", "summary"));
        Assert.Throws<ArgumentException>(() => new GameEditLog(Guid.NewGuid(), "", "name", "summary"));
    }
}
