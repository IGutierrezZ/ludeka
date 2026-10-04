using System;
using System.Collections.Generic;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Xunit;

namespace Ludeka.UnitTests.Domain;

public class GameBarcodeTests
{
    private static Game CreateSampleGame(string? ean = null, IEnumerable<string>? additionalBarcodes = null)
    {
        return new Game(
            bggId: 13,
            originalTitle: "Catan",
            spanishTitle: "Catan",
            designer: "Klaus Teuber",
            publisher: "Devir",
            yearPublished: 1995,
            coverImageUrl: "https://example.com/catan.jpg",
            thumbnailUrl: "https://example.com/catan-thumb.jpg",
            description: "Juego de colonos.",
            bggRating: 7.1,
            bggRank: 500,
            ludistRating: 7.5,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: false,
            age: new AgeRating(10, 10),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(60, 90, 75),
            ean: ean,
            additionalBarcodes: additionalBarcodes
        );
    }

    [Fact]
    public void Game_ShouldInitializeWithValidEan()
    {
        var game = CreateSampleGame(ean: "8436017220100");

        Assert.Equal("8436017220100", game.Ean);
        Assert.True(game.MatchesBarcode("8436017220100"));
        Assert.True(game.MatchesBarcode(" 8436-0172-2010-0 "));
    }

    [Fact]
    public void Game_ShouldThrowArgumentException_ForInvalidEan()
    {
        Assert.Throws<ArgumentException>(() => CreateSampleGame(ean: "8436017220101")); // Check digit inválido
    }

    [Fact]
    public void Game_UpdateEan_ShouldAllowClearingEan()
    {
        var game = CreateSampleGame(ean: "8436017220100");
        Assert.NotNull(game.Ean);

        game.UpdateEan(null);
        Assert.Null(game.Ean);

        game.UpdateEan("   ");
        Assert.Null(game.Ean);
    }

    [Fact]
    public void Game_UpdateAdditionalBarcodes_ShouldFilterDuplicatesAndInvalidBarcodes()
    {
        var game = CreateSampleGame(ean: "8436017220100");

        game.UpdateAdditionalBarcodes([
            "8436017220124", // Carcassonne válido
            "8436017220100", // Mismo que el principal -> no debe duplicarse
            "invalido",      // No válido -> se descarta silenciosamente
            "4006381333931"  // Válido
        ]);

        Assert.Equal(2, game.AdditionalBarcodes.Count);
        Assert.Contains("8436017220124", game.AdditionalBarcodes);
        Assert.Contains("4006381333931", game.AdditionalBarcodes);
        Assert.DoesNotContain("8436017220100", game.AdditionalBarcodes);

        Assert.True(game.MatchesBarcode("8436017220124"));
        Assert.True(game.MatchesBarcode("4006381333931"));
        Assert.False(game.MatchesBarcode("9780201379624")); // Otro EAN no registrado
    }
}
