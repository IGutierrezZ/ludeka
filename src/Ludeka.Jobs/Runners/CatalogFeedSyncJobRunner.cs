using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Jobs;
using Microsoft.Extensions.Logging;

namespace Ludeka.Jobs.Runners;

/// <summary>
/// Trabajo autónomo para la sincronización periódica de feeds de catálogo comercial (Google Shopping XML/CSV).
/// Descarga los feeds activos en streaming, cruza ofertas por EAN y actualiza la base de datos de precios.
/// </summary>
public sealed class CatalogFeedSyncJobRunner : IJobRunner
{
    private readonly IJobExecutionCoordinator _coordinator;
    private readonly ICatalogFeedSyncService _syncService;
    private readonly ILogger<CatalogFeedSyncJobRunner> _logger;

    public CatalogFeedSyncJobRunner(
        IJobExecutionCoordinator coordinator,
        ICatalogFeedSyncService syncService,
        ILogger<CatalogFeedSyncJobRunner> logger)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => JobNames.FeedSync;

    public Task<JobLeaseOutcome> RunAsync(CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var windowKey = JobWindowKeyCalculator.PerSecond(nowUtc);

        return _coordinator.ExecuteWithWindowLeaseAsync(
            Name,
            windowKey,
            async (heartbeat, workCt) =>
            {
                _logger.LogInformation("Iniciando trabajo '{JobName}' para sincronización de feeds de afiliados...", Name);
                await heartbeat.BeatAsync(workCt);

                var results = await _syncService.SyncAllActiveFeedsAsync(workCt);

                int totalItems = results.Sum(r => r.ItemsRead);
                int totalMatched = results.Sum(r => r.MatchedCount);
                int totalFailed = results.Count(r => !r.Success);

                await heartbeat.BeatAsync(workCt);

                _logger.LogInformation(
                    "Trabajo '{JobName}' finalizado. Feeds procesados: {FeedsCount}, Fallidos: {FailedCount}, Items: {Items}, Ofertas cruzadas: {Matched}.",
                    Name, results.Count, totalFailed, totalItems, totalMatched);

                return new JobWorkResult(totalMatched, totalFailed, totalFailed > 0 ? $"{totalFailed} feeds fallaron durante la sincronización." : null);
            },
            ct);
    }
}
