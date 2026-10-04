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

public class YouTubeAutoIngestJobRunnerTests
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

    private class FakeAutoIngestService : IYouTubeCatalogAutoIngestService
    {
        public int RunScheduledCalledCount { get; private set; }
        public int? PassedLimit { get; private set; }
        public int? PassedMaxRank { get; private set; }

        public Task<YouTubeCatalogAutoIngestResultDto> RunScheduledAutoIngestAsync(int? customLimit = null, int? maxRank = null, CancellationToken ct = default)
        {
            RunScheduledCalledCount++;
            PassedLimit = customLimit;
            PassedMaxRank = maxRank;

            return Task.FromResult(new YouTubeCatalogAutoIngestResultDto(
                GamesEvaluated: 60,
                VideosIngested: 15,
                SkippedCount: 45,
                ErrorsCount: 0,
                Duration: TimeSpan.FromSeconds(5),
                ExecutedAt: DateTimeOffset.UtcNow));
        }

        public Task<YouTubeCatalogAutoIngestResultDto> ExecuteAutoIngestAsync(int? customLimit = null, int? maxRank = null, CancellationToken ct = default)
            => RunScheduledAutoIngestAsync(customLimit, maxRank, ct);
    }

    [Fact]
    public void Name_RetornaNombreConstanteDeJobNames()
    {
        var coordinator = new FakeCoordinator();
        var service = new FakeAutoIngestService();
        var options = Microsoft.Extensions.Options.Options.Create(new YouTubeAutoIngestOptions());

        var runner = new YouTubeAutoIngestJobRunner(coordinator, service, options);

        Assert.Equal(JobNames.YouTubeAutoIngest, runner.Name);
        Assert.Equal("youtube-auto-ingest", runner.Name);
    }

    [Fact]
    public async Task RunAsync_InvocaCoordinadorConVentanaDiariaYEjecutaAutoIngesta()
    {
        var coordinator = new FakeCoordinator();
        var service = new FakeAutoIngestService();
        var options = Microsoft.Extensions.Options.Options.Create(new YouTubeAutoIngestOptions
        {
            DailyGamesLimit = 60,
            MaxBggRank = 4000
        });

        var runner = new YouTubeAutoIngestJobRunner(coordinator, service, options);

        var outcome = await runner.RunAsync(CancellationToken.None);

        Assert.Equal(JobLeaseOutcome.Completed, outcome);
        Assert.True(coordinator.ExecuteCalled);
        Assert.Equal(JobNames.YouTubeAutoIngest, coordinator.CapturedJobName);

        var expectedWindowKey = JobWindowKeyCalculator.DailyUtc(DateTimeOffset.UtcNow);
        Assert.Equal(expectedWindowKey, coordinator.CapturedWindowKey);

        Assert.Equal(1, service.RunScheduledCalledCount);
        Assert.Equal(60, service.PassedLimit);
        Assert.Equal(4000, service.PassedMaxRank);

        Assert.NotNull(coordinator.CapturedWorkResult);
        Assert.Equal(15, coordinator.CapturedWorkResult.Processed);
        Assert.Equal(0, coordinator.CapturedWorkResult.Failed);
    }
}
