using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Jobs;
using Ludeka.Jobs;
using Ludeka.Jobs.Runners;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ludeka.UnitTests.Jobs;

public class BggImagesTop3000JobRunnerTests
{
    private class FakeCoordinator : IJobExecutionCoordinator
    {
        public bool ExecuteCalled { get; private set; }
        public string? CapturedJobName { get; private set; }
        public string? CapturedWindowKey { get; private set; }
        public JobWorkResult? CapturedResult { get; private set; }

        public async Task<JobLeaseOutcome> ExecuteWithWindowLeaseAsync(
            string jobName,
            string windowKey,
            Func<IJobHeartbeat, CancellationToken, Task<JobWorkResult>> work,
            CancellationToken ct = default)
        {
            ExecuteCalled = true;
            CapturedJobName = jobName;
            CapturedWindowKey = windowKey;

            CapturedResult = await work(new FakeHeartbeat(), ct);
            return JobLeaseOutcome.Completed;
        }

        private class FakeHeartbeat : IJobHeartbeat
        {
            public Task BeatAsync(CancellationToken ct = default) => Task.CompletedTask;
        }
    }

    private class FakeImagesSyncService : IBggImagesSyncService
    {
        public int CallCount { get; private set; }
        public int LastRequestedAfterRank { get; private set; }

        public Task<BggImagesSyncResultDto> SyncTopRankedImagesBatchAsync(
            int afterRank = 0,
            int batchSize = 25,
            int maxRank = 3000,
            int delayMs = 800,
            CancellationToken ct = default)
        {
            CallCount++;
            LastRequestedAfterRank = afterRank;

            if (CallCount == 1)
            {
                return Task.FromResult(new BggImagesSyncResultDto(
                    EvaluatedCount: 25,
                    UpdatedCount: 15,
                    SkippedCount: 10,
                    FailedCount: 0,
                    LastRankProcessed: 25,
                    HasMore: true,
                    Message: "Primer lote procesado."
                ));
            }

            return Task.FromResult(new BggImagesSyncResultDto(
                EvaluatedCount: 10,
                UpdatedCount: 5,
                SkippedCount: 5,
                FailedCount: 0,
                LastRankProcessed: 35,
                HasMore: false,
                Message: "Segundo y último lote."
            ));
        }
    }

    [Fact]
    public void Constructor_WithNullArguments_ThrowsArgumentNullException()
    {
        var coordinator = new FakeCoordinator();
        var syncService = new FakeImagesSyncService();
        var logger = NullLogger<BggImagesTop3000JobRunner>.Instance;

        Assert.Throws<ArgumentNullException>(() => new BggImagesTop3000JobRunner(null!, syncService, logger));
        Assert.Throws<ArgumentNullException>(() => new BggImagesTop3000JobRunner(coordinator, null!, logger));
        Assert.Throws<ArgumentNullException>(() => new BggImagesTop3000JobRunner(coordinator, syncService, null!));
    }

    [Fact]
    public void Name_ReturnsExpectedJobName()
    {
        var runner = new BggImagesTop3000JobRunner(
            new FakeCoordinator(),
            new FakeImagesSyncService(),
            NullLogger<BggImagesTop3000JobRunner>.Instance);

        Assert.Equal(JobNames.BggImagesTop3000, runner.Name);
        Assert.Equal("bgg-images-top3000", runner.Name);
    }

    [Fact]
    public async Task RunAsync_IteratesBatchesUntilHasMoreIsFalse_AndAggregatesMetrics()
    {
        var coordinator = new FakeCoordinator();
        var syncService = new FakeImagesSyncService();
        var runner = new BggImagesTop3000JobRunner(
            coordinator,
            syncService,
            NullLogger<BggImagesTop3000JobRunner>.Instance);

        var outcome = await runner.RunAsync(CancellationToken.None);

        Assert.Equal(JobLeaseOutcome.Completed, outcome);
        Assert.True(coordinator.ExecuteCalled);
        Assert.Equal(JobNames.BggImagesTop3000, coordinator.CapturedJobName);
        Assert.Equal(2, syncService.CallCount);
        Assert.Equal(25, syncService.LastRequestedAfterRank);

        Assert.NotNull(coordinator.CapturedResult);
        Assert.Equal(20, coordinator.CapturedResult.Processed);
        Assert.Contains("Sincronización Top 3.000 finalizada hasta rango #35", coordinator.CapturedResult.Message);
        Assert.Contains("20 actualizados", coordinator.CapturedResult.Message);
        Assert.Contains("15 omitidos", coordinator.CapturedResult.Message);
        Assert.Contains("0 fallos", coordinator.CapturedResult.Message);
    }
}
