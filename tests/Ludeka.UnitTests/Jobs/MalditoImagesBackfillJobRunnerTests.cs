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

public class MalditoImagesBackfillJobRunnerTests
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

    private class FakeMalditoExtractor : IMalditoReleasesExtractor
    {
        public Dictionary<int, MalditoCatalogPageResultDto> Pages { get; } = new();
        public Dictionary<string, MalditoProductGalleryDto> Galleries { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> RequestedGalleryUrls { get; } = new();

        public Task<IReadOnlyList<EditorialReleaseItem>> ExtractReleasesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<EditorialReleaseItem>>([]);

        public IReadOnlyList<EditorialReleaseItem> ParseHtml(string homeHtml, string? catalogHtml = null) => [];

        public Task<MalditoProductGalleryDto?> ExtractProductGalleryAsync(string productUrl, CancellationToken ct = default)
        {
            RequestedGalleryUrls.Add(productUrl);
            return Task.FromResult(Galleries.TryGetValue(productUrl, out var gallery) ? gallery : null);
        }

        public Task<MalditoCatalogPageResultDto> ExtractCatalogPageAsync(int page = 1, CancellationToken ct = default)
        {
            if (Pages.TryGetValue(page, out var res))
            {
                return Task.FromResult(res);
            }

            return Task.FromResult(new MalditoCatalogPageResultDto([], false));
        }
    }

    private class FakeGameRepository : IGameRepository
    {
        public List<Game> Games { get; } = new();
        public List<Game> UpdatedGames { get; } = new();

        public Task<IReadOnlyList<Game>> GetAllGamesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Game>>(Games.ToList());

        public Task UpdateAsync(Game game, CancellationToken ct = default)
        {
            UpdatedGames.Add(game);
            int idx = Games.FindIndex(g => g.Id == game.Id);
            if (idx >= 0) Games[idx] = game;
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default) => Task.FromResult(Games.FirstOrDefault(g => g.BggId == bggId));
        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Games.FirstOrDefault(g => g.Id == id));
        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(Games.FirstOrDefault(g => g.Slug == slug));
        public Task<bool> HasAnyAsync(CancellationToken ct = default) => Task.FromResult(Games.Count > 0);
        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default)
            => Task.FromResult<(IReadOnlyList<Game>, int)>((Games.Take(pageSize).ToList(), Games.Count));
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
            publisher: "Maldito Games",
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
    public async Task RunAsync_MatchesByEanAndTitle_UpdatesImagesEanAndMalditoOffer()
    {
        // Arrange
        var coordinator = new FakeCoordinator();
        var extractor = new FakeMalditoExtractor();
        var gameRepo = new FakeGameRepository();

        var terraforming = CreateGame(167791, "terraforming-mars", "Terraforming Mars", ean: null);
        gameRepo.Games.Add(terraforming);

        extractor.Pages[1] = new MalditoCatalogPageResultDto(
            Items: new List<MalditoCatalogItemDto>
            {
                new("https://tienda.malditogames.com/34-terraforming-mars.html", "Terraforming Mars", "8436578810017", null)
            },
            HasNextPage: false);

        extractor.Galleries["https://tienda.malditogames.com/34-terraforming-mars.html"] = new MalditoProductGalleryDto(
            CoverImageUrl: "https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436578810017-1200-face3d.jpg",
            TableImageUrl: "https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436578810017-1200-frontflat.jpg",
            BackCoverImageUrl: "https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436578810017-1200-backflat.jpg",
            FrontFlatImageUrl: "https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436578810017-1200-frontflat.jpg",
            Ean: "8436578810017",
            Pvp: 65.00m);

        var runner = new MalditoImagesBackfillJobRunner(coordinator, extractor, gameRepo, NullLogger<MalditoImagesBackfillJobRunner>.Instance);

        // Act
        var outcome = await runner.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(JobLeaseOutcome.Completed, outcome);
        Assert.True(coordinator.ExecuteCalled);
        Assert.Equal(JobNames.MalditoImagesBackfill, coordinator.CapturedJobName);
        Assert.Equal(1, coordinator.CapturedResult!.Processed);

        Assert.Single(gameRepo.UpdatedGames);
        var updated = gameRepo.UpdatedGames[0];
        Assert.Equal("8436578810017", updated.Ean);
        Assert.Contains("face3d", updated.CoverImageUrl!);
        Assert.Contains("backflat", updated.BackCoverImageUrl!);

        var offer = Assert.Single(updated.PurchaseLinks, p => p.StoreName == "Maldito Games");
        Assert.Equal(65.00m, offer.Price);
        Assert.Equal("https://tienda.malditogames.com/34-terraforming-mars.html", offer.AffiliateUrl);
    }

    [Fact]
    public async Task RunAsync_WhenGameAlreadyHasCompleteMediaEanAndMalditoOffer_SkipsGallery()
    {
        // Arrange
        var coordinator = new FakeCoordinator();
        var extractor = new FakeMalditoExtractor();
        var gameRepo = new FakeGameRepository();

        var game = CreateGame(
            100,
            "crucero-galactico",
            "Crucero Galáctico",
            ean: "8436578819720",
            cover: "https://example.com/8436578819720-face3d.jpg",
            table: "https://example.com/8436578819720-table.jpg",
            back: "https://example.com/8436578819720-backflat.jpg",
            purchaseLinks: new[] { new GamePurchaseLink("Maldito Games", "https://tienda.malditogames.com/1259-crucero.html", 115.00m) });
        gameRepo.Games.Add(game);

        extractor.Pages[1] = new MalditoCatalogPageResultDto(
            Items: new List<MalditoCatalogItemDto>
            {
                new("https://tienda.malditogames.com/1259-crucero.html", "Crucero Galáctico", "8436578819720", null)
            },
            HasNextPage: false);

        var runner = new MalditoImagesBackfillJobRunner(coordinator, extractor, gameRepo, NullLogger<MalditoImagesBackfillJobRunner>.Instance);

        // Act
        var outcome = await runner.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(JobLeaseOutcome.Completed, outcome);
        Assert.Empty(extractor.RequestedGalleryUrls);
        Assert.Empty(gameRepo.UpdatedGames);
        Assert.Equal(0, coordinator.CapturedResult!.Processed);
    }

    [Fact]
    public async Task RunAsync_ToleratesSinglePageFailureAndContinues()
    {
        // Arrange
        var coordinator = new FakeCoordinator();
        var extractor = new FakeMalditoExtractor();
        var gameRepo = new FakeGameRepository();

        var game = CreateGame(200, "scythe", "Scythe", ean: "8436578810208");
        gameRepo.Games.Add(game);

        extractor.Pages[1] = new MalditoCatalogPageResultDto([], HasNextPage: true, Success: false);
        extractor.Pages[2] = new MalditoCatalogPageResultDto(
            Items: new List<MalditoCatalogItemDto>
            {
                new("https://tienda.malditogames.com/scythe.html", "Scythe", "8436578810208", null)
            },
            HasNextPage: false,
            Success: true);

        extractor.Galleries["https://tienda.malditogames.com/scythe.html"] = new MalditoProductGalleryDto(
            CoverImageUrl: "https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436578810208-1200-face3d.jpg",
            TableImageUrl: null,
            BackCoverImageUrl: null,
            FrontFlatImageUrl: null,
            Ean: "8436578810208",
            Pvp: 90.00m);

        var runner = new MalditoImagesBackfillJobRunner(coordinator, extractor, gameRepo, NullLogger<MalditoImagesBackfillJobRunner>.Instance);

        // Act
        var outcome = await runner.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(JobLeaseOutcome.Completed, outcome);
        Assert.Equal(1, coordinator.CapturedResult!.Processed);
        Assert.Equal(1, coordinator.CapturedResult.Failed);
        Assert.Single(gameRepo.UpdatedGames);
    }

    [Fact]
    public async Task RunAsync_AbortsWhenTwoConsecutivePagesFail()
    {
        // Arrange
        var coordinator = new FakeCoordinator();
        var extractor = new FakeMalditoExtractor();
        var gameRepo = new FakeGameRepository();

        extractor.Pages[1] = new MalditoCatalogPageResultDto([], HasNextPage: true, Success: false);
        extractor.Pages[2] = new MalditoCatalogPageResultDto([], HasNextPage: true, Success: false);
        extractor.Pages[3] = new MalditoCatalogPageResultDto(
            Items: new List<MalditoCatalogItemDto>
            {
                new("https://tienda.malditogames.com/floe.html", "Floe", "8436578819999", null)
            },
            HasNextPage: false,
            Success: true);

        var runner = new MalditoImagesBackfillJobRunner(coordinator, extractor, gameRepo, NullLogger<MalditoImagesBackfillJobRunner>.Instance);

        // Act
        var outcome = await runner.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(JobLeaseOutcome.Completed, outcome);
        Assert.Equal(0, coordinator.CapturedResult!.Processed);
        Assert.Equal(2, coordinator.CapturedResult.Failed);
        Assert.Empty(extractor.RequestedGalleryUrls);
    }
}
