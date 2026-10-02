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

public class BggReconcileExpansionsJobRunnerTests
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
        public bool ReconcileCalled { get; private set; }
        public int CapturedBatchSize { get; private set; }

        public Task<BggRawSnapshotStatusDto> GetStatusAsync(CancellationToken ct = default)
            => Task.FromResult(new BggRawSnapshotStatusDto(100, 50, 50, 10, 5, 5));

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
            => RunScheduledReconcileAndLinkExpansionsFromSnapshotsAsync(batchSize, ct);

        public Task<BggExpansionReconciliationResultDto> RunScheduledReconcileAndLinkExpansionsFromSnapshotsAsync(int batchSize = 200, CancellationToken ct = default)
        {
            ReconcileCalled = true;
            CapturedBatchSize = batchSize;
            return Task.FromResult(new BggExpansionReconciliationResultDto(
                TotalEvaluated: 1500,
                ReclassifiedExpansionsCount: 350,
                LinkedExpansionsCount: 320,
                ReclassifiedTitles: ["Exp 1", "Exp 2"],
                LinkedExpansions: ["Exp 1 → Base 1"],
                Message: "Reconciliación completada con éxito."
            ));
        }
    }

    [Fact]
    public void Constructor_WithNullArguments_ThrowsArgumentNullException()
    {
        var coordinator = new FakeCoordinator();
        var syncService = new FakeSyncService();
        var logger = NullLogger<BggReconcileExpansionsJobRunner>.Instance;

        Assert.Throws<ArgumentNullException>(() => new BggReconcileExpansionsJobRunner(null!, syncService, logger));
        Assert.Throws<ArgumentNullException>(() => new BggReconcileExpansionsJobRunner(coordinator, null!, logger));
        Assert.Throws<ArgumentNullException>(() => new BggReconcileExpansionsJobRunner(coordinator, syncService, null!));
    }

    [Fact]
    public void Name_ReturnsExpectedJobName()
    {
        var runner = new BggReconcileExpansionsJobRunner(
            new FakeCoordinator(),
            new FakeSyncService(),
            NullLogger<BggReconcileExpansionsJobRunner>.Instance);

        Assert.Equal(JobNames.BggReconcileExpansions, runner.Name);
        Assert.Equal("bgg-reconcile-expansions", runner.Name);
    }

    [Fact]
    public async Task RunAsync_CallsScheduledReconcile_AndReturnsExpectedResult()
    {
        var coordinator = new FakeCoordinator();
        var syncService = new FakeSyncService();
        var runner = new BggReconcileExpansionsJobRunner(coordinator, syncService, NullLogger<BggReconcileExpansionsJobRunner>.Instance);

        var outcome = await runner.RunAsync(CancellationToken.None);

        Assert.Equal(JobLeaseOutcome.Completed, outcome);
        Assert.True(coordinator.ExecuteCalled);
        Assert.Equal(JobNames.BggReconcileExpansions, coordinator.CapturedJobName);
        Assert.True(syncService.ReconcileCalled);
        Assert.Equal(200, syncService.CapturedBatchSize);

        Assert.NotNull(coordinator.CapturedResult);
        Assert.Equal(1500, coordinator.CapturedResult.Processed);
        Assert.Equal(0, coordinator.CapturedResult.Failed);
        Assert.Contains("350 reclasificados", coordinator.CapturedResult.Message);
        Assert.Contains("320 vinculados", coordinator.CapturedResult.Message);
    }
}
