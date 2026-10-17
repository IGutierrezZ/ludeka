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
/// Trabajo desatendido de sincronización diaria de Reels de la cuenta oficial de Instagram de Ludeka
/// hacia el canal oficial de YouTube como YouTube Shorts (INC-150).
/// Se ejecuta bajo coordinación de ventana diaria UTC e invoca <see cref="IInstagramYouTubeSyncService"/>.
/// </summary>
public sealed class InstagramYouTubeSyncJobRunner : IJobRunner
{
    private readonly IJobExecutionCoordinator _coordinator;
    private readonly IInstagramYouTubeSyncService _syncService;
    private readonly IOptions<InstagramYouTubeSyncOptions> _options;

    public InstagramYouTubeSyncJobRunner(
        IJobExecutionCoordinator coordinator,
        IInstagramYouTubeSyncService syncService,
        IOptions<InstagramYouTubeSyncOptions> options)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string Name => JobNames.InstagramYouTubeSync;

    public Task<JobLeaseOutcome> RunAsync(CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var windowKey = JobWindowKeyCalculator.DailyUtc(nowUtc);
        var maxUploads = _options.Value.MaxUploadsPerRun;

        return _coordinator.ExecuteWithWindowLeaseAsync(
            Name,
            windowKey,
            async (_, workCt) =>
            {
                var result = await _syncService.SyncReelsToYouTubeAsync(maxUploads, workCt);
                var summary = $"Discovered:{result.DiscoveredReelsCount};Enqueued:{result.NewlyEnqueuedCount};Uploaded:{result.UploadedCount};Skipped:{result.SkippedCount};QuotaStop:{result.StoppedByQuotaLimit}";
                return new JobWorkResult(result.UploadedCount, result.FailedCount, summary);
            },
            ct);
    }
}
