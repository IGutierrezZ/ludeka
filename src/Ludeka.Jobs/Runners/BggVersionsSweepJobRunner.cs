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
                _logger.LogInformation("Iniciando barrido local de versiones BGG para títulos en español y códigos EAN...");
                await heartbeat.BeatAsync(workCt);

                int totalEvaluated = 0;
                int totalTitles = 0;
                int totalEans = 0;
                int lastBggId = 0;
                bool hasMore = true;

                while (hasMore)
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
                    "Barrido de versiones BGG finalizado: {Total} evaluados, {Titles} títulos ES actualizados, {Eans} EANs asignados.",
                    totalEvaluated, totalTitles, totalEans);

                string summary = $"Barrido completado: {totalEvaluated} evaluados, {totalTitles} títulos ES actualizados, {totalEans} EANs asignados.";
                return new JobWorkResult(totalEvaluated, 0, summary);
            },
            ct);
    }
}
