using System;
using System.Collections.Generic;

namespace Ludeka.Application.DTOs;

/// <summary>
/// Representa un juego del Top o lista más destacada (Hotness) de BoardGameGeek.
/// </summary>
public record BggTopGameDto(
    int BggId,
    string Title,
    int? BggRank,
    int? YearPublished,
    string? ThumbnailUrl
);

/// <summary>
/// Opciones de configuración para el orquestador nocturno de catalogación inteligente.
/// </summary>
public record NightlyCatalogingOptions
{
    /// <summary>
    /// Cupo máximo diario de juegos a catalogar en la ejecución nocturna (por defecto 20).
    /// </summary>
    public int DailyCatalogingLimit { get; set; } = 20;

    /// <summary>
    /// Pausa mínima en segundos entre llamadas externas consecutivas a BGG o Gemini (por defecto 2.5s).
    /// </summary>
    public double MinDelaySecondsBetweenCalls { get; set; } = 2.5;

    /// <summary>
    /// Hora UTC en la que se dispara automáticamente el batch nocturno (por defecto 3 = 03:00 UTC).
    /// </summary>
    public int ExecutionHourUtc { get; set; } = 3;

    /// <summary>
    /// Habilita o deshabilita la ejecución automática en segundo plano.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Resultado tras procesar una ejecución de catalogación nocturna o bajo demanda.
/// </summary>
public record NightlyCatalogingResultDto(
    Guid LogId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    int QueueProcessedCount,
    int NewsDiscoveryCount,
    int TopBackfillCount,
    int TotalCatalogedCount,
    int FailedCount,
    IReadOnlyList<string> CatalogedTitles,
    string Status,
    string? ErrorMessage,
    int BggDiscoveryCount = 0
);

/// <summary>
/// DTO para la visualización en la bitácora de administración de una ejecución nocturna.
/// </summary>
public record NightlyCatalogingExecutionLogDto(
    Guid Id,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int QueueProcessedCount,
    int NewsDiscoveryCount,
    int TopBackfillCount,
    int TotalCatalogedCount,
    int FailedCount,
    IReadOnlyList<string> CatalogedTitles,
    string Status,
    string? ErrorMessage,
    int BggDiscoveryCount = 0
);

/// <summary>
/// Resultado del análisis sintáctico y extracción de juego sobre una novedad editorial.
/// </summary>
public record NewsExtractionResultDto(
    Guid ReleaseId,
    string? ExtractedTitle,
    bool LinkedToExistingGame,
    Guid? ExistingGameId,
    bool EnqueuedToBgg,
    int? EnqueuedBggId
);
