using System.Collections.Generic;

namespace Ludeka.Application.DTOs;

/// <summary>
/// Resultado del proceso de auto-descubrimiento y encolado de novedades o tendencias desde BoardGameGeek.
/// </summary>
public record BggDiscoveryResultDto(
    int TotalScanned,
    int DiscoveredCount,
    int EnqueuedCount,
    int AlreadyCatalogedCount,
    int AlreadyInQueueCount,
    IReadOnlyList<string> EnqueuedTitles
);

/// <summary>
/// DTO representativo de un juego en la foto diaria de tendencias de BGG.
/// </summary>
public record DailyTrendingGameDto(
    System.Guid Id,
    System.DateOnly DateUtc,
    int Rank,
    int BggId,
    string Title,
    int? YearPublished,
    string? ThumbnailUrl,
    System.Guid? GameId,
    string? Slug
);

/// <summary>
/// Resultado del proceso diario de sincronización de tendencias (Hotness) e ingesta inmediata de ausentes.
/// </summary>
public record BggTrendingSyncResultDto(
    System.DateOnly DateUtc,
    int TotalTrendingProcessed,
    int AlreadyCatalogedCount,
    int NewlyCatalogedCount,
    int FailedCount,
    IReadOnlyList<string> NewlyCatalogedTitles
);
