using System;
using System.Globalization;
using System.Text.Json;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Core.Helpers;

/// <summary>
/// Motor canónico de cálculo y extrapolación de complejidad/dureza para juegos de mesa (INC-131).
/// Si existe un peso BGG continuo (1.0 - 5.0), se aplican umbrales comunitarios contrastados.
/// En caso contrario, se aplica la heurística de respaldo por duración, edad y estilo.
/// </summary>
public static class ComplexityCalculator
{
    public const double LightThreshold = 2.20;
    public const double HeavyThreshold = 3.25;

    public static GameComplexity Calculate(double? bggWeight, GameStyle style, int maxMinutes, int communityAge)
    {
        if (bggWeight.HasValue && bggWeight.Value > 0)
        {
            if (bggWeight.Value < LightThreshold)
                return GameComplexity.Light;
            if (bggWeight.Value >= HeavyThreshold)
                return GameComplexity.Heavy;
            return GameComplexity.Medium;
        }

        // Heurística de respaldo (fallback cuando no hay votos de peso en BGG)
        if (style == GameStyle.PartyGame || style == GameStyle.FillerAbstract || (maxMinutes <= 30 && communityAge <= 10))
            return GameComplexity.Light;
        if (maxMinutes >= 120 || communityAge >= 14 || (maxMinutes >= 90 && style == GameStyle.Eurogame))
            return GameComplexity.Heavy;
        return GameComplexity.Medium;
    }

    public static GameComplexity Calculate(Game? game)
    {
        if (game == null) return GameComplexity.Medium;
        return Calculate(game.BggWeight, game.Style, game.Duration?.MaxMinutes ?? 0, game.Age?.CommunityAge ?? 0);
    }

    /// <summary>
    /// Extrae de forma defensiva el valor averageweight desde el JSON de un BggRawSnapshot (INC-130).
    /// </summary>
    public static double? ExtractWeightFromJson(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return null;

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("statistics", out var stats) &&
                stats.TryGetProperty("ratings", out var ratings) &&
                ratings.TryGetProperty("averageweight", out var avgWeight))
            {
                string? valStr = null;
                if (avgWeight.ValueKind == JsonValueKind.Object)
                {
                    if (avgWeight.TryGetProperty("@value", out var valProp))
                    {
                        valStr = valProp.GetString();
                    }
                    else if (avgWeight.TryGetProperty("#text", out var textProp))
                    {
                        valStr = textProp.GetString();
                    }
                }
                else if (avgWeight.ValueKind == JsonValueKind.String)
                {
                    valStr = avgWeight.GetString();
                }
                else if (avgWeight.ValueKind == JsonValueKind.Number && avgWeight.TryGetDouble(out double numVal))
                {
                    if (numVal > 0)
                        return Math.Round(Math.Clamp(numVal, 1.0, 5.0), 2);
                }

                if (!string.IsNullOrWhiteSpace(valStr) &&
                    double.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double wt) &&
                    wt > 0)
                {
                    return Math.Round(Math.Clamp(wt, 1.0, 5.0), 2);
                }
            }
        }
        catch
        {
            // Retorno defensivo null ante JSON corrupto o incompleto
        }

        return null;
    }
}

