namespace Ludeka.Core.Enums;

/// <summary>
/// Criterios de ordenación disponibles para el catálogo de juegos.
/// </summary>
public enum GameSortOrder
{
    /// <summary>Ranking oficial de BGG (defecto editorial).</summary>
    Rank = 0,

    /// <summary>Mayor puntuación / valoración comunitaria.</summary>
    RatingDesc = 1,

    /// <summary>Menor dureza / peso cognitivo (más accesible primero).</summary>
    ComplexityAsc = 2,

    /// <summary>Mayor dureza / peso cognitivo (juegos más complejos primero).</summary>
    ComplexityDesc = 3,

    /// <summary>Menor duración estimada (partidas más rápidas primero).</summary>
    DurationAsc = 4,

    /// <summary>Mayor duración estimada (partidas más largas primero).</summary>
    DurationDesc = 5,

    /// <summary>Año de publicación más reciente (novedades primero).</summary>
    YearDesc = 6,

    /// <summary>Alfabético por título en español (A-Z).</summary>
    TitleAsc = 7
}
