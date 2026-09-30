using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Directory;
using Ludeka.Core.Enums;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class CachedDirectoryServicesTests
{
    private readonly IMemoryCache _memoryCache;

    public CachedDirectoryServicesTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
    }

    [Fact]
    public async Task CachedStoreService_GetAllAsync_ShouldHitCacheOnSecondCall()
    {
        // Arrange
        var inner = new TestStoreService();
        var cached = new CachedStoreService(inner, _memoryCache);

        // Act
        var first = await cached.GetAllAsync();
        var second = await cached.GetAllAsync();

        // Assert
        Assert.Equal(1, inner.GetAllCallCount);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task CachedStoreService_CreateOrUpdate_ShouldInvalidateCache()
    {
        // Arrange
        var inner = new TestStoreService();
        var cached = new CachedStoreService(inner, _memoryCache);

        await cached.GetAllAsync();
        Assert.Equal(1, inner.GetAllCallCount);

        // Act: Crear una tienda invalida la caché
        await cached.CreateAsync(new CreateStoreDto(Name: "Nueva Tienda", Slug: null, Type: StoreType.OnlineOnly, Country: "ES"));

        // Siguiente GetAllAsync debe consultar al servicio interno
        await cached.GetAllAsync();

        // Assert
        Assert.Equal(2, inner.GetAllCallCount);
    }

    [Fact]
    public async Task CachedStoreService_GetBySlugAsync_ShouldCacheStoreDetails()
    {
        // Arrange
        var inner = new TestStoreService();
        var cached = new CachedStoreService(inner, _memoryCache);

        // Act
        var detail1 = await cached.GetBySlugAsync("zacatrus");
        var detail2 = await cached.GetBySlugAsync("zacatrus");

        // Assert
        Assert.Equal(1, inner.GetBySlugCallCount);
        Assert.Same(detail1, detail2);
    }

    [Fact]
    public async Task CachedPublisherService_GetAllAsync_ShouldHitCacheOnSecondCall()
    {
        // Arrange
        var inner = new TestPublisherService();
        var cached = new CachedPublisherService(inner, _memoryCache);

        // Act
        var first = await cached.GetAllAsync();
        var second = await cached.GetAllAsync();

        // Assert
        Assert.Equal(1, inner.GetAllCallCount);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task CachedPublisherService_UpdateOrDelete_ShouldInvalidateCache()
    {
        // Arrange
        var inner = new TestPublisherService();
        var cached = new CachedPublisherService(inner, _memoryCache);

        await cached.GetAllAsync();
        Assert.Equal(1, inner.GetAllCallCount);

        // Act: Modificar una editorial invalida la caché
        await cached.UpdateAsync(Guid.NewGuid(), new UpdatePublisherDto(Name: "Devir Modificado", Country: "ES", City: null, Description: null, LogoUrl: null, WebsiteUrl: null, SocialLinks: null));

        // Siguiente GetAllAsync debe volver a consultar a inner
        await cached.GetAllAsync();

        // Assert
        Assert.Equal(2, inner.GetAllCallCount);
    }

    [Fact]
    public async Task CachedPublisherService_GetBySlugAsync_ShouldCachePublisherDetails()
    {
        // Arrange
        var inner = new TestPublisherService();
        var cached = new CachedPublisherService(inner, _memoryCache);

        // Act
        var p1 = await cached.GetBySlugAsync("devir");
        var p2 = await cached.GetBySlugAsync("devir");

        // Assert
        Assert.Equal(1, inner.GetBySlugCallCount);
        Assert.Same(p1, p2);
    }

    private class TestStoreService : IStoreService
    {
        public int GetAllCallCount { get; private set; }
        public int GetBySlugCallCount { get; private set; }

        public Task<IReadOnlyList<StoreDto>> GetAllAsync(string? search = null, StoreType? type = null, string? country = null, CancellationToken ct = default)
        {
            GetAllCallCount++;
            var list = new List<StoreDto>
            {
                new(Guid.NewGuid(), "Zacatrus", "zacatrus", StoreType.Hybrid, "ES", "Madrid", "Calle Mayor", "Tienda física y online", null, "https://zacatrus.es", null, true, 10, [], DateTimeOffset.UtcNow)
            };
            return Task.FromResult<IReadOnlyList<StoreDto>>(list);
        }

        public Task<StoreDetailDto?> GetBySlugAsync(string slug, CancellationToken ct = default)
        {
            GetBySlugCallCount++;
            var detail = new StoreDetailDto(Guid.NewGuid(), "Zacatrus", slug, StoreType.Hybrid, "ES", "Madrid", "Calle Mayor", "Descripción", null, "https://zacatrus.es", null, true, [], [], DateTimeOffset.UtcNow, null);
            return Task.FromResult<StoreDetailDto?>(detail);
        }

        public Task<StoreDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return Task.FromResult<StoreDetailDto?>(null);
        }

        public Task<StoreDto> CreateAsync(CreateStoreDto dto, CancellationToken ct = default)
        {
            return Task.FromResult(new StoreDto(Guid.NewGuid(), dto.Name, "slug", dto.Type, dto.Country, null, null, null, null, null, null, false, 0, [], DateTimeOffset.UtcNow));
        }

        public Task<StoreDto> UpdateAsync(Guid id, UpdateStoreDto dto, CancellationToken ct = default)
        {
            return Task.FromResult(new StoreDto(id, dto.Name, "slug", dto.Type, dto.Country, null, null, null, null, null, null, false, 0, [], DateTimeOffset.UtcNow));
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }

    private class TestPublisherService : IPublisherService
    {
        public int GetAllCallCount { get; private set; }
        public int GetBySlugCallCount { get; private set; }

        public Task<IReadOnlyList<PublisherDto>> GetAllAsync(string? search = null, CancellationToken ct = default)
        {
            GetAllCallCount++;
            var list = new List<PublisherDto>
            {
                new(Guid.NewGuid(), "Devir", "devir", "ES", "Barcelona", "Editorial", null, "https://devir.es", 50, [], DateTimeOffset.UtcNow)
            };
            return Task.FromResult<IReadOnlyList<PublisherDto>>(list);
        }

        public Task<PublisherDetailDto?> GetBySlugAsync(string slug, CancellationToken ct = default)
        {
            GetBySlugCallCount++;
            var detail = new PublisherDetailDto(Guid.NewGuid(), "Devir", slug, "ES", "Barcelona", "Editorial", null, "https://devir.es", [], [], DateTimeOffset.UtcNow, null);
            return Task.FromResult<PublisherDetailDto?>(detail);
        }

        public Task<PublisherDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return Task.FromResult<PublisherDetailDto?>(null);
        }

        public Task<PublisherDto> CreateAsync(CreatePublisherDto dto, CancellationToken ct = default)
        {
            return Task.FromResult(new PublisherDto(Guid.NewGuid(), dto.Name, "slug", dto.Country, null, null, null, null, 0, [], DateTimeOffset.UtcNow));
        }

        public Task<PublisherDto> UpdateAsync(Guid id, UpdatePublisherDto dto, CancellationToken ct = default)
        {
            return Task.FromResult(new PublisherDto(id, dto.Name, "slug", dto.Country, null, null, null, null, 0, [], DateTimeOffset.UtcNow));
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }
}
