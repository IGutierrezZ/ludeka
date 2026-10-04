using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.Features.Jobs;
using Microsoft.Extensions.Logging;

namespace Ludeka.Jobs.Runners;

/// <summary>
/// Trabajo autónomo para el barrido local de versiones de BGG en el catálogo caliente.
/// Extrae títulos en español, editoriales y códigos de barras EAN-13 normalizados
/// desde los snapshots crudos almacenados, sin llamadas HTTP externas.
/// </summary>
public sealed class BggVersionsSweepJobRunner : IJobRunner
{
    private readonly IJobExecutionCoordinator _coordinator;
    private readonly IBggRawSnapshotSyncService _service;
    private readonly ILogger<BggVersionsSweepJobRunner> _logger;

    public BggVersionsSweepJobRunner(
        IJobExecutionCoordinator coordinator,
        IBggRawSnapshotSyncService service,
        ILogger<BggVersionsSweepJobRunner> logger)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => JobNames.BggVersionsSweep;

    public Task<JobLeaseOutcome> RunAsync(CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var windowKey = JobWindowKeyCalculator.PerSecond(nowUtc);

        return _coordinator.ExecuteWithWindowLeaseAsync(
            Name,
            windowKey,
            async (heartbeat, workCt) =>
            {
                _logger.LogInformation("Fase 1/2: Sincronizando versiones BGG pendientes (&versions=1)...");
                int totalVersionsSynced = 0;
                int totalVersionsFailed = 0;
                int batchNumber = 0;

                while (!workCt.IsCancellationRequested)
                {
                    batchNumber++;
                    await heartbeat.BeatAsync(workCt);

                    var syncResult = await _service.RunScheduledSyncVersionsBatchAsync(batchSize: 20, delayMs: 1200, workCt);
                    if (syncResult.ProcessedCount == 0)
                    {
                        _logger.LogInformation("No se detectan más snapshots pendientes de versiones BGG.");
                        break;
                    }

                    totalVersionsSynced += syncResult.SuccessCount;
                    totalVersionsFailed += syncResult.FailedCount;

                    _logger.LogInformation(
                        "[Versiones Lote #{Batch}] Éxito: {Success}, Fallos: {Failed}. Acumulado sincronizadas: {Total}.",
                        batchNumber, syncResult.SuccessCount, syncResult.FailedCount, totalVersionsSynced);

                    if (syncResult.SuccessCount == 0 && syncResult.FailedCount == 0)
                    {
                        break;
                    }
                }

                _logger.LogInformation("Fase 2/2: Iniciando barrido local de versiones BGG para títulos en español y códigos EAN...");
                await heartbeat.BeatAsync(workCt);

                int totalEvaluated = 0;
                int totalTitles = 0;
                int totalEans = 0;
                int lastBggId = 0;
                bool hasMore = true;

                while (hasMore && !workCt.IsCancellationRequested)
                {
                    await heartbeat.BeatAsync(workCt);
                    var result = await _service.RunScheduledSweepCatalogFromVersionsAsync(batchSize: 200, lastBggId: lastBggId, ct: workCt);
                    totalEvaluated += result.EvaluatedCount;
                    totalTitles += result.UpdatedTitlesCount;
                    totalEans += result.UpdatedEansCount;
                    lastBggId = result.LastBggIdProcessed;
                    hasMore = result.HasMore && result.EvaluatedCount > 0;
                }

                _logger.LogInformation(
                    "Sincronización y barrido de versiones BGG finalizado: {VersionsSynced} versiones sincronizadas, {Total} evaluados, {Titles} títulos ES actualizados, {Eans} EANs asignados.",
                    totalVersionsSynced, totalEvaluated, totalTitles, totalEans);

                string summary = $"Sincronización y barrido completados: {totalVersionsSynced} versiones sincronizadas ({totalVersionsFailed} errores), {totalEvaluated} evaluados, {totalTitles} títulos ES actualizados, {totalEans} EANs asignados.";
                return new JobWorkResult(totalEvaluated + totalVersionsSynced, totalVersionsFailed, summary);
            },
            ct);
    }
}
