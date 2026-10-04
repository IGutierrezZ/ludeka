using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Jobs;
using Ludeka.Core.Entities;
using Ludeka.Jobs;
using Ludeka.Jobs.Runners;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ludeka.UnitTests.Jobs;

public class CatalogFeedSyncJobRunnerTests
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

    private class FakeCatalogFeedSyncService : ICatalogFeedSyncService
    {
        public bool SyncAllCalled { get; private set; }
        public List<FeedSyncResult> ResultsToReturn { get; set; } = [];

        public Task<List<FeedSyncResult>> SyncAllActiveFeedsAsync(CancellationToken ct = default)
        {
            SyncAllCalled = true;
            return Task.FromResult(ResultsToReturn);
        }

        public Task<FeedSyncResult> SyncFeedSourceAsync(AffiliateFeedSource source, CancellationToken ct = default)
            => Task.FromResult(new FeedSyncResult(source.Id, source.StoreName, true, 0, 0, 0, 0));

        public Task<FeedSyncResult> SyncFeedSourceByIdAsync(Guid sourceId, CancellationToken ct = default)
            => Task.FromResult(new FeedSyncResult(sourceId, "Test", true, 0, 0, 0, 0));
    }

    [Fact]
    public void Name_ShouldMatchJobNamesFeedSync()
    {
        var runner = new CatalogFeedSyncJobRunner(
            new FakeCoordinator(),
            new FakeCatalogFeedSyncService(),
            NullLogger<CatalogFeedSyncJobRunner>.Instance);

        Assert.Equal(JobNames.FeedSync, runner.Name);
        Assert.Equal("feed-sync", runner.Name);
    }

    [Fact]
    public async Task RunAsync_ExecutesWithCoordinator_AndInvokesSyncAllActiveFeeds()
    {
        // Arrange
        var coordinator = new FakeCoordinator();
        var syncService = new FakeCatalogFeedSyncService
        {
            ResultsToReturn =
            [
                new FeedSyncResult(Guid.NewGuid(), "Zacatrus", true, 50, 40, 2, 1),
                new FeedSyncResult(Guid.NewGuid(), "Jugamos Otra", true, 30, 25, 0, 0)
            ]
        };

        var runner = new CatalogFeedSyncJobRunner(
            coordinator,
            syncService,
            NullLogger<CatalogFeedSyncJobRunner>.Instance);

        // Act
        var outcome = await runner.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(JobLeaseOutcome.Completed, outcome);
        Assert.True(coordinator.ExecuteCalled);
        Assert.Equal(JobNames.FeedSync, coordinator.CapturedJobName);
        Assert.NotNull(coordinator.CapturedResult);
        Assert.Equal(65, coordinator.CapturedResult.Processed);
        Assert.Equal(0, coordinator.CapturedResult.Failed);
        Assert.Null(coordinator.CapturedResult.Message);
        Assert.True(syncService.SyncAllCalled);
    }

    [Fact]
    public async Task RunAsync_WhenSomeFeedsFail_ReportsFailedItemsAndErrorMessage()
    {
        // Arrange
        var coordinator = new FakeCoordinator();
        var syncService = new FakeCatalogFeedSyncService
        {
            ResultsToReturn =
            [
                new FeedSyncResult(Guid.NewGuid(), "Zacatrus", true, 50, 40, 2, 1),
                new FeedSyncResult(Guid.NewGuid(), "Tienda Caida", false, 0, 0, 0, 0, "HTTP 500")
            ]
        };

        var runner = new CatalogFeedSyncJobRunner(
            coordinator,
            syncService,
            NullLogger<CatalogFeedSyncJobRunner>.Instance);

        // Act
        var outcome = await runner.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(JobLeaseOutcome.Completed, outcome);
        Assert.NotNull(coordinator.CapturedResult);
        Assert.Equal(40, coordinator.CapturedResult.Processed);
        Assert.Equal(1, coordinator.CapturedResult.Failed);
        Assert.Contains("1 feeds fallaron", coordinator.CapturedResult.Message);
    }

    [Fact]
    public void AddLudekaJobRunners_RegistersCatalogFeedSyncJobRunner()
    {
        var services = new ServiceCollection();

        // Act
        services.AddLudekaJobRunners();

        // Assert
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IJobRunner) &&
            d.ImplementationType == typeof(CatalogFeedSyncJobRunner));

        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }
}
