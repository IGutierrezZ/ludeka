using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.Features.Jobs;
using Microsoft.Extensions.Logging;

namespace Ludeka.Jobs.Runners;

/// <summary>
/// Trabajo autónomo para la sincronización y enriquecimiento de medios de imágenes comunitarias
/// (portada, contraportada y fotografía en mesa) para el Top 3.000 de BoardGameGeek.
/// </summary>
public sealed class BggImagesTop3000JobRunner : IJobRunner
{
    private readonly IJobExecutionCoordinator _coordinator;
    private readonly IBggImagesSyncService _service;
    private readonly ILogger<BggImagesTop3000JobRunner> _logger;

    public BggImagesTop3000JobRunner(
        IJobExecutionCoordinator coordinator,
        IBggImagesSyncService service,
        ILogger<BggImagesTop3000JobRunner> logger)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => JobNames.BggImagesTop3000;

    public Task<JobLeaseOutcome> RunAsync(CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var windowKey = JobWindowKeyCalculator.PerSecond(nowUtc);

        return _coordinator.ExecuteWithWindowLeaseAsync(
            Name,
            windowKey,
            async (heartbeat, workCt) =>
            {
                _logger.LogInformation("Iniciando sincronización desatendida de medios comunitarios BGG Top 3.000...");
                int totalEvaluated = 0;
                int totalUpdated = 0;
                int totalSkipped = 0;
                int totalFailed = 0;
                int currentRank = 0;
                const int maxRank = 3000;
                const int batchSize = 25;
                bool hasMore = true;

                while (hasMore && currentRank < maxRank && !workCt.IsCancellationRequested)
                {
                    await heartbeat.BeatAsync(workCt);

                    var result = await _service.SyncTopRankedImagesBatchAsync(
                        afterRank: currentRank,
                        batchSize: batchSize,
                        maxRank: maxRank,
                        delayMs: 800,
                        ct: workCt);

                    totalEvaluated += result.EvaluatedCount;
                    totalUpdated += result.UpdatedCount;
                    totalSkipped += result.SkippedCount;
                    totalFailed += result.FailedCount;

                    if (result.LastRankProcessed > currentRank)
                    {
                        currentRank = result.LastRankProcessed;
                    }
                    else
                    {
                        // Si no avanzó el rango procesado, se ha alcanzado el límite de juegos disponibles
                        break;
                    }

                    hasMore = result.HasMore && currentRank < maxRank;

                    _logger.LogInformation(
                        "[Top 3.000 Imágenes] Rango #{Rank} alcanzado. Lote: {Updated} actualizados, {Skipped} omitidos, {Failed} fallos. Acumulado: {TotalUpdated} actualizados.",
                        currentRank, result.UpdatedCount, result.SkippedCount, result.FailedCount, totalUpdated);
                }

                string summary = $"Sincronización Top 3.000 finalizada hasta rango #{currentRank}: {totalUpdated} actualizados, {totalSkipped} omitidos, {totalFailed} fallos de {totalEvaluated} evaluados.";
                _logger.LogInformation(summary);

                return new JobWorkResult(totalUpdated, totalFailed, summary);
            },
            ct);
    }
}
