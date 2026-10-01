using System;
using System.Collections.Generic;
using Ludeka.Core.Enums;

namespace Ludeka.Application.DTOs;

/// <summary>
/// DTO con la información de un título en el Top 50 de tendencias y su movimiento relativo.
/// </summary>
public record TrendingGameItemDto(
    int Rank,
    int? PreviousRank,
    RankMovement Movement,
    int PositionsChanged,
    int BggId,
    string Title,
    int? YearPublished,
    string? ThumbnailUrl,
    Guid? GameId,
    string? Slug,
    string? SpanishTitle,
    double? BggRating,
    int? BggRank
);

/// <summary>
/// Fotografía comparativa completa de las tendencias del día respecto a la fecha anterior disponible.
/// </summary>
public record TrendingComparisonDto(
    DateOnly DateUtc,
    DateOnly? PreviousDateUtc,
    IReadOnlyList<TrendingGameItemDto> Items
);
