using System;
using System.Collections.Generic;
using System.Linq;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Infrastructure.Services;

/// <summary>
/// Generador heurístico determinista para aportes lúdicos de expansiones.
/// Proporciona veredictos y etiquetas estructuradas de forma resiliente (Zero-Crash Fallback).
/// </summary>
public static class HeuristicExpansionAporteGenerator
{
    public static ExpansionAporteAiDto Generate(Game expansion, Game? baseGame = null, string modelName = "Heurística Editorial")
    {
        ArgumentNullException.ThrowIfNull(expansion);

        string desc = (expansion.Description ?? string.Empty).ToLowerInvariant();
        string title = (expansion.SpanishTitle ?? expansion.OriginalTitle).ToLowerInvariant();
        string baseTitle = baseGame != null ? baseGame.SpanishTitle : "el juego base";

        // 1. Detección de impacto en comensales (ExtraPlayerCount)
        int? extraPlayerCount = null;
        if (baseGame != null)
        {
            int baseMax = baseGame.Scalability.Count > 0 ? baseGame.Scalability.Max(s => s.PlayerCount) : 4;
            int expMax = expansion.Scalability.Count > 0 ? expansion.Scalability.Max(s => s.PlayerCount) : 0;
            if (expMax > baseMax)
            {
                extraPlayerCount = expMax - baseMax;
            }
        }

        if (!extraPlayerCount.HasValue)
        {
            if (desc.Contains("5-6") || desc.Contains("5 y 6") || desc.Contains("6 jugadores") || desc.Contains("quinto jugador"))
            {
                extraPlayerCount = desc.Contains("6") ? 2 : 1;
            }
        }

        // 2. Detección de impacto en duración (ExtraDurationMinutes)
        int? extraDurationMinutes = null;
        if (desc.Contains("acelera") || desc.Contains("arranque rápido") || desc.Contains("prelude") || desc.Contains("preludio"))
        {
            extraDurationMinutes = -15;
        }
        else if (extraPlayerCount.HasValue && extraPlayerCount.Value > 0)
        {
            extraDurationMinutes = extraPlayerCount.Value * 15;
        }
        else if (desc.Contains("tablero") || desc.Contains("nuevo mapa") || desc.Contains("módulos adicionales"))
        {
            extraDurationMinutes = 15;
        }

        // 3. Etiquetas de impacto lúdico (ImpactTags)
        var tags = new HashSet<ExpansionImpactTag>();

        if (extraPlayerCount.HasValue && extraPlayerCount.Value > 0)
        {
            tags.Add(ExpansionImpactTag.AddsPlayers);
        }

        if (expansion.IsOfficialSolo || desc.Contains("solitario") || desc.Contains("solo mode") || desc.Contains("automa") || desc.Contains("bot"))
        {
            tags.Add(ExpansionImpactTag.AddsSoloMode);
        }

        if (desc.Contains("asimetr") || desc.Contains("facciones") || desc.Contains("factions") || desc.Contains("habilidades únicas") || desc.Contains("roles"))
        {
            tags.Add(ExpansionImpactTag.AddsAsymmetry);
        }

        if (desc.Contains("módulo") || desc.Contains("modul") || desc.Contains("variantes") || desc.Contains("mini-expansion"))
        {
            tags.Add(ExpansionImpactTag.ModularContent);
        }

        if (desc.Contains("tablero") || desc.Contains("nuevo mapa") || desc.Contains("nuevos mapas") || desc.Contains("board") || desc.Contains("map"))
        {
            tags.Add(ExpansionImpactTag.NewMapOrFactions);
        }

        if (desc.Contains("balance") || desc.Contains("rebalance") || desc.Contains("equilibra") || desc.Contains("corrige"))
        {
            tags.Add(ExpansionImpactTag.FixesBalance);
        }

        if (extraDurationMinutes.HasValue && extraDurationMinutes.Value < 0)
        {
            tags.Add(ExpansionImpactTag.TightensTime);
        }

        if (desc.Contains("2 jugadores") || desc.Contains("duelo") || desc.Contains("para dos") || desc.Contains("parejas"))
        {
            tags.Add(ExpansionImpactTag.ImprovesTwoPlayers);
        }

        if (tags.Count == 0)
        {
            tags.Add(ExpansionImpactTag.ModularContent);
        }

        // 4. Estimación de necesidad editorial (ExpansionNecessity)
        ExpansionNecessity necessity;
        if (desc.Contains("imprescindible") || desc.Contains("must-have") || desc.Contains("esencial") || expansion.BggRating >= 8.2)
        {
            necessity = ExpansionNecessity.MustHave;
        }
        else if (expansion.BggRating >= 7.8 || tags.Contains(ExpansionImpactTag.FixesBalance) || tags.Contains(ExpansionImpactTag.AddsAsymmetry))
        {
            necessity = ExpansionNecessity.HighlyRecommended;
        }
        else if (expansion.BggRating >= 7.2 || tags.Contains(ExpansionImpactTag.AddsPlayers))
        {
            necessity = ExpansionNecessity.Situational;
        }
        else if (expansion.BggRating >= 6.5)
        {
            necessity = ExpansionNecessity.OnlyForFans;
        }
        else
        {
            necessity = ExpansionNecessity.Dispensable;
        }

        // 5. Redacción de síntesis estructurada del aporte
        string tagNarrative = tags.Count switch
        {
            1 => "introduce nuevo contenido modular específico",
            _ => "aporta mecánicas ampliadas, mayor variabilidad y opciones tácticas renovadas"
        };

        string cleanDescSnippet = string.Empty;
        if (!string.IsNullOrWhiteSpace(expansion.Description))
        {
            var sentences = expansion.Description.Split(['.', '\n'], StringSplitOptions.RemoveEmptyEntries);
            if (sentences.Length > 0)
            {
                cleanDescSnippet = sentences[0].Trim();
                if (!cleanDescSnippet.EndsWith('.')) cleanDescSnippet += ".";
            }
        }

        string summary = $"Esta expansión oficial enriquece a {baseTitle}: {tagNarrative}. " +
            (string.IsNullOrWhiteSpace(cleanDescSnippet)
                ? $"Aumenta la rejugabilidad general sin desvirtuar la esencia central del juego base."
                : $"{cleanDescSnippet} Diseñada para grupos que deseen explorar nuevas dimensiones estratégicas en mesa.");

        return new ExpansionAporteAiDto(
            ExpansionId: expansion.Id,
            WhatItBringsSummary: summary.Trim(),
            Necessity: necessity,
            ImpactTags: tags.ToList(),
            ExtraPlayerCount: extraPlayerCount,
            ExtraDurationMinutes: extraDurationMinutes,
            Model: modelName,
            GeneratedAt: DateTime.UtcNow
        );
    }
}
