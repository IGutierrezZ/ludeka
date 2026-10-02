using System;
using System.Collections.Generic;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Ludeka.Infrastructure.Services;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class HeuristicExpansionAporteGeneratorTests
{
    private static Game CreateGame(
        int bggId,
        string title,
        GameType type = GameType.Expansion,
        string description = "",
        double bggRating = 7.5,
        IEnumerable<ScalabilityEntry>? scalability = null,
        bool isOfficialSolo = false)
    {
        return new Game(
            bggId: bggId,
            originalTitle: title,
            spanishTitle: title,
            designer: "Autor Test",
            publisher: "Editorial Test",
            yearPublished: 2022,
            coverImageUrl: "https://example.com/cover.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            description: description,
            bggRating: bggRating,
            bggRank: 500,
            ludistRating: bggRating,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: isOfficialSolo,
            age: new AgeRating(10, 10),
            language: LanguageDependence.None,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(45, 60, 20),
            scalability: scalability,
            type: type
        );
    }

    [Fact]
    public void Generate_ThrowsArgumentNullException_WhenExpansionIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => HeuristicExpansionAporteGenerator.Generate(null!));
    }

    [Fact]
    public void Generate_DetectsAddsPlayers_WhenScalabilityExceedsBaseGame()
    {
        // Arrange
        var baseGame = CreateGame(199792, "Everdell", GameType.BaseGame, scalability: [
            new ScalabilityEntry(1, "1J", ScalabilityStatus.Recommended),
            new ScalabilityEntry(4, "4J", ScalabilityStatus.MustPlay)
        ]);

        var expansion = CreateGame(328001, "Everdell: New Leaf", GameType.Expansion, scalability: [
            new ScalabilityEntry(1, "1J", ScalabilityStatus.Recommended),
            new ScalabilityEntry(6, "6J", ScalabilityStatus.Recommended)
        ]);

        // Act
        var result = HeuristicExpansionAporteGenerator.Generate(expansion, baseGame);

        // Assert
        Assert.Equal(2, result.ExtraPlayerCount);
        Assert.Contains(ExpansionImpactTag.AddsPlayers, result.ImpactTags);
        Assert.True(result.ExtraDurationMinutes > 0);
    }

    [Fact]
    public void Generate_DetectsAddsPlayersFromDescription_WhenNoScalabilityProvided()
    {
        // Arrange
        var expansion = CreateGame(12345, "Expansión 5-6 Jugadores", GameType.Expansion,
            description: "Permite añadir hasta 6 jugadores a la partida con nuevos componentes.");

        // Act
        var result = HeuristicExpansionAporteGenerator.Generate(expansion);

        // Assert
        Assert.Equal(2, result.ExtraPlayerCount);
        Assert.Contains(ExpansionImpactTag.AddsPlayers, result.ImpactTags);
    }

    [Fact]
    public void Generate_DetectsSoloModeAndTightensTime()
    {
        // Arrange
        var expansion = CreateGame(54321, "Prelude", GameType.Expansion,
            description: "Acelera el arranque rápido e introduce un desafiante modo solitario con automa.",
            bggRating: 8.5);

        // Act
        var result = HeuristicExpansionAporteGenerator.Generate(expansion);

        // Assert
        Assert.Contains(ExpansionImpactTag.AddsSoloMode, result.ImpactTags);
        Assert.Contains(ExpansionImpactTag.TightensTime, result.ImpactTags);
        Assert.Equal(-15, result.ExtraDurationMinutes);
        Assert.Equal(ExpansionNecessity.MustHave, result.Necessity);
    }

    [Fact]
    public void Generate_DetectsAsymmetryAndModularContent()
    {
        // Arrange
        var expansion = CreateGame(67890, "Facciones y Módulos", GameType.Expansion,
            description: "Incorpora facciones con habilidades únicas y varios módulos combinables para el mapa.",
            bggRating: 7.9);

        // Act
        var result = HeuristicExpansionAporteGenerator.Generate(expansion);

        // Assert
        Assert.Contains(ExpansionImpactTag.AddsAsymmetry, result.ImpactTags);
        Assert.Contains(ExpansionImpactTag.ModularContent, result.ImpactTags);
        Assert.Contains(ExpansionImpactTag.NewMapOrFactions, result.ImpactTags);
        Assert.Equal(ExpansionNecessity.HighlyRecommended, result.Necessity);
    }

    [Fact]
    public void Generate_FallsBackToModularContent_WhenNoKeywordsMatch()
    {
        // Arrange
        var expansion = CreateGame(99999, "Expansión Genérica", GameType.Expansion,
            description: "Contenido editorial adicional para disfrutar en mesa.",
            bggRating: 6.8);

        // Act
        var result = HeuristicExpansionAporteGenerator.Generate(expansion);

        // Assert
        Assert.Contains(ExpansionImpactTag.ModularContent, result.ImpactTags);
        Assert.Equal(ExpansionNecessity.OnlyForFans, result.Necessity);
        Assert.False(string.IsNullOrWhiteSpace(result.WhatItBringsSummary));
        Assert.Contains("enriquece a el juego base", result.WhatItBringsSummary);
        Assert.Contains("Contenido editorial adicional", result.WhatItBringsSummary);
    }
}
