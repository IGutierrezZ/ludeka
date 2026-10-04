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

public class AffiliateClickServiceTests
{
    private class FakeGameRepository : IGameRepository
    {
        public readonly Dictionary<Guid, Game> Games = new();

        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Games.TryGetValue(id, out var g) ? g : null);

        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default)
            => Task.FromResult(Games.Values.FirstOrDefault(g => g.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase)));

        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default)
            => Task.FromResult(Games.Values.FirstOrDefault(g => g.BggId == bggId));

        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default)
            => Task.FromResult(((IReadOnlyList<Game>)Games.Values.ToList(), Games.Count));

        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default)
        {
            foreach (var g in games) Games[g.Id] = g;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Game game, CancellationToken ct = default)
        {
            Games[game.Id] = game;
            return Task.CompletedTask;
        }

        public Task<bool> HasAnyAsync(CancellationToken ct = default) => Task.FromResult(Games.Count > 0);
        public Task<IReadOnlyList<Game>> GetGamesWithPurchaseLinksAsync(int maxGames = 0, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<Game>)Games.Values.Where(g => g.PurchaseLinks.Count > 0).ToList());
        public Task<IReadOnlyList<Game>> GetAllBaseGamesAsync(CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<Game>)Games.Values.Where(g => !g.IsExpansion).ToList());
        public Task<IReadOnlyList<Game>> GetUnlinkedExpansionsAsync(CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<Game>)Games.Values.Where(g => g.IsExpansion && !g.BaseGameId.HasValue).ToList());
    }

    private class FakeAffiliateClickRepository : IAffiliateClickRepository
    {
        public readonly List<AffiliateClickLog> Clicks = [];

        public Task RecordClickAsync(AffiliateClickLog click, CancellationToken ct = default)
        {
            Clicks.Add(click);
            return Task.CompletedTask;
        }

        public Task<int> GetClickCountAsync(Guid? gameId = null, string? storeName = null, CancellationToken ct = default)
        {
            var query = Clicks.AsEnumerable();
            if (gameId.HasValue) query = query.Where(c => c.GameId == gameId.Value);
            if (!string.IsNullOrWhiteSpace(storeName)) query = query.Where(c => c.StoreName.Equals(storeName, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(query.Count());
        }

        public Task<IReadOnlyList<AffiliateClickLog>> GetRecentClicksAsync(int limit = 50, CancellationToken ct = default)
        {
            return Task.FromResult((IReadOnlyList<AffiliateClickLog>)Clicks.OrderByDescending(c => c.ClickedAtUtc).Take(limit).ToList());
        }
    }

    private readonly FakeGameRepository _gameRepo = new();
    private readonly FakeAffiliateClickRepository _clickRepo = new();
    private readonly AffiliateUrlResolver _resolver;
    private readonly AffiliateClickService _service;

    public AffiliateClickServiceTests()
    {
        var options = Options.Create(new AffiliateOptions
        {
            Enabled = true,
            Stores = new Dictionary<string, StoreAffiliateRule>(StringComparer.OrdinalIgnoreCase)
            {
                ["Zacatrus"] = new() { ParamName = "ref", AffiliateTag = "ludeka", DomainMatch = "zacatrus.es" },
                ["Amazon"] = new() { ParamName = "tag", AffiliateTag = "ludeka-21", DomainMatch = "amazon.es" }
            }
        });
        _resolver = new AffiliateUrlResolver(options);
        _service = new AffiliateClickService(
            _gameRepo,
            _resolver,
            _clickRepo,
            NullLogger<AffiliateClickService>.Instance
        );
    }

    private static Game CreateSampleGame()
    {
        var game = new Game(
            bggId: 266192,
            originalTitle: "Wingspan",
            spanishTitle: "Wingspan",
            designer: "Elizabeth Hargrave",
            publisher: "Maldito Games",
            yearPublished: 2019,
            coverImageUrl: "https://example.com/wingspan.jpg",
            thumbnailUrl: "https://example.com/wingspan-thumb.jpg",
            description: "Juego de aves.",
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
            purchaseLinks: [
                new GamePurchaseLink(
                    storeName: "Zacatrus",
                    affiliateUrl: "https://zacatrus.es/wingspan.html",
                    price: 49.95m,
                    currency: "€",
                    country: "España"
                )
            ]
        );
        return game;
    }

    [Fact]
    public async Task ResolveAndTrackRedirectAsync_WithExistingOffer_ShouldRecordClickAndReturnEnrichedUrl()
    {
        // Arrange
        var game = CreateSampleGame();
        await _gameRepo.AddRangeAsync([game]);

        // Act
        var result = await _service.ResolveAndTrackRedirectAsync("wingspan", "zacatrus", "España");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("ref=ludeka", result);
        Assert.Contains("zacatrus.es/wingspan.html", result);

        Assert.Single(_clickRepo.Clicks);
        var logged = _clickRepo.Clicks[0];
        Assert.Equal("wingspan", logged.GameSlug);
        Assert.Equal("Zacatrus", logged.StoreName);
        Assert.Equal("España", logged.Country);
    }

    [Fact]
    public async Task ResolveAndTrackRedirectAsync_WithKnownStoreWithoutOffer_ShouldGenerateSearchUrlAndRecordClick()
    {
        // Arrange
        var game = CreateSampleGame();
        await _gameRepo.AddRangeAsync([game]);

        // Act
        var result = await _service.ResolveAndTrackRedirectAsync("wingspan", "amazon", "España");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("amazon.es", result);
        Assert.Contains("tag=ludeka-21", result);
        Assert.Contains("Wingspan", result);

        Assert.Single(_clickRepo.Clicks);
        var logged = _clickRepo.Clicks[0];
        Assert.Equal("wingspan", logged.GameSlug);
        Assert.Equal("Amazon", logged.StoreName);
    }

    [Fact]
    public async Task ResolveAndTrackRedirectAsync_WithUnknownGame_ShouldReturnNullAndNotRecordClick()
    {
        // Act
        var result = await _service.ResolveAndTrackRedirectAsync("juego-inexistente", "zacatrus");

        // Assert
        Assert.Null(result);
        Assert.Empty(_clickRepo.Clicks);
    }

    [Fact]
    public async Task TrackDirectRedirectAsync_WithAllowedDomain_ShouldRecordClickAndReturnEnrichedUrl()
    {
        // Act
        var result = await _service.TrackDirectRedirectAsync(
            gameId: Guid.NewGuid(),
            gameSlug: "catan",
            gameTitle: "Catan",
            storeName: "Zacatrus",
            targetUrl: "https://zacatrus.es/catan.html",
            country: "España"
        );

        // Assert
        Assert.NotNull(result);
        Assert.Contains("ref=ludeka", result);
        Assert.Single(_clickRepo.Clicks);
    }

    [Fact]
    public async Task TrackDirectRedirectAsync_WithUnallowedDomain_ShouldRejectAndReturnNull()
    {
        // Act (intento de redirección abierta)
        var result = await _service.TrackDirectRedirectAsync(
            gameId: Guid.NewGuid(),
            gameSlug: "catan",
            gameTitle: "Catan",
            storeName: "SitioSospechoso",
            targetUrl: "https://evil.com/phishing",
            country: "España"
        );

        // Assert
        Assert.Null(result);
        Assert.Empty(_clickRepo.Clicks);
    }
}
