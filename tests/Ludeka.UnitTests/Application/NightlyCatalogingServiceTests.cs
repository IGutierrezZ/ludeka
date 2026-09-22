using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Bgg;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class NightlyCatalogingServiceTests
{
    [Fact]
    public async Task ExecuteNightlyCatalogingAsync_WhenQueueHasEnoughGames_ProcessesQueueWithoutTopBggBackfill()
    {
        // Arrange
        var service = CreateService(out var pendingRepo, out var bggClient, out var gameRepo, out _, out _, out _, out var logRepo, limit: 2);

        var game1 = CreateDummyGame(101, "Catan");
        var game2 = CreateDummyGame(102, "Carcassonne");
        bggClient.Games[101] = game1;
        bggClient.Games[102] = game2;

        pendingRepo.Items.Add(new PendingBggImport(101, "Catan", 1995));
        pendingRepo.Items.Add(new PendingBggImport(102, "Carcassonne", 2000));

        // Act
        var result = await service.ExecuteNightlyCatalogingAsync();

        // Assert
        Assert.Equal(2, result.TotalCatalogedCount);
        Assert.Equal(2, result.QueueProcessedCount);
        Assert.Equal(0, result.TopBackfillCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal("Completed", result.Status);

        Assert.Equal(2, gameRepo.Games.Count);
        Assert.All(pendingRepo.Items, i => Assert.Equal(CatalogQueueStatus.Completed, i.Status));

        var savedLog = Assert.Single(logRepo.Logs);
        Assert.Equal(2, savedLog.TotalCatalogedCount);
        Assert.Equal(2, savedLog.QueueProcessedCount);
    }

    [Fact]
    public async Task ExecuteNightlyCatalogingAsync_WhenQueueIsIncomplete_BackfillsWithTopBggUpToDailyLimit()
    {
        // Arrange
        var service = CreateService(out var pendingRepo, out var bggClient, out var gameRepo, out _, out _, out _, out var logRepo, limit: 3);

        // 1 juego en la cola
        var qGame = CreateDummyGame(201, "Apiary");
        bggClient.Games[201] = qGame;
        pendingRepo.Items.Add(new PendingBggImport(201, "Apiary", 2023, origin: CatalogQueueOrigin.NewsDiscovery));

        // 3 juegos en el Top de BGG
        var top1 = CreateDummyGame(301, "Brass: Birmingham");
        var top2 = CreateDummyGame(302, "Pandemic Legacy: Season 1");
        var top3 = CreateDummyGame(303, "Gloomhaven");
        bggClient.Games[301] = top1;
        bggClient.Games[302] = top2;
        bggClient.Games[303] = top3;

        bggClient.TopGames =
        [
            new BggTopGameDto(301, "Brass: Birmingham", 1, 2018, null),
            new BggTopGameDto(302, "Pandemic Legacy: Season 1", 2, 2015, null),
            new BggTopGameDto(303, "Gloomhaven", 3, 2017, null)
        ];

        // Act
        var result = await service.ExecuteNightlyCatalogingAsync();

        // Assert: 1 de cola + 2 de relleno = 3 (cupo diario)
        Assert.Equal(3, result.TotalCatalogedCount);
        Assert.Equal(1, result.QueueProcessedCount);
        Assert.Equal(2, result.TopBackfillCount);
        Assert.Equal(0, result.FailedCount);

        Assert.Equal(3, gameRepo.Games.Count);
        Assert.Contains(gameRepo.Games, g => g.SpanishTitle == "Apiary");
        Assert.Contains(gameRepo.Games, g => g.SpanishTitle == "Brass: Birmingham");
        Assert.Contains(gameRepo.Games, g => g.SpanishTitle == "Pandemic Legacy: Season 1");

        // El tercer juego de BGG no se ingesta porque ya se alcanzó el límite de 3
        Assert.DoesNotContain(gameRepo.Games, g => g.SpanishTitle == "Gloomhaven");

        var savedLog = Assert.Single(logRepo.Logs);
        Assert.Equal(1, savedLog.QueueProcessedCount);
        Assert.Equal(2, savedLog.TopBackfillCount);
        Assert.Equal(3, savedLog.TotalCatalogedCount);
    }

    [Fact]
    public async Task ExecuteNightlyCatalogingAsync_WhenQueueExceedsLimit_ProcessesStrictlyUpToLimit()
    {
        // Arrange
        var service = CreateService(out var pendingRepo, out var bggClient, out var gameRepo, out _, out _, out _, out _, limit: 2);

        for (int i = 1; i <= 5; i++)
        {
            int id = 400 + i;
            var g = CreateDummyGame(id, $"Juego {i}");
            bggClient.Games[id] = g;
            pendingRepo.Items.Add(new PendingBggImport(id, $"Juego {i}", 2020));
        }

        // Act
        var result = await service.ExecuteNightlyCatalogingAsync();

        // Assert
        Assert.Equal(2, result.TotalCatalogedCount);
        Assert.Equal(2, result.QueueProcessedCount);
        Assert.Equal(2, gameRepo.Games.Count);

        Assert.Equal(2, pendingRepo.Items.Count(i => i.Status == CatalogQueueStatus.Completed));
        Assert.Equal(3, pendingRepo.Items.Count(i => i.Status == CatalogQueueStatus.Pending));
    }

    [Fact]
    public async Task ExecuteNightlyCatalogingAsync_WhenItemFails_MarksAsFailedAndContinuesBatch()
    {
        // Arrange
        var service = CreateService(out var pendingRepo, out var bggClient, out var gameRepo, out _, out _, out _, out var logRepo, limit: 2);

        // Juego 1 falla porque BGG devuelve null
        pendingRepo.Items.Add(new PendingBggImport(501, "Juego Inexistente BGG", 2020));
        bggClient.Games[501] = null;

        // Juego 2 existe
        var game2 = CreateDummyGame(502, "Wingspan");
        bggClient.Games[502] = game2;
        pendingRepo.Items.Add(new PendingBggImport(502, "Wingspan", 2019));

        // Act
        var result = await service.ExecuteNightlyCatalogingAsync();

        // Assert
        Assert.Equal(1, result.TotalCatalogedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Single(gameRepo.Games);

        var failedItem = pendingRepo.Items.First(i => i.BggId == 501);
        Assert.Equal(CatalogQueueStatus.Failed, failedItem.Status);
        Assert.NotNull(failedItem.ErrorMessage);

        var successItem = pendingRepo.Items.First(i => i.BggId == 502);
        Assert.Equal(CatalogQueueStatus.Completed, successItem.Status);

        var savedLog = Assert.Single(logRepo.Logs);
        Assert.Equal(1, savedLog.FailedCount);
    }

    [Fact]
    public async Task ExecuteNightlyCatalogingAsync_LinksPendingReleasesRetrospectively()
    {
        // Arrange
        var service = CreateService(out var pendingRepo, out var bggClient, out _, out _, out var releaseRepo, out var newsExtractor, out _, limit: 2);

        var game = CreateDummyGame(601, "Apiary");
        bggClient.Games[601] = game;
        pendingRepo.Items.Add(new PendingBggImport(601, "Apiary", 2023));

        var release = new WeeklyRelease("Devir anuncia Apiary", "Devir", new DateOnly(2026, 9, 20));
        releaseRepo.Releases.Add(release);
        newsExtractor.TitleToExtract = "Apiary";

        // Act
        await service.ExecuteNightlyCatalogingAsync();

        // Assert
        Assert.NotNull(release.GameId);
        Assert.Equal(game.Id, release.GameId);
    }

    [Fact]
    public async Task ExecuteNightlyCatalogingAsync_WithDiscoveryService_ExecutesPhase1_5AndPersistsBggDiscoveryCount()
    {
        // Arrange
        var fakeDiscovery = new FakeDiscoveryService { DiscoveredToReturn = 3 };
        var service = CreateService(out var pendingRepo, out var bggClient, out var gameRepo, out _, out _, out _, out var logRepo, limit: 5, discoveryService: fakeDiscovery);

        // Act
        var result = await service.ExecuteNightlyCatalogingAsync();

        // Assert
        Assert.Equal(3, result.BggDiscoveryCount);
        Assert.True(fakeDiscovery.WasCalled);

        var savedLog = Assert.Single(logRepo.Logs);
        Assert.Equal(3, savedLog.BggDiscoveryCount);
    }

    [Fact]
    public async Task ExecuteNightlyCatalogingAsync_WhenStagingIsEmpty_TriggersAutonomousSeeding()
    {
        // Arrange
        var fakeMassIngestion = new FakeMassIngestionService { TotalInStaging = 0 };
        var service = CreateService(
            out _, out _, out _, out _, out _, out _, out _,
            limit: 5,
            massIngestionService: fakeMassIngestion);

        // Act
        var result = await service.ExecuteNightlyCatalogingAsync();

        // Assert
        Assert.Equal(1, fakeMassIngestion.RunScheduledCallCount);
        Assert.Equal(1, fakeMassIngestion.DrainCallCount);
    }

    [Fact]
    public async Task ExecuteNightlyCatalogingAsync_WhenStagingHasItems_DoesNotTriggerAutonomousSeeding()
    {
        // Arrange
        var fakeMassIngestion = new FakeMassIngestionService { TotalInStaging = 500 };
        var service = CreateService(
            out _, out _, out _, out _, out _, out _, out _,
            limit: 5,
            massIngestionService: fakeMassIngestion);

        // Act
        var result = await service.ExecuteNightlyCatalogingAsync();

        // Assert
        Assert.Equal(0, fakeMassIngestion.RunScheduledCallCount);
        Assert.Equal(1, fakeMassIngestion.DrainCallCount);
    }

    private static NightlyCatalogingService CreateService(
        out FakePendingRepo pendingRepo,
        out FakeBggClient bggClient,
        out FakeGameRepo gameRepo,
        out FakeCollectionRepo collectionRepo,
        out FakeReleaseRepo releaseRepo,
        out FakeNewsExtractor newsExtractor,
        out FakeLogRepo logRepo,
        int limit = 20,
        IBggDiscoveryService? discoveryService = null,
        IBggMassIngestionService? massIngestionService = null)
    {
        pendingRepo = new FakePendingRepo();
        bggClient = new FakeBggClient();
        gameRepo = new FakeGameRepo();
        collectionRepo = new FakeCollectionRepo();
        releaseRepo = new FakeReleaseRepo();
        newsExtractor = new FakeNewsExtractor();
        logRepo = new FakeLogRepo();

        var options = Options.Create(new NightlyCatalogingOptions
        {
            DailyCatalogingLimit = limit,
            MinDelaySecondsBetweenCalls = 0.0, // Delay 0 para ejecución instantánea en tests
            ExecutionHourUtc = 3,
            Enabled = true
        });

        return new NightlyCatalogingService(
            pendingRepo,
            bggClient,
            gameRepo,
            collectionRepo,
            releaseRepo,
            newsExtractor,
            logRepo,
            options,
            NullLogger<NightlyCatalogingService>.Instance,
            aiSummaryService: null,
            massIngestionService: massIngestionService,
            discoveryService: discoveryService
        );
    }

    private static Game CreateDummyGame(int bggId, string title)
    {
        return new Game(
            bggId: bggId,
            originalTitle: title,
            spanishTitle: title,
            designer: "Autor Test",
            publisher: "Editorial Test",
            yearPublished: 2022,
            coverImageUrl: "https://example.com/cover.jpg",
            thumbnailUrl: null,
            description: "Descripción de prueba",
            bggRating: 8.0,
            bggRank: 10,
            ludistRating: 8.0,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: true,
            age: new AgeRating(10, 10),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(30, 60, 20)
        );
    }

    private class FakePendingRepo : IPendingBggImportRepository
    {
        public List<PendingBggImport> Items = [];

        public Task<PendingBggImport?> GetByBggIdAsync(int bggId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(i => i.BggId == bggId));

        public Task<IReadOnlyList<PendingBggImport>> GetTopPendingAsync(int limit = 50, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PendingBggImport>>(Items.Where(i => i.Status == CatalogQueueStatus.Pending).Take(limit).ToList());

        public Task<IReadOnlyList<PendingBggImport>> GetAllAsync(CatalogQueueStatus? status = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PendingBggImport>>(status.HasValue ? Items.Where(i => i.Status == status.Value).ToList() : Items);

        public Task AddAsync(PendingBggImport item, CancellationToken ct = default)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(PendingBggImport item, CancellationToken ct = default) => Task.CompletedTask;
        public Task ResetFailedToPendingAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetTotalPendingCountAsync(CancellationToken ct = default) =>
            Task.FromResult(Items.Count(i => i.Status == CatalogQueueStatus.Pending));
    }

    private class FakeBggClient : IBggClient
    {
        public Dictionary<int, Game?> Games = [];
        public List<BggTopGameDto> TopGames = [];

        public Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default) =>
            Task.FromResult(Games.GetValueOrDefault(bggId));

        public Task<IReadOnlyList<BggCollectionItemDto>> FetchUserCollectionAsync(string username, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BggCollectionItemDto>>([]);

        public Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BggSearchResultDto>>([]);

        public Task<IReadOnlyList<BggTopGameDto>> FetchTopGamesAsync(int limit = 50, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BggTopGameDto>>(TopGames.Take(limit).ToList());
    }

    private class FakeGameRepo : IGameRepository
    {
        public List<Game> Games = [];

        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Games.FirstOrDefault(g => g.Id == id));

        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult(Games.FirstOrDefault(g => g.Slug == slug));

        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default) =>
            Task.FromResult(Games.FirstOrDefault(g => g.BggId == bggId));

        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
            Task.FromResult(((IReadOnlyList<Game>)Games, Games.Count));

        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default)
        {
            Games.AddRange(games);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Game game, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> HasAnyAsync(CancellationToken ct = default) => Task.FromResult(Games.Count > 0);
    }

    private class FakeCollectionRepo : IUserCollectionRepository
    {
        public Task PromotePendingItemsAsync(int bggId, Guid gameId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<UserCollectionItem?> GetByUserAndGameAsync(string userId, Guid gameId, CancellationToken cancellationToken = default) => Task.FromResult<UserCollectionItem?>(null);
        public Task<UserCollectionItem?> GetByUserAndBggIdAsync(string userId, int bggId, CancellationToken cancellationToken = default) => Task.FromResult<UserCollectionItem?>(null);
        public Task<List<UserCollectionItem>> GetByUserIdAsync(string userId, CollectionStatus? status = null, CancellationToken cancellationToken = default) => Task.FromResult<List<UserCollectionItem>>([]);
        public Task<List<UserCollectionItem>> GetPendingItemsByBggIdAsync(int bggId, CancellationToken cancellationToken = default) => Task.FromResult<List<UserCollectionItem>>([]);
        public Task<Dictionary<CollectionStatus, int>> GetCountsByStatusAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult(new Dictionary<CollectionStatus, int>());
        public Task AddAsync(UserCollectionItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(UserCollectionItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(UserCollectionItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private class FakeReleaseRepo : IWeeklyReleaseRepository
    {
        public List<WeeklyRelease> Releases = [];

        public Task<IReadOnlyList<WeeklyRelease>> GetReleasesAsync(DateOnly? fromDate = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WeeklyRelease>>(Releases);

        public Task<WeeklyRelease?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Releases.FirstOrDefault(r => r.Id == id));

        public Task AddAsync(WeeklyRelease release, CancellationToken ct = default)
        {
            Releases.Add(release);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(WeeklyRelease release, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            Releases.RemoveAll(r => r.Id == id);
            return Task.CompletedTask;
        }
    }

    private class FakeNewsExtractor : INewsGameExtractor
    {
        public string? TitleToExtract = null;

        public string? ExtractGameTitle(string newsTitle, string? newsNotes = null) => TitleToExtract;
        public Task<NewsExtractionResultDto> ProcessReleaseAsync(WeeklyRelease release, CancellationToken ct = default) =>
            Task.FromResult(new NewsExtractionResultDto(release.Id, TitleToExtract, false, null, false, null));
        public Task<int> DiscoverAndEnqueueFromReleasesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    private class FakeLogRepo : INightlyCatalogingLogRepository
    {
        public List<NightlyCatalogingExecutionLog> Logs = [];

        public Task AddAsync(NightlyCatalogingExecutionLog log, CancellationToken ct = default)
        {
            Logs.Add(log);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(NightlyCatalogingExecutionLog log, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<NightlyCatalogingExecutionLog>> GetRecentLogsAsync(int limit = 20, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<NightlyCatalogingExecutionLog>>(Logs.OrderByDescending(l => l.StartedAt).Take(limit).ToList());

        public Task<NightlyCatalogingExecutionLog?> GetLatestLogAsync(CancellationToken ct = default) =>
            Task.FromResult(Logs.OrderByDescending(l => l.StartedAt).FirstOrDefault());
    }

    private class FakeDiscoveryService : IBggDiscoveryService
    {
        public bool WasCalled { get; private set; }
        public int DiscoveredToReturn { get; set; } = 0;

        public Task<BggDiscoveryResultDto> DiscoverAndEnqueueBggTrendsAsync(int maxItems = 50, CancellationToken ct = default)
        {
            WasCalled = true;
            return Task.FromResult(new BggDiscoveryResultDto(maxItems, DiscoveredToReturn, DiscoveredToReturn, 0, 0, []));
        }

        public Task<BggDiscoveryResultDto> RunBggTrendsDiscoveryAsync(int maxItems = 50, CancellationToken ct = default)
        {
            WasCalled = true;
            return Task.FromResult(new BggDiscoveryResultDto(maxItems, DiscoveredToReturn, DiscoveredToReturn, 0, 0, []));
        }

        public Task<BggDiscoveryResultDto> DiscoverAndEnqueueNewReleasesAsync(int? targetYear = null, int maxItems = 50, CancellationToken ct = default)
        {
            WasCalled = true;
            return Task.FromResult(new BggDiscoveryResultDto(maxItems, DiscoveredToReturn, DiscoveredToReturn, 0, 0, []));
        }
    }

    private class FakeMassIngestionService : IBggMassIngestionService
    {
        public int TotalInStaging { get; set; } = 0;
        public int RunScheduledCallCount { get; private set; }
        public int DrainCallCount { get; private set; }

        public Task<BggStagingMetricsDto> GetMetricsAsync(CancellationToken ct = default) =>
            Task.FromResult(new BggStagingMetricsDto(
                TotalInStaging: TotalInStaging,
                PendingFetchCount: TotalInStaging,
                FetchedCount: 0,
                PendingImagesCount: 0,
                ImagesCompletedCount: 0,
                PendingAiCount: 0,
                AiCompletedCount: 0,
                AiQuotaExceededCount: 0,
                PendingPromotionCount: 0,
                PromotedCount: 0,
                FailedCount: 0
            ));

        public Task<int> RunScheduledDownloadAndIngestLatestRanksAsync(int? minUsersRated = null, CancellationToken ct = default)
        {
            RunScheduledCallCount++;
            return Task.FromResult(100);
        }

        public Task<BggMassIngestionCycleResultDto> RunScheduledDrainCycleAsync(CancellationToken ct = default)
        {
            DrainCallCount++;
            return Task.FromResult(new BggMassIngestionCycleResultDto(0, 0, 0, 0, false, "Drain OK"));
        }

        public Task<int> DownloadAndIngestLatestRanksAsync(int? minUsersRated = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> IngestRanksDumpAsync(System.IO.Stream dumpStream, int minUsersRated = 1000, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> ProcessPendingDetailsBatchAsync(int batchSize = 20, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> ProcessPendingImagesBatchAsync(int batchSize = 10, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AiBatchProcessingResultDto> ProcessPendingAiBatchAsync(int gamesPerBatch = 8, int maxBatches = 5, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> PromoteReadyToCatalogBatchAsync(int batchSize = 50, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<BggMassIngestionCycleResultDto> RunDrainCycleAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task ClearStagingAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> ResetQuotaExceededStatusAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task<BggMassIngestionContinuousDrainResultDto> RunContinuousDrainAsync(int maxItems = 4000, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<BggMassIngestionContinuousDrainResultDto> RunScheduledContinuousDrainAsync(int maxItems = 4000, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
