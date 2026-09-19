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
/// Trabajo fino del radar de precios (INC-47, R6, diseño §8.1/§8.2). Calcula su ventana por
/// bloque de <see cref="PriceRadarOptions.CheckIntervalHours"/> horas (diseño §7.2) e invoca al
/// coordinador, que a su vez invoca <see cref="IPriceRadarService.ScanWantToBuyPricesAsync"/>.
/// </summary>
public sealed class PriceRadarJobRunner : IJobRunner
{
    private readonly IJobExecutionCoordinator _coordinator;
    private readonly IPriceRadarService _service;
    private readonly IOptions<PriceRadarOptions> _options;

    public PriceRadarJobRunner(
        IJobExecutionCoordinator coordinator,
        IPriceRadarService service,
        IOptions<PriceRadarOptions> options)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string Name => JobNames.PriceRadar;

    public Task<JobLeaseOutcome> RunAsync(CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var blockHours = Math.Max(1, _options.Value.CheckIntervalHours);
        var windowKey = JobWindowKeyCalculator.HourlyBlock(nowUtc, blockHours);
        var maxGames = _options.Value.MaxGamesPerScan;

        return _coordinator.ExecuteWithWindowLeaseAsync(
            Name,
            windowKey,
            async (_, workCt) =>
            {
                var scanned = await _service.ScanWantToBuyPricesAsync(maxGames, workCt);
                return new JobWorkResult(scanned, 0, null);
            },
            ct);
    }
}
