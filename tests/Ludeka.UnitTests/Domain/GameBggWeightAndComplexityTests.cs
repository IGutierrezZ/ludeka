using System.Collections.Generic;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.Helpers;
using Ludeka.Core.ValueObjects;
using Xunit;

namespace Ludeka.UnitTests.Domain;

public class GameBggWeightAndComplexityTests
{
    [Theory]
    [InlineData(1.00, GameComplexity.Light)]
    [InlineData(1.80, GameComplexity.Light)]
    [InlineData(2.19, GameComplexity.Light)]
    [InlineData(2.20, GameComplexity.Medium)]
    [InlineData(2.80, GameComplexity.Medium)]
    [InlineData(3.24, GameComplexity.Medium)]
    [InlineData(3.25, GameComplexity.Heavy)]
    [InlineData(4.50, GameComplexity.Heavy)]
    [InlineData(5.00, GameComplexity.Heavy)]
    public void ComplexityCalculator_WithBggWeight_ShouldClassifyCorrectly(double weight, GameComplexity expected)
    {
        // Act: Probamos con estilo y duración que normalmente serían opuestos para verificar que BggWeight manda
        var result = ComplexityCalculator.Calculate(weight, GameStyle.Eurogame, 150, 14);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ComplexityCalculator_WithNullOrZeroWeight_ShouldFallbackToHeuristic()
    {
        // Light por PartyGame
        var light = ComplexityCalculator.Calculate(null, GameStyle.PartyGame, 20, 8);
        Assert.Equal(GameComplexity.Light, light);

        // Heavy por Eurogame largo
        var heavy = ComplexityCalculator.Calculate(0.0, GameStyle.Eurogame, 120, 14);
        Assert.Equal(GameComplexity.Heavy, heavy);

        // Medium por descarte
        var medium = ComplexityCalculator.Calculate(null, GameStyle.Ameritrash, 60, 12);
        Assert.Equal(GameComplexity.Medium, medium);
    }

    [Fact]
    public void Game_UpdateBggWeight_ShouldRoundAndClampProperly()
    {
        var game = CreateSampleGame();

        // Inicialmente null
        Assert.Null(game.BggWeight);

        // Asignación con decimales largos -> redondeo a 2 decimales
        game.UpdateBggWeight(2.4632);
        Assert.Equal(2.46, game.BggWeight);

        // Clamping superior > 5.0 -> 5.0
        game.UpdateBggWeight(5.5);
        Assert.Equal(5.0, game.BggWeight);

        // Clamping inferior < 1.0 (pero > 0) -> 1.0
        game.UpdateBggWeight(0.8);
        Assert.Equal(1.0, game.BggWeight);

        // <= 0 o null -> null
        game.UpdateBggWeight(0.0);
        Assert.Null(game.BggWeight);

        game.UpdateBggWeight(null);
        Assert.Null(game.BggWeight);
    }

    [Fact]
    public void Game_ComplexityProperty_ShouldEvaluateViaComplexityCalculator()
    {
        var game = CreateSampleGame();
        game.UpdateBggWeight(3.88);

        Assert.Equal(GameComplexity.Heavy, game.Complexity);

        game.UpdateBggWeight(1.50);
        Assert.Equal(GameComplexity.Light, game.Complexity);
    }

    [Fact]
    public void ComplexityCalculator_ExtractWeightFromJson_ShouldParseCorrectly()
    {
        // Formato objeto con @value
        string jsonWithValue = @"{""statistics"":{""ratings"":{""averageweight"":{""@value"":""3.2645""}}}}";
        var weight1 = ComplexityCalculator.ExtractWeightFromJson(jsonWithValue);
        Assert.Equal(3.26, weight1);

        // Formato string directo
        string jsonWithString = @"{""statistics"":{""ratings"":{""averageweight"":""2.15""}}}";
        var weight2 = ComplexityCalculator.ExtractWeightFromJson(jsonWithString);
        Assert.Equal(2.15, weight2);

        // Formato numérico
        string jsonWithNumber = @"{""statistics"":{""ratings"":{""averageweight"":1.75}}}";
        var weight3 = ComplexityCalculator.ExtractWeightFromJson(jsonWithNumber);
        Assert.Equal(1.75, weight3);

        // Clamping y defensivo
        string jsonZero = @"{""statistics"":{""ratings"":{""averageweight"":{""@value"":""0""}}}}";
        Assert.Null(ComplexityCalculator.ExtractWeightFromJson(jsonZero));

        string jsonInvalid = @"{""statistics"":{}}";
        Assert.Null(ComplexityCalculator.ExtractWeightFromJson(jsonInvalid));

        Assert.Null(ComplexityCalculator.ExtractWeightFromJson(""));
        Assert.Null(ComplexityCalculator.ExtractWeightFromJson(null));
    }

    [Fact]
    public void ComplexityCalculator_GetEffectiveWeight_WithBggWeight_ShouldReturnBggWeight()
    {
        var eff1 = ComplexityCalculator.GetEffectiveWeight(2.35, GameStyle.PartyGame, 15, 6);
        Assert.Equal(2.35, eff1);

        var eff2 = ComplexityCalculator.GetEffectiveWeight(4.12, GameStyle.FillerAbstract, 20, 8);
        Assert.Equal(4.12, eff2);
    }

    [Fact]
    public void ComplexityCalculator_GetEffectiveWeight_WithoutBggWeight_ShouldReturnExtrapolatedWeight()
    {
        // Light -> 1.60
        var effLight = ComplexityCalculator.GetEffectiveWeight(null, GameStyle.PartyGame, 20, 8);
        Assert.Equal(ComplexityCalculator.DefaultExtrapolatedLight, effLight);
        Assert.Equal(1.60, effLight);

        // Heavy -> 3.80
        var effHeavy = ComplexityCalculator.GetEffectiveWeight(0.0, GameStyle.Eurogame, 150, 14);
        Assert.Equal(ComplexityCalculator.DefaultExtrapolatedHeavy, effHeavy);
        Assert.Equal(3.80, effHeavy);

        // Medium -> 2.70
        var effMed = ComplexityCalculator.GetEffectiveWeight(null, GameStyle.Ameritrash, 60, 10);
        Assert.Equal(ComplexityCalculator.DefaultExtrapolatedMedium, effMed);
        Assert.Equal(2.70, effMed);
    }

    [Fact]
    public void ComplexityCalculator_GetEffectiveWeight_GameOverload_ShouldResolveCorrectly()
    {
        var gameWithWeight = CreateSampleGame(bggWeight: 3.15);
        Assert.Equal(3.15, ComplexityCalculator.GetEffectiveWeight(gameWithWeight));

        var gameWithoutWeight = CreateSampleGame(bggWeight: null);
        // Sample game tiene Eurogame, maxMinutes 60, age 10 -> Medium (2.70)
        Assert.Equal(2.70, ComplexityCalculator.GetEffectiveWeight(gameWithoutWeight));

        Assert.Equal(ComplexityCalculator.DefaultExtrapolatedMedium, ComplexityCalculator.GetEffectiveWeight(null as Game));
    }

    private static Game CreateSampleGame(double? bggWeight = null)
    {
        return new Game(
            bggId: 100,
            originalTitle: "Test Game",
            spanishTitle: "Juego de Prueba",
            designer: "Autor Test",
            publisher: "Editorial Test",
            yearPublished: 2024,
            coverImageUrl: "/images/test.jpg",
            thumbnailUrl: "/images/test-thumb.jpg",
            description: "Descripción de prueba",
            bggRating: 7.5,
            bggRank: 100,
            ludistRating: 7.5,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: false,
            age: new AgeRating(10, 10),
            language: LanguageDependence.None,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(30, 60, 20),
            bggWeight: bggWeight
        );
    }
}
