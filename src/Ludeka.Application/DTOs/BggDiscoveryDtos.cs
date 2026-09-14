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
