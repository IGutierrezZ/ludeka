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
/// Trabajo fino de auto-ingesta periódica de vídeos de YouTube para catálogo prioritario (INC-106).
/// Calcula su ventana diaria UTC e invoca al coordinador de idempotencia, que a su vez invoca
/// <see cref="IYouTubeCatalogAutoIngestService.RunScheduledAutoIngestAsync"/> con límite diario de 60 juegos.
/// </summary>
public sealed class YouTubeAutoIngestJobRunner : IJobRunner
{
    private readonly IJobExecutionCoordinator _coordinator;
    private readonly IYouTubeCatalogAutoIngestService _service;
    private readonly IOptions<YouTubeAutoIngestOptions> _options;

    public YouTubeAutoIngestJobRunner(
        IJobExecutionCoordinator coordinator,
        IYouTubeCatalogAutoIngestService service,
        IOptions<YouTubeAutoIngestOptions> options)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string Name => JobNames.YouTubeAutoIngest;

    public Task<JobLeaseOutcome> RunAsync(CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var windowKey = JobWindowKeyCalculator.DailyUtc(nowUtc);
        var limit = _options.Value.DailyGamesLimit;
        var maxRank = _options.Value.MaxBggRank;

        return _coordinator.ExecuteWithWindowLeaseAsync(
            Name,
            windowKey,
            async (_, workCt) =>
            {
                var result = await _service.RunScheduledAutoIngestAsync(limit, maxRank, workCt);
                return new JobWorkResult(result.VideosIngested, result.ErrorsCount, $"Evaluated:{result.GamesEvaluated};Skipped:{result.SkippedCount}");
            },
            ct);
    }
}
