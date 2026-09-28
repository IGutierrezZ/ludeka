using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Jobs;
using Ludeka.Jobs;
using Ludeka.Jobs.Runners;
using Xunit;

namespace Ludeka.UnitTests.Jobs;

public class BackfillQualityJobRunnerTests
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

        public Task<BggQualitySweepBatchResultDto> RunScheduledSweepCatalogQualityBatchAsync(int afterBggId = 0, int batchSize = 50, CancellationToken ct = default)
        {
            RunScheduledCallCount++;
            if (RunScheduledCallCount == 1)
            {
                return Task.FromResult(new BggQualitySweepBatchResultDto(50, 45, 5, 0, 50, true, "50 evaluados"));
            }
            return Task.FromResult(new BggQualitySweepBatchResultDto(20, 10, 10, 0, 70, false, "20 evaluados"));
        }

        public Task<BggQualitySweepBatchResultDto> SweepCatalogQualityBatchAsync(int afterBggId = 0, int batchSize = 50, CancellationToken ct = default)
            => RunScheduledSweepCatalogQualityBatchAsync(afterBggId, batchSize, ct);

        public Task<int> GetTotalCatalogCountAsync(CancellationToken ct = default) => Task.FromResult(70);

        public Task<BggQualityBackfillResultDto> RunScheduledBackfillCatalogQualityBatchAsync(int batchSize = 50, CancellationToken ct = default)
            => Task.FromResult(new BggQualityBackfillResultDto(0, 0, 0, "0 pendientes"));

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
        public Task<BggMassIngestionContinuousDrainResultDto> RunScheduledContinuousDrainAsync(int maxItems = 4000, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<BggQualityBackfillResultDto> BackfillCatalogQualityBatchAsync(int batchSize = 50, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> GetPendingQualityBackfillCountAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    [Fact]
    public async Task RunAsync_InvokesBackfillBatchesUntilExhausted_AndReturnsCompletedOutcome()
    {
        // Arrange
        var coordinator = new FakeCoordinator();
        var massIngestion = new FakeMassIngestionService();

        var runner = new BackfillQualityJobRunner(coordinator, massIngestion);

        // Act
        var outcome = await runner.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(JobNames.BackfillQuality, runner.Name);
        Assert.True(coordinator.ExecuteCalled);
        Assert.Equal(JobNames.BackfillQuality, coordinator.CapturedJobName);
        Assert.NotNull(coordinator.CapturedWindowKey);

        Assert.Equal(2, massIngestion.RunScheduledCallCount);
        Assert.Equal(JobLeaseOutcome.Completed, outcome);
    }
}
