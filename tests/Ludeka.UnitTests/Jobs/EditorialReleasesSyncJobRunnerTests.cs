using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Jobs;
using Ludeka.Jobs;
using Ludeka.Jobs.Runners;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ludeka.UnitTests.Jobs;

public class EditorialReleasesSyncJobRunnerTests
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

    private class FakeEditorialReleasesSyncService : IEditorialReleasesSyncService
    {
        public bool SyncAllCalled { get; private set; }
        public EditorialSyncSummaryDto SummaryToReturn { get; set; } = new(0, 0, 0, 0, 0, [], []);

        public Task<EditorialSyncSummaryDto> SyncAllEditorialReleasesAsync(CancellationToken ct = default)
        {
            SyncAllCalled = true;
            return Task.FromResult(SummaryToReturn);
        }

        public Task<EditorialSyncResultDto> SyncPublisherReleasesAsync(string publisher, CancellationToken ct = default)
        {
            return Task.FromResult(new EditorialSyncResultDto(publisher, true, 0, 0, 0, 0, 0, null));
        }
    }

    [Fact]
    public void Name_ShouldMatchJobNamesEditorialReleasesSync()
    {
        var runner = new EditorialReleasesSyncJobRunner(
            new FakeCoordinator(),
            new FakeEditorialReleasesSyncService(),
            NullLogger<EditorialReleasesSyncJobRunner>.Instance);

        Assert.Equal(JobNames.EditorialReleasesSync, runner.Name);
        Assert.Equal("editorial-releases-sync", runner.Name);
    }

    [Fact]
    public async Task RunAsync_ExecutesWithCoordinator_AndInvokesSyncAllEditorialReleases()
    {
        // Arrange
        var coordinator = new FakeCoordinator();
        var syncService = new FakeEditorialReleasesSyncService
        {
            SummaryToReturn = new EditorialSyncSummaryDto(
                TotalFound: 15,
                CreatedCount: 10,
                UpdatedCount: 2,
                GamesLinkedCount: 8,
                GamesImportedFromBggCount: 4,
                PublisherResults: [],
                Errors: [])
        };

        var runner = new EditorialReleasesSyncJobRunner(
            coordinator,
            syncService,
            NullLogger<EditorialReleasesSyncJobRunner>.Instance);

        // Act
        var outcome = await runner.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(JobLeaseOutcome.Completed, outcome);
        Assert.True(coordinator.ExecuteCalled);
        Assert.Equal(JobNames.EditorialReleasesSync, coordinator.CapturedJobName);
        Assert.NotNull(coordinator.CapturedResult);
        Assert.Equal(12, coordinator.CapturedResult.Processed);
        Assert.Equal(0, coordinator.CapturedResult.Failed);
        Assert.Null(coordinator.CapturedResult.Message);
        Assert.True(syncService.SyncAllCalled);
    }

    [Fact]
    public async Task RunAsync_WhenSomeErrorsOccur_ReportsFailedItemsAndErrorMessage()
    {
        // Arrange
        var coordinator = new FakeCoordinator();
        var syncService = new FakeEditorialReleasesSyncService
        {
            SummaryToReturn = new EditorialSyncSummaryDto(
                TotalFound: 5,
                CreatedCount: 2,
                UpdatedCount: 0,
                GamesLinkedCount: 1,
                GamesImportedFromBggCount: 0,
                PublisherResults: [],
                Errors: ["Devir: HTTP 500", "Maldito: timeout"])
        };

        var runner = new EditorialReleasesSyncJobRunner(
            coordinator,
            syncService,
            NullLogger<EditorialReleasesSyncJobRunner>.Instance);

        // Act
        var outcome = await runner.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(JobLeaseOutcome.Completed, outcome);
        Assert.NotNull(coordinator.CapturedResult);
        Assert.Equal(2, coordinator.CapturedResult.Processed);
        Assert.Equal(2, coordinator.CapturedResult.Failed);
        Assert.Contains("Devir: HTTP 500", coordinator.CapturedResult.Message);
        Assert.Contains("Maldito: timeout", coordinator.CapturedResult.Message);
    }

    [Fact]
    public void AddLudekaJobRunners_RegistersEditorialReleasesSyncJobRunner()
    {
        var services = new ServiceCollection();

        // Act
        services.AddLudekaJobRunners();

        // Assert
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IJobRunner) &&
            d.ImplementationType == typeof(EditorialReleasesSyncJobRunner));

        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }
}
