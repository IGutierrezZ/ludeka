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

public class DevirImagesBackfillJobRunnerTests
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

    private class FakeDevirExtractor : IDevirReleasesExtractor
    {
        public Dictionary<int, DevirCatalogPageResultDto> Pages { get; } = new();
        public Dictionary<string, DevirProductGalleryDto> Galleries { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> RequestedGalleryUrls { get; } = new();

        public Task<IReadOnlyList<EditorialReleaseItem>> ExtractReleasesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<EditorialReleaseItem>>([]);

        public IReadOnlyList<EditorialReleaseItem> ParseHtml(string html) => [];
        public IReadOnlyList<EditorialReleaseItem> ParseHtml(string html, DateOnly? referenceDate = null) => [];

        public Task<DevirProductGalleryDto?> ExtractProductGalleryAsync(string productUrl, CancellationToken ct = default)
        {
            RequestedGalleryUrls.Add(productUrl);
            return Task.FromResult(Galleries.TryGetValue(productUrl, out var gallery) ? gallery : null);
        }

        public DevirProductGalleryDto? ParseProductGalleryHtml(string html) => null;

        public Task<DevirCatalogPageResultDto> ExtractCatalogPageAsync(int page = 1, CancellationToken ct = default)
        {
            if (Pages.TryGetValue(page, out var res))
            {
                return Task.FromResult(res);
            }

            return Task.FromResult(new DevirCatalogPageResultDto([], false));
        }

        public DevirCatalogPageResultDto ParseCatalogPageHtml(string html) => new([], false);
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
            publisher: "Devir",
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
    public async Task RunAsync_WhenCatalogItemMatchesGameMissingMedia_FetchesGalleryAndUpdatesGame()
    {
        // Arrange
        var coordinator = new FakeCoordinator();
        var extractor = new FakeDevirExtractor();
        var gameRepo = new FakeGameRepository();

        var polilla = CreateGame(1111, "polilla-tramposa", "Polilla Tramposa", ean: "8436017221138", cover: "https://devir.es/old-cover.jpg");
        gameRepo.Games.Add(polilla);

        extractor.Pages[1] = new DevirCatalogPageResultDto(
            Items: new List<DevirCatalogItemDto>
            {
                new("https://devir.es/bichos-polilla-tramposa", "Polilla Tramposa", "8436017221138", "https://devir.es/face3d.jpg")
            },
            HasNextPage: false);

        extractor.Galleries["https://devir.es/bichos-polilla-tramposa"] = new DevirProductGalleryDto(
            CoverImageUrl: "https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436017221138-1200-face3d.jpg",
            TableImageUrl: "https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436017221138-1200-components1.jpg",
            BackCoverImageUrl: "https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436017221138-1200-backflat.jpg",
            FrontFlatImageUrl: "https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436017221138-1200-frontflat.jpg",
            Ean: "8436017221138",
            Pvp: 12.95m);

        var runner = new DevirImagesBackfillJobRunner(coordinator, extractor, gameRepo, NullLogger<DevirImagesBackfillJobRunner>.Instance);

        // Act
        var outcome = await runner.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(JobLeaseOutcome.Completed, outcome);
        Assert.True(coordinator.ExecuteCalled);
        Assert.Equal(JobNames.DevirImagesBackfill, coordinator.CapturedJobName);
        Assert.NotNull(coordinator.CapturedResult);
        Assert.Equal(1, coordinator.CapturedResult.Processed);
        Assert.Equal(0, coordinator.CapturedResult.Failed);

        Assert.Single(gameRepo.UpdatedGames);
        var updated = gameRepo.UpdatedGames.First();
        Assert.Equal("https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436017221138-1200-face3d.jpg", updated.CoverImageUrl);
        Assert.Equal("https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436017221138-1200-components1.jpg", updated.TableImageUrl);
        Assert.Equal("https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436017221138-1200-backflat.jpg", updated.BackCoverImageUrl);
    }

    [Fact]
    public async Task RunAsync_WhenGameAlreadyHasCompleteMediaEanAndOffer_SkipsGalleryExtraction()
    {
        // Arrange: un juego que ya tiene cover 3D, mesa, contraportada, EAN y oferta de Devir
        var coordinator = new FakeCoordinator();
        var extractor = new FakeDevirExtractor();
        var gameRepo = new FakeGameRepository();

        var catan = CreateGame(
            13,
            "catan",
            "Catan",
            ean: "8436017220018",
            cover: "https://devir.es/catan-face3d.jpg",
            table: "https://devir.es/catan-components1.jpg",
            back: "https://devir.es/catan-backflat.jpg",
            purchaseLinks: new[] { new GamePurchaseLink("Devir", "https://devir.es/catan", 45.00m) });
        gameRepo.Games.Add(catan);

        extractor.Pages[1] = new DevirCatalogPageResultDto(
            Items: new List<DevirCatalogItemDto>
            {
                new("https://devir.es/catan", "Catan", "8436017220018", "https://devir.es/catan-face3d.jpg")
            },
            HasNextPage: false);

        var runner = new DevirImagesBackfillJobRunner(coordinator, extractor, gameRepo, NullLogger<DevirImagesBackfillJobRunner>.Instance);

        // Act
        var outcome = await runner.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(JobLeaseOutcome.Completed, outcome);
        Assert.Empty(extractor.RequestedGalleryUrls); // No se consumió la ficha ni la red
        Assert.Empty(gameRepo.UpdatedGames);
        Assert.Equal(0, coordinator.CapturedResult!.Processed);
    }

    [Fact]
    public async Task RunAsync_WhenGameHasImages_StillUpdatesEanAndDevirOfferIfMissing()
    {
        // Arrange: juego con imágenes pero sin EAN ni oferta de Devir
        var coordinator = new FakeCoordinator();
        var extractor = new FakeDevirExtractor();
        var gameRepo = new FakeGameRepository();

        var game = CreateGame(
            14,
            "fantasma-blitz",
            "Fantasma Blitz",
            ean: null,
            cover: "https://devir.es/fantasma-face3d.jpg",
            table: "https://devir.es/fantasma-components1.jpg",
            back: "https://devir.es/fantasma-backflat.jpg");
        gameRepo.Games.Add(game);

        extractor.Pages[1] = new DevirCatalogPageResultDto(
            Items: new List<DevirCatalogItemDto>
            {
                new("https://devir.es/fantasma-blitz", "Fantasma Blitz", "8436017220124", null)
            },
            HasNextPage: false);

        extractor.Galleries["https://devir.es/fantasma-blitz"] = new DevirProductGalleryDto(
            CoverImageUrl: "https://devir.es/fantasma-face3d.jpg",
            TableImageUrl: "https://devir.es/fantasma-components1.jpg",
            BackCoverImageUrl: "https://devir.es/fantasma-backflat.jpg",
            FrontFlatImageUrl: null,
            Ean: "8436017220124",
            Pvp: 16.50m);

        var runner = new DevirImagesBackfillJobRunner(coordinator, extractor, gameRepo, NullLogger<DevirImagesBackfillJobRunner>.Instance);

        // Act
        var outcome = await runner.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(JobLeaseOutcome.Completed, outcome);
        Assert.Single(gameRepo.UpdatedGames);
        var updated = gameRepo.UpdatedGames[0];
        Assert.Equal("8436017220124", updated.Ean);
        var devirOffer = Assert.Single(updated.PurchaseLinks, p => p.StoreName == "Devir");
        Assert.Equal(16.50m, devirOffer.Price);
        Assert.Equal("https://devir.es/fantasma-blitz", devirOffer.AffiliateUrl);
    }

    [Fact]
    public async Task RunAsync_WhenDevirHasMultiplePages_IteratesThroughAllPages()
    {
        // Arrange
        var coordinator = new FakeCoordinator();
        var extractor = new FakeDevirExtractor();
        var gameRepo = new FakeGameRepository();

        var game1 = CreateGame(1, "juego-uno", "Juego Uno", ean: "8436589622340");
        var game2 = CreateGame(2, "juego-dos", "Juego Dos", ean: "8436625610973");
        gameRepo.Games.Add(game1);
        gameRepo.Games.Add(game2);

        extractor.Pages[1] = new DevirCatalogPageResultDto(
            Items: new List<DevirCatalogItemDto>
            {
                new("https://devir.es/juego-uno", "Juego Uno", "8436589622340", null)
            },
            HasNextPage: true);

        extractor.Pages[2] = new DevirCatalogPageResultDto(
            Items: new List<DevirCatalogItemDto>
            {
                new("https://devir.es/juego-dos", "Juego Dos", "8436625610973", null)
            },
            HasNextPage: false);

        extractor.Galleries["https://devir.es/juego-uno"] = new DevirProductGalleryDto(
            CoverImageUrl: "https://devir.es/uno-3d.jpg",
            TableImageUrl: "https://devir.es/uno-table.jpg",
            BackCoverImageUrl: null,
            FrontFlatImageUrl: null);

        extractor.Galleries["https://devir.es/juego-dos"] = new DevirProductGalleryDto(
            CoverImageUrl: "https://devir.es/dos-3d.jpg",
            TableImageUrl: null,
            BackCoverImageUrl: "https://devir.es/dos-back.jpg",
            FrontFlatImageUrl: null);

        var runner = new DevirImagesBackfillJobRunner(coordinator, extractor, gameRepo, NullLogger<DevirImagesBackfillJobRunner>.Instance);

        // Act
        var outcome = await runner.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(JobLeaseOutcome.Completed, outcome);
        Assert.Equal(2, coordinator.CapturedResult!.Processed);
        Assert.Equal(2, gameRepo.UpdatedGames.Count);
        Assert.Contains("https://devir.es/juego-uno", extractor.RequestedGalleryUrls);
        Assert.Contains("https://devir.es/juego-dos", extractor.RequestedGalleryUrls);
    }

    [Fact]
    public async Task RunAsync_ToleratesSinglePageFailureAndContinuesToNextPage()
    {
        var coordinator = new FakeCoordinator();
        var extractor = new FakeDevirExtractor();
        var gameRepo = new FakeGameRepository();

        var gameDos = CreateGame(2002, "juego-dos", "Juego Dos", ean: "8436625610973");
        gameRepo.Games.Add(gameDos);

        // Página 1 falla (Success = false)
        extractor.Pages[1] = new DevirCatalogPageResultDto([], HasNextPage: true, Success: false);

        // Página 2 funciona (Success = true) y devuelve Juego Dos
        extractor.Pages[2] = new DevirCatalogPageResultDto(
            Items: new List<DevirCatalogItemDto>
            {
                new("https://devir.es/juego-dos", "Juego Dos", "8436625610973", null)
            },
            HasNextPage: false,
            Success: true);

        extractor.Galleries["https://devir.es/juego-dos"] = new DevirProductGalleryDto(
            CoverImageUrl: "https://devir.es/dos-3d.jpg",
            TableImageUrl: null,
            BackCoverImageUrl: null,
            FrontFlatImageUrl: null);

        var runner = new DevirImagesBackfillJobRunner(coordinator, extractor, gameRepo, NullLogger<DevirImagesBackfillJobRunner>.Instance);

        var outcome = await runner.RunAsync(CancellationToken.None);

        Assert.Equal(JobLeaseOutcome.Completed, outcome);
        Assert.Equal(1, coordinator.CapturedResult!.Processed);
        Assert.Equal(1, coordinator.CapturedResult.Failed);
        Assert.Single(gameRepo.UpdatedGames);
    }

    [Fact]
    public async Task RunAsync_AbortsWhenTwoConsecutivePagesFail()
    {
        var coordinator = new FakeCoordinator();
        var extractor = new FakeDevirExtractor();
        var gameRepo = new FakeGameRepository();

        // Página 1 y 2 fallan consecutivamente
        extractor.Pages[1] = new DevirCatalogPageResultDto([], HasNextPage: true, Success: false);
        extractor.Pages[2] = new DevirCatalogPageResultDto([], HasNextPage: true, Success: false);
        extractor.Pages[3] = new DevirCatalogPageResultDto(
            Items: new List<DevirCatalogItemDto>
            {
                new("https://devir.es/juego-tres", "Juego Tres", "8436625619999", null)
            },
            HasNextPage: false,
            Success: true);

        var runner = new DevirImagesBackfillJobRunner(coordinator, extractor, gameRepo, NullLogger<DevirImagesBackfillJobRunner>.Instance);

        var outcome = await runner.RunAsync(CancellationToken.None);

        Assert.Equal(JobLeaseOutcome.Completed, outcome);
        Assert.Equal(0, coordinator.CapturedResult!.Processed);
        Assert.Equal(2, coordinator.CapturedResult.Failed);
        Assert.DoesNotContain("https://devir.es/juego-tres", extractor.RequestedGalleryUrls);
    }
}

