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
using Xunit;

namespace Ludeka.UnitTests.Application;

public class BggCatalogQueueServiceTests
{
    private class FakePendingRepo : IPendingBggImportRepository
    {
        public List<PendingBggImport> Items = [];

        public Task<PendingBggImport?> GetByBggIdAsync(int bggId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(i => i.BggId == bggId));

        public Task<IReadOnlyList<PendingBggImport>> GetTopPendingAsync(int limit = 50, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<PendingBggImport>)Items.Where(i => i.Status == CatalogQueueStatus.Pending || i.Status == CatalogQueueStatus.Failed).OrderBy(i => i.Status == CatalogQueueStatus.Failed ? 1 : 0).ThenByDescending(i => i.RequestedCount).Take(limit).ToList());

        public Task<IReadOnlyList<PendingBggImport>> GetAllAsync(CatalogQueueStatus? status = null, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<PendingBggImport>)(status.HasValue ? Items.Where(i => i.Status == status.Value).ToList() : Items.ToList()));

        public Task<int> GetTotalPendingCountAsync(CancellationToken ct = default) =>
            Task.FromResult(Items.Count(i => i.Status == CatalogQueueStatus.Pending || i.Status == CatalogQueueStatus.Failed));

        public Task AddAsync(PendingBggImport item, CancellationToken ct = default)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(PendingBggImport item, CancellationToken ct = default) => Task.CompletedTask;

        public Task ResetFailedToPendingAsync(CancellationToken ct = default)
        {
            foreach (var item in Items.Where(i => i.Status == CatalogQueueStatus.Failed))
            {
                item.ResetToPending();
            }
            return Task.CompletedTask;
        }
    }

    private class FakeBggClient : IBggClient
    {
        public Dictionary<int, Game?> Games = [];

        public Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default) =>
            Task.FromResult(Games.GetValueOrDefault(bggId));

        public Task<IReadOnlyList<BggCollectionItemDto>> FetchUserCollectionAsync(string username, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BggCollectionItemDto>>([]);

        public Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BggSearchResultDto>>([]);

        public Task<IReadOnlyList<BggTopGameDto>> FetchTopGamesAsync(int limit = 50, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BggTopGameDto>>([]);
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
        public List<UserCollectionItem> Items = [];

        public Task<UserCollectionItem?> GetByUserAndGameAsync(string userId, Guid gameId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(i => i.UserId == userId && i.GameId == gameId));

        public Task<UserCollectionItem?> GetByUserAndBggIdAsync(string userId, int bggId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(i => i.UserId == userId && (i.BggId == bggId || (i.Game != null && i.Game.BggId == bggId))));

        public Task<List<UserCollectionItem>> GetByUserIdAsync(string userId, CollectionStatus? status = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(status.HasValue ? Items.Where(i => i.UserId == userId && i.Status == status.Value).ToList() : Items.Where(i => i.UserId == userId).ToList());

        public Task<List<UserCollectionItem>> GetPendingItemsByBggIdAsync(int bggId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Where(i => i.BggId == bggId && i.GameId == null).ToList());

        public Task PromotePendingItemsAsync(int bggId, Guid gameId, CancellationToken cancellationToken = default)
        {
            foreach (var item in Items.Where(i => i.BggId == bggId && i.GameId == null))
            {
                item.PromoteToCataloged(gameId);
            }
            return Task.CompletedTask;
        }

        public Task<List<UserCollectionItem>> GetPlayedByUserIdAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Where(i => i.UserId == userId && i.IsPlayed).ToList());

        public Task<int> GetPlayedCountAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Count(i => i.UserId == userId && i.IsPlayed));

        public Task<Dictionary<CollectionStatus, int>> GetCountsByStatusAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Where(i => i.UserId == userId && i.Status.HasValue).GroupBy(i => i.Status!.Value).ToDictionary(g => g.Key, g => g.Count()));

        public Task AddAsync(UserCollectionItem item, CancellationToken cancellationToken = default)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(UserCollectionItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveAsync(UserCollectionItem item, CancellationToken cancellationToken = default)
        {
            Items.Remove(item);
            return Task.CompletedTask;
        }
    }

    private static Game CreateGame(int bggId, string title)
    {
        return new Game(
            bggId: bggId,
            originalTitle: title,
            spanishTitle: title,
            designer: "Autor",
            publisher: "Editorial",
            yearPublished: 2021,
            coverImageUrl: null,
            thumbnailUrl: null,
            description: null,
            bggRating: 8.5,
            bggRank: 4,
            ludistRating: 8.5,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: true,
            age: new AgeRating(14, 14),
            language: LanguageDependence.Low,
            footprint: TableFootprint.TableMonster,
            duration: new GameDuration(90, 150, 45)
        );
    }

    [Fact]
    public async Task ProcessPendingQueueBatchAsync_CatalogsGameAndPromotesPendingUsers()
    {
        // Arrange
        var pendingRepo = new FakePendingRepo();
        var pendingArkNova = new PendingBggImport(342942, "Ark Nova");
        pendingArkNova.IncrementRequestCount(); // 2 solicitudes
        pendingRepo.Items.Add(pendingArkNova);

        var game = CreateGame(342942, "Ark Nova");
        var bggClient = new FakeBggClient();
        bggClient.Games[342942] = game;

        var gameRepo = new FakeGameRepo();
        var collectionRepo = new FakeCollectionRepo();

        // 2 usuarios esperando el juego
        var user1Item = new UserCollectionItem("user-1", 342942, "Ark Nova", CollectionStatus.InCollection);
        var user2Item = new UserCollectionItem("user-2", 342942, "Ark Nova", CollectionStatus.Wishlist);
        collectionRepo.Items.Add(user1Item);
        collectionRepo.Items.Add(user2Item);

        var service = new BggCatalogQueueService(pendingRepo, bggClient, gameRepo, collectionRepo);

        // Act
        var result = await service.ProcessPendingQueueBatchAsync(10);

        // Assert
        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Contains("Ark Nova", result.CatalogedGameTitles);

        // El juego se ha añadido a la base de datos
        Assert.Single(gameRepo.Games);
        Assert.Equal(342942, gameRepo.Games[0].BggId);

        // Los usuarios han sido promovidos de forma atómica
        Assert.False(user1Item.IsPendingCataloging);
        Assert.Equal(game.Id, user1Item.GameId);
        Assert.False(user2Item.IsPendingCataloging);
        Assert.Equal(game.Id, user2Item.GameId);

        // La cola se marca como completada
        Assert.Equal(CatalogQueueStatus.Completed, pendingArkNova.Status);
        Assert.NotNull(pendingArkNova.ProcessedAt);
    }

    [Fact]
    public async Task ProcessPendingQueueBatchAsync_WhenBggFails_MarksAsFailedAndDoesNotCreateDummyGame()
    {
        // Arrange
        var pendingRepo = new FakePendingRepo();
        var pendingItem = new PendingBggImport(99999, "Juego Desconocido");
        pendingRepo.Items.Add(pendingItem);

        var bggClient = new FakeBggClient(); // No tiene el juego -> devolverá null
        var gameRepo = new FakeGameRepo();
        var collectionRepo = new FakeCollectionRepo();

        var service = new BggCatalogQueueService(pendingRepo, bggClient, gameRepo, collectionRepo);

        // Act
        var result = await service.ProcessPendingQueueBatchAsync(10);

        // Assert
        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Empty(result.CatalogedGameTitles);

        // Cero mocks: no se añade ningún juego ficticio a la base de datos
        Assert.Empty(gameRepo.Games);

        // El ítem se marca como Failed con el mensaje descriptivo y NO se pierde
        Assert.Equal(CatalogQueueStatus.Failed, pendingItem.Status);
        Assert.NotNull(pendingItem.ErrorMessage);
    }

    [Fact]
    public async Task ResetFailedItemsAsync_ResetsFailedItemsToPending()
    {
        // Arrange
        var pendingRepo = new FakePendingRepo();
        var pendingItem = new PendingBggImport(99999, "Juego Fallido");
        pendingItem.MarkAsFailed("Error previo");
        pendingRepo.Items.Add(pendingItem);

        var service = new BggCatalogQueueService(pendingRepo, new FakeBggClient(), new FakeGameRepo(), new FakeCollectionRepo());

        // Act
        await service.ResetFailedItemsAsync();

        // Assert
        Assert.Equal(CatalogQueueStatus.Pending, pendingItem.Status);
        Assert.Null(pendingItem.ErrorMessage);
    }

    [Fact]
    public async Task ProcessPendingQueueBatchAsync_WhenGameIsNew_GeneratesAndAttachesAiSummary()
    {
        // Arrange
        var pendingRepo = new FakePendingRepo();
        var pending = new PendingBggImport(13, "Catan");
        pendingRepo.Items.Add(pending);

        var bggClient = new FakeBggClient();
        var game = CreateGame(13, "Catan");
        bggClient.Games[13] = game;

        var gameRepo = new FakeGameRepo();
        var collectionRepo = new FakeCollectionRepo();
        var aiService = new FakeAiSummaryService();

        var service = new BggCatalogQueueService(pendingRepo, bggClient, gameRepo, collectionRepo, aiService);

        // Act
        var result = await service.ProcessPendingQueueBatchAsync(10);

        // Assert
        Assert.Equal(1, result.SuccessCount);
        Assert.Single(gameRepo.Games);
        var cataloged = gameRepo.Games[0];
        Assert.NotNull(cataloged.AiSummary);
        Assert.Equal("Síntesis generada para Catan", cataloged.AiSummary.GeneralVerdict);
        Assert.Equal("Fake AI Model", cataloged.AiSummary.Model);
    }

    private class FakeAiSummaryService : IAiGameSummaryService
    {
        public Task<AiGameSummaryDto> GenerateSummaryAsync(Game game, CancellationToken ct = default)
        {
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

        public Task<AiGameSummaryDto> EnsureSummaryForGameAsync(Guid gameId, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<AiBatchProcessingResultDto> ProcessPendingSummariesBatchAsync(int batchSize = 20, CancellationToken ct = default)
        {
            return Task.FromResult(new AiBatchProcessingResultDto(0, 0, 0, []));
        }

        public Task<AiBatchResultDto> GenerateBatchSummariesAsync(IReadOnlyList<AiGameBatchInputDto> games, CancellationToken ct = default)
        {
            var dict = new Dictionary<int, AiGameSummaryDto>();
            foreach (var g in games)
            {
                dict[g.BggId] = new AiGameSummaryDto(Guid.Empty, g.SpanishTitle, "Ideal 4", "10+", "Mesa", $"Síntesis para {g.SpanishTitle}", "FakeModel", DateTime.UtcNow);
            }
            return Task.FromResult(new AiBatchResultDto(true, false, dict, null));
        }
    }
}
