using System;
using System.Collections.Generic;
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

public class BggRawBackfillJobRunnerTests
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

    private class FakeSyncService : IBggRawSnapshotSyncService
    {
        public int SyncBatchCallCount { get; private set; }
        public bool AutoLinkCalled { get; private set; }
        public bool DiscoverCalled { get; private set; }

        public Task<BggRawSnapshotStatusDto> GetStatusAsync(CancellationToken ct = default)
            => Task.FromResult(new BggRawSnapshotStatusDto(100, 50, 50, 10, 5, 5));

        public Task<BggRawSnapshotSyncResultDto> SyncBatchAsync(int batchSize = 20, int delayMs = 1200, CancellationToken ct = default)
            => RunScheduledSyncBatchAsync(batchSize, delayMs, ct);

        public Task<BggRawSnapshotSyncResultDto> RunScheduledSyncBatchAsync(int batchSize = 20, int delayMs = 1200, CancellationToken ct = default)
        {
            SyncBatchCallCount++;
            if (SyncBatchCallCount == 1)
            {
                return Task.FromResult(new BggRawSnapshotSyncResultDto(50, 48, 2, ["Juego 1"], [], "Lote 1"));
            }
            if (SyncBatchCallCount == 2)
            {
                return Task.FromResult(new BggRawSnapshotSyncResultDto(30, 30, 0, ["Juego 2"], [], "Lote 2"));
            }

            return Task.FromResult(new BggRawSnapshotSyncResultDto(0, 0, 0, [], [], "Fin"));
        }

        public Task<BggExpansionDiscoveryResultDto> DiscoverAndEnqueueMissingExpansionsAsync(int maxToEnqueue = 50, CancellationToken ct = default)
            => RunScheduledDiscoverAndEnqueueMissingExpansionsAsync(maxToEnqueue, ct);

        public Task<BggExpansionDiscoveryResultDto> RunScheduledDiscoverAndEnqueueMissingExpansionsAsync(int maxToEnqueue = 50, CancellationToken ct = default)
        {
            DiscoverCalled = true;
            return Task.FromResult(new BggExpansionDiscoveryResultDto(10, 8, ["Exp 1", "Exp 2"]));
        }

        public Task<int> AutoLinkExistingExpansionsAsync(CancellationToken ct = default)
            => RunScheduledAutoLinkExistingExpansionsAsync(ct);

        public Task<int> RunScheduledAutoLinkExistingExpansionsAsync(CancellationToken ct = default)
        {
            AutoLinkCalled = true;
            return Task.FromResult(4);
        }
    }

    [Fact]
    public void Constructor_WithNullArguments_ThrowsArgumentNullException()
    {
        var coordinator = new FakeCoordinator();
        var syncService = new FakeSyncService();
        var logger = NullLogger<BggRawBackfillJobRunner>.Instance;

        Assert.Throws<ArgumentNullException>(() => new BggRawBackfillJobRunner(null!, syncService, logger));
        Assert.Throws<ArgumentNullException>(() => new BggRawBackfillJobRunner(coordinator, null!, logger));
        Assert.Throws<ArgumentNullException>(() => new BggRawBackfillJobRunner(coordinator, syncService, null!));
    }

    [Fact]
    public void Name_ReturnsExpectedJobName()
    {
        var runner = new BggRawBackfillJobRunner(
            new FakeCoordinator(),
            new FakeSyncService(),
            NullLogger<BggRawBackfillJobRunner>.Instance);

        Assert.Equal(JobNames.BggRawBackfill, runner.Name);
        Assert.Equal("bgg-raw-backfill", runner.Name);
    }

    [Fact]
    public async Task RunAsync_IteratesBatchesUntilDone_AndExecutesAutoLinkAndDiscovery()
    {
        var coordinator = new FakeCoordinator();
        var syncService = new FakeSyncService();
        var runner = new BggRawBackfillJobRunner(coordinator, syncService, NullLogger<BggRawBackfillJobRunner>.Instance);

        var outcome = await runner.RunAsync(CancellationToken.None);

        Assert.Equal(JobLeaseOutcome.Completed, outcome);
        Assert.True(coordinator.ExecuteCalled);
        Assert.Equal(JobNames.BggRawBackfill, coordinator.CapturedJobName);

        // 2 lotes procesados + 1 lote que devolvió 0
        Assert.Equal(3, syncService.SyncBatchCallCount);
        Assert.True(syncService.AutoLinkCalled);
        Assert.True(syncService.DiscoverCalled);

        Assert.NotNull(coordinator.CapturedResult);
        Assert.Equal(80, coordinator.CapturedResult.Processed);
        Assert.Equal(2, coordinator.CapturedResult.Failed);
        Assert.Contains("78 sincronizados", coordinator.CapturedResult.Message);
        Assert.Contains("4 expansiones vinculadas", coordinator.CapturedResult.Message);
    }
}
