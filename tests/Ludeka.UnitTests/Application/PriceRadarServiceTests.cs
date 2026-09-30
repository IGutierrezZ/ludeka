using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Catalog;
using Ludeka.Application.Options;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class PriceRadarServiceTests
{
    private class FakeGamePriceRepository : IGamePriceRepository
    {
        public List<GamePriceSnapshot> Snapshots { get; } = new();

        public Task RecordSnapshotAsync(GamePriceSnapshot snapshot, CancellationToken ct = default)
        {
            Snapshots.Add(snapshot);
            return Task.CompletedTask;
        }

        public Task RecordSnapshotsBatchAsync(IEnumerable<GamePriceSnapshot> snapshots, CancellationToken ct = default)
        {
            Snapshots.AddRange(snapshots);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<GamePriceSnapshot>> GetHistoryAsync(Guid gameId, int limit = 50, CancellationToken ct = default)
        {
            var list = Snapshots.Where(s => s.GameId == gameId).OrderByDescending(s => s.RecordedAtUtc).Take(limit).ToList();
            return Task.FromResult<IReadOnlyList<GamePriceSnapshot>>(list);
        }

        public Task<GamePriceMetrics> GetMetricsAsync(Guid gameId, CancellationToken ct = default)
        {
            var list = Snapshots.Where(s => s.GameId == gameId).ToList();
            return Task.FromResult(GamePriceMetrics.Calculate(gameId, list));
        }

        public Task<IReadOnlyDictionary<Guid, GamePriceMetrics>> GetMetricsBatchAsync(IEnumerable<Guid> gameIds, CancellationToken ct = default)
        {
            var dict = gameIds.ToDictionary(
                id => id,
                id => GamePriceMetrics.Calculate(id, Snapshots.Where(s => s.GameId == id).ToList())
            );
            return Task.FromResult<IReadOnlyDictionary<Guid, GamePriceMetrics>>(dict);
        }

        public Task<IReadOnlyList<GamePriceSnapshot>> GetRecentSnapshotsAsync(int limit = 100, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<GamePriceSnapshot>>(Snapshots.OrderByDescending(s => s.RecordedAtUtc).Take(limit).ToList());
        }
    }

    private class FakeGameRepository : IGameRepository
    {
        public List<Game> Games { get; } = new();

        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Games.FirstOrDefault(g => g.Id == id));

        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default)
            => Task.FromResult(Games.FirstOrDefault(g => g.Slug == slug));

        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default)
            => Task.FromResult(Games.FirstOrDefault(g => g.BggId == bggId));

        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default)
            => Task.FromResult<(IReadOnlyList<Game>, int)>((Games, Games.Count));

        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default)
        {
            Games.AddRange(games);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Game game, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> HasAnyAsync(CancellationToken ct = default) => Task.FromResult(Games.Count > 0);
        public Task<IReadOnlyList<Game>> GetAllGamesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Game>>(Games);
        public Task<IReadOnlyList<Game>> GetGamesWithPurchaseLinksAsync(int? limit = null, CancellationToken ct = default)
        {
            var res = Games.Where(g => g.PurchaseLinks != null && g.PurchaseLinks.Count > 0);
            if (limit.HasValue && limit.Value > 0) res = res.Take(limit.Value);
            return Task.FromResult<IReadOnlyList<Game>>(res.ToList());
        }
    }

    private class FakeUserCollectionRepository : IUserCollectionRepository
    {
        public List<UserCollectionItem> Items { get; } = new();

        public Task<UserCollectionItem?> GetByUserAndGameAsync(string userId, Guid gameId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(i => i.UserId == userId && i.GameId == gameId));

        public Task<UserCollectionItem?> GetByUserAndBggIdAsync(string userId, int bggId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(i => i.UserId == userId && i.BggId == bggId));

        public Task<List<UserCollectionItem>> GetByUserIdAsync(string userId, CollectionStatus? status = null, CancellationToken cancellationToken = default)
        {
            var q = Items.Where(i => i.UserId == userId);
            if (status.HasValue) q = q.Where(i => i.Status == status.Value);
            return Task.FromResult(q.ToList());
        }

        public Task<List<UserCollectionItem>> GetPendingItemsByBggIdAsync(int bggId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Where(i => i.BggId == bggId && i.GameId == null).ToList());

        public Task PromotePendingItemsAsync(int bggId, Guid gameId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Dictionary<CollectionStatus, int>> GetCountsByStatusAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult(new Dictionary<CollectionStatus, int>());
        public Task AddAsync(UserCollectionItem item, CancellationToken cancellationToken = default) { Items.Add(item); return Task.CompletedTask; }
        public Task UpdateAsync(UserCollectionItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(UserCollectionItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private class FakeStoreStockService : IStoreStockService
    {
        public Dictionary<string, StoreStockInfo> Responses { get; } = new(StringComparer.OrdinalIgnoreCase);

        public ValueTask<StoreStockInfo> GetStockAsync(string storeName, string affiliateUrl, CancellationToken cancellationToken = default)
        {
            if (Responses.TryGetValue(affiliateUrl, out var info)) return ValueTask.FromResult(info);
            return ValueTask.FromResult(StoreStockInfo.InStock(10, 35m));
        }

        public ValueTask<IReadOnlyDictionary<string, StoreStockInfo>> GetStockBatchAsync(IEnumerable<GamePurchaseLink> offers, CancellationToken cancellationToken = default)
        {
            var dict = new Dictionary<string, StoreStockInfo>();
            foreach (var o in offers)
            {
                if (Responses.TryGetValue(o.AffiliateUrl, out var info))
                {
                    dict[o.AffiliateUrl] = info;
                }
                else
                {
                    dict[o.AffiliateUrl] = StoreStockInfo.InStock(5, o.Price);
                }
            }
            return ValueTask.FromResult<IReadOnlyDictionary<string, StoreStockInfo>>(dict);
        }

        public void InvalidateStockCache(string affiliateUrl) { }
    }

    private static Game CreateGame(string title, decimal price, string store = "Zacatrus", string country = "España")
    {
        return new Game(
            bggId: Random.Shared.Next(1, 999999),
            originalTitle: title,
            spanishTitle: title,
            designer: "Autor",
            publisher: "Editorial",
            yearPublished: 2024,
            coverImageUrl: "https://ludeka.test/c.jpg",
            thumbnailUrl: null,
            description: "Desc",
            bggRating: 8.0,
            bggRank: 5,
            ludistRating: 8.2,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: true,
            age: new AgeRating(10, 10),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(30, 45, 15),
            purchaseLinks: new List<GamePurchaseLink>
            {
                new(store, $"https://{store.ToLowerInvariant().Replace(" ", "")}.com/juego", price, country: country)
            }
        );
    }

    [Fact]
    public async Task GetTopDiscountsAsync_FiltersAndOrdersByDiscountPercentage()
    {
        var priceRepo = new FakeGamePriceRepository();
        var gameRepo = new FakeGameRepository();
        var collRepo = new FakeUserCollectionRepository();
        var stockService = new FakeStoreStockService();
        var options = Options.Create(new PriceRadarOptions { MinDiscountPercentage = 10.0 });
        var service = new PriceRadarService(priceRepo, gameRepo, collRepo, stockService, options, NullLogger<PriceRadarService>.Instance);

        var gameCheap = CreateGame("Terraforming Mars", 30m, "Zacatrus");
        var gameExpensive = CreateGame("Ark Nova", 65m, "Jugamos Otra");

        // Agregar histórico para establecer precio medio de referencia alto
        priceRepo.Snapshots.Add(new GamePriceSnapshot(gameCheap.Id, "Zacatrus", "https://zacatrus.com/juego", 50m));
        priceRepo.Snapshots.Add(new GamePriceSnapshot(gameExpensive.Id, "Jugamos Otra", "https://jugamosotra.com/juego", 65m));

        gameRepo.Games.Add(gameCheap);
        gameRepo.Games.Add(gameExpensive);

        var topDiscounts = await service.GetTopDiscountsAsync(10);

        Assert.Single(topDiscounts);
        Assert.Equal(gameCheap.Id, topDiscounts[0].GameId);
        Assert.Equal(40.0, topDiscounts[0].DiscountPercentage); // 30€ vs 50€ = 40%
        Assert.True(topDiscounts[0].IsAllTimeLow);
    }

    [Fact]
    public async Task GetUserWantToBuyAlertsAsync_ReturnsAlertsOnlyForUserFollowedGames()
    {
        var priceRepo = new FakeGamePriceRepository();
        var gameRepo = new FakeGameRepository();
        var collRepo = new FakeUserCollectionRepository();
        var stockService = new FakeStoreStockService();
        var options = Options.Create(new PriceRadarOptions { MinDiscountPercentage = 10.0 });
        var service = new PriceRadarService(priceRepo, gameRepo, collRepo, stockService, options, NullLogger<PriceRadarService>.Instance);

        var game1 = CreateGame("Cascadia", 28m);
        var game2 = CreateGame("Wingspan", 35m);

        priceRepo.Snapshots.Add(new GamePriceSnapshot(game1.Id, "Zacatrus", "https://zacatrus.com/juego", 40m));
        priceRepo.Snapshots.Add(new GamePriceSnapshot(game2.Id, "Zacatrus", "https://zacatrus.com/juego", 50m));

        gameRepo.Games.Add(game1);
        gameRepo.Games.Add(game2);

        // Usuario sigue solo Cascadia
        var item = new UserCollectionItem("user-1", game1.Id, CollectionStatus.WantToBuy);
        item.AttachGame(game1);
        collRepo.Items.Add(item);

        var alerts = await service.GetUserWantToBuyAlertsAsync("user-1");

        Assert.Single(alerts);
        Assert.Equal(game1.Id, alerts[0].GameId);
        Assert.Equal(30.0, alerts[0].DiscountPercentage); // 28€ vs 40€ = 30%
    }

    [Fact]
    public async Task ScanWantToBuyPricesAsync_CapturesAndPersistsNewPrices()
    {
        var priceRepo = new FakeGamePriceRepository();
        var gameRepo = new FakeGameRepository();
        var collRepo = new FakeUserCollectionRepository();
        var stockService = new FakeStoreStockService();
        var options = Options.Create(new PriceRadarOptions());
        var service = new PriceRadarService(priceRepo, gameRepo, collRepo, stockService, options, NullLogger<PriceRadarService>.Instance);

        var game = CreateGame("Heat", 45m);
        gameRepo.Games.Add(game);

        stockService.Responses[game.PurchaseLinks[0].AffiliateUrl] = StoreStockInfo.InStock(5, 39.95m);

        var scanned = await service.ScanWantToBuyPricesAsync(10);

        Assert.Equal(1, scanned);
        Assert.Single(priceRepo.Snapshots);
        Assert.Equal(39.95m, priceRepo.Snapshots[0].Price);
    }
}
