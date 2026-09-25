using System;
using System.Collections.Generic;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Xunit;

namespace Ludeka.UnitTests.Domain;

public class CardSleeveDomainTests
{
    [Fact]
    public void SleeveItem_CalculatesPacksNeeded_CorrectlyForDifferentSizes()
    {
        // 110 cartas (ej. Terraforming Mars o Wingspan)
        var sleeve = new SleeveItem("Standard Card Game", 63.5, 88.0, 110, null);

        Assert.Equal(3, sleeve.CalculatePacksNeeded(50));
        Assert.Equal(3, sleeve.PacksNeeded50);
        Assert.Equal(2, sleeve.CalculatePacksNeeded(100));
        Assert.Equal(2, sleeve.PacksNeeded100);
        Assert.Equal("63.5 x 88 mm", sleeve.DimensionText);
    }

    [Theory]
    [InlineData(0, 50, 0)]
    [InlineData(50, 50, 1)]
    [InlineData(51, 50, 2)]
    [InlineData(100, 100, 1)]
    [InlineData(101, 100, 2)]
    public void SleeveItem_CalculatesPacksNeeded_EdgeCases(int cardCount, int packSize, int expectedPacks)
    {
        var sleeve = new SleeveItem("Test", 56, 87, cardCount, null);
        Assert.Equal(expectedPacks, sleeve.CalculatePacksNeeded(packSize));
    }

    [Fact]
    public void SleeveItem_ThrowsException_WhenPackSizeIsZeroOrNegative()
    {
        var sleeve = new SleeveItem("Test", 56, 87, 50, null);

        Assert.Throws<ArgumentOutOfRangeException>(() => sleeve.CalculatePacksNeeded(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => sleeve.CalculatePacksNeeded(-10));
    }

    [Fact]
    public void SleeveItem_ShipsTo_WorksWithCountries()
    {
        var sleeve = new SleeveItem(
            FormatName: "Mini Euro",
            WidthMm: 44,
            HeightMm: 68,
            CardCount: 73,
            AffiliateUrl: "https://zacatrus.es/fundas",
            StoreName: "Zacatrus",
            Country: "España",
            ShippingCountries: ["Portugal", "Francia"]);

        Assert.True(sleeve.ShipsTo("España"));
        Assert.True(sleeve.ShipsTo("espana"));
        Assert.True(sleeve.ShipsTo("Portugal"));
        Assert.False(sleeve.ShipsTo("México"));
        Assert.True(sleeve.ShipsTo(null)); // Sin filtro, permite ver
    }

    [Fact]
    public void StandardSleeveCatalog_MatchesStandardFormats_WithinTolerance()
    {
        // 63.5 x 88 mm debe coincidir con "Standard Card Game"
        var format1 = StandardSleeveCatalog.Match(63.5, 88.0);
        Assert.NotNull(format1);
        Assert.Equal("Standard Card Game", format1.Name);

        // 64 x 89 mm está dentro de la tolerancia de 1.5 mm
        var format2 = StandardSleeveCatalog.Match(64.0, 89.0);
        Assert.NotNull(format2);
        Assert.Equal("Standard Card Game", format2.Name);

        // 44 x 68 mm debe coincidir con "Mini Euro"
        var miniEuro = StandardSleeveCatalog.Match(44.0, 68.0);
        Assert.NotNull(miniEuro);
        Assert.Equal("Mini Euro", miniEuro.Name);

        // Medida no estándar muy distante
        var unknown = StandardSleeveCatalog.Match(150.0, 200.0);
        Assert.Null(unknown);
    }

    [Fact]
    public void StandardSleeveCatalog_FindByName_FindsFormat()
    {
        var format = StandardSleeveCatalog.FindByName("mini euro");
        Assert.NotNull(format);
        Assert.Equal("Mini Euro", format.Name);

        var tarot = StandardSleeveCatalog.FindByName("Tarot");
        Assert.NotNull(tarot);
        Assert.Contains("Tarot", tarot.Name);
    }

    [Fact]
    public void Game_UpdateSleeves_MutatesCollectionProperly()
    {
        var game = new Game(
            bggId: 1001,
            originalTitle: "Test Game",
            spanishTitle: "Juego de Prueba",
            designer: "Autor",
            publisher: "Editorial",
            yearPublished: 2024,
            coverImageUrl: null,
            thumbnailUrl: null,
            description: "Desc",
            bggRating: 8.0,
            bggRank: 10,
            ludistRating: 8.0,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: false,
            age: new AgeRating(10, 10),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(30, 60, 20),
            scalability: []
        );

        Assert.Empty(game.Sleeves);

        var sleeve1 = new SleeveItem("Mini Euro", 44, 68, 73, null);
        var sleeve2 = new SleeveItem("Tarot", 65, 100, 12, null);

        game.AddSleeve(sleeve1);
        Assert.Single(game.Sleeves);

        game.UpdateSleeves([sleeve1, sleeve2]);
        Assert.Equal(2, game.Sleeves.Count);

        game.ClearSleeves();
        Assert.Empty(game.Sleeves);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SleeveItem_Constructor_ThrowsArgumentException_WhenFormatNameIsNullOrWhiteSpace(string? formatName)
    {
        Assert.Throws<ArgumentException>(() => new SleeveItem(formatName!, 63.5, 88.0, 50, null));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-10.0)]
    [InlineData(20.0)]
    [InlineData(29.9)]
    [InlineData(250.1)]
    [InlineData(300.0)]
    public void SleeveItem_Constructor_ThrowsArgumentOutOfRangeException_WhenWidthIsOutOfRange(double widthMm)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SleeveItem("Standard", widthMm, 88.0, 50, null));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-10.0)]
    [InlineData(20.0)]
    [InlineData(29.9)]
    [InlineData(250.1)]
    [InlineData(300.0)]
    public void SleeveItem_Constructor_ThrowsArgumentOutOfRangeException_WhenHeightIsOutOfRange(double heightMm)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SleeveItem("Standard", 63.5, heightMm, 50, null));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-50)]
    public void SleeveItem_Constructor_ThrowsArgumentOutOfRangeException_WhenCardCountIsNegative(int cardCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SleeveItem("Standard", 63.5, 88.0, cardCount, null));
    }

    [Fact]
    public void StandardSleeveCatalog_MatchesAndMatch_SupportsOrientationIndependentDimensions()
    {
        var format = StandardSleeveCatalog.Match(88.0, 63.5);
        Assert.NotNull(format);
        Assert.Equal("Standard Card Game", format.Name);

        Assert.True(format.Matches(88.0, 63.5));
        Assert.True(format.Matches(63.5, 88.0));

        var miniEuro = StandardSleeveCatalog.Match(68.0, 44.0);
        Assert.NotNull(miniEuro);
        Assert.Equal("Mini Euro", miniEuro.Name);
        Assert.True(miniEuro.Matches(68.0, 44.0));
    }

    [Theory]
    [InlineData("Mini European", "Mini Euro")]
    [InlineData("mini european", "Mini Euro")]
    [InlineData("Standard European", "Euro Standard")]
    [InlineData("Chimera Standard", "Chimera / USA")]
    public void StandardSleeveCatalog_FindByName_FindsCommonAliases(string alias, string expectedCanonicalName)
    {
        var format = StandardSleeveCatalog.FindByName(alias);
        Assert.NotNull(format);
        Assert.Equal(expectedCanonicalName, format.Name);
    }
}
