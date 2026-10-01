using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Catalog;
using Ludeka.Application.Features.Home;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class HomeDashboardServiceTests
{
    private class FakeCatalogService : ICatalogService
    {
        public List<GameSummaryDto> GamesToReturn { get; set; } = [];

        public Task<CatalogResult> GetCatalogAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default)
        {
            return Task.FromResult(new CatalogResult(GamesToReturn, GamesToReturn.Count, page, pageSize));
        }

        public Task<GameDetailDto?> GetGameBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult<GameDetailDto?>(null);
        public Task<IReadOnlyList<GameSummaryDto>> GetQuickSearchAsync(string term, int limit = 5, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<GameSummaryDto>>([]);
    }

    private class FakeGiveawayService : IGiveawayService
    {
        public List<GiveawayDto> GiveawaysToReturn { get; set; } = [];

        public Task<IReadOnlyList<GiveawayDto>> GetGiveawaysAsync(bool includeExpired = false, string? country = null, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<GiveawayDto>>(GiveawaysToReturn);
        }

        public Task<GiveawayDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<GiveawayDto?>(null);
        public Task<GiveawayDto> CreateOrMergeGiveawayAsync(CreateGiveawayRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<GiveawayDto> UpdateGiveawayAsync(UpdateGiveawayRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SetPromotedAsync(Guid id, bool isPromoted, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteGiveawayAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private class FakeWeeklyReleaseService : IWeeklyReleaseService
    {
        public List<WeeklyReleaseDto> ReleasesToReturn { get; set; } = [];

        public Task<IReadOnlyList<WeeklyReleaseDto>> GetReleasesAsync(DateOnly? fromDate = null, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<WeeklyReleaseDto>>(ReleasesToReturn);
        }

        public Task<WeeklyReleaseDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<WeeklyReleaseDto?>(null);
        public Task<WeeklyReleaseDto> CreateReleaseAsync(CreateWeeklyReleaseRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<WeeklyReleaseDto> UpdateReleaseAsync(Guid id, UpdateWeeklyReleaseRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteReleaseAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private class FakeBoardGameEventRepository : IBoardGameEventRepository
    {
        public List<BoardGameEvent> EventsToReturn { get; set; } = [];

        public Task<IReadOnlyList<BoardGameEvent>> GetUpcomingEventsAsync(int limit = 20, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<BoardGameEvent>>(EventsToReturn.Take(limit).ToList());
        }

        public Task<IReadOnlyList<BoardGameEvent>> GetAllEventsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BoardGameEvent>>(EventsToReturn);
        public Task<BoardGameEvent?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<BoardGameEvent?>(null);
        public Task AddAsync(BoardGameEvent boardGameEvent, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(BoardGameEvent boardGameEvent, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private class FakeHomeDashboardService : IHomeDashboardService
    {
        public int CallCount { get; private set; }
        public HomeDashboardDto DtoToReturn { get; set; } = new([], [], [], []);

        public Task<HomeDashboardDto> GetDashboardDataAsync(CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(DtoToReturn);
        }
    }

    private class FakeDailyTrendingGameRepo : IDailyTrendingGameRepository
    {
        public List<DailyTrendingGame> ItemsToReturn { get; set; } = [];

        public Task<DateOnly?> GetLatestDateAsync(CancellationToken ct = default) =>
            Task.FromResult(ItemsToReturn.Select(t => (DateOnly?)t.DateUtc).Max());

        public Task<IReadOnlyList<DailyTrendingGame>> GetLatestTrendingAsync(int limit = 50, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DailyTrendingGame>>(ItemsToReturn.Take(limit).ToList());

        public Task<IReadOnlyList<DailyTrendingGame>> GetTrendingByDateAsync(DateOnly dateUtc, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DailyTrendingGame>>(ItemsToReturn.Where(t => t.DateUtc == dateUtc).ToList());

        public Task LinkGameAsync(int bggId, Guid gameId, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpsertDailyTrendingBatchAsync(IEnumerable<DailyTrendingGame> items, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task GetDashboardDataAsync_AggregatesAllFourLanesCorrectly()
    {
        // Arrange
        var catalogFake = new FakeCatalogService
        {
            GamesToReturn =
            [
                CreateGameSummary("Brass Birmingham", 8.6, 1),
                CreateGameSummary("Ark Nova", 8.5, 2)
            ]
        };

        var giveawayFake = new FakeGiveawayService
        {
            GiveawaysToReturn =
            [
                CreateGiveawayDto("Sorteo Estándar", DateTimeOffset.UtcNow.AddDays(1), isPromoted: false),
                CreateGiveawayDto("Sorteo Promocionado", DateTimeOffset.UtcNow.AddDays(5), isPromoted: true)
            ]
        };

        var releaseFake = new FakeWeeklyReleaseService
        {
            ReleasesToReturn =
            [
                new(Guid.NewGuid(), "Novedad 1", "Devir", new DateOnly(2026, 9, 1), null, null, 30m, false, null),
                new(Guid.NewGuid(), "Novedad 2", "Maldito", new DateOnly(2026, 9, 15), null, null, 50m, false, null)
            ]
        };

        var eventFake = new FakeBoardGameEventRepository
        {
            EventsToReturn =
            [
                new("Festival Córdoba", "Desc", "https://img.com/c.jpg", new DateOnly(2026, 10, 9), new DateOnly(2026, 10, 12), "Córdoba"),
                new("InterOcio", "Desc", "https://img.com/i.jpg", new DateOnly(2027, 3, 12), new DateOnly(2027, 3, 14), "Madrid")
            ]
        };

        var service = new HomeDashboardService(catalogFake, giveawayFake, releaseFake, eventFake);

        // Act
        var result = await service.GetDashboardDataAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TopGames.Count);
        Assert.Equal(2, result.Giveaways.Count);
        Assert.Equal(2, result.RecentReleases.Count);
        Assert.Equal(2, result.UpcomingEvents.Count);

        // Verificar orden de sorteos: Promocionado primero
        Assert.True(result.Giveaways[0].IsPromoted);
        Assert.Equal("Sorteo Promocionado", result.Giveaways[0].Title);
        Assert.False(result.Giveaways[1].IsPromoted);

        // Verificar orden de novedades: Fecha más reciente primero
        Assert.Equal("Novedad 2", result.RecentReleases[0].Title);
        Assert.Equal(new DateOnly(2026, 9, 15), result.RecentReleases[0].ReleaseDate);

        // Verificar eventos: Córdoba antes que InterOcio
        Assert.Equal("Festival Córdoba", result.UpcomingEvents[0].Title);
        Assert.Equal("InterOcio", result.UpcomingEvents[1].Title);
    }

    [Fact]
    public async Task GetDashboardDataAsync_PromotedGiveawaysTakePriorityOverEarlierNonPromoted()
    {
        // Arrange
        var standardSoon = CreateGiveawayDto("Estándar Mañana", DateTimeOffset.UtcNow.AddDays(1), isPromoted: false);
        var promotedLater = CreateGiveawayDto("Promocionado Semana Que Viene", DateTimeOffset.UtcNow.AddDays(7), isPromoted: true);

        var catalogFake = new FakeCatalogService();
        var giveawayFake = new FakeGiveawayService { GiveawaysToReturn = [standardSoon, promotedLater] };
        var releaseFake = new FakeWeeklyReleaseService();
        var eventFake = new FakeBoardGameEventRepository();

        var service = new HomeDashboardService(catalogFake, giveawayFake, releaseFake, eventFake);

        // Act
        var result = await service.GetDashboardDataAsync();

        // Assert: El promocionado figura en primera posición
        Assert.Equal(2, result.Giveaways.Count);
        Assert.Equal("Promocionado Semana Que Viene", result.Giveaways[0].Title);
        Assert.True(result.Giveaways[0].IsPromoted);
        Assert.Equal("Estándar Mañana", result.Giveaways[1].Title);
        Assert.False(result.Giveaways[1].IsPromoted);
    }

    [Fact]
    public async Task GetDashboardDataAsync_WhenTrendingRepositoryProvided_PopulatesTrendingGamesWithRanks()
    {
        // Arrange
        var game = new Game(
            bggId: 777,
            originalTitle: "Juego en Tendencia",
            spanishTitle: "Juego en Tendencia",
            designer: "Autor",
            publisher: "Editorial",
            yearPublished: 2026,
            coverImageUrl: null,
            thumbnailUrl: null,
            description: "Desc",
            bggRating: 8.2,
            bggRank: 50,
            ludistRating: 8.2,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: true,
            age: new AgeRating(10, 10),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(30, 60, 20)
        );

        var trendingItem = new DailyTrendingGame(
            dateUtc: DateOnly.FromDateTime(DateTime.UtcNow),
            rank: 1,
            bggId: 777,
            title: "Juego en Tendencia",
            gameId: game.Id
        );
        typeof(DailyTrendingGame).GetProperty("Game")!.SetValue(trendingItem, game);

        var trendingFake = new FakeDailyTrendingGameRepo { ItemsToReturn = [trendingItem] };
        var service = new HomeDashboardService(
            new FakeCatalogService(),
            new FakeGiveawayService(),
            new FakeWeeklyReleaseService(),
            new FakeBoardGameEventRepository(),
            trendingFake
        );

        // Act
        var result = await service.GetDashboardDataAsync();

        // Assert
        Assert.NotNull(result.TrendingGames);
        var trendingGame = Assert.Single(result.TrendingGames);
        Assert.Equal("Juego en Tendencia", trendingGame.SpanishTitle);
        Assert.Equal(1, trendingGame.TrendingRank);
    }

    [Fact]
    public async Task CachedHomeDashboardService_CachesAndInvalidatesProperly()
    {
        // Arrange
        var fakeInner = new FakeHomeDashboardService();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cachedService = new CachedHomeDashboardService(fakeInner, memoryCache);

        // Act 1: First call -> goes to inner
        var first = await cachedService.GetDashboardDataAsync();
        Assert.NotNull(first);
        Assert.Equal(1, fakeInner.CallCount);

        // Act 2: Second call -> served from cache, inner NOT called again
        var second = await cachedService.GetDashboardDataAsync();
        Assert.NotNull(second);
        Assert.Equal(1, fakeInner.CallCount);

        // Act 3: Invalidate -> next call hits inner
        cachedService.Invalidate();
        var third = await cachedService.GetDashboardDataAsync();
        Assert.NotNull(third);
        Assert.Equal(2, fakeInner.CallCount);
    }

    private static GameSummaryDto CreateGameSummary(string title, double rating, int rank) => new(
        Id: Guid.NewGuid(),
        BggId: rank * 100,
        Slug: title.ToLowerInvariant().Replace(" ", "-"),
        SpanishTitle: title,
        OriginalTitle: title,
        Designer: "Autor",
        Publisher: "Editorial",
        YearPublished: 2022,
        CoverImageUrl: "https://example.com/cover.jpg",
        ThumbnailUrl: "https://example.com/thumb.jpg",
        BggRating: rating,
        BggRank: rank,
        LudistRating: rating,
        IdealPlayerCountText: "Ideal 2J",
        Confrontation: ConfrontationType.Competitive,
        Style: GameStyle.Eurogame,
        IsOfficialSolo: false,
        IsAccessibleEarlier: false,
        CommunityAge: 14,
        BoxAge: 14,
        Language: LanguageDependence.Low,
        Footprint: TableFootprint.StandardTable,
        EstimatedPerPlayerMinutes: 30);

    private static GiveawayDto CreateGiveawayDto(string title, DateTimeOffset deadline, bool isPromoted) => new(
        Id: Guid.NewGuid(),
        Title: title,
        Organizer: "Organizador",
        Collaborator: null,
        FormattedOrganizer: "Organizador",
        Url: "https://instagram.com/p/test",
        Platform: GiveawayPlatform.Instagram,
        DeadlineAt: deadline,
        RemainingTimeText: "En unos días",
        IsExpired: false,
        GameId: null,
        GameTitle: null,
        ThumbnailUrl: null,
        IsCommunityExclusive: false,
        CreatedAt: DateTimeOffset.UtcNow,
        IsPromoted: isPromoted);
}
