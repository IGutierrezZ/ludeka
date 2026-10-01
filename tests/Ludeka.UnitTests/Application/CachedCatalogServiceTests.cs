using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Catalog;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class CachedCatalogServiceTests
{
    private static Game CreateSampleGame(string originalTitle, string spanishTitle)
    {
        return new Game(
            bggId: 266192,
            originalTitle: originalTitle,
            spanishTitle: spanishTitle,
            designer: "Elizabeth Hargrave",
            publisher: "Maldito Games",
            yearPublished: 2019,
            coverImageUrl: "https://cf.geekdo-images.com/sample.jpg",
            thumbnailUrl: "https://cf.geekdo-images.com/sample_t.jpg",
            description: "Juego competitivo de aves.",
            bggRating: 8.1,
            bggRank: 25,
            ludistRating: 8.5,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: true,
            age: new AgeRating(BoxAge: 14, CommunityAge: 10),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(40, 70, 25),
            scalability: new List<ScalabilityEntry>
            {
                new(2, "2J", ScalabilityStatus.MustPlay, 100, 10, 2)
            }
        );
    }

    private class TestCatalogService : ICatalogService
    {
        public int GetCatalogCallCount { get; private set; }
        public int GetGameBySlugCallCount { get; private set; }
        public int GetQuickSearchCallCount { get; private set; }

        public Task<CatalogResult> GetCatalogAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default)
        {
            GetCatalogCallCount++;
            var sample = CreateSampleGame("Wingspan", "Wingspan");
            var dtos = new List<GameSummaryDto> { GameSummaryDto.FromEntity(sample) };
            return Task.FromResult(new CatalogResult(dtos, 1, page, pageSize));
        }

        public Task<GameDetailDto?> GetGameBySlugAsync(string slug, CancellationToken ct = default)
        {
            GetGameBySlugCallCount++;
            if (slug == "non-existent") return Task.FromResult<GameDetailDto?>(null);

            var sample = CreateSampleGame("Wingspan", "Wingspan");
            return Task.FromResult<GameDetailDto?>(GameDetailDto.FromEntity(sample));
        }

        public Task<IReadOnlyList<GameSummaryDto>> GetQuickSearchAsync(string term, int limit = 5, CancellationToken ct = default)
        {
            GetQuickSearchCallCount++;
            var sample = CreateSampleGame("Wingspan", "Wingspan");
            var list = new List<GameSummaryDto> { GameSummaryDto.FromEntity(sample) };
            return Task.FromResult<IReadOnlyList<GameSummaryDto>>(list);
        }
    }

    [Fact]
    public async Task GetCatalogAsync_ShouldCallInnerServiceOnce_AndUseCacheOnSecondCall()
    {
        // Arrange
        var inner = new TestCatalogService();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CachedCatalogService(inner, cache);
        var criteria = new GameFilterCriteria(EspecialParejas: true);

        // Act - 1st call (Miss)
        var result1 = await service.GetCatalogAsync(criteria, 1, 20);

        // Act - 2nd call (Hit)
        var result2 = await service.GetCatalogAsync(criteria, 1, 20);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(1, inner.GetCatalogCallCount);
        Assert.Single(result1.Games);
        Assert.Single(result2.Games);
    }

    [Fact]
    public async Task GetCatalogAsync_WhenSortOrderChanges_ShouldCallInnerServiceAgain()
    {
        // Arrange
        var inner = new TestCatalogService();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CachedCatalogService(inner, cache);
        var criteriaRank = new GameFilterCriteria(SortBy: GameSortOrder.Rank);
        var criteriaRating = new GameFilterCriteria(SortBy: GameSortOrder.RatingDesc);

        // Act
        var result1 = await service.GetCatalogAsync(criteriaRank, 1, 20);
        var result2 = await service.GetCatalogAsync(criteriaRating, 1, 20);

        // Assert: SortBy distinto debe generar claves de caché distintas y llamar a inner dos veces
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(2, inner.GetCatalogCallCount);
    }

    [Fact]
    public async Task GetGameBySlugAsync_ShouldCallInnerServiceOnce_AndUseCacheOnSecondCall()
    {
        // Arrange
        var inner = new TestCatalogService();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CachedCatalogService(inner, cache);

        // Act - 1st call (Miss)
        var result1 = await service.GetGameBySlugAsync("wingspan");

        // Act - 2nd call (Hit)
        var result2 = await service.GetGameBySlugAsync("wingspan");

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(1, inner.GetGameBySlugCallCount);
        Assert.Equal("Wingspan", result1.SpanishTitle);
        Assert.Equal(result1.Id, result2.Id);
    }

    [Fact]
    public async Task Invalidate_ShouldEvictSlugFromCache()
    {
        // Arrange
        var inner = new TestCatalogService();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CachedCatalogService(inner, cache);

        // Act 1: Populate cache
        await service.GetGameBySlugAsync("wingspan");
        Assert.Equal(1, inner.GetGameBySlugCallCount);

        // Act 2: Invalidate
        service.Invalidate("wingspan");

        // Act 3: Next call should hit inner again
        await service.GetGameBySlugAsync("wingspan");

        // Assert
        Assert.Equal(2, inner.GetGameBySlugCallCount);
    }

    [Fact]
    public async Task GetQuickSearchAsync_ShouldCacheResults()
    {
        // Arrange
        var inner = new TestCatalogService();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CachedCatalogService(inner, cache);

        // Act
        var res1 = await service.GetQuickSearchAsync("Wing", 5);
        var res2 = await service.GetQuickSearchAsync("Wing", 5);

        // Assert
        Assert.Single(res1);
        Assert.Single(res2);
        Assert.Equal(1, inner.GetQuickSearchCallCount);
    }

    [Fact]
    public async Task GetCatalogAsync_DifferentFootprint_ShouldDifferentiateCacheKeys()
    {
        // Arrange
        var inner = new TestCatalogService();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CachedCatalogService(inner, cache);

        var criteriaSmall = new GameFilterCriteria(Footprint: Ludeka.Core.Enums.TableFootprint.SmallTable);
        var criteriaMonster = new GameFilterCriteria(Footprint: Ludeka.Core.Enums.TableFootprint.TableMonster);

        // Act
        await service.GetCatalogAsync(criteriaSmall, 1, 20);
        await service.GetCatalogAsync(criteriaMonster, 1, 20);

        // Assert: 2 distinct inner calls because footprint produces distinct cache keys
        Assert.Equal(2, inner.GetCatalogCallCount);
    }

    [Fact]
    public async Task GetCatalogAsync_DifferentTypeFilter_ShouldDifferentiateCacheKeys()
    {
        // Arrange
        var inner = new TestCatalogService();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CachedCatalogService(inner, cache);

        var criteriaBase = new GameFilterCriteria(TypeFilter: Ludeka.Core.Enums.GameType.BaseGame);
        var criteriaExpansion = new GameFilterCriteria(TypeFilter: Ludeka.Core.Enums.GameType.Expansion);

        // Act
        await service.GetCatalogAsync(criteriaBase, 1, 20);
        await service.GetCatalogAsync(criteriaExpansion, 1, 20);

        // Assert: 2 distinct inner calls because TypeFilter produces distinct cache keys
        Assert.Equal(2, inner.GetCatalogCallCount);
    }

    [Fact]
    public async Task GetCatalogAsync_DifferentMultiSelectStyles_ShouldDifferentiateCacheKeys()
    {
        // Arrange
        var inner = new TestCatalogService();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CachedCatalogService(inner, cache);

        var criteriaEuro = new GameFilterCriteria(Styles: new[] { GameStyle.Eurogame });
        var criteriaAmeri = new GameFilterCriteria(Styles: new[] { GameStyle.Ameritrash });

        // Act
        await service.GetCatalogAsync(criteriaEuro, 1, 20);
        await service.GetCatalogAsync(criteriaAmeri, 1, 20);

        // Assert: 2 llamadas distintas porque los estilos multiselección producen claves distintas
        Assert.Equal(2, inner.GetCatalogCallCount);
    }

    [Fact]
    public async Task GetCatalogAsync_DifferentMultiSelectConfrontations_ShouldDifferentiateCacheKeys()
    {
        // Arrange
        var inner = new TestCatalogService();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CachedCatalogService(inner, cache);

        var criteriaComp = new GameFilterCriteria(Confrontations: new[] { ConfrontationType.Competitive });
        var criteriaCoop = new GameFilterCriteria(Confrontations: new[] { ConfrontationType.Cooperative });

        // Act
        await service.GetCatalogAsync(criteriaComp, 1, 20);
        await service.GetCatalogAsync(criteriaCoop, 1, 20);

        // Assert
        Assert.Equal(2, inner.GetCatalogCallCount);
    }

    [Fact]
    public async Task GetCatalogAsync_DifferentMultiSelectPlayerCounts_ShouldDifferentiateCacheKeys()
    {
        // Arrange
        var inner = new TestCatalogService();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CachedCatalogService(inner, cache);

        var criteria2P = new GameFilterCriteria(PlayerCounts: new[] { 2 }, PlayerCountsMatchAll: true);
        var criteria4P = new GameFilterCriteria(PlayerCounts: new[] { 4 }, PlayerCountsMatchAll: true);

        // Act
        await service.GetCatalogAsync(criteria2P, 1, 20);
        await service.GetCatalogAsync(criteria4P, 1, 20);

        // Assert
        Assert.Equal(2, inner.GetCatalogCallCount);
    }

    [Fact]
    public async Task GetCatalogAsync_DifferentComplexities_ShouldDifferentiateCacheKeys()
    {
        // Arrange
        var inner = new TestCatalogService();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CachedCatalogService(inner, cache);

        var criteriaLight = new GameFilterCriteria(Complexities: new[] { GameComplexity.Light });
        var criteriaHeavy = new GameFilterCriteria(Complexities: new[] { GameComplexity.Heavy });

        // Act
        await service.GetCatalogAsync(criteriaLight, 1, 20);
        await service.GetCatalogAsync(criteriaHeavy, 1, 20);

        // Assert
        Assert.Equal(2, inner.GetCatalogCallCount);
    }

    [Fact]
    public async Task GetCatalogAsync_DifferentMultiSelectFootprints_ShouldDifferentiateCacheKeys()
    {
        // Arrange
        var inner = new TestCatalogService();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CachedCatalogService(inner, cache);

        var criteriaSmall = new GameFilterCriteria(Footprints: new[] { TableFootprint.SmallTable });
        var criteriaMonster = new GameFilterCriteria(Footprints: new[] { TableFootprint.TableMonster });

        // Act
        await service.GetCatalogAsync(criteriaSmall, 1, 20);
        await service.GetCatalogAsync(criteriaMonster, 1, 20);

        // Assert
        Assert.Equal(2, inner.GetCatalogCallCount);
    }

    [Fact]
    public async Task GetCatalogAsync_DifferentMultiSelectTypes_ShouldDifferentiateCacheKeys()
    {
        // Arrange
        var inner = new TestCatalogService();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CachedCatalogService(inner, cache);

        var criteriaBase = new GameFilterCriteria(Types: new[] { GameType.BaseGame });
        var criteriaExpansion = new GameFilterCriteria(Types: new[] { GameType.Expansion });

        // Act
        await service.GetCatalogAsync(criteriaBase, 1, 20);
        await service.GetCatalogAsync(criteriaExpansion, 1, 20);

        // Assert
        Assert.Equal(2, inner.GetCatalogCallCount);
    }

    [Fact]
    public async Task GetCatalogAsync_DifferentMultiSelectMaxDurations_ShouldDifferentiateCacheKeys()
    {
        // Arrange
        var inner = new TestCatalogService();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CachedCatalogService(inner, cache);

        var criteria30 = new GameFilterCriteria(MaxDurations: new[] { 30 });
        var criteria60 = new GameFilterCriteria(MaxDurations: new[] { 60 });

        // Act
        await service.GetCatalogAsync(criteria30, 1, 20);
        await service.GetCatalogAsync(criteria60, 1, 20);

        // Assert
        Assert.Equal(2, inner.GetCatalogCallCount);
    }

    [Fact]
    public async Task GetCatalogAsync_EmptyVsFilteredCriteria_ShouldDifferentiateCacheKeys()
    {
        // Arrange
        var inner = new TestCatalogService();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CachedCatalogService(inner, cache);

        var criteriaEmpty = new GameFilterCriteria();
        var criteriaFiltered = new GameFilterCriteria(Styles: new[] { GameStyle.Eurogame }, Complexities: new[] { GameComplexity.Medium });

        // Act
        await service.GetCatalogAsync(criteriaEmpty, 1, 20);
        await service.GetCatalogAsync(criteriaFiltered, 1, 20);

        // Assert: Una llamada para la consulta vacía y otra para la filtrada
        Assert.Equal(2, inner.GetCatalogCallCount);
    }
}
