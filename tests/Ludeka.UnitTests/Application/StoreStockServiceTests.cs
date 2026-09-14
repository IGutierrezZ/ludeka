using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.Features.Catalog;
using Ludeka.Application.Options;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class StoreStockServiceTests
{
    private class FakeStockClient : IStoreStockClient
    {
        public int Priority { get; set; } = 10;
        public int CallCount { get; private set; }
        public Func<string, string, CancellationToken, Task<StoreStockInfo>>? Handler { get; set; }

        public bool CanHandle(string storeName, string affiliateUrl) => true;

        public async Task<StoreStockInfo> CheckStockAsync(string storeName, string affiliateUrl, CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (Handler != null)
            {
                return await Handler(storeName, affiliateUrl, cancellationToken);
            }
            return StoreStockInfo.InStock(quantity: 10, price: 29.99m, note: "Test Fake");
        }
    }

    [Fact]
    public async Task GetStockAsync_ShouldCacheSuccessfulResult_AndNotCallClientTwice()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new FakeStockClient();
        var options = Options.Create(new StoreStockOptions { DefaultTtlMinutes = 30 });
        var service = new StoreStockService(cache, [client], options);

        var first = await service.GetStockAsync("Zacatrus", "https://zacatrus.es/juego-a.html");
        var second = await service.GetStockAsync("Zacatrus", "https://zacatrus.es/juego-a.html");

        Assert.Equal(StockStatus.InStock, first.Status);
        Assert.Equal(StockStatus.InStock, second.Status);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task InvalidateStockCache_ShouldForceSubsequentClientCall()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new FakeStockClient();
        var options = Options.Create(new StoreStockOptions { DefaultTtlMinutes = 30 });
        var service = new StoreStockService(cache, [client], options);

        await service.GetStockAsync("Zacatrus", "https://zacatrus.es/juego-a.html");
        Assert.Equal(1, client.CallCount);

        service.InvalidateStockCache("https://zacatrus.es/juego-a.html");

        await service.GetStockAsync("Zacatrus", "https://zacatrus.es/juego-a.html");
        Assert.Equal(2, client.CallCount);
    }

    [Fact]
    public async Task GetStockAsync_WhenClientTimesOut_ShouldGracefullyDegradeToUnknown()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new FakeStockClient
        {
            Handler = async (store, url, ct) =>
            {
                // Simular demora superior al timeout de 100ms configurado (cancelación garantizada)
                await Task.Delay(3000, ct);
                return StoreStockInfo.InStock(1);
            }
        };
        var options = Options.Create(new StoreStockOptions
        {
            TimeoutMilliseconds = 100, // Timeout corto para el test
            ErrorTtlMinutes = 5
        });
        var service = new StoreStockService(cache, [client], options);

        var result = await service.GetStockAsync("Tienda Lenta", "https://lenta.com/juego.html");

        Assert.Equal(StockStatus.Unknown, result.Status);
        Assert.Contains("Tiempo de espera agotado", result.StatusNote);
    }

    [Fact]
    public async Task GetStockAsync_WhenClientThrowsException_ShouldReturnUnknownWithoutCrashing()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new FakeStockClient
        {
            Handler = (store, url, ct) => throw new HttpRequestException("Servidor caído (500)")
        };
        var options = Options.Create(new StoreStockOptions());
        var service = new StoreStockService(cache, [client], options);

        var result = await service.GetStockAsync("Tienda Caida", "https://caida.com/juego.html");

        Assert.Equal(StockStatus.Unknown, result.Status);
        Assert.Contains("Error al conectar", result.StatusNote);
    }

    [Fact]
    public async Task GetStockAsync_WhenLiveCheckingDisabled_ShouldReturnUnknownWithoutCallingClient()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new FakeStockClient();
        var options = Options.Create(new StoreStockOptions { EnableLiveChecking = false });
        var service = new StoreStockService(cache, [client], options);

        var result = await service.GetStockAsync("Zacatrus", "https://zacatrus.es/juego.html");

        Assert.Equal(StockStatus.Unknown, result.Status);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task GetStockBatchAsync_ShouldProcessMultipleOffersAndDeduplicateUrls()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new FakeStockClient();
        var options = Options.Create(new StoreStockOptions());
        var service = new StoreStockService(cache, [client], options);

        var offers = new List<GamePurchaseLink>
        {
            new("Zacatrus", "https://zacatrus.es/juego-1.html", 30m),
            new("Dungeon Marvels", "https://dungeon.es/juego-1.html", 29m),
            new("Zacatrus Repetida", "https://zacatrus.es/juego-1.html", 30m) // Misma URL
        };

        var batchResult = await service.GetStockBatchAsync(offers);

        Assert.Equal(2, batchResult.Count);
        Assert.True(batchResult.ContainsKey("https://zacatrus.es/juego-1.html"));
        Assert.True(batchResult.ContainsKey("https://dungeon.es/juego-1.html"));
        Assert.Equal(2, client.CallCount);
    }
}
