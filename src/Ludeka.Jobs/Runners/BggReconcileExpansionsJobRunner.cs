using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.Features.Jobs;
using Microsoft.Extensions.Logging;

namespace Ludeka.Jobs.Runners;

/// <summary>
/// Trabajo autónomo de reconciliación y vinculación masiva de expansiones desde los snapshots crudos de BGG.
/// Recorre los snapshots almacenados en BggRawSnapshots de forma determinista y sin llamadas HTTP externas,
/// reclasificando títulos con GameType.Expansion y vinculando sus BaseGameId.
/// </summary>
public sealed class BggReconcileExpansionsJobRunner : IJobRunner
{
    private readonly IJobExecutionCoordinator _coordinator;
    private readonly IBggRawSnapshotSyncService _service;
    private readonly ILogger<BggReconcileExpansionsJobRunner> _logger;

    public BggReconcileExpansionsJobRunner(
        IJobExecutionCoordinator coordinator,
        IBggRawSnapshotSyncService service,
        ILogger<BggReconcileExpansionsJobRunner> logger)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => JobNames.BggReconcileExpansions;

    public Task<JobLeaseOutcome> RunAsync(CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var windowKey = JobWindowKeyCalculator.PerSecond(nowUtc);

        return _coordinator.ExecuteWithWindowLeaseAsync(
            Name,
            windowKey,
            async (heartbeat, workCt) =>
            {
                _logger.LogInformation("Iniciando reconciliación y vinculación masiva de expansiones desde snapshots satélite BGG...");
                await heartbeat.BeatAsync(workCt);

                var result = await _service.RunScheduledReconcileAndLinkExpansionsFromSnapshotsAsync(batchSize: 200, workCt);

                _logger.LogInformation(
                    "Reconciliación de expansiones finalizada: {Total} evaluados, {Reclassified} reclasificadas a Expansion, {Linked} vinculadas a su juego base.",
                    result.TotalEvaluated, result.ReclassifiedExpansionsCount, result.LinkedExpansionsCount);

                string summary = $"Reconciliación completada: {result.TotalEvaluated} evaluados, {result.ReclassifiedExpansionsCount} reclasificados, {result.LinkedExpansionsCount} vinculados.";
                return new JobWorkResult(result.TotalEvaluated, 0, summary);
            },
            ct);
    }
}
