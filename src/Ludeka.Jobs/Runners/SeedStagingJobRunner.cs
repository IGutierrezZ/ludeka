using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Jobs;
using Microsoft.Extensions.Options;

namespace Ludeka.Jobs.Runners;

/// <summary>
/// Trabajo autónomo de sembrado inicial de catálogo BGG en staging (INC-53).
/// Descarga en streaming continuo el volcado de ranks y lo puebla en BggCatalogStaging.
/// </summary>
public sealed class SeedStagingJobRunner : IJobRunner
{
    private readonly IJobExecutionCoordinator _coordinator;
    private readonly IBggMassIngestionService _service;
    private readonly IOptions<BggMassIngestionOptions> _options;

    public SeedStagingJobRunner(
        IJobExecutionCoordinator coordinator,
        IBggMassIngestionService service,
        IOptions<BggMassIngestionOptions> options)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string Name => JobNames.SeedStaging;

    public Task<JobLeaseOutcome> RunAsync(CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var windowKey = JobWindowKeyCalculator.DailyUtc(nowUtc);
        var minVotes = _options.Value.MinUsersRated;

        return _coordinator.ExecuteWithWindowLeaseAsync(
            Name,
            windowKey,
            async (_, workCt) =>
            {
                int count = await _service.RunScheduledDownloadAndIngestLatestRanksAsync(minVotes, workCt);
                return new JobWorkResult(count, 0, "Completed");
            },
            ct);
    }
}
