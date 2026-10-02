using System;
using System.Collections.Generic;

namespace Ludeka.Application.DTOs;

/// <summary>
/// Enlace de expansión extraído de BGG XMLAPI2 (inbound hacia juego base u outbound hacia expansiones).
/// </summary>
public record BggExpansionLinkDto(
    int BggId,
    string Title,
    bool IsInbound
);

/// <summary>
/// Métricas y estado global de la tabla satélite de snapshots de BGG y su relación con el catálogo.
/// </summary>
public record BggRawSnapshotStatusDto(
    int TotalGamesWithBggId,
    int TotalSnapshots,
    int PendingSnapshots,
    int TotalExpansions,
    int LinkedExpansions,
    int UnlinkedExpansions
);

/// <summary>
/// Resultado tras procesar un lote de sincronización de snapshots de BGG.
/// </summary>
public record BggRawSnapshotSyncResultDto(
    int ProcessedCount,
    int SuccessCount,
    int FailedCount,
    IReadOnlyList<string> SyncedTitles,
    IReadOnlyList<string> LinkedExpansions,
    string? Message = null
);

/// <summary>
/// Resultado del descubrimiento y encolado de expansiones no catalogadas desde los snapshots BGG.
/// </summary>
public record BggExpansionDiscoveryResultDto(
    int DiscoveredCount,
    int EnqueuedCount,
    IReadOnlyList<string> EnqueuedTitles
);

/// <summary>
/// Resultado tras reconciliar y vincular masivamente expansiones desde los snapshots satélite de BGG.
/// </summary>
public record BggExpansionReconciliationResultDto(
    int TotalEvaluated,
    int ReclassifiedExpansionsCount,
    int LinkedExpansionsCount,
    IReadOnlyList<string> ReclassifiedTitles,
    IReadOnlyList<string> LinkedExpansions,
    string? Message = null
);

