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

namespace Ludeka.UnitTests.Application;

public class BggDiscoveryServiceTrendingTests
{
    private static Game CreateSampleGame(int bggId, string title)
    {
        return new Game(
            bggId: bggId,
            originalTitle: title,
            spanishTitle: title,
            designer: "Autor Test",
            publisher: "Editorial Test",
            yearPublished: 2025,
            coverImageUrl: "https://example.com/cover.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            description: "Descripción de prueba",
            bggRating: 8.5,
            bggRank: 10,
            ludistRating: 8.5,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: true,
            age: new AgeRating(12, 12),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(30, 60, 20)
        );
    }

    [Fact]
    public async Task RunDailyTrendingSyncAsync_WhenGameNotCataloged_IngestsImmediatelyEnsuresSnapshotAndEnrichesAi()
    {
        // Arrange
        var bggClient = new FakeBggClient();
        bggClient.TopGames =
        [
            new BggTopGameDto(801, "Juego Trending Nuevo", 1, 2026, "https://example.com/thumb801.jpg")
        ];
        var fetchedGame = CreateSampleGame(801, "Juego Trending Nuevo");
        bggClient.GamesById[801] = fetchedGame;

        var gameRepo = new FakeGameRepo();
        var pendingRepo = new FakePendingRepo();
        var trendingRepo = new FakeTrendingRepo();
        var snapshotSync = new FakeSnapshotSyncService();
        var aiSummary = new FakeAiSummaryService();
        var collectionRepo = new FakeUserCollectionRepo();

        var service = new BggDiscoveryService(
            bggClient,
            gameRepo,
            pendingRepo,
            NullLogger<BggDiscoveryService>.Instance,
            stagingRepo: null,
            permissionGuard: null,
            trendingRepo: trendingRepo,
            snapshotSyncService: snapshotSync,
            aiSummaryService: aiSummary,
            collectionRepo: collectionRepo
        );

        // Act
        var result = await service.RunDailyTrendingSyncAsync(50);

        // Assert
        Assert.Equal(1, result.TotalTrendingProcessed);
        Assert.Equal(0, result.AlreadyCatalogedCount);
        Assert.Equal(1, result.NewlyCatalogedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Contains("Juego Trending Nuevo", result.NewlyCatalogedTitles);

        // Snapshot satélite asegurado
        Assert.Contains(801, snapshotSync.EnsuredBggIds);

        // Resumen IA generado
        Assert.Contains(801, aiSummary.GeneratedSummaryBggIds);

        // Juego persistido en catálogo
        var savedGame = Assert.Single(gameRepo.Games);
        Assert.Equal(801, savedGame.BggId);
        Assert.NotNull(savedGame.AiSummary);

        // Tendencia diaria persistida y vinculada al juego
        var savedTrending = Assert.Single(trendingRepo.Items);
        Assert.Equal(1, savedTrending.Rank);
        Assert.Equal(801, savedTrending.BggId);
        Assert.Equal("Juego Trending Nuevo", savedTrending.Title);
        Assert.Equal(savedGame.Id, savedTrending.GameId);
    }

    [Fact]
    public async Task RunDailyTrendingSyncAsync_WhenGameAlreadyCataloged_LinksDirectlyWithoutFetchingFullGame()
    {
        // Arrange
        var existingGame = CreateSampleGame(901, "Juego Ya Existente");
        existingGame.SetAiSummary(new AiGameSummary("Veredicto previo", "2-4", "10+", "Mesa", "gemini", DateTime.UtcNow));

        var bggClient = new FakeBggClient();
        bggClient.TopGames =
        [
            new BggTopGameDto(901, "Juego Ya Existente", 1, 2025, "https://example.com/thumb901.jpg")
        ];

        var gameRepo = new FakeGameRepo();
        gameRepo.Games.Add(existingGame);

        var pendingRepo = new FakePendingRepo();
        var trendingRepo = new FakeTrendingRepo();
        var snapshotSync = new FakeSnapshotSyncService();
        var aiSummary = new FakeAiSummaryService();

        var service = new BggDiscoveryService(
            bggClient,
            gameRepo,
            pendingRepo,
            NullLogger<BggDiscoveryService>.Instance,
            trendingRepo: trendingRepo,
            snapshotSyncService: snapshotSync,
            aiSummaryService: aiSummary
        );

        // Act
        var result = await service.RunDailyTrendingSyncAsync(50);

        // Assert
        Assert.Equal(1, result.TotalTrendingProcessed);
        Assert.Equal(1, result.AlreadyCatalogedCount);
        Assert.Equal(0, result.NewlyCatalogedCount);
        Assert.Equal(0, result.FailedCount);

        // No se solicitó fetch completo
        Assert.Empty(bggClient.FetchedGameIds);

        // Tendencia diaria guardada con el ID del juego existente
        var savedTrending = Assert.Single(trendingRepo.Items);
        Assert.Equal(901, savedTrending.BggId);
        Assert.Equal(existingGame.Id, savedTrending.GameId);
    }

    [Fact]
    public async Task RunDailyTrendingSyncAsync_WhenBggFails_HandlesGracefully()
    {
        // Arrange
        var bggClient = new FakeBggClient { ThrowOnFetchTop = true };
        var service = new BggDiscoveryService(
            bggClient,
            new FakeGameRepo(),
            new FakePendingRepo(),
            NullLogger<BggDiscoveryService>.Instance
        );

        // Act
        var result = await service.RunDailyTrendingSyncAsync(50);

        // Assert
        Assert.Equal(0, result.TotalTrendingProcessed);
        Assert.Equal(0, result.NewlyCatalogedCount);
    }

    private class FakeBggClient : IBggClient
    {
        public List<BggTopGameDto> TopGames = [];
        public Dictionary<int, Game> GamesById = [];
        public List<int> FetchedGameIds = [];
        public bool ThrowOnFetchTop = false;

        public Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default)
        {
            FetchedGameIds.Add(bggId);
            GamesById.TryGetValue(bggId, out var game);
            return Task.FromResult(game);
        }

        public Task<IReadOnlyList<BggCollectionItemDto>> FetchUserCollectionAsync(string username, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BggCollectionItemDto>>([]);
        public Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BggSearchResultDto>>([]);

        public Task<IReadOnlyList<BggTopGameDto>> FetchTopGamesAsync(int limit = 50, CancellationToken ct = default)
        {
            if (ThrowOnFetchTop)
                throw new InvalidOperationException("Simulated BGG timeout");
            return Task.FromResult<IReadOnlyList<BggTopGameDto>>(TopGames.Take(limit).ToList());
        }
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

    private class FakeTrendingRepo : IDailyTrendingGameRepository
    {
        public List<DailyTrendingGame> Items = [];

        public Task<DateOnly?> GetLatestDateAsync(CancellationToken ct = default) =>
            Task.FromResult(Items.Select(i => (DateOnly?)i.DateUtc).Max());

        public Task<DateOnly?> GetPreviousDateAsync(DateOnly dateUtc, CancellationToken ct = default) =>
            Task.FromResult(Items.Where(i => i.DateUtc < dateUtc).Select(i => (DateOnly?)i.DateUtc).Max());

        public Task<IReadOnlyList<DailyTrendingGame>> GetLatestTrendingAsync(int limit = 50, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DailyTrendingGame>>(Items.OrderBy(i => i.Rank).Take(limit).ToList());

        public Task<IReadOnlyList<DailyTrendingGame>> GetTrendingByDateAsync(DateOnly dateUtc, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DailyTrendingGame>>(Items.Where(i => i.DateUtc == dateUtc).OrderBy(i => i.Rank).ToList());

        public Task LinkGameAsync(int bggId, Guid gameId, CancellationToken ct = default)
        {
            foreach (var item in Items.Where(i => i.BggId == bggId))
            {
                item.LinkToGame(gameId);
            }
            return Task.CompletedTask;
        }

        public Task UpsertDailyTrendingBatchAsync(IEnumerable<DailyTrendingGame> items, CancellationToken ct = default)
        {
            Items.AddRange(items);
            return Task.CompletedTask;
        }
    }

    private class FakeSnapshotSyncService : IBggRawSnapshotSyncService
    {
        public List<int> EnsuredBggIds = [];

        public Task<bool> EnsureSnapshotAsync(int bggId, CancellationToken ct = default)
        {
            EnsuredBggIds.Add(bggId);
            return Task.FromResult(true);
        }

        public Task<BggRawSnapshotStatusDto> GetStatusAsync(CancellationToken ct = default)
            => Task.FromResult(new BggRawSnapshotStatusDto(100, 50, 50, 10, 5, 5));

        public Task<BggRawSnapshotSyncResultDto> SyncBatchAsync(int batchSize = 20, int delayMs = 1200, CancellationToken ct = default)
            => Task.FromResult(new BggRawSnapshotSyncResultDto(0, 0, 0, [], [], "Fin"));

        public Task<BggRawSnapshotSyncResultDto> RunScheduledSyncBatchAsync(int batchSize = 20, int delayMs = 1200, CancellationToken ct = default)
            => Task.FromResult(new BggRawSnapshotSyncResultDto(0, 0, 0, [], [], "Fin"));

        public Task<BggExpansionDiscoveryResultDto> DiscoverAndEnqueueMissingExpansionsAsync(int maxToEnqueue = 50, CancellationToken ct = default)
            => Task.FromResult(new BggExpansionDiscoveryResultDto(10, 8, ["Exp 1", "Exp 2"]));

        public Task<BggExpansionDiscoveryResultDto> RunScheduledDiscoverAndEnqueueMissingExpansionsAsync(int maxToEnqueue = 50, CancellationToken ct = default)
            => Task.FromResult(new BggExpansionDiscoveryResultDto(10, 8, ["Exp 1", "Exp 2"]));

        public Task<int> AutoLinkExistingExpansionsAsync(CancellationToken ct = default)
            => Task.FromResult(0);

        public Task<int> RunScheduledAutoLinkExistingExpansionsAsync(CancellationToken ct = default)
            => Task.FromResult(0);
    }

    private class FakeAiSummaryService : IAiGameSummaryService
    {
        public List<int> GeneratedSummaryBggIds = [];

        public Task<AiGameSummaryDto> GenerateSummaryAsync(Game game, CancellationToken ct = default)
        {
            GeneratedSummaryBggIds.Add(game.BggId);
            return Task.FromResult(new AiGameSummaryDto(
                game.Id,
                game.SpanishTitle,
                "Ideal a 4",
                "Desde 10 años",
                "Mesa estándar",
                $"Síntesis generada para {game.SpanishTitle}",
                "Fake AI Model",
                DateTime.UtcNow
            ));
        }

        public Task<AiGameSummaryDto> EnsureSummaryForGameAsync(Guid gameId, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<AiBatchProcessingResultDto> ProcessPendingSummariesBatchAsync(int batchSize = 20, CancellationToken ct = default) =>
            Task.FromResult(new AiBatchProcessingResultDto(0, 0, 0, []));

        public Task<AiBatchResultDto> GenerateBatchSummariesAsync(IReadOnlyList<AiGameBatchInputDto> games, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private class FakeUserCollectionRepo : IUserCollectionRepository
    {
        public List<(int BggId, Guid GameId)> PromotedItems = [];
        public List<UserCollectionItem> Items = [];

        public Task AddAsync(UserCollectionItem item, CancellationToken ct = default)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task<UserCollectionItem?> GetByUserAndGameAsync(string userId, Guid gameId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(i => i.UserId == userId && i.GameId == gameId));

        public Task<UserCollectionItem?> GetByUserAndBggIdAsync(string userId, int bggId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(i => i.UserId == userId && i.BggId == bggId));

        public Task<List<UserCollectionItem>> GetByUserIdAsync(string userId, CollectionStatus? status = null, CancellationToken ct = default) =>
            Task.FromResult(Items.Where(i => i.UserId == userId).ToList());

        public Task<List<UserCollectionItem>> GetPendingItemsByBggIdAsync(int bggId, CancellationToken ct = default) =>
            Task.FromResult(Items.Where(i => i.BggId == bggId && i.GameId == null).ToList());

        public Task PromotePendingItemsAsync(int bggId, Guid gameId, CancellationToken ct = default)
        {
            PromotedItems.Add((bggId, gameId));
            return Task.CompletedTask;
        }

        public Task<List<UserCollectionItem>> GetPlayedByUserIdAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult(Items.Where(i => i.UserId == userId && i.IsPlayed).ToList());

        public Task<int> GetPlayedCountAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult(Items.Count(i => i.UserId == userId && i.IsPlayed));

        public Task<Dictionary<CollectionStatus, int>> GetCountsByStatusAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<CollectionStatus, int>());

        public Task UpdateAsync(UserCollectionItem item, CancellationToken ct = default) => Task.CompletedTask;

        public Task RemoveAsync(UserCollectionItem item, CancellationToken ct = default)
        {
            Items.Remove(item);
            return Task.CompletedTask;
        }
    }
}
