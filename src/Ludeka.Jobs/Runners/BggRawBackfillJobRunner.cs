using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.Features.Jobs;
using Microsoft.Extensions.Logging;

namespace Ludeka.Jobs.Runners;

/// <summary>
/// Trabajo autónomo de volcado continuo de snapshots crudos de BGG hacia la tabla satélite.
/// Itera lotes de 50 juegos respetando la limitación de tasa (~1.200 ms) hasta que todos los títulos
/// del catálogo dispongan de su correspondiente payload crudo almacenado.
/// Al finalizar el barrido, ejecuta auto-vinculación de expansiones y descubrimiento de expansiones satélite.
/// </summary>
public sealed class BggRawBackfillJobRunner : IJobRunner
{
    private readonly IJobExecutionCoordinator _coordinator;
    private readonly IBggRawSnapshotSyncService _service;
    private readonly ILogger<BggRawBackfillJobRunner> _logger;

    public BggRawBackfillJobRunner(
        IJobExecutionCoordinator coordinator,
        IBggRawSnapshotSyncService service,
        ILogger<BggRawBackfillJobRunner> logger)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => JobNames.BggRawBackfill;

    public Task<JobLeaseOutcome> RunAsync(CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var windowKey = JobWindowKeyCalculator.PerSecond(nowUtc);

        return _coordinator.ExecuteWithWindowLeaseAsync(
            Name,
            windowKey,
            async (heartbeat, workCt) =>
            {
                int totalProcessed = 0;
                int totalSuccess = 0;
                int totalFailed = 0;
                int batchNumber = 0;

                _logger.LogInformation("Iniciando volcado masivo continuo de snapshots crudos BGG hacia la tabla satélite...");

                while (!workCt.IsCancellationRequested)
                {
                    batchNumber++;
                    await heartbeat.BeatAsync(workCt);

                    var result = await _service.RunScheduledSyncBatchAsync(batchSize: 50, delayMs: 1200, workCt);

                    if (result.ProcessedCount == 0)
                    {
                        _logger.LogInformation("No se detectan más títulos pendientes de snapshot satélite.");
                        break;
                    }

                    totalProcessed += result.ProcessedCount;
                    totalSuccess += result.SuccessCount;
                    totalFailed += result.FailedCount;

                    _logger.LogInformation(
                        "[Lote #{Batch}] Procesados: {Success} éxito, {Failed} fallos. Total acumulado sincronizados: {TotalSuccess}.",
                        batchNumber, result.SuccessCount, result.FailedCount, totalSuccess);

                    if (result.SuccessCount == 0 && result.FailedCount == 0)
                    {
                        break;
                    }
                }

                int linkedExpansions = 0;
                int discoveredExpansions = 0;

                if (!workCt.IsCancellationRequested)
                {
                    try
                    {
                        _logger.LogInformation("Ejecutando auto-vinculación de expansiones huérfanas existentes en catálogo...");
                        linkedExpansions = await _service.RunScheduledAutoLinkExistingExpansionsAsync(workCt);
                        _logger.LogInformation("Expansiones auto-vinculadas: {Count}", linkedExpansions);

                        _logger.LogInformation("Ejecutando descubrimiento de expansiones no catalogadas...");
                        var discovery = await _service.RunScheduledDiscoverAndEnqueueMissingExpansionsAsync(maxToEnqueue: 200, workCt);
                        discoveredExpansions = discovery.EnqueuedCount;
                        _logger.LogInformation("Expansiones descubiertas y encoladas: {Count}", discoveredExpansions);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Fallo no bloqueante al finalizar auto-vinculación o descubrimiento de expansiones.");
                    }
                }

                string summary = $"Volcado de snapshots completado: {totalSuccess} sincronizados ({totalFailed} errores), {linkedExpansions} expansiones vinculadas, {discoveredExpansions} descubiertas.";
                _logger.LogInformation("{Summary}", summary);

                return new JobWorkResult(totalProcessed, totalFailed, summary);
            },
            ct);
    }
}
