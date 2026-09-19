using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Jobs;
using Ludeka.Application.Options;
using Microsoft.Extensions.Options;

namespace Ludeka.Jobs.Runners;

/// <summary>
/// Trabajo fino del recolector de canales sociales (INC-47, R6, diseño §8.1/§8.2). Calcula su
/// ventana por bloque de <see cref="SocialCollectorOptions.IntervalMinutes"/> minutos (diseño §7.2)
/// e invoca al coordinador, que a su vez invoca
/// <see cref="ISocialCollectorService.RunScheduledCollectionAsync"/>.
/// </summary>
public sealed class SocialCollectorJobRunner : IJobRunner
{
    private readonly IJobExecutionCoordinator _coordinator;
    private readonly ISocialCollectorService _service;
    private readonly IOptions<SocialCollectorOptions> _options;

    public SocialCollectorJobRunner(
        IJobExecutionCoordinator coordinator,
        ISocialCollectorService service,
        IOptions<SocialCollectorOptions> options)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string Name => JobNames.SocialCollector;

    public Task<JobLeaseOutcome> RunAsync(CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var blockMinutes = Math.Max(5, _options.Value.IntervalMinutes);
        var windowKey = JobWindowKeyCalculator.MinuteBlock(nowUtc, blockMinutes);
        var maxItemsPerAccount = _options.Value.MaxItemsPerAccount;

        return _coordinator.ExecuteWithWindowLeaseAsync(
            Name,
            windowKey,
            async (_, workCt) =>
            {
                var result = await _service.RunScheduledCollectionAsync(maxItemsPerAccount, workCt);
                return new JobWorkResult(result.ItemsImported, result.ErrorsCount, null);
            },
            ct);
    }
}
