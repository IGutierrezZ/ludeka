using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.Features.Jobs;
using Ludeka.Application.Options;
using Microsoft.Extensions.Options;

namespace Ludeka.Jobs.Runners;

/// <summary>
/// Runner para el trabajo de mantenimiento de retención de datos y purga de sorteos, eventos y novedades caducados.
/// Se ejecuta de forma idempotente con concesión de ventana diaria (JobWindowKeyCalculator.DailyUtc).
/// </summary>
public sealed class DataRetentionJobRunner : IJobRunner
{
    private readonly IJobExecutionCoordinator _coordinator;
    private readonly IDataRetentionService _retentionService;
    private readonly IOptions<DataRetentionOptions> _options;

    public DataRetentionJobRunner(
        IJobExecutionCoordinator coordinator,
        IDataRetentionService retentionService,
        IOptions<DataRetentionOptions> options)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _retentionService = retentionService ?? throw new ArgumentNullException(nameof(retentionService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string Name => JobNames.DataRetention;

    public Task<JobLeaseOutcome> RunAsync(CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var windowKey = JobWindowKeyCalculator.DailyUtc(nowUtc);

        return _coordinator.ExecuteWithWindowLeaseAsync(
            Name,
            windowKey,
            async (_, workCt) =>
            {
                var result = await _retentionService.PurgeExpiredDataAsync(workCt);
                var summary = $"Purgadas {result.TotalPurgedEntities} entidades ({result.PurgedGiveawaysCount} sorteos, {result.PurgedEventsCount} eventos, {result.PurgedReleasesCount} novedades). {result.DeletedImagesCount} imágenes liberadas.";
                return new JobWorkResult(result.TotalPurgedEntities, result.FailedImagesCount, summary);
            },
            ct);
    }
}
