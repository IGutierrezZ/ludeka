using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Jobs;
using Ludeka.Jobs;
using Ludeka.Jobs.Runners;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Jobs;

public class DrainStagingJobRunnerTests
{
    private class FakeCoordinator : IJobExecutionCoordinator
    {
        public bool ExecuteCalled { get; private set; }
        public string? CapturedJobName { get; private set; }
        public string? CapturedWindowKey { get; private set; }

        public async Task<JobLeaseOutcome> ExecuteWithWindowLeaseAsync(
            string jobName,
            string windowKey,
            Func<IJobHeartbeat, CancellationToken, Task<JobWorkResult>> work,
            CancellationToken ct = default)
        {
            ExecuteCalled = true;
            CapturedJobName = jobName;
            CapturedWindowKey = windowKey;

            await work(new FakeHeartbeat(), ct);
            return JobLeaseOutcome.Completed;
        }

        private class FakeHeartbeat : IJobHeartbeat
        {
            public Task BeatAsync(CancellationToken ct = default) => Task.CompletedTask;
        }
    }

    private class FakeMassIngestionService : IBggMassIngestionService
    {
        public int RunScheduledCallCount { get; private set; }
        public int CapturedMaxItems { get; private set; }

        public Task<BggMassIngestionContinuousDrainResultDto> RunScheduledContinuousDrainAsync(int maxItems = 4000, CancellationToken ct = default)
        {
            RunScheduledCallCount++;
            CapturedMaxItems = maxItems;
            return Task.FromResult(new BggMassIngestionContinuousDrainResultDto(
                CyclesExecuted: 5,
                TotalDetailsFetched: 20,
                TotalImagesProcessed: 10,
                TotalAiSummariesGenerated: 16,
                TotalPromotedToCatalog: 16,
                StoppedDueToAiQuota: false,
                CompletedAllStaging: false,
                Message: "5 ciclos completados"
            ));
        }

        public Task<int> DownloadAndIngestLatestRanksAsync(int? minUsersRated = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> RunScheduledDownloadAndIngestLatestRanksAsync(int? minUsersRated = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> IngestRanksDumpAsync(System.IO.Stream dumpStream, int minUsersRated = 1000, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> ProcessPendingDetailsBatchAsync(int batchSize = 20, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> ProcessPendingImagesBatchAsync(int batchSize = 10, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AiBatchProcessingResultDto> ProcessPendingAiBatchAsync(int gamesPerBatch = 8, int maxBatches = 5, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> PromoteReadyToCatalogBatchAsync(int batchSize = 50, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<BggStagingMetricsDto> GetMetricsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<BggMassIngestionCycleResultDto> RunDrainCycleAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<BggMassIngestionCycleResultDto> RunScheduledDrainCycleAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task ClearStagingAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> ResetQuotaExceededStatusAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task<BggMassIngestionContinuousDrainResultDto> RunContinuousDrainAsync(int maxItems = 4000, CancellationToken ct = default) => throw new NotImplementedException();
    }

    [Fact]
    public async Task RunAsync_InvokesServiceWithConfiguredMaxItems_AndReturnsCompletedOutcome()
    {
        // Arrange
        var coordinator = new FakeCoordinator();
        var massIngestion = new FakeMassIngestionService();

        var runner = new DrainStagingJobRunner(coordinator, massIngestion);

        // Act
        var outcome = await runner.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(JobNames.DrainStaging, runner.Name);
        Assert.True(coordinator.ExecuteCalled);
        Assert.Equal(JobNames.DrainStaging, coordinator.CapturedJobName);
        Assert.NotNull(coordinator.CapturedWindowKey);

        Assert.Equal(1, massIngestion.RunScheduledCallCount);
        Assert.Equal(4000, massIngestion.CapturedMaxItems);

        Assert.Equal(JobLeaseOutcome.Completed, outcome);
    }
}
