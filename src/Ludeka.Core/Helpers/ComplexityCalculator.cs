using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Core.Helpers;

/// <summary>
/// Motor canónico de cálculo y extrapolación de complejidad/dureza para juegos de mesa (INC-130).
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
}
