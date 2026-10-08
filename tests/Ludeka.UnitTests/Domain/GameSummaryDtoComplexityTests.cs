using System;
using Ludeka.Application.DTOs;
using Ludeka.Core.Enums;
using Ludeka.Core.Helpers;
using Xunit;

namespace Ludeka.UnitTests.Domain;

public class GameSummaryDtoComplexityTests
{
    [Fact]
    public void GameSummaryDto_WithBggWeight_ShouldFormatBadgeAndTooltipProperly()
    {
        var dto = CreateDto(bggWeight: 2.34);

        Assert.Equal(2.34, dto.EffectiveWeight);
        Assert.Equal(GameComplexity.Medium, dto.Complexity);
        Assert.Equal("2.3/5", dto.ComplexityDisplayBadge);
        Assert.Equal("Dureza: 2.3/5 (Medio)", dto.ComplexityTooltip);
    }

    [Fact]
    public void GameSummaryDto_WithoutBggWeight_ShouldFallbackToQualitativeExtrapolation()
    {
        // PartyGame sin peso -> Light (1.60)
        var dtoLight = CreateDto(bggWeight: null, style: GameStyle.PartyGame);
        Assert.Equal(ComplexityCalculator.DefaultExtrapolatedLight, dtoLight.EffectiveWeight);
        Assert.Equal(GameComplexity.Light, dtoLight.Complexity);
        Assert.Equal("Ligero", dtoLight.ComplexityDisplayBadge);
        Assert.Equal("Dureza estimada: Ligero", dtoLight.ComplexityTooltip);

        // Eurogame 120min + 14 años sin peso -> Heavy (3.80)
        var dtoHeavy = CreateDto(bggWeight: null, style: GameStyle.Eurogame, perPlayerMinutes: 60, communityAge: 14);
        Assert.Equal(ComplexityCalculator.DefaultExtrapolatedHeavy, dtoHeavy.EffectiveWeight);
        Assert.Equal(GameComplexity.Heavy, dtoHeavy.Complexity);
        Assert.Equal("Duro", dtoHeavy.ComplexityDisplayBadge);
        Assert.Equal("Dureza estimada: Duro", dtoHeavy.ComplexityTooltip);

        // Ameritrash sin peso -> Medium (2.70)
        var dtoMedium = CreateDto(bggWeight: null, style: GameStyle.Ameritrash, perPlayerMinutes: 30, communityAge: 10);
        Assert.Equal(ComplexityCalculator.DefaultExtrapolatedMedium, dtoMedium.EffectiveWeight);
        Assert.Equal(GameComplexity.Medium, dtoMedium.Complexity);
        Assert.Equal("Medio", dtoMedium.ComplexityDisplayBadge);
        Assert.Equal("Dureza estimada: Medio", dtoMedium.ComplexityTooltip);
    }

    private static GameSummaryDto CreateDto(
        double? bggWeight,
        GameStyle style = GameStyle.Eurogame,
        int perPlayerMinutes = 30,
        int communityAge = 10)
    {
        return new GameSummaryDto(
            Id: Guid.NewGuid(),
            BggId: 101,
            Slug: "test-game",
            SpanishTitle: "Juego Test",
            OriginalTitle: "Test Game",
            Designer: "Autor",
            Publisher: "Editorial",
            YearPublished: 2024,
            CoverImageUrl: null,
            ThumbnailUrl: null,
            BggRating: 8.0,
            BggRank: 50,
            LudistRating: 8.0,
            IdealPlayerCountText: "2-4",
            Confrontation: ConfrontationType.Competitive,
            Style: style,
            IsOfficialSolo: false,
            IsAccessibleEarlier: false,
            CommunityAge: communityAge,
            BoxAge: communityAge,
            Language: LanguageDependence.None,
            Footprint: TableFootprint.StandardTable,
            EstimatedPerPlayerMinutes: perPlayerMinutes,
            BggWeight: bggWeight
        );
    }
}
