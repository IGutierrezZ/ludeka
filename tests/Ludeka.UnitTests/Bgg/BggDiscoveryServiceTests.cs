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
using Xunit;

namespace Ludeka.UnitTests.Bgg;

public class BggDiscoveryServiceTests
{
    [Fact]
    public async Task DiscoverAndEnqueueBggTrendsAsync_WhenCandidatesAreNew_EnqueuesWithCorrectOrigin()
    {
        // Arrange
        int currentYear = DateTime.UtcNow.Year;
        var bggClient = new FakeBggClient();
        bggClient.TopGames =
        [
            new BggTopGameDto(501, "Super Hot 2026", 1, currentYear, "https://example.com/thumb1.jpg"),
            new BggTopGameDto(502, "Vintage Hit 2018", 2, 2018, "https://example.com/thumb2.jpg")
        ];

        var gameRepo = new FakeGameRepo();
        var pendingRepo = new FakePendingRepo();
        var service = new BggDiscoveryService(bggClient, gameRepo, pendingRepo, NullLogger<BggDiscoveryService>.Instance);

        // Act
        var result = await service.DiscoverAndEnqueueBggTrendsAsync(50);

        // Assert
        Assert.Equal(2, result.TotalScanned);
        Assert.Equal(2, result.DiscoveredCount);
        Assert.Equal(2, result.EnqueuedCount);
        Assert.Equal(0, result.AlreadyCatalogedCount);
        Assert.Equal(0, result.AlreadyInQueueCount);
        Assert.Equal(2, result.EnqueuedTitles.Count);

        Assert.Equal(2, pendingRepo.Items.Count);
        var item1 = pendingRepo.Items.First(i => i.BggId == 501);
        var item2 = pendingRepo.Items.First(i => i.BggId == 502);

        // Lanzamiento reciente del año -> BggNewReleases
        Assert.Equal(CatalogQueueOrigin.BggNewReleases, item1.Origin);
        Assert.Equal(currentYear, item1.YearPublished);
        Assert.Equal("Super Hot 2026", item1.Title);

        // Tendencia de año anterior -> BggHotness
        Assert.Equal(CatalogQueueOrigin.BggHotness, item2.Origin);
        Assert.Equal(2018, item2.YearPublished);
        Assert.Equal("Vintage Hit 2018", item2.Title);
    }

    [Fact]
    public async Task DiscoverAndEnqueueBggTrendsAsync_WhenGameAlreadyInCatalog_SkipsAndCountsAsAlreadyCataloged()
    {
        // Arrange
        var bggClient = new FakeBggClient();
        bggClient.TopGames =
        [
            new BggTopGameDto(13, "Catan", 1, 1995, null),
            new BggTopGameDto(601, "Nuevo Inédito", 2, 2026, null)
        ];

        var gameRepo = new FakeGameRepo();
        gameRepo.Games.Add(CreateDummyGame(13, "Catan"));

        var pendingRepo = new FakePendingRepo();
        var service = new BggDiscoveryService(bggClient, gameRepo, pendingRepo, NullLogger<BggDiscoveryService>.Instance);

        // Act
        var result = await service.DiscoverAndEnqueueBggTrendsAsync(50);

        // Assert
        Assert.Equal(2, result.TotalScanned);
        Assert.Equal(1, result.DiscoveredCount);
        Assert.Equal(1, result.EnqueuedCount);
        Assert.Equal(1, result.AlreadyCatalogedCount);
        Assert.Equal(0, result.AlreadyInQueueCount);

        var enqueued = Assert.Single(pendingRepo.Items);
        Assert.Equal(601, enqueued.BggId);
    }

    [Fact]
    public async Task DiscoverAndEnqueueBggTrendsAsync_WhenGameAlreadyInQueue_SkipsAndCountsAsAlreadyInQueue()
    {
        // Arrange
        var bggClient = new FakeBggClient();
        bggClient.TopGames =
        [
            new BggTopGameDto(701, "Ya En Cola", 1, 2026, null),
            new BggTopGameDto(702, "Completamente Nuevo", 2, 2026, null)
        ];

        var gameRepo = new FakeGameRepo();
        var pendingRepo = new FakePendingRepo();
        pendingRepo.Items.Add(new PendingBggImport(701, "Ya En Cola", 2026, origin: CatalogQueueOrigin.UserImport));

        var service = new BggDiscoveryService(bggClient, gameRepo, pendingRepo, NullLogger<BggDiscoveryService>.Instance);

        // Act
        var result = await service.DiscoverAndEnqueueBggTrendsAsync(50);

        // Assert
        Assert.Equal(2, result.TotalScanned);
        Assert.Equal(1, result.DiscoveredCount);
        Assert.Equal(1, result.EnqueuedCount);
        Assert.Equal(0, result.AlreadyCatalogedCount);
        Assert.Equal(1, result.AlreadyInQueueCount);

        Assert.Equal(2, pendingRepo.Items.Count);
        Assert.Contains(pendingRepo.Items, i => i.BggId == 702 && i.Origin == CatalogQueueOrigin.BggNewReleases);
    }

    [Fact]
    public async Task DiscoverAndEnqueueBggTrendsAsync_WhenGameInStaging_SkipsAndCountsAsAlreadyInQueue()
    {
        // Arrange
        var bggClient = new FakeBggClient();
        bggClient.TopGames =
        [
            new BggTopGameDto(801, "En Staging Masivo", 1, 2024, null)
        ];

        var gameRepo = new FakeGameRepo();
        var pendingRepo = new FakePendingRepo();
        var stagingRepo = new FakeStagingRepo();
        stagingRepo.Items.Add(new BggCatalogStagingItem(801, "En Staging Masivo", 100, 1));

        var service = new BggDiscoveryService(bggClient, gameRepo, pendingRepo, NullLogger<BggDiscoveryService>.Instance, stagingRepo);

        // Act
        var result = await service.DiscoverAndEnqueueBggTrendsAsync(50);

        // Assert
        Assert.Equal(1, result.TotalScanned);
        Assert.Equal(0, result.DiscoveredCount);
        Assert.Equal(0, result.EnqueuedCount);
        Assert.Equal(1, result.AlreadyInQueueCount);
        Assert.Empty(pendingRepo.Items);
    }

    [Fact]
    public async Task DiscoverAndEnqueueNewReleasesAsync_FiltersOutOlderReleases()
    {
        // Arrange
        int targetYear = 2026;
        var bggClient = new FakeBggClient();
        bggClient.TopGames =
        [
            new BggTopGameDto(901, "Hit 2026", 1, 2026, null),
            new BggTopGameDto(902, "Hit 2025", 2, 2025, null),
            new BggTopGameDto(903, "Clásico 2012", 3, 2012, null)
        ];

        var gameRepo = new FakeGameRepo();
        var pendingRepo = new FakePendingRepo();
        var service = new BggDiscoveryService(bggClient, gameRepo, pendingRepo, NullLogger<BggDiscoveryService>.Instance);

        // Act
        var result = await service.DiscoverAndEnqueueNewReleasesAsync(targetYear: targetYear, maxItems: 50);

        // Assert: Solo deben encolarse los de 2025 o 2026 (targetYear y targetYear - 1)
        Assert.Equal(3, result.TotalScanned);
        Assert.Equal(2, result.DiscoveredCount);
        Assert.Equal(2, result.EnqueuedCount);
        Assert.DoesNotContain(pendingRepo.Items, i => i.BggId == 903);
        Assert.Contains(pendingRepo.Items, i => i.BggId == 901 && i.Origin == CatalogQueueOrigin.BggNewReleases);
        Assert.Contains(pendingRepo.Items, i => i.BggId == 902 && i.Origin == CatalogQueueOrigin.BggNewReleases);
    }

    private static Game CreateDummyGame(int bggId, string title)
    {
        return new Game(
            bggId: bggId,
            originalTitle: title,
            spanishTitle: title,
            designer: "Autor Test",
            publisher: "Editorial Test",
            yearPublished: 2020,
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

    private class FakeBggClient : IBggClient
    {
        public List<BggTopGameDto> TopGames = [];

        public Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default) => Task.FromResult<Game?>(null);
        public Task<IReadOnlyList<BggCollectionItemDto>> FetchUserCollectionAsync(string username, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BggCollectionItemDto>>([]);
        public Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BggSearchResultDto>>([]);
        public Task<IReadOnlyList<BggTopGameDto>> FetchTopGamesAsync(int limit = 50, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BggTopGameDto>>(TopGames.Take(limit).ToList());
    }

    private class FakeGameRepo : IGameRepository
    {
        public List<Game> Games = [];

        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Games.FirstOrDefault(g => g.Id == id));
        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(Games.FirstOrDefault(g => g.Slug == slug));
        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default) => Task.FromResult(Games.FirstOrDefault(g => g.BggId == bggId));
        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default) => Task.FromResult(((IReadOnlyList<Game>)Games, Games.Count));
        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default)
        {
            Games.AddRange(games);
            return Task.CompletedTask;
        }
        public Task UpdateAsync(Game game, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> HasAnyAsync(CancellationToken ct = default) => Task.FromResult(Games.Count > 0);
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

        public Task<int> GetTotalPendingCountAsync(CancellationToken ct = default) =>
            Task.FromResult(Items.Count(i => i.Status == CatalogQueueStatus.Pending));

        public Task AddAsync(PendingBggImport item, CancellationToken ct = default)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(PendingBggImport item, CancellationToken ct = default) => Task.CompletedTask;
        public Task ResetFailedToPendingAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private class FakeStagingRepo : IBggCatalogStagingRepository
    {
        public List<BggCatalogStagingItem> Items = [];

        public Task UpsertBatchAsync(IEnumerable<BggCatalogStagingItem> items, CancellationToken ct = default)
        {
            Items.AddRange(items);
            return Task.CompletedTask;
        }

        public Task<BggCatalogStagingItem?> GetByBggIdAsync(int bggId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(i => i.BggId == bggId));

        public Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingFetchBatchAsync(int batchSize = 20, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BggCatalogStagingItem>>(Items.Take(batchSize).ToList());

        public Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingImagesBatchAsync(int batchSize = 10, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BggCatalogStagingItem>>(Items.Take(batchSize).ToList());

        public Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingAiBatchAsync(int batchSize = 10, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BggCatalogStagingItem>>(Items.Take(batchSize).ToList());

        public Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingPromotionBatchAsync(int batchSize = 50, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BggCatalogStagingItem>>(Items.Take(batchSize).ToList());

        public Task UpdateAsync(BggCatalogStagingItem item, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateBatchAsync(IEnumerable<BggCatalogStagingItem> items, CancellationToken ct = default) => Task.CompletedTask;

        public Task<BggStagingMetricsDto> GetMetricsAsync(CancellationToken ct = default) =>
            Task.FromResult(new BggStagingMetricsDto(Items.Count, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        public Task ResetQuotaExceededStatusAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<int> GetTotalCountAsync(CancellationToken ct = default) =>
            Task.FromResult(Items.Count);

        public Task ClearStagingAsync(CancellationToken ct = default)
        {
            Items.Clear();
            return Task.CompletedTask;
        }
    }
}
