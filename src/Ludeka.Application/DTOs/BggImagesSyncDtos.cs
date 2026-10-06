namespace Ludeka.Application.DTOs;

/// <summary>
/// Resultado de un ciclo de sincronización de medios comunitarios (portada, contraportada, mesa) para juegos rankeados en BGG.
/// </summary>
public record BggImagesSyncResultDto(
    int EvaluatedCount,
    int UpdatedCount,
    int SkippedCount,
    int FailedCount,
    int LastRankProcessed,
    bool HasMore,
    string Message
);
