using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Jobs;
using Ludeka.Application.Options;
using Ludeka.Jobs;
using Ludeka.Jobs.Runners;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Jobs;

public class DataRetentionJobRunnerTests
{
    private class FakeCoordinator : IJobExecutionCoordinator
    {
        public bool ExecuteCalled { get; private set; }
        public string? CapturedJobName { get; private set; }
        public string? CapturedWindowKey { get; private set; }
        public JobWorkResult? CapturedWorkResult { get; private set; }

        public async Task<JobLeaseOutcome> ExecuteWithWindowLeaseAsync(
            string jobName,
            string windowKey,
            Func<IJobHeartbeat, CancellationToken, Task<JobWorkResult>> work,
            CancellationToken ct = default)
        {
            ExecuteCalled = true;
            CapturedJobName = jobName;
            CapturedWindowKey = windowKey;

            CapturedWorkResult = await work(new FakeHeartbeat(), ct);
            return JobLeaseOutcome.Completed;
        }

        private class FakeHeartbeat : IJobHeartbeat
        {
            public Task BeatAsync(CancellationToken ct = default) => Task.CompletedTask;
        }
    }

    private class FakeDataRetentionService : IDataRetentionService
    {
        public int PurgeCalledCount { get; private set; }

        public Task<DataRetentionResult> PurgeExpiredDataAsync(CancellationToken ct = default)
        {
            PurgeCalledCount++;
            return Task.FromResult(new DataRetentionResult(
                PurgedGiveawaysCount: 3,
                PurgedEventsCount: 2,
                PurgedReleasesCount: 1,
                DeletedImagesCount: 5,
                FailedImagesCount: 0));
        }
    }

    [Fact]
    public void Name_RetornaNombreConstanteDeJobNames()
    {
        var coordinator = new FakeCoordinator();
        var service = new FakeDataRetentionService();
        var options = Options.Create(new DataRetentionOptions());

        var runner = new DataRetentionJobRunner(coordinator, service, options);

        Assert.Equal(JobNames.DataRetention, runner.Name);
        Assert.Equal("data-retention", runner.Name);
    }

    [Fact]
    public async Task RunAsync_InvocaCoordinadorConVentanaDiariaYEjecutaPurga()
    {
        var coordinator = new FakeCoordinator();
        var service = new FakeDataRetentionService();
        var options = Options.Create(new DataRetentionOptions());

        var runner = new DataRetentionJobRunner(coordinator, service, options);

        var outcome = await runner.RunAsync(CancellationToken.None);

        Assert.Equal(JobLeaseOutcome.Completed, outcome);
        Assert.True(coordinator.ExecuteCalled);
        Assert.Equal(JobNames.DataRetention, coordinator.CapturedJobName);

        var expectedWindowKey = JobWindowKeyCalculator.DailyUtc(DateTimeOffset.UtcNow);
        Assert.Equal(expectedWindowKey, coordinator.CapturedWindowKey);

        Assert.Equal(1, service.PurgeCalledCount);
        Assert.NotNull(coordinator.CapturedWorkResult);
        Assert.Equal(6, coordinator.CapturedWorkResult.Processed); // 3 sorteos + 2 eventos + 1 novedad = 6
        Assert.Equal(0, coordinator.CapturedWorkResult.Failed);
    }
}
