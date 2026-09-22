using System;
using System.Collections.Generic;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Application.DTOs;

public record ExpansionSummaryDto(
    Guid Id,
    int BggId,
    string Slug,
    string SpanishTitle,
    string OriginalTitle,
    int YearPublished,
    string? CoverImageUrl,
    double LudistRating,
    double BggRating,
    ExpansionNecessity? Necessity,
    string NecessityBadgeText,
    IReadOnlyList<ExpansionImpactTag> ImpactTags,
    IReadOnlyList<string> ImpactTagLabels,
    string? WhatItBringsSummary,
    int? ExtraPlayerCount,
    int? ExtraDurationMinutes
)
{
    public static ExpansionSummaryDto FromEntity(Game g)
    {
        string necessityText = g.ExpansionNecessity switch
        {
            ExpansionNecessity.MustHave => "🟢 Imprescindible, mejora el juego base",
            ExpansionNecessity.HighlyRecommended => "🟢 Muy recomendada",
            ExpansionNecessity.Situational => "🟡 Recomendada según perfil",
            ExpansionNecessity.OnlyForFans => "🔵 Solo para muy cafeteros / completistas",
            ExpansionNecessity.Dispensable => "⚪ Prescindible / Aporta poco",
            _ => "Expansión Oficial"
        };

        var tagLabels = new List<string>();
        foreach (var tag in g.ImpactTags ?? [])
        {
            string label = tag switch
            {
                ExpansionImpactTag.AddsPlayers => g.ExtraPlayerCount.HasValue && g.ExtraPlayerCount > 0 ? $"+{g.ExtraPlayerCount} Jugador(es)" : "Agrega más jugadores",
                ExpansionImpactTag.ImprovesTwoPlayers => "Imprescindible para 2 jugadores",
                ExpansionImpactTag.FixesBalance => "Corrige balance / ritmo",
                ExpansionImpactTag.AddsSoloMode => "Introduce modo solitario",
                ExpansionImpactTag.AddsAsymmetry => "Añade asimetría y facciones",
                ExpansionImpactTag.ModularContent => "Módulos combinables",
                ExpansionImpactTag.NewMapOrFactions => "Nuevos mapas / tableros",
                ExpansionImpactTag.TightensTime => g.ExtraDurationMinutes.HasValue && g.ExtraDurationMinutes < 0 ? $"Acelera la partida ({g.ExtraDurationMinutes} min)" : "Ajusta duración de partida",
                _ => tag.ToString()
            };
            tagLabels.Add(label);
        }

        return new ExpansionSummaryDto(
            g.Id,
            g.BggId,
            g.Slug,
            g.SpanishTitle,
            g.OriginalTitle,
            g.YearPublished,
            g.CoverImageUrl,
            g.LudistRating,
            g.BggRating,
            g.ExpansionNecessity,
            necessityText,
            (g.ImpactTags ?? new List<ExpansionImpactTag>()).AsReadOnly(),
            tagLabels.AsReadOnly(),
            g.WhatItBringsSummary,
            g.ExtraPlayerCount,
            g.ExtraDurationMinutes
        );
    }
}

public record ParentGameSummaryDto(
    Guid Id,
    int BggId,
    string Slug,
    string SpanishTitle,
    string OriginalTitle,
    string? CoverImageUrl,
    double LudistRating,
    double BggRating,
    int YearPublished,
    string Designer
);

public record ExpansionSynergyDto(
    Guid Id,
    Guid ExpansionAId,
    string ExpansionATitle,
    Guid ExpansionBId,
    string ExpansionBTitle,
    ExpansionSynergyLevel Level,
    string LevelTitle,
    string Reason
)
{
    public static ExpansionSynergyDto FromEntity(ExpansionSynergy s, string titleA, string titleB)
    {
        string levelTitle = s.Level switch
        {
            ExpansionSynergyLevel.PerfectCombo => "🟢 Sinergia Óptima / Combo Estrella",
            ExpansionSynergyLevel.CompatibleWithCaution => "🟡 Compatible con Reservas / Sobrecarga",
            ExpansionSynergyLevel.Incompatible => "🔴 Incompatible / Conflicto",
            _ => s.Level.ToString()
        };

        return new ExpansionSynergyDto(
            s.Id,
            s.ExpansionAId,
            titleA,
            s.ExpansionBId,
            titleB,
            s.Level,
            levelTitle,
            s.Reason
        );
    }
}

public record ExpansionRecipeDto(
    Guid Id,
    Guid BaseGameId,
    string Name,
    string Description,
    string IdealFor,
    IReadOnlyList<Guid> IncludedExpansionIds,
    IReadOnlyList<ExpansionSummaryDto> IncludedExpansions
);

public record ExpansionMixerEvaluationDto(
    string GlobalStatus,           // "Balanced", "Caution", "Conflict"
    string GlobalStatusBadge,      // "🟢 Mesa Equilibrada", "🟡 Mesa Exigente / Sobrecarga", "🔴 Conflicto de Componentes"
    string GlobalStatusMessage,
    int BaseMinPlayers,
    int BaseMaxPlayers,
    int ResultingMinPlayers,
    int ResultingMaxPlayers,
    int BaseEstimatedMinutes,
    int ResultingEstimatedMinutes,
    IReadOnlyList<ExpansionSynergyDto> PairwiseSynergies,
    IReadOnlyList<string> ConflictWarnings,
    IReadOnlyList<string> CautionWarnings,
    IReadOnlyList<string> SynergyHighlights
);
