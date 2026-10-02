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
    int FailedCount,
    int UnpromotedFailedCount = 0
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
    /// Umbral mínimo de valoraciones para admitir un título en staging (por defecto 100, catálogo amplio de calidad).
    /// </summary>
    public int MinUsersRated { get; set; } = 100;

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

    /// <summary>
    /// Retardo de cortesía en milisegundos entre llamadas individuales a BGG Thing XMLAPI2 (por defecto 1000 ms = 1 segundo).
    /// </summary>
    public int DelayBetweenBggCallsMs { get; set; } = 1000;

    /// <summary>
    /// Retardo en milisegundos entre llamadas consecutivas en lote a Gemini Flash para respetar el límite de 15 RPM del Free Tier (por defecto 4000 ms = 4 segundos).
    /// </summary>
    public int DelayBetweenGeminiBatchesMs { get; set; } = 4000;

    /// <summary>
    /// Plantilla URL para descargar el volcado CSV de ranks de BGG. Formato string con marcador {0:yyyy-MM-dd}.
    /// Por defecto apunta al mirror diario público en GitHub Raw / Fastly CDN.
    /// </summary>
    public string RanksDumpUrlPattern { get; set; } =
        "https://raw.githubusercontent.com/beefsack/bgg-ranking-historicals/master/{0:yyyy-MM-dd}.csv";

    /// <summary>
    /// Número máximo de días a retroceder en caso de que la fecha actual no esté disponible (HTTP 404).
    /// </summary>
    public int MaxFallbackDays { get; set; } = 5;
}

/// <summary>
/// Resultado acumulado de la ejecución de drenaje masivo continuo de staging en segundo plano.
/// </summary>
public record BggMassIngestionContinuousDrainResultDto(
    int CyclesExecuted,
    int TotalDetailsFetched,
    int TotalImagesProcessed,
    int TotalAiSummariesGenerated,
    int TotalPromotedToCatalog,
    bool StoppedDueToAiQuota,
    bool CompletedAllStaging,
    string Message
);

/// <summary>
/// Resultado del proceso de enriquecimiento retroactivo de calidad (escalabilidad, fundas, duraciones, huella y localización) para juegos ya existentes en el catálogo.
/// </summary>
public record BggQualityBackfillResultDto(
    int ProcessedCount,
    int UpdatedCount,
    int FailedCount,
    string Message
)
{
    public int EvaluatedCount => ProcessedCount;
}

/// <summary>
/// Resultado de un lote del barrido completo y determinista de calidad de catálogo (~10.000 juegos).
/// </summary>
public record BggQualitySweepBatchResultDto(
    int EvaluatedCount,
    int UpdatedCount,
    int SkippedCount,
    int FailedCount,
    int LastBggIdProcessed,
    bool HasMore,
    string Message
);
