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
            async (_, workCt) =>
            {
                int totalUpdated = 0;
                int totalFailed = 0;

                while (!workCt.IsCancellationRequested)
                {
                    var result = await _service.RunScheduledBackfillCatalogQualityBatchAsync(50, workCt);
                    totalUpdated += result.UpdatedCount;
                    totalFailed += result.FailedCount;

                    // Si no se evaluó ningún título o no hubo progreso ni errores, finalizar
                    if (result.EvaluatedCount == 0 || (result.UpdatedCount == 0 && result.FailedCount == 0))
                    {
                        break;
                    }
                }

                return new JobWorkResult(totalUpdated, totalFailed, "Completed");
            },
            ct);
    }
}
