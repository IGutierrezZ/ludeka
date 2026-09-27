using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Orquestador para la ingesta masiva de catálogo BGG, obtención de galería comunitaria, síntesis IA en lote y promoción a Games.
/// </summary>
public interface IBggMassIngestionService
{
    /// <summary>
    /// Procesa un flujo de volcado CSV de ranks de BGG e inserta en staging los títulos que cumplan el umbral de votos (por defecto >= 1000).
    /// </summary>
    Task<int> IngestRanksDumpAsync(Stream dumpStream, int minUsersRated = 1000, CancellationToken ct = default);

    /// <summary>
    /// Procesa un lote de registros pendientes en staging para consultar /xmlapi2/thing en BGG (hasta 20 IDs).
    /// </summary>
    Task<int> ProcessPendingDetailsBatchAsync(int batchSize = 20, CancellationToken ct = default);

    /// <summary>
    /// Procesa un lote de registros en staging para buscar fotos en GeekDo y subirlas como WebP a Cloudflare R2.
    /// </summary>
    Task<int> ProcessPendingImagesBatchAsync(int batchSize = 10, CancellationToken ct = default);

    /// <summary>
    /// Procesa un lote de registros en staging para generar su síntesis editorial con Gemini Flash agrupados en prompts.
    /// </summary>
    Task<AiBatchProcessingResultDto> ProcessPendingAiBatchAsync(int gamesPerBatch = 8, int maxBatches = 5, CancellationToken ct = default);

    /// <summary>
    /// Promueve a la tabla principal Games los registros de staging que se encuentren completamente listos.
    /// </summary>
    Task<int> PromoteReadyToCatalogBatchAsync(int batchSize = 50, CancellationToken ct = default);

    /// <summary>
    /// Obtiene las métricas de avance y conteo por estados de la tabla de staging.
    /// </summary>
    Task<BggStagingMetricsDto> GetMetricsAsync(CancellationToken ct = default);

    /// <summary>
    /// Ejecuta un ciclo de drenaje progresivo que avanza cada fase según las capacidades configuradas.
    /// Exige sesión y permiso de edición de fichas (INC-46, W1).
    /// </summary>
    Task<BggMassIngestionCycleResultDto> RunDrainCycleAsync(CancellationToken ct = default);

    /// <summary>
    /// Ciclo de sistema del proceso nocturno (INC-46, W1): misma lógica sin la guarda de la interfaz.
    /// </summary>
    Task<BggMassIngestionCycleResultDto> RunScheduledDrainCycleAsync(CancellationToken ct = default);

    /// <summary>
    /// Descarga y procesa de forma autónoma el volcado más reciente de clasificación de BGG en staging.
    /// Exige sesión y permiso de edición de fichas (INC-46, W1).
    /// </summary>
    Task<int> DownloadAndIngestLatestRanksAsync(int? minUsersRated = null, CancellationToken ct = default);

    /// <summary>
    /// Versión de sistema para ejecutor en segundo plano (Ludeka.Jobs) sin la guarda de sesión interactiva.
    /// </summary>
    Task<int> RunScheduledDownloadAndIngestLatestRanksAsync(int? minUsersRated = null, CancellationToken ct = default);

    /// <summary>
    /// Vacía por completo la tabla intermedia de staging de catálogo BGG.
    /// Exige sesión y permiso de edición de fichas (INC-46, W1).
    /// </summary>
    Task ClearStagingAsync(CancellationToken ct = default);

    /// <summary>
    /// Restablece el estado de los juegos pausados por cuota de IA a pendiente para permitir su reintento.
    /// Exige sesión y permiso de edición de fichas (INC-46, W1).
    /// </summary>
    Task<int> ResetQuotaExceededStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Ejecuta el drenaje continuo de staging iterando ciclos progresivos en segundo plano hasta completar o alcanzar el límite.
    /// Exige sesión y permiso de edición de fichas (INC-46, W1).
    /// </summary>
    Task<BggMassIngestionContinuousDrainResultDto> RunContinuousDrainAsync(int maxItems = 4000, CancellationToken ct = default);

    /// <summary>
    /// Versión de sistema del drenaje continuo para ejecutores en segundo plano (Ludeka.Jobs).
    /// </summary>
    Task<BggMassIngestionContinuousDrainResultDto> RunScheduledContinuousDrainAsync(int maxItems = 4000, CancellationToken ct = default);

    /// <summary>
    /// Ejecuta el enriquecimiento retroactivo de calidad (escalabilidad, fundas, duraciones, huella y localización) para juegos existentes.
    /// Exige sesión y permiso de edición de fichas.
    /// </summary>
    Task<BggQualityBackfillResultDto> BackfillCatalogQualityBatchAsync(int batchSize = 50, CancellationToken ct = default)
        => Task.FromResult(new BggQualityBackfillResultDto(0, 0, 0, "Noop"));

    /// <summary>
    /// Versión de sistema para enriquecimiento retroactivo de calidad por lotes sin guarda interactiva (Ludeka.Jobs).
    /// </summary>
    Task<BggQualityBackfillResultDto> RunScheduledBackfillCatalogQualityBatchAsync(int batchSize = 50, CancellationToken ct = default)
        => Task.FromResult(new BggQualityBackfillResultDto(0, 0, 0, "Noop"));

    /// <summary>
    /// Obtiene el total de títulos existentes en catálogo que aún no han recibido enriquecimiento de calidad (escalabilidad vacía).
    /// </summary>
    Task<int> GetPendingQualityBackfillCountAsync(CancellationToken ct = default)
        => Task.FromResult(0);
}

/// <summary>
/// Resumen de los resultados de un ciclo de drenaje de staging.
/// </summary>
public record BggMassIngestionCycleResultDto(
    int DetailsFetchedCount,
    int ImagesProcessedCount,
    int AiProcessedCount,
    int PromotedToCatalogCount,
    bool AiQuotaExhausted,
    string? Message
);
