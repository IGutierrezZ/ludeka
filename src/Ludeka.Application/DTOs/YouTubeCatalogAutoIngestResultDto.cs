using System;

namespace Ludeka.Application.DTOs;

/// <summary>
/// Resultado de la ejecución del lote desatendido de auto-ingesta de vídeos de YouTube para el catálogo.
/// </summary>
public record YouTubeCatalogAutoIngestResultDto(
    int GamesEvaluated,
    int VideosIngested,
    int SkippedCount,
    int ErrorsCount,
    TimeSpan Duration,
    DateTimeOffset ExecutedAt);
