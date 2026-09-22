using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Jobs;

namespace Ludeka.Jobs.Runners;

/// <summary>
/// Trabajo autónomo de drenaje masivo continuo de catálogo BGG en staging.
/// Drena progresivamente detalles BGG (1s delay), fotos GeekDo/R2, IA por lotes (15 RPM) y promoción a Games.
/// </summary>
public sealed class DrainStagingJobRunner : IJobRunner
{
    private readonly IJobExecutionCoordinator _coordinator;
    private readonly IBggMassIngestionService _service;

    public DrainStagingJobRunner(
        IJobExecutionCoordinator coordinator,
        IBggMassIngestionService service)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public string Name => JobNames.DrainStaging;

    public Task<JobLeaseOutcome> RunAsync(CancellationToken ct)
    {
        // Al ser un proceso de drenaje bajo demanda, usa granularidad de un segundo para permitir ejecuciones consecutivas
        var nowUtc = DateTimeOffset.UtcNow;
        var windowKey = JobWindowKeyCalculator.PerSecond(nowUtc);

        return _coordinator.ExecuteWithWindowLeaseAsync(
            Name,
            windowKey,
            async (_, workCt) =>
            {
                var result = await _service.RunScheduledContinuousDrainAsync(maxItems: 4000, workCt);
                string status = result.CompletedAllStaging ? "Completed" : (result.StoppedDueToAiQuota ? "QuotaPaused" : "Partial");
                return new JobWorkResult(result.TotalPromotedToCatalog, result.StoppedDueToAiQuota ? 1 : 0, status);
            },
            ct);
    }
}
