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

public class BggVersionsSweepJobRunnerTests
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
        public int SweepCallCount { get; private set; }
        public int LastRequestedBggId { get; private set; }

        public Task<BggRawSnapshotStatusDto> GetStatusAsync(CancellationToken ct = default)
            => Task.FromResult(new BggRawSnapshotStatusDto(0, 0, 0, 0, 0, 0));

        public Task<BggRawSnapshotSyncResultDto> SyncBatchAsync(int batchSize = 20, int delayMs = 1200, CancellationToken ct = default)
            => Task.FromResult(new BggRawSnapshotSyncResultDto(0, 0, 0, [], []));

        public Task<BggRawSnapshotSyncResultDto> RunScheduledSyncBatchAsync(int batchSize = 20, int delayMs = 1200, CancellationToken ct = default)
            => Task.FromResult(new BggRawSnapshotSyncResultDto(0, 0, 0, [], []));

        public Task<BggExpansionDiscoveryResultDto> DiscoverAndEnqueueMissingExpansionsAsync(int maxToEnqueue = 50, CancellationToken ct = default)
            => Task.FromResult(new BggExpansionDiscoveryResultDto(0, 0, []));

        public Task<BggExpansionDiscoveryResultDto> RunScheduledDiscoverAndEnqueueMissingExpansionsAsync(int maxToEnqueue = 50, CancellationToken ct = default)
            => Task.FromResult(new BggExpansionDiscoveryResultDto(0, 0, []));

        public Task<int> AutoLinkExistingExpansionsAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> RunScheduledAutoLinkExistingExpansionsAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task<bool> EnsureSnapshotAsync(int bggId, CancellationToken ct = default) => Task.FromResult(true);

        public Task<BggExpansionReconciliationResultDto> ReconcileAndLinkExpansionsFromSnapshotsAsync(int batchSize = 200, CancellationToken ct = default)
            => Task.FromResult(new BggExpansionReconciliationResultDto(0, 0, 0, [], []));

        public Task<BggExpansionReconciliationResultDto> RunScheduledReconcileAndLinkExpansionsFromSnapshotsAsync(int batchSize = 200, CancellationToken ct = default)
            => Task.FromResult(new BggExpansionReconciliationResultDto(0, 0, 0, [], []));

        public Task<BggVersionCatalogSweepResultDto> RunScheduledSweepCatalogFromVersionsAsync(int batchSize = 200, int lastBggId = 0, CancellationToken ct = default)
        {
            SweepCallCount++;
            LastRequestedBggId = lastBggId;

            if (SweepCallCount == 1)
            {
                return Task.FromResult(new BggVersionCatalogSweepResultDto(
                    EvaluatedCount: 200,
                    UpdatedTitlesCount: 50,
                    UpdatedEansCount: 40,
                    SkippedCount: 110,
                    FailedCount: 0,
                    LastBggIdProcessed: 500,
                    HasMore: true,
                    Message: "Primer lote procesado."
                ));
            }

            return Task.FromResult(new BggVersionCatalogSweepResultDto(
                EvaluatedCount: 50,
                UpdatedTitlesCount: 10,
                UpdatedEansCount: 10,
                SkippedCount: 30,
                FailedCount: 0,
                LastBggIdProcessed: 600,
                HasMore: false,
                Message: "Último lote procesado."
            ));
        }
    }

    [Fact]
    public void Constructor_WithNullArguments_ThrowsArgumentNullException()
    {
        var coordinator = new FakeCoordinator();
        var syncService = new FakeSyncService();
        var logger = NullLogger<BggVersionsSweepJobRunner>.Instance;

        Assert.Throws<ArgumentNullException>(() => new BggVersionsSweepJobRunner(null!, syncService, logger));
        Assert.Throws<ArgumentNullException>(() => new BggVersionsSweepJobRunner(coordinator, null!, logger));
        Assert.Throws<ArgumentNullException>(() => new BggVersionsSweepJobRunner(coordinator, syncService, null!));
    }

    [Fact]
    public void Name_ReturnsExpectedJobName()
    {
        var runner = new BggVersionsSweepJobRunner(
            new FakeCoordinator(),
            new FakeSyncService(),
            NullLogger<BggVersionsSweepJobRunner>.Instance);

        Assert.Equal(JobNames.BggVersionsSweep, runner.Name);
    }

    [Fact]
    public async Task RunAsync_IteratesBatchesUntilHasMoreIsFalse_AndAggregatesMetrics()
    {
        var coordinator = new FakeCoordinator();
        var syncService = new FakeSyncService();
        var runner = new BggVersionsSweepJobRunner(
            coordinator,
            syncService,
            NullLogger<BggVersionsSweepJobRunner>.Instance);

        var outcome = await runner.RunAsync(CancellationToken.None);

        Assert.Equal(JobLeaseOutcome.Completed, outcome);
        Assert.True(coordinator.ExecuteCalled);
        Assert.Equal(JobNames.BggVersionsSweep, coordinator.CapturedJobName);
        Assert.Equal(2, syncService.SweepCallCount);
        Assert.Equal(500, syncService.LastRequestedBggId);

        Assert.NotNull(coordinator.CapturedResult);
        Assert.Equal(250, coordinator.CapturedResult.Processed);
        Assert.Contains("250 evaluados", coordinator.CapturedResult.Message);
        Assert.Contains("60 títulos ES actualizados", coordinator.CapturedResult.Message);
        Assert.Contains("50 EANs asignados", coordinator.CapturedResult.Message);
    }
}
