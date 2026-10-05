using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Affiliates;
using Ludeka.Application.Options;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class AmazonPriceSyncServiceTests
{
    private class FakeGameRepository : IGameRepository
    {
        public readonly Dictionary<Guid, Game> Store = new();
        public bool UpdateCalled { get; private set; }

        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            Store.TryGetValue(id, out var game);
            return Task.FromResult(game);
        }

        public Task UpdateAsync(Game game, CancellationToken ct = default)
        {
            UpdateCalled = true;
            Store[game.Id] = game;
            return Task.CompletedTask;
        }

        // Métodos auxiliares de IGameRepository no usados en el test
        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria filter, int page = 1, int pageSize = 12, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> HasAnyAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Game>> GetGamesWithoutAiSummaryAsync(int limit = 20, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Game>> GetByPublisherAsync(string publisherName, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Game>> GetGamesPendingQualityBackfillAsync(int limit = 50, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Game>> GetGamesPendingQualityBackfillAsync(int afterBggId, int limit, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> GetGamesPendingQualityBackfillCountAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Game>> GetGamesCursorPagedAsync(int afterBggId, int limit = 50, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> GetTotalCatalogCountAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Game>> GetByDesignerAsync(string designerName, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Game>> GetAllGamesAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Game>> QuickSearchAsync(string term, int limit = 5, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<string, int>> GetOfferCountsByStoreAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<string, int>> GetGameCountsByPublisherAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Game>> GetGamesWithStoreOffersAsync(string storeName, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Game>> GetGamesWithPurchaseLinksAsync(int? limit = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Game>> GetByBggIdsAsync(IEnumerable<int> bggIds, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Game>> GetUnlinkedExpansionsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Game>> GetTopRankedGamesWithoutVideosAsync(int maxRank = 4000, int limit = 60, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> GetTopRankedGamesWithoutVideosCountAsync(int maxRank = 4000, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<int>> GetTopRankedBaseGameBggIdsAsync(int limit = 1200, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private class FakeGamePriceRepository : IGamePriceRepository
    {
        public readonly List<GamePriceSnapshot> Snapshots = new();

        public Task RecordSnapshotAsync(GamePriceSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            Snapshots.Add(snapshot);
            return Task.CompletedTask;
        }

        public Task RecordSnapshotsBatchAsync(IEnumerable<GamePriceSnapshot> snapshots, CancellationToken cancellationToken = default)
        {
            Snapshots.AddRange(snapshots);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<GamePriceSnapshot>> GetHistoryAsync(Guid gameId, int limit = 50, CancellationToken cancellationToken = default)
        {
            var results = Snapshots
                .Where(s => s.GameId == gameId)
                .OrderByDescending(s => s.RecordedAtUtc)
                .Take(limit)
                .ToList();
            return Task.FromResult<IReadOnlyList<GamePriceSnapshot>>(results);
        }

        public Task<GamePriceMetrics> GetMetricsAsync(Guid gameId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<Guid, GamePriceMetrics>> GetMetricsBatchAsync(IEnumerable<Guid> gameIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<GamePriceSnapshot>> GetRecentSnapshotsAsync(int limit = 100, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private class FakeAmazonProductProvider : IAmazonProductProvider
    {
        public Func<string, Task<string?>>? LookupAsinHandler { get; set; }
        public Func<string, Task<AmazonProductPriceResult?>>? GetPriceHandler { get; set; }
        public int LookupAsinCalls { get; private set; }
        public int GetPriceCalls { get; private set; }

        public Task<string?> LookupAsinByEanAsync(string ean, CancellationToken ct = default)
        {
            LookupAsinCalls++;
            return LookupAsinHandler != null ? LookupAsinHandler(ean) : Task.FromResult<string?>(null);
        }

        public Task<AmazonProductPriceResult?> GetPriceAndStockAsync(string asin, CancellationToken ct = default)
        {
            GetPriceCalls++;
            return GetPriceHandler != null ? GetPriceHandler(asin) : Task.FromResult<AmazonProductPriceResult?>(null);
        }
    }

    private class FakeAffiliateUrlResolver : IAffiliateUrlResolver
    {
        public string ResolveAffiliateUrl(string rawUrl, string? storeName = null)
        {
            return rawUrl.Contains("tag=") ? rawUrl : $"{rawUrl}?tag=ludeka-21";
        }

        public bool IsAllowedStoreUrl(string url) => true;
        public string BuildSearchUrl(string storeName, string searchQuery) => $"https://www.amazon.es/s?k={searchQuery}&tag=ludeka-21";
    }

    private static Game CreateGame(string? ean = null, string? asin = null)
    {
        var game = new Game(
            bggId: 266192,
            originalTitle: "Wingspan",
            spanishTitle: "Wingspan",
            designer: "Elizabeth Hargrave",
            publisher: "Maldito Games",
            yearPublished: 2019,
            coverImageUrl: "https://example.com/cover.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            description: "Juego de aves",
            bggRating: 8.1,
            bggRank: 25,
            ludistRating: 8.5,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: true,
            age: new AgeRating(10, 10),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(40, 70, 25),
            ean: ean,
            asin: asin
        );
        return game;
    }

    [Fact]
    public async Task SyncGamePriceAsync_WhenGameNotFound_ReturnsNull()
    {
        var gameRepo = new FakeGameRepository();
        var priceRepo = new FakeGamePriceRepository();
        var provider = new FakeAmazonProductProvider();
        var service = new AmazonPriceSyncService(
            gameRepo,
            priceRepo,
            provider,
            new FakeAffiliateUrlResolver(),
            Microsoft.Extensions.Options.Options.Create(new AmazonOptions()),
            NullLogger<AmazonPriceSyncService>.Instance
        );

        var result = await service.SyncGamePriceAsync(Guid.NewGuid());

        Assert.Null(result);
        Assert.Equal(0, provider.LookupAsinCalls);
        Assert.Equal(0, provider.GetPriceCalls);
    }

    [Fact]
    public async Task SyncGamePriceAsync_WhenCacheValidUnder24Hours_ReturnsCachedSnapshotWithoutCallingProvider()
    {
        var game = CreateGame(asin: "B07MZT757D");
        var gameRepo = new FakeGameRepository();
        gameRepo.Store[game.Id] = game;

        var priceRepo = new FakeGamePriceRepository();
        var cachedSnapshot = new GamePriceSnapshot(
            gameId: game.Id,
            storeName: "Amazon",
            affiliateUrl: "https://www.amazon.es/dp/B07MZT757D?tag=ludeka-21",
            price: 49.99m,
            inStock: true,
            currency: "€",
            recordedAtUtc: DateTimeOffset.UtcNow.AddHours(-10) // 10 horas de antigüedad (< 24h)
        );
        priceRepo.Snapshots.Add(cachedSnapshot);

        var provider = new FakeAmazonProductProvider();
        var service = new AmazonPriceSyncService(
            gameRepo,
            priceRepo,
            provider,
            new FakeAffiliateUrlResolver(),
            Microsoft.Extensions.Options.Options.Create(new AmazonOptions()),
            NullLogger<AmazonPriceSyncService>.Instance
        );

        var result = await service.SyncGamePriceAsync(game.Id);

        Assert.NotNull(result);
        Assert.Equal(49.99m, result.Price);
        Assert.Equal(0, provider.GetPriceCalls); // No consumió cuota externa
    }

    [Fact]
    public async Task SyncGamePriceAsync_WhenCacheExpiredOver24Hours_CallsProviderAndRecordsNewSnapshot()
    {
        var game = CreateGame(asin: "B07MZT757D");
        var gameRepo = new FakeGameRepository();
        gameRepo.Store[game.Id] = game;

        var priceRepo = new FakeGamePriceRepository();
        var expiredSnapshot = new GamePriceSnapshot(
            gameId: game.Id,
            storeName: "Amazon",
            affiliateUrl: "https://www.amazon.es/dp/B07MZT757D?tag=ludeka-21",
            price: 60.00m,
            inStock: true,
            currency: "€",
            recordedAtUtc: DateTimeOffset.UtcNow.AddHours(-25) // 25 horas (> 24h expirado)
        );
        priceRepo.Snapshots.Add(expiredSnapshot);

        var provider = new FakeAmazonProductProvider
        {
            GetPriceHandler = asin => Task.FromResult<AmazonProductPriceResult?>(new AmazonProductPriceResult(
                asin: asin,
                price: 52.00m,
                currency: "€",
                inStock: true,
                productUrl: $"https://www.amazon.es/dp/{asin}"
            ))
        };

        var service = new AmazonPriceSyncService(
            gameRepo,
            priceRepo,
            provider,
            new FakeAffiliateUrlResolver(),
            Microsoft.Extensions.Options.Options.Create(new AmazonOptions()),
            NullLogger<AmazonPriceSyncService>.Instance
        );

        var result = await service.SyncGamePriceAsync(game.Id);

        Assert.NotNull(result);
        Assert.Equal(52.00m, result.Price);
        Assert.Equal(1, provider.GetPriceCalls);
        Assert.Equal(2, priceRepo.Snapshots.Count); // Se registró el nuevo snapshot
        Assert.Contains(game.PurchaseLinks, l => l.StoreName == "Amazon" && l.Price == 52.00m);
    }

    [Fact]
    public async Task SyncGamePriceAsync_WhenGameHasNoAsinButHasEan_ResolvesAsinSavesGameAndFetchesPrice()
    {
        var game = CreateGame(ean: "8435407626492", asin: null);
        var gameRepo = new FakeGameRepository();
        gameRepo.Store[game.Id] = game;

        var priceRepo = new FakeGamePriceRepository();
        var provider = new FakeAmazonProductProvider
        {
            LookupAsinHandler = ean => Task.FromResult<string?>("B07MZT757D"),
            GetPriceHandler = asin => Task.FromResult<AmazonProductPriceResult?>(new AmazonProductPriceResult(
                asin: asin,
                price: 48.50m,
                currency: "€",
                inStock: true
            ))
        };

        var service = new AmazonPriceSyncService(
            gameRepo,
            priceRepo,
            provider,
            new FakeAffiliateUrlResolver(),
            Microsoft.Extensions.Options.Options.Create(new AmazonOptions()),
            NullLogger<AmazonPriceSyncService>.Instance
        );

        var result = await service.SyncGamePriceAsync(game.Id);

        Assert.NotNull(result);
        Assert.Equal(48.50m, result.Price);
        Assert.Equal(1, provider.LookupAsinCalls);
        Assert.Equal(1, provider.GetPriceCalls);
        Assert.Equal("B07MZT757D", game.Asin); // Se persistió el ASIN en el juego
        Assert.True(gameRepo.UpdateCalled);
    }

    [Fact]
    public async Task SyncGamePriceAsync_WhenNeitherAsinNorEanPresent_ReturnsNullWithoutCallingProvider()
    {
        var game = CreateGame(ean: null, asin: null);
        var gameRepo = new FakeGameRepository();
        gameRepo.Store[game.Id] = game;

        var priceRepo = new FakeGamePriceRepository();
        var provider = new FakeAmazonProductProvider();

        var service = new AmazonPriceSyncService(
            gameRepo,
            priceRepo,
            provider,
            new FakeAffiliateUrlResolver(),
            Microsoft.Extensions.Options.Options.Create(new AmazonOptions()),
            NullLogger<AmazonPriceSyncService>.Instance
        );

        var result = await service.SyncGamePriceAsync(game.Id);

        Assert.Null(result);
        Assert.Equal(0, provider.LookupAsinCalls);
        Assert.Equal(0, provider.GetPriceCalls);
    }
}
