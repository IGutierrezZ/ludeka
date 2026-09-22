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

public class SeedDirectoryJobRunnerTests
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

    private class FakeDirectorySeederService : IDirectorySeederService
    {
        public int SeedDirectoryCallCount { get; private set; }

        public Task<DirectorySeedResultDto> SeedDirectoryAsync(CancellationToken cancellationToken = default)
        {
            SeedDirectoryCallCount++;
            return Task.FromResult(new DirectorySeedResultDto(
                PublishersAdded: 46,
                PublishersUpdated: 0,
                StoresAdded: 37,
                StoresUpdated: 0,
                CreatorsAdded: 35,
                CreatorsUpdated: 0,
                TotalPublishers: 46,
                TotalStores: 37,
                TotalCreators: 35
            ));
        }
    }

    [Fact]
    public async Task RunAsync_InvokesDirectorySeederService_AndReturnsCompletedOutcome()
    {
        // Arrange
        var coordinator = new FakeCoordinator();
        var seederService = new FakeDirectorySeederService();
        var logger = NullLogger<SeedDirectoryJobRunner>.Instance;

        var runner = new SeedDirectoryJobRunner(coordinator, seederService, logger);

        // Act
        var outcome = await runner.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(JobNames.SeedDirectory, runner.Name);
        Assert.True(coordinator.ExecuteCalled);
        Assert.Equal(JobNames.SeedDirectory, coordinator.CapturedJobName);
        Assert.NotNull(coordinator.CapturedWindowKey);

        Assert.Equal(1, seederService.SeedDirectoryCallCount);
        Assert.Equal(JobLeaseOutcome.Completed, outcome);
    }
}
