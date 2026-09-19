using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Jobs;
using Microsoft.Extensions.Options;

namespace Ludeka.Jobs.Runners;

/// <summary>
/// Trabajo fino del lote nocturno de catalogación (INC-47, R6, diseño §8.1/§8.2). Calcula su
/// <c>WindowKey</c> diaria (diseño §7.2) e invoca al coordinador de idempotencia (R5), que a su vez
/// invoca <see cref="INightlyCatalogingService.RunScheduledCatalogingAsync"/> — el mismo punto de
/// entrada de sistema que ya usaba <c>NightlyCatalogingHostedService</c>.
/// </summary>
public sealed class NightlyCatalogingJobRunner : IJobRunner
{
    private readonly IJobExecutionCoordinator _coordinator;
    private readonly INightlyCatalogingService _service;
    private readonly IOptions<NightlyCatalogingOptions> _options;

    public NightlyCatalogingJobRunner(
        IJobExecutionCoordinator coordinator,
        INightlyCatalogingService service,
        IOptions<NightlyCatalogingOptions> options)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string Name => JobNames.NightlyCataloging;

    public Task<JobLeaseOutcome> RunAsync(CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var windowKey = JobWindowKeyCalculator.DailyUtc(nowUtc);
        var limit = _options.Value.DailyCatalogingLimit;

        return _coordinator.ExecuteWithWindowLeaseAsync(
            Name,
            windowKey,
            async (_, workCt) =>
            {
                var result = await _service.RunScheduledCatalogingAsync(limit, workCt);
                return new JobWorkResult(result.TotalCatalogedCount, result.FailedCount, result.Status);
            },
            ct);
    }
}
