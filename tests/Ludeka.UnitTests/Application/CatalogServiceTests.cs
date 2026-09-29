using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Catalog;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class CatalogServiceTests
{
    private class FakeGameRepository : IGameRepository
    {
        public List<Game> Store { get; } = [];

        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Store.Find(g => g.Id == id));

        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult(Store.Find(g => g.Slug == slug));

        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default) =>
            Task.FromResult(Store.Find(g => g.BggId == bggId));

        public virtual Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default)
        {
            var filtered = Store.FindAll(g =>
                string.IsNullOrEmpty(criteria.SearchTerm) ||
                g.SpanishTitle.Contains(criteria.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                g.OriginalTitle.Contains(criteria.SearchTerm, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(((IReadOnlyList<Game>)filtered, filtered.Count));
        }

        public virtual Task<IReadOnlyList<Game>> QuickSearchAsync(string term, int limit = 5, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Game>>([]);

        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default)
        {
            Store.AddRange(games);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Game game, CancellationToken ct = default)
        {
            var idx = Store.FindIndex(g => g.Id == game.Id);
            if (idx >= 0) Store[idx] = game;
            return Task.CompletedTask;
        }

        public Task<bool> HasAnyAsync(CancellationToken ct = default) =>
            Task.FromResult(Store.Count > 0);
    }

    [Fact]
    public async Task GetGameBySlugAsync_ShouldReturnCorrectDetailDto_WhenGameExists()
    {
        // Arrange
        var fakeRepo = new FakeGameRepository();
        var game = new Game(
            bggId: 174430,
            originalTitle: "Gloomhaven",
            spanishTitle: "Gloomhaven",
            designer: "Isaac Childres",
            publisher: "Cephalofair Games",
            yearPublished: 2017,
            coverImageUrl: "https://geekdo.com/gh.jpg",
            thumbnailUrl: null,
            description: "Campana de mazmorreo cooperativo.",
            bggRating: 8.6,
            bggRank: 3,
            ludistRating: 9.0,
            confrontation: ConfrontationType.Cooperative,
            style: GameStyle.NarrativeCampaign,
            isOfficialSolo: true,
            age: new AgeRating(14, 14),
            language: LanguageDependence.High,
            footprint: TableFootprint.TableMonster,
            duration: new GameDuration(60, 120, 30)
        );
        fakeRepo.Store.Add(game);
        var service = new CatalogService(fakeRepo);

        // Act
        var result = await service.GetGameBySlugAsync("gloomhaven");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Gloomhaven", result.SpanishTitle);
        Assert.Equal(ConfrontationType.Cooperative, result.Confrontation);
        Assert.Equal(TableFootprint.TableMonster, result.Footprint);
    }

    [Fact]
    public async Task GetGameBySlugAsync_ShouldReturnNull_WhenSlugDoesNotExist()
    {
        // Arrange
        var fakeRepo = new FakeGameRepository();
        var service = new CatalogService(fakeRepo);

        // Act
        var result = await service.GetGameBySlugAsync("no-existe");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetQuickSearchAsync_WithEmptyTerm_ReturnsEmptyList()
    {
        // Arrange
        var fakeRepo = new FakeGameRepository();
        var service = new CatalogService(fakeRepo);

        // Act
        var result = await service.GetQuickSearchAsync("   ");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetQuickSearchAsync_WhenQuickSearchReturnsItems_ReturnsSummaryDtosWithoutFallback()
    {
        // Arrange
        var customRepo = new QuickSearchTestRepository();
        var service = new CatalogService(customRepo);

        // Act
        var results = await service.GetQuickSearchAsync("Terraforming", 3);

        // Assert
        Assert.Single(results);
        Assert.Equal("Terraforming Mars", results[0].SpanishTitle);
        Assert.True(customRepo.QuickSearchCalled);
        Assert.False(customRepo.SearchAsyncCalled);
    }

    private class QuickSearchTestRepository : FakeGameRepository
    {
        public bool QuickSearchCalled { get; private set; }
        public bool SearchAsyncCalled { get; private set; }

        public override Task<IReadOnlyList<Game>> QuickSearchAsync(string term, int limit = 5, CancellationToken ct = default)
        {
            QuickSearchCalled = true;
            var game = new Game(167791, "Terraforming Mars", "Terraforming Mars", "Jacob Fryxelius", "FryxGames", 2016, "", "", "", 8.4, 6, 8.8,
                ConfrontationType.Competitive, GameStyle.Eurogame, true, new AgeRating(12, 12), LanguageDependence.Low, TableFootprint.StandardTable, new GameDuration(90, 120, 30));
            return Task.FromResult<IReadOnlyList<Game>>([game]);
        }

        public override Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default)
        {
            SearchAsyncCalled = true;
            return base.SearchAsync(criteria, page, pageSize, ct);
        }
    }
}
