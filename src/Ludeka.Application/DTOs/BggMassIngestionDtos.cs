using System;
using System.Collections.Generic;

namespace Ludeka.Application.DTOs;

/// <summary>
/// Representa una fila leída y parseada del volcado de ranks de BGG (bg_ranks).
/// </summary>
public record BggRanksDumpRowDto(
    int BggId,
    string Title,
    int? YearPublished,
    int? BggRank,
    double BayesAverage,
    double AverageRating,
    int UsersRated
);

/// <summary>
/// Métricas agregadas del estado de la tabla de staging de catálogo BGG.
/// </summary>
public record BggStagingMetricsDto(
    int TotalInStaging,
    int PendingFetchCount,
    int FetchedCount,
    int PendingImagesCount,
    int ImagesCompletedCount,
    int PendingAiCount,
    int AiCompletedCount,
    int AiQuotaExceededCount,
    int PendingPromotionCount,
    int PromotedCount,
    int FailedCount
);

/// <summary>
/// URLs de las 3 imágenes más votadas comunitariamente en GeekDo.
/// </summary>
public record GeekDoGalleryImagesDto(
    string? FrontCoverUrl,
    string? BackCoverUrl,
    string? TableOrGameplayUrl
);

/// <summary>
/// Entrada estructurada para un juego en una solicitud de síntesis con IA en lote (5-10 juegos por prompt).
/// </summary>
public record AiGameBatchInputDto(
    int BggId,
    string SpanishTitle,
    string OriginalTitle,
    string? Designer,
    string? Publisher,
    int YearPublished,
    string? Description,
    double Rating,
    int MinPlayers,
    int MaxPlayers,
    int MinAge
);

/// <summary>
/// Resultado de la llamada en lote a Gemini Flash para múltiples juegos.
/// </summary>
public record AiBatchResultDto(
    bool Success,
    bool QuotaExhausted,
    IReadOnlyDictionary<int, AiGameSummaryDto> Summaries,
    string? ErrorMessage
);

/// <summary>
/// Opciones de configuración para la ingesta masiva de catálogo y control de lotes.
/// </summary>
public class BggMassIngestionOptions
{
    public const string SectionName = "BggMassIngestion";

    /// <summary>
    /// Umbral mínimo de valoraciones para admitir un título en staging (por defecto 30).
    /// </summary>
    public int MinUsersRated { get; set; } = 30;

    /// <summary>
    /// Tamaño de lote para consultar detalles /xmlapi2/thing de BGG (máximo 20 recomendado por BGG).
    /// </summary>
    public int FetchBatchSize { get; set; } = 20;

    /// <summary>
    /// Tamaño de lote para procesar y subir imágenes GeekDo hacia Cloudflare R2.
    /// </summary>
    public int ImagesBatchSize { get; set; } = 10;

    /// <summary>
    /// Cantidad de juegos enviados en cada prompt conjunto a Gemini Flash (5 a 10).
    /// </summary>
    public int AiBatchSize { get; set; } = 8;

    /// <summary>
    /// Tamaño de lote para promover registros completos de staging a la tabla principal Games.
    /// </summary>
    public int PromotionBatchSize { get; set; } = 50;

    /// <summary>
    /// Activa el modo simulado de volcado y GeekDo para desarrollo local y tests.
    /// </summary>
    public bool Simulate { get; set; } = false;
}
