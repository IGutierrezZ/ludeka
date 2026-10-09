using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Jobs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Ludeka.Jobs;
using Ludeka.Jobs.Runners;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ludeka.UnitTests.Jobs;

public class ArrakisImagesBackfillJobRunnerTests
{
    private class FakeCoordinator : IJobExecutionCoordinator
    {
        public bool ExecuteCalled { get; private set; }
        public string? CapturedJobName { get; private set; }
        public string? CapturedWindowKey { get; private set; }
        public JobWorkResult? CapturedResult { get; private set; }

        public async Task<JobLeaseOutcome> ExecuteWithWindowLeaseAsync(
            string jobName,
            string windowKey,
            Func<IJobHeartbeat, CancellationToken, Task<JobWorkResult>> work,
            CancellationToken ct = default)
        {
            ExecuteCalled = true;
            CapturedJobName = jobName;
            CapturedWindowKey = windowKey;

            CapturedResult = await work(new FakeHeartbeat(), ct);
            return JobLeaseOutcome.Completed;
        }

        private class FakeHeartbeat : IJobHeartbeat
        {
            public Task BeatAsync(CancellationToken ct = default) => Task.CompletedTask;
        }
    }

    private class FakeArrakisExtractor : IArrakisReleasesExtractor
    {
        public List<ArrakisCatalogItemDto> CatalogItems { get; set; } = [];
        public Dictionary<string, ArrakisProductGalleryDto> Galleries { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> RequestedGalleryUrls { get; } = [];

        public Task<IReadOnlyList<EditorialReleaseItem>> ExtractReleasesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<EditorialReleaseItem>>([]);

        public IReadOnlyList<EditorialReleaseItem> ParseHtml(string homeHtml, string? catalogHtml = null) => [];

        public Task<ArrakisProductGalleryDto?> ExtractProductGalleryAsync(string productUrl, CancellationToken ct = default)
        {
            RequestedGalleryUrls.Add(productUrl);
            return Task.FromResult(Galleries.TryGetValue(productUrl, out var gallery) ? gallery : null);
        }

        public ArrakisProductGalleryDto? ParseProductFichaHtml(string html, string productUrl) => null;

        public Task<IReadOnlyList<ArrakisCatalogItemDto>> ExtractFullCatalogAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ArrakisCatalogItemDto>>(CatalogItems);

        public IReadOnlyList<ArrakisCatalogItemDto> ParseCatalogHtml(string html) => CatalogItems;
    }

    private class FakeGameRepository : IGameRepository
    {
        public List<Game> Games { get; } = [];
        public List<Game> UpdatedGames { get; } = [];
        public List<Game> AddedGames { get; } = [];

        public Task<IReadOnlyList<Game>> GetAllGamesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Game>>(Games.ToList());

        public Task UpdateAsync(Game game, CancellationToken ct = default)
        {
            UpdatedGames.Add(game);
            int idx = Games.FindIndex(g => g.Id == game.Id);
            if (idx >= 0) Games[idx] = game;
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default)
        {
            var list = games.ToList();
            AddedGames.AddRange(list);
            Games.AddRange(list);
            return Task.CompletedTask;
        }

        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default) => Task.FromResult(Games.FirstOrDefault(g => g.BggId == bggId));
        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Games.FirstOrDefault(g => g.Id == id));
        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(Games.FirstOrDefault(g => g.Slug == slug));
        public Task<bool> HasAnyAsync(CancellationToken ct = default) => Task.FromResult(Games.Count > 0);
        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default)
            => Task.FromResult<(IReadOnlyList<Game>, int)>((Games.Take(pageSize).ToList(), Games.Count));
    }

    private class FakeBggClient : IBggClient
    {
        public Dictionary<int, Game> GamesById { get; } = [];

        public Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default)
            => Task.FromResult(GamesById.TryGetValue(bggId, out var g) ? g : null);

        public Task<IReadOnlyList<BggTopGameDto>> FetchTopGamesAsync(int limit = 50, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BggTopGameDto>>([]);

        public Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BggSearchResultDto>>([]);
    }

    private static Game CreateGame(
        int bggId,
        string slug,
        string title,
        string? ean = null,
        string? cover = null,
        string? table = null,
        string? back = null,
        IEnumerable<GamePurchaseLink>? purchaseLinks = null)
    {
        var game = new Game(
            bggId: bggId,
            originalTitle: title,
            spanishTitle: title,
            designer: "Autor Test",
            publisher: "Arrakis Games",
            yearPublished: 2024,
            coverImageUrl: cover ?? "https://example.com/cover.jpg",
            thumbnailUrl: cover ?? "https://example.com/thumb.jpg",
            description: "Descripción test",
            bggRating: 7.5,
            bggRank: 100,
            ludistRating: 8.0,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: false,
            age: new AgeRating(10, 10),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(45, 45, 45),
            purchaseLinks: purchaseLinks,
            customSlug: slug);

        if (!string.IsNullOrWhiteSpace(table) || !string.IsNullOrWhiteSpace(back))
        {
            game.UpdateMediaUrls(coverImageUrl: game.CoverImageUrl, thumbnailUrl: game.ThumbnailUrl, backCoverImageUrl: back, tableImageUrl: table);
        }

        if (!string.IsNullOrWhiteSpace(ean))
        {
            game.UpdateEan(ean);
        }

        return game;
    }

    [Fact]
    public void Name_MatchesExpectedConstant()
    {
        var runner = new ArrakisImagesBackfillJobRunner(
            new FakeCoordinator(),
            new FakeArrakisExtractor(),
            new FakeGameRepository(),
            NullLogger<ArrakisImagesBackfillJobRunner>.Instance);

        Assert.Equal(JobNames.ArrakisImagesBackfill, runner.Name);
    }

    [Fact]
    public async Task RunAsync_MatchesByBggIdAndTitle_UpdatesImagesEanAndArrakisOffer()
    {
        // Arrange
        var coordinator = new FakeCoordinator();
        var extractor = new FakeArrakisExtractor();
        var gameRepo = new FakeGameRepository();

        var spiritIsland = CreateGame(162886, "spirit-island", "Spirit Island", ean: null);
        gameRepo.Games.Add(spiritIsland);

        extractor.CatalogItems =
        [
            new("https://arrakisgames.com/spirit-island/", "Spirit Island", "https://arrakisgames.com/spirit.png")
        ];

        extractor.Galleries["https://arrakisgames.com/spirit-island/"] = new ArrakisProductGalleryDto(
            CoverImageUrl: "https://arrakisgames.com/wp-content/uploads/Spirit_HD.png",
            TableImageUrl: "https://arrakisgames.com/wp-content/uploads/Spirit_mesa.jpg",
            BackCoverImageUrl: null,
            FrontFlatImageUrl: null,
            Ean: "8421005001106",
            Pvp: 84.95m,
            BggId: 162886,
            BggUrl: "https://boardgamegeek.com/boardgame/162886/spirit-island",
            Title: "Spirit Island",
            StatusText: "Reimpresión: Noviembre 2026");

        var runner = new ArrakisImagesBackfillJobRunner(
            coordinator,
            extractor,
            gameRepo,
            NullLogger<ArrakisImagesBackfillJobRunner>.Instance);

        // Act
        var outcome = await runner.RunAsync(CancellationToken.None);

        // Assert
        Assert.True(coordinator.ExecuteCalled);
        Assert.Equal(JobNames.ArrakisImagesBackfill, coordinator.CapturedJobName);
        Assert.Single(gameRepo.UpdatedGames);

        var updated = gameRepo.UpdatedGames.First();
        Assert.Equal("8421005001106", updated.Ean);
        Assert.Equal("https://arrakisgames.com/wp-content/uploads/Spirit_HD.png", updated.CoverImageUrl);
        Assert.Equal("https://arrakisgames.com/wp-content/uploads/Spirit_mesa.jpg", updated.TableImageUrl);
        Assert.Equal("Arrakis Games", updated.SpanishPublisher);

        var arrakisOffer = Assert.Single(updated.PurchaseLinks, p => p.StoreName == "Arrakis Games");
        Assert.Equal(84.95m, arrakisOffer.Price);
        Assert.Equal("https://arrakisgames.com/spirit-island/", arrakisOffer.AffiliateUrl);
        Assert.True(arrakisOffer.InStock);
        Assert.Equal("España", arrakisOffer.Country);
    }

    [Fact]
    public async Task RunAsync_WhenGameNotFoundLocally_ImportsFromBggUsingBggId()
    {
        // Arrange
        var coordinator = new FakeCoordinator();
        var extractor = new FakeArrakisExtractor();
        var gameRepo = new FakeGameRepository();
        var bggClient = new FakeBggClient();

        extractor.CatalogItems =
        [
            new("https://arrakisgames.com/boonlake/", "Boonlake", "https://arrakisgames.com/boonlake.jpg")
        ];

        extractor.Galleries["https://arrakisgames.com/boonlake/"] = new ArrakisProductGalleryDto(
            CoverImageUrl: "https://arrakisgames.com/wp-content/uploads/Boonlake_caja.jpg",
            TableImageUrl: "https://arrakisgames.com/wp-content/uploads/Boonlake_table.jpg",
            BackCoverImageUrl: null,
            FrontFlatImageUrl: null,
            Ean: "8436577750185",
            Pvp: 64.95m,
            BggId: 343905,
            BggUrl: "https://boardgamegeek.com/boardgame/343905/boonlake",
            Title: "Boonlake",
            StatusText: "Disponible");

        var bggGame = CreateGame(343905, "boonlake", "Boonlake");
        bggClient.GamesById[343905] = bggGame;

        var runner = new ArrakisImagesBackfillJobRunner(
            coordinator,
            extractor,
            gameRepo,
            bggClient,
            NullLogger<ArrakisImagesBackfillJobRunner>.Instance);

        // Act
        var outcome = await runner.RunAsync(CancellationToken.None);

        // Assert
        Assert.True(coordinator.ExecuteCalled);
        Assert.Single(gameRepo.AddedGames);

        var imported = gameRepo.AddedGames.First();
        Assert.Equal(343905, imported.BggId);
        Assert.Equal("8436577750185", imported.Ean);
        Assert.Equal("https://arrakisgames.com/wp-content/uploads/Boonlake_caja.jpg", imported.CoverImageUrl);
        Assert.Equal("https://arrakisgames.com/wp-content/uploads/Boonlake_table.jpg", imported.TableImageUrl);
        Assert.Equal("Arrakis Games", imported.SpanishPublisher);

        var arrakisOffer = Assert.Single(imported.PurchaseLinks, p => p.StoreName == "Arrakis Games");
        Assert.Equal(64.95m, arrakisOffer.Price);
        Assert.True(arrakisOffer.InStock);
    }
}
