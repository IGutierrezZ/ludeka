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

public class SeedStagingJobRunnerTests
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
        public int? CapturedMinVotes { get; private set; }

        public Task<int> RunScheduledDownloadAndIngestLatestRanksAsync(int? minUsersRated = null, CancellationToken ct = default)
        {
            RunScheduledCallCount++;
            CapturedMinVotes = minUsersRated;
            return Task.FromResult(7850);
        }

        public Task<int> DownloadAndIngestLatestRanksAsync(int? minUsersRated = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> IngestRanksDumpAsync(System.IO.Stream dumpStream, int minUsersRated = 1000, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> ProcessPendingDetailsBatchAsync(int batchSize = 20, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> ProcessPendingImagesBatchAsync(int batchSize = 10, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AiBatchProcessingResultDto> ProcessPendingAiBatchAsync(int gamesPerBatch = 8, int maxBatches = 5, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> PromoteReadyToCatalogBatchAsync(int batchSize = 50, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<BggStagingMetricsDto> GetMetricsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<BggMassIngestionCycleResultDto> RunDrainCycleAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<BggMassIngestionCycleResultDto> RunScheduledDrainCycleAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task ClearStagingAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task RunAsync_InvokesServiceWithConfiguredMinVotes_AndReturnsCompletedOutcome()
    {
        // Arrange
        var coordinator = new FakeCoordinator();
        var massIngestion = new FakeMassIngestionService();
        var options = Options.Create(new BggMassIngestionOptions { MinUsersRated = 35 });

        var runner = new SeedStagingJobRunner(coordinator, massIngestion, options);

        // Act
        var outcome = await runner.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(JobNames.SeedStaging, runner.Name);
        Assert.True(coordinator.ExecuteCalled);
        Assert.Equal(JobNames.SeedStaging, coordinator.CapturedJobName);
        Assert.NotNull(coordinator.CapturedWindowKey);

        Assert.Equal(1, massIngestion.RunScheduledCallCount);
        Assert.Equal(35, massIngestion.CapturedMinVotes);

        Assert.Equal(JobLeaseOutcome.Completed, outcome);
    }
}
