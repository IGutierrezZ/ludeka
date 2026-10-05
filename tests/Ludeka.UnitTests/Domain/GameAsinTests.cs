using System;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Xunit;

namespace Ludeka.UnitTests.Domain;

public class GameAsinTests
{
    private static Game CreateTestGame()
    {
        return new Game(
            bggId: 174430,
            originalTitle: "Gloomhaven",
            spanishTitle: "Gloomhaven",
            designer: "Isaac Childres",
            publisher: "Cephalofair Games",
            yearPublished: 2017,
            coverImageUrl: "https://example.com/cover.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            description: "Juego de rol táctico cooperativo.",
            bggRating: 8.7,
            bggRank: 3,
            ludistRating: 9.0,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: true,
            age: new AgeRating(14, 14),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(60, 120, 30)
        );
    }

    [Fact]
    public void Game_InitialAsin_ShouldBeNull()
    {
        var game = CreateTestGame();
        Assert.Null(game.Asin);
    }

    [Theory]
    [InlineData("B07MZT757D", "B07MZT757D")]
    [InlineData("  b07mzt757d  ", "B07MZT757D")]
    [InlineData("b01g958v6u", "B01G958V6U")]
    public void SetAsin_WithValidString_NormalizesAndTrimsToUpperCase(string input, string expected)
    {
        var game = CreateTestGame();
        game.SetAsin(input);

        Assert.Equal(expected, game.Asin);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetAsin_WithNullOrWhitespace_SetsAsinToNull(string? input)
    {
        var game = CreateTestGame();
        game.SetAsin("B07MZT757D");
        Assert.NotNull(game.Asin);

        game.SetAsin(input);
        Assert.Null(game.Asin);
    }

    [Fact]
    public void SetAsin_WithLongerThan20Characters_TruncatesSafelyTo20()
    {
        var game = CreateTestGame();
        var longAsin = "B07MZT757D1234567890EXTRA";
        game.SetAsin(longAsin);

        Assert.Equal(20, game.Asin!.Length);
        Assert.Equal("B07MZT757D1234567890", game.Asin);
    }
}
