using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.Features.Jobs;

namespace Ludeka.Jobs.Runners;

/// <summary>
/// Trabajo autónomo de enriquecimiento masivo de calidad para títulos existentes en el catálogo.
/// Itera lotes de 50 juegos enriqueciendo escalabilidad, fundas, duraciones, huella y editoriales
/// mediante estrategia Staging-First y fallback a BGG XMLAPI2.
/// </summary>
public sealed class BackfillQualityJobRunner : IJobRunner
{
    private readonly IJobExecutionCoordinator _coordinator;
    private readonly IBggMassIngestionService _service;

    public BackfillQualityJobRunner(
        IJobExecutionCoordinator coordinator,
        IBggMassIngestionService service)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public string Name => JobNames.BackfillQuality;

    public Task<JobLeaseOutcome> RunAsync(CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var windowKey = JobWindowKeyCalculator.PerSecond(nowUtc);

        return _coordinator.ExecuteWithWindowLeaseAsync(
            Name,
            windowKey,
            async (heartbeat, workCt) =>
            {
                int totalEvaluated = 0;
                int totalUpdated = 0;
                int totalSkipped = 0;
                int totalFailed = 0;
                int currentAfterBggId = 0;

                while (!workCt.IsCancellationRequested)
                {
                    await heartbeat.BeatAsync(workCt);
                    var result = await _service.RunScheduledSweepCatalogQualityBatchAsync(currentAfterBggId, 50, workCt);
                    totalEvaluated += result.EvaluatedCount;
                    totalUpdated += result.UpdatedCount;
                    totalSkipped += result.SkippedCount;
                    totalFailed += result.FailedCount;
                    currentAfterBggId = result.LastBggIdProcessed;

                    if (!result.HasMore || result.EvaluatedCount == 0)
                    {
                        break;
                    }
                }

                string summary = $"Barrido de calidad completado: {totalEvaluated} evaluados ({totalUpdated} actualizados, {totalSkipped} ya correctos, {totalFailed} errores).";
                return new JobWorkResult(totalEvaluated, totalFailed, summary);
            },
            ct);
    }
}
