using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Releases;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class EditorialReleasesSyncServiceTests
{
    private readonly FakeDevirExtractor _devirExtractor = new();
    private readonly FakeMalditoExtractor _malditoExtractor = new();
    private readonly FakeArrakisExtractor _arrakisExtractor = new();
    private readonly FakeWeeklyReleaseRepository _weeklyReleaseRepo = new();
    private readonly FakeGameRepository _gameRepo = new();
    private readonly FakeBggClient _bggClient = new();
    private readonly FakeAiMatcherService _aiMatcher = new();

    private EditorialReleasesSyncService CreateService(bool useAiMatcher = false)
    {
        return new EditorialReleasesSyncService(
            _devirExtractor,
            _malditoExtractor,
            _arrakisExtractor,
            _weeklyReleaseRepo,
            _gameRepo,
            _bggClient,
            useAiMatcher ? _aiMatcher : null,
            NullLogger<EditorialReleasesSyncService>.Instance);
    }

    [Fact]
    public async Task SyncAllEditorialReleasesAsync_CreatesNewReleasesAndLinksExistingGames()
    {
        // Arrange: un juego existente en catálogo con EAN
        var existingGame = CreateSampleGame(1001, "vileborn", "Vileborn", "VILEBORN", yearPublished: 2026);
        existingGame.UpdateEan("8436625615992");
        _gameRepo.Add(existingGame);

        // Devir extractor devuelve 1 juego que coincide con el EAN
        _devirExtractor.ItemsToReturn = new List<EditorialReleaseItem>
        {
            new("VILEBORN", "Devir", new DateOnly(2026, 11, 1), "Noviembre 2026", 45m, "8436625615992", "https://devir.es/vileborn.jpg")
        };

        var service = CreateService();

        // Act
        var summary = await service.SyncAllEditorialReleasesAsync();

        // Assert
        Assert.Equal(1, summary.TotalFound);
        Assert.Equal(1, summary.CreatedCount);
        Assert.Equal(1, summary.GamesLinkedCount);
        Assert.Single(_weeklyReleaseRepo.Releases);

        var created = _weeklyReleaseRepo.Releases.First();
        Assert.Equal("VILEBORN", created.Title);
        Assert.Equal("Devir", created.Publisher);
        Assert.Equal(existingGame.Id, created.GameId);
        Assert.Equal(45m, created.EstimatedPvp);
        Assert.False(created.IsReprint);
    }

    [Fact]
    public async Task SyncAllEditorialReleasesAsync_ImportsFromBggWhenGameNotFoundLocally()
    {
        // Devir devuelve Daggerheart (no existe en catálogo local)
        _devirExtractor.ItemsToReturn = new List<EditorialReleaseItem>
        {
            new("DAGGERHEART", "Devir", new DateOnly(2027, 1, 1), "2027", 59.99m, null, "https://devir.es/dagger.jpg")
        };

        // BGG mock search y fetch
        _bggClient.SearchResults["DAGGERHEART"] = new List<BggSearchResultDto>
        {
            new(390001, "Daggerheart", 2025)
        };

        var bggGame = CreateSampleGame(390001, "daggerheart", "Daggerheart", "Daggerheart", yearPublished: 2025);
        _bggClient.GamesById[390001] = bggGame;

        var service = CreateService();

        // Act
        var summary = await service.SyncAllEditorialReleasesAsync();

        // Assert
        Assert.Equal(1, summary.TotalFound);
        Assert.Equal(1, summary.CreatedCount);
        Assert.Equal(1, summary.GamesImportedFromBggCount);
        Assert.Equal(1, summary.GamesLinkedCount);

        var release = _weeklyReleaseRepo.Releases.First();
        Assert.Equal("DAGGERHEART", release.Title);
        Assert.NotNull(release.GameId);
        Assert.Equal(bggGame.Id, release.GameId);
        // Como el año de BGG es 2025 (anterior al actual), se detecta como reimpresión / edición española
        Assert.True(release.IsReprint);
    }

    [Fact]
    public async Task SyncAllEditorialReleasesAsync_WhenReleaseHas3dBoxImage_UpdatesMatchedGameCover()
    {
        var game = CreateSampleGame(6001, "salton-sea", "Salton Sea", "Salton Sea", yearPublished: 2024);
        game.UpdateImages("https://example.com/2d-flat.jpg");
        _gameRepo.Add(game);

        _devirExtractor.ItemsToReturn = new List<EditorialReleaseItem>
        {
            new("Salton Sea", "Devir", new DateOnly(2026, 11, 1), "Noviembre 2026", 35.00m, null,
                "https://devir.es/img/8436625615555-face3d.jpg", SourceUrl: "https://devir.es/salton-sea", IsMonthOnly: true)
        };

        var service = CreateService();
        var summary = await service.SyncAllEditorialReleasesAsync();

        Assert.Equal(1, summary.TotalFound);
        Assert.Equal(1, summary.GamesLinkedCount);
        Assert.Contains("face3d", game.CoverImageUrl);
    }

    [Fact]
    public async Task SyncAllEditorialReleasesAsync_IncludesArrakisGamesPublisher()
    {
        _arrakisExtractor.ItemsToReturn =
        [
            new("Spirit Island", "Arrakis Games", new DateOnly(2026, 11, 1), "En Noviembre", 84.95m, "8421005001106", "https://arrakisgames.com/spirit.png", IsReprint: true)
        ];

        var service = CreateService();
        var summary = await service.SyncAllEditorialReleasesAsync();

        Assert.Contains(summary.PublisherResults, p => p.Publisher == "Arrakis Games");
        var arrakisResult = summary.PublisherResults.First(p => p.Publisher == "Arrakis Games");
        Assert.True(arrakisResult.Success);
        Assert.Equal(1, arrakisResult.ItemsFound);
        Assert.Equal(1, arrakisResult.ReleasesCreated);
    }

    [Fact]
    public async Task SyncPublisherReleasesAsync_Arrakis_ImportsFromBggDirectlyUsingBggId()
    {
        _arrakisExtractor.ItemsToReturn =
        [
            new("Spirit Island", "Arrakis Games", new DateOnly(2026, 11, 1), "En Noviembre", 84.95m, "8421005001106", "https://arrakisgames.com/spirit.png", IsReprint: true, BggId: 162886)
        ];

        var bggGame = CreateSampleGame(162886, "spirit-island", "Spirit Island", "Spirit Island", yearPublished: 2017);
        _bggClient.GamesById[162886] = bggGame;

        var service = CreateService();
        var result = await service.SyncPublisherReleasesAsync("Arrakis Games");

        Assert.True(result.Success);
        Assert.Equal(1, result.ItemsFound);
        Assert.Equal(1, result.GamesImported);
        Assert.Equal(1, result.GamesLinked);

        var release = Assert.Single(_weeklyReleaseRepo.Releases);
        Assert.Equal("Spirit Island", release.Title);
        Assert.Equal("Arrakis Games", release.Publisher);
        Assert.Equal(bggGame.Id, release.GameId);
        Assert.True(release.IsReprint);
        Assert.Equal("8421005001106", bggGame.Ean);
        Assert.Equal("Arrakis Games", bggGame.SpanishPublisher);
    }

    [Fact]
    public async Task SyncAllEditorialReleasesAsync_IsIdempotent_UpdatesExistingReleaseWithoutDuplicating()
    {
        var floeGame = CreateSampleGame(2001, "floe", "Floe", "Floe", yearPublished: 2026);
        _gameRepo.Add(floeGame);

        // Ya existe una novedad registrada previamente sin PVP y vinculada
        var initialRelease = new WeeklyRelease(
            title: "Floe",
            publisher: "Maldito Games",
            releaseDate: new DateOnly(2027, 1, 1),
            gameId: floeGame.Id,
            coverImageUrl: "https://maldito.es/floe.jpg",
            estimatedPvp: null);
        _weeklyReleaseRepo.Releases.Add(initialRelease);

        // Maldito ahora devuelve Floe con PVP confirmado
        _malditoExtractor.ItemsToReturn = new List<EditorialReleaseItem>
        {
            new("Floe", "Maldito Games", new DateOnly(2027, 1, 1), "2027", 49.95m, null, "https://maldito.es/floe.jpg", Notes: "Preventa abierta", IsMonthOnly: true)
        };

        var service = CreateService();

        // Act
        var summary = await service.SyncAllEditorialReleasesAsync();

        // Assert: no se crea un duplicado, se actualiza
        Assert.Equal(1, summary.TotalFound);
        Assert.Equal(0, summary.CreatedCount);
        Assert.Equal(1, summary.UpdatedCount);
        Assert.Single(_weeklyReleaseRepo.Releases);

        var updated = _weeklyReleaseRepo.Releases.First();
        Assert.Equal("Floe", updated.Title);
        Assert.Equal(49.95m, updated.EstimatedPvp);
        Assert.True(updated.IsMonthOnly);
    }

    [Fact]
    public async Task SyncAllEditorialReleasesAsync_WhenNoMatchingBoardGameFound_SendsToPendingModerationWithAiSuggestion()
    {
        // Maldito Games devuelve un título que no está en catálogo ("Crucero Galáctico")
        _malditoExtractor.ItemsToReturn = new List<EditorialReleaseItem>
        {
            new("Crucero Galáctico", "Maldito Games", new DateOnly(2026, 11, 1), "Noviembre 2026", 55m, null, "https://maldito.es/crucero.jpg")
        };

        _aiMatcher.SuggestionsByTitle["Crucero Galáctico"] = new AiReleaseMatchResultDto(
            SuggestedBggId: 367209,
            SuggestedTitle: "Galactic Cruise",
            Reasoning: "Edición en castellano de Maldito Games de Galactic Cruise.");

        var service = CreateService(useAiMatcher: true);

        // Act
        var summary = await service.SyncAllEditorialReleasesAsync();

        // Assert: no se descarta, se crea como novedad en estado PendingModeration con la sugerencia de IA
        Assert.Equal(1, summary.TotalFound);
        Assert.Equal(1, summary.CreatedCount);
        Assert.Equal(0, summary.GamesLinkedCount);
        Assert.Single(_weeklyReleaseRepo.Releases);

        var release = _weeklyReleaseRepo.Releases.First();
        Assert.Equal("Crucero Galáctico", release.Title);
        Assert.Equal(WeeklyReleaseStatus.PendingModeration, release.Status);
        Assert.Null(release.GameId);
        Assert.Equal(367209, release.AiSuggestedBggId);
        Assert.Equal("Galactic Cruise", release.AiSuggestedTitle);
        Assert.Contains("Galactic Cruise", release.AiMatchReasoning);
    }

    [Fact]
    public async Task SyncAllEditorialReleasesAsync_PurgesPreexistingLegacyOrphanReleasesWithoutGameId()
    {
        // Supongamos que en una ejecución anterior se guardó una novedad corrupta publicada sin GameId y sin IA
        var orphanRelease = new WeeklyRelease(
            title: "Corrupted Release",
            publisher: "Devir",
            releaseDate: new DateOnly(2026, 10, 1),
            gameId: null);
        _weeklyReleaseRepo.Releases.Add(orphanRelease);

        var service = CreateService();

        // Act: Devir devuelve 0 items en esta pasada
        _devirExtractor.ItemsToReturn = new List<EditorialReleaseItem>();
        await service.SyncPublisherReleasesAsync("Devir");

        // Assert: la novedad corrupta ha sido purgada automáticamente
        Assert.Empty(_weeklyReleaseRepo.Releases);
    }

    [Fact]
    public async Task SyncPublisherReleasesAsync_WhenMatchedGame_EnrichesTableAndBackCoverMediaUrls()
    {
        var game = CreateSampleGame(4521, "the-hanging-gardens", "The Hanging Gardens", "The Hanging Gardens", yearPublished: 2024);
        game.UpdateEan("8436625610973");
        _gameRepo.Add(game);

        _devirExtractor.ItemsToReturn = new List<EditorialReleaseItem>
        {
            new(
                Title: "THE HANGING GARDENS",
                Publisher: "Devir",
                ReleaseDate: new DateOnly(2026, 10, 19),
                TargetDateText: "19 de octubre",
                EstimatedPvp: 25m,
                Ean: "8436625610973",
                CoverImageUrl: "https://devir.es/face3d.jpg",
                SourceUrl: "https://devir.es/the-hanging-gardens",
                TableImageUrl: "https://devir.es/components1.jpg",
                BackCoverImageUrl: "https://devir.es/backflat.jpg")
        };

        var service = CreateService();

        // Act
        var result = await service.SyncPublisherReleasesAsync("Devir");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.GamesLinked);
        Assert.Equal("https://devir.es/face3d.jpg", game.CoverImageUrl);
        Assert.Equal("https://devir.es/components1.jpg", game.TableImageUrl);
        Assert.Equal("https://devir.es/backflat.jpg", game.BackCoverImageUrl);
    }

    [Fact]
    public async Task SyncAllEditorialReleasesAsync_DoesNotPurgePendingModerationReleases()
    {
        // Una novedad pendiente de moderación sin GameId nunca debe ser purgada automáticamente
        var pendingRelease = new WeeklyRelease(
            title: "Crucero Galáctico",
            publisher: "Maldito Games",
            releaseDate: new DateOnly(2026, 11, 1),
            gameId: null);
        pendingRelease.SetPendingModeration(367209, "Galactic Cruise", "Traducción pendiente de revisión");
        _weeklyReleaseRepo.Releases.Add(pendingRelease);

        var service = CreateService();

        // Act: Maldito devuelve 0 items en esta pasada
        _malditoExtractor.ItemsToReturn = new List<EditorialReleaseItem>();
        await service.SyncPublisherReleasesAsync("Maldito Games");

        // Assert: la novedad pendiente de moderación sigue existiendo
        Assert.Single(_weeklyReleaseRepo.Releases);
        Assert.Equal(WeeklyReleaseStatus.PendingModeration, _weeklyReleaseRepo.Releases.First().Status);
    }

    [Fact]
    public async Task SyncAllEditorialReleasesAsync_LinksMalditoGamesWithEanFromImage()
    {
        // Arrange: un juego existente en catálogo con el EAN extraído de la imagen
        var rangers = CreateSampleGame(3001, "earthborne-rangers", "Earthborne Rangers", "Earthborne Rangers", yearPublished: 2023);
        rangers.UpdateEan("8436578818099");
        _gameRepo.Add(rangers);

        _malditoExtractor.ItemsToReturn = new List<EditorialReleaseItem>
        {
            new(
                Title: "Earthborne Rangers",
                Publisher: "Maldito Games",
                ReleaseDate: new DateOnly(2026, 10, 22),
                TargetDateText: "22 de octubre",
                EstimatedPvp: 100m,
                Ean: "8436578818099",
                CoverImageUrl: "https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436578818099-1200-face3d.jpg",
                Notes: "Reimpresión oficial en Maldito Games",
                SourceUrl: "https://tienda.malditogames.com/1243-earthborne-rangers.html",
                IsReprint: true,
                IsMonthOnly: false)
        };

        var service = CreateService();

        // Act
        var summary = await service.SyncAllEditorialReleasesAsync();

        // Assert
        Assert.Equal(1, summary.TotalFound);
        Assert.Equal(1, summary.CreatedCount);
        Assert.Equal(1, summary.GamesLinkedCount);

        var created = _weeklyReleaseRepo.Releases.First();
        Assert.Equal("Earthborne Rangers", created.Title);
        Assert.Equal("Maldito Games", created.Publisher);
        Assert.Equal(rangers.Id, created.GameId);
        Assert.Equal(100m, created.EstimatedPvp);
        Assert.True(created.IsReprint);
    }

    [Fact]
    public async Task SyncAllEditorialReleasesAsync_MatchesGameByCleanCommercialTitle()
    {
        // Arrange: en catálogo existe "Viticulture"
        var viticulture = CreateSampleGame(4001, "viticulture", "Viticulture", "Viticulture", yearPublished: 2015);
        _gameRepo.Add(viticulture);

        // Maldito extrae "Viticulture Edición Esencial"
        _malditoExtractor.ItemsToReturn = new List<EditorialReleaseItem>
        {
            new(
                Title: "Viticulture Edición Esencial",
                Publisher: "Maldito Games",
                ReleaseDate: new DateOnly(2026, 10, 1),
                TargetDateText: "1 de octubre",
                EstimatedPvp: 60m,
                Ean: null,
                CoverImageUrl: null)
        };

        var service = CreateService();

        // Act
        var summary = await service.SyncAllEditorialReleasesAsync();

        // Assert: se limpia la coletilla y vincula a Viticulture
        Assert.Equal(1, summary.TotalFound);
        Assert.Equal(1, summary.GamesLinkedCount);
        var created = _weeklyReleaseRepo.Releases.First();
        Assert.Equal(viticulture.Id, created.GameId);
    }

    [Fact]
    public async Task SyncAllEditorialReleasesAsync_MatchesGameByBaseTitle()
    {
        // Arrange: en catálogo existe "Castle Combo"
        var castleCombo = CreateSampleGame(5001, "castle-combo", "Castle Combo", "Castle Combo", yearPublished: 2024);
        _gameRepo.Add(castleCombo);

        // Maldito extrae "Castle Combo - ¡Fuera de la mazmorra!"
        _malditoExtractor.ItemsToReturn = new List<EditorialReleaseItem>
        {
            new(
                Title: "Castle Combo - ¡Fuera de la mazmorra!",
                Publisher: "Maldito Games",
                ReleaseDate: null,
                TargetDateText: null,
                EstimatedPvp: 6m,
                Ean: "8436625618603",
                CoverImageUrl: null)
        };

        var service = CreateService();

        // Act
        var summary = await service.SyncAllEditorialReleasesAsync();

        // Assert: coincide por título base "Castle Combo"
        Assert.Equal(1, summary.TotalFound);
        Assert.Equal(1, summary.GamesLinkedCount);
        var created = _weeklyReleaseRepo.Releases.First();
        Assert.Equal(castleCombo.Id, created.GameId);
    }

    [Theory]
    [InlineData("Viticulture Edición Esencial", "Viticulture")]
    [InlineData("Speakeasy Edición Kickstarter", "Speakeasy")]
    [InlineData("Revenant: Edición Almirante", "Revenant")]
    [InlineData("Revenant: Edición Almirante con pintado Wash", "Revenant")]
    [InlineData("Juego Base", "Juego Base")]
    public void CleanCommercialTitle_RemovesCommercialSuffixes(string raw, string expected)
    {
        var cleaned = EditorialReleasesSyncService.CleanCommercialTitle(raw);
        Assert.Equal(expected, cleaned);
    }

    [Theory]
    [InlineData("Castle Combo - ¡Fuera de la mazmorra!", "Castle Combo")]
    [InlineData("Wingspan - Packs de cartas diseñadas por fans – Conjunto 1", "Wingspan")]
    [InlineData("Una aventura salvaje - Senderos", "Una aventura salvaje")]
    [InlineData("Revenant: Razón y Fe", "Revenant")]
    [InlineData("Scythe", "Scythe")]
    public void ExtractBaseTitle_ExtractsPrefixProperly(string raw, string expected)
    {
        var baseTitle = EditorialReleasesSyncService.ExtractBaseTitle(raw);
        Assert.Equal(expected, baseTitle);
    }

    [Fact]
    public async Task SyncAllEditorialReleasesAsync_WhenItemHasSubtitleOrIsModule_DoesNotOverwriteBaseGameCoverOrEan()
    {
        // Arrange: juego base Viticulture en catálogo con carátula oficial limpia y sin EAN
        var viticulture = CreateSampleGame(180263, "viticulture", "Viticulture Essential Edition", "Viticulture", yearPublished: 2015);
        viticulture.UpdateImages("/images/games/viticulture.png", "/images/games/viticulture.webp");
        _gameRepo.Add(viticulture);

        // Maldito extractor devuelve una expansión o módulo con subtítulo ("Viticulture: Bordeaux") y caja 3D
        const string bordeauxCover = "https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436625618092-1200-face3d.jpg";
        _malditoExtractor.ItemsToReturn = new List<EditorialReleaseItem>
        {
            new(
                Title: "Viticulture: Bordeaux",
                Publisher: "Maldito Games",
                ReleaseDate: new DateOnly(2026, 10, 1),
                TargetDateText: "Octubre 2026",
                EstimatedPvp: 20.00m,
                Ean: "8436625618092",
                CoverImageUrl: bordeauxCover,
                Notes: "Novedad en catálogo de Maldito Games",
                SourceUrl: "https://tienda.malditogames.com/viticulture-bordeaux.html")
        };

        var service = CreateService();

        // Act
        var summary = await service.SyncAllEditorialReleasesAsync();

        // Assert
        Assert.Equal(1, summary.TotalFound);
        Assert.Equal(1, summary.CreatedCount);
        Assert.Equal(1, summary.GamesLinkedCount);

        // La novedad semanal se vincula a Viticulture para que el usuario pueda navegar a la ficha
        var release = _weeklyReleaseRepo.Releases.First();
        Assert.Equal(viticulture.Id, release.GameId);
        Assert.Equal("Viticulture: Bordeaux", release.Title);
        Assert.Equal(bordeauxCover, release.CoverImageUrl);

        // PERO el juego base NUNCA debe ver su carátula, miniatura o EAN contaminados por la expansión
        var storedGame = await _gameRepo.GetByIdAsync(viticulture.Id);
        Assert.NotNull(storedGame);
        Assert.Equal("/images/games/viticulture.png", storedGame.CoverImageUrl);
        Assert.Equal("/images/games/viticulture.webp", storedGame.ThumbnailUrl);
        Assert.Null(storedGame.Ean);
    }

    private static Game CreateSampleGame(int bggId, string slug, string originalTitle, string spanishTitle, int yearPublished)
    {
        return new Game(
            bggId: bggId,
            originalTitle: originalTitle,
            spanishTitle: spanishTitle,
            designer: "Autor Test",
            publisher: "Editorial Test",
            yearPublished: yearPublished,
            coverImageUrl: "https://example.com/cover.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
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
            customSlug: slug);
    }

    // --- Fakes ---

    private class FakeDevirExtractor : IDevirReleasesExtractor
    {
        public List<EditorialReleaseItem> ItemsToReturn { get; set; } = [];
        public DevirProductGalleryDto? GalleryToReturn { get; set; }
        public Task<IReadOnlyList<EditorialReleaseItem>> ExtractReleasesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<EditorialReleaseItem>>(ItemsToReturn);
        public IReadOnlyList<EditorialReleaseItem> ParseHtml(string html) => ItemsToReturn;
        public IReadOnlyList<EditorialReleaseItem> ParseHtml(string html, DateOnly? referenceDate = null) => ItemsToReturn;
        public Task<DevirProductGalleryDto?> ExtractProductGalleryAsync(string productUrl, CancellationToken ct = default)
            => Task.FromResult(GalleryToReturn);
        public DevirProductGalleryDto? ParseProductGalleryHtml(string html) => GalleryToReturn;
        public Task<DevirCatalogPageResultDto> ExtractCatalogPageAsync(int page = 1, CancellationToken ct = default)
            => Task.FromResult(new DevirCatalogPageResultDto(CatalogItemsToReturn, HasNextPageToReturn));
        public DevirCatalogPageResultDto ParseCatalogPageHtml(string html)
            => new DevirCatalogPageResultDto(CatalogItemsToReturn, HasNextPageToReturn);
        public List<DevirCatalogItemDto> CatalogItemsToReturn { get; set; } = [];
        public bool HasNextPageToReturn { get; set; }
    }

    private class FakeMalditoExtractor : IMalditoReleasesExtractor
    {
        public List<EditorialReleaseItem> ItemsToReturn { get; set; } = [];
        public Task<IReadOnlyList<EditorialReleaseItem>> ExtractReleasesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<EditorialReleaseItem>>(ItemsToReturn);
        public IReadOnlyList<EditorialReleaseItem> ParseHtml(string homeHtml, string? catalogHtml = null) => ItemsToReturn;
        public Task<MalditoProductGalleryDto?> ExtractProductGalleryAsync(string productUrl, CancellationToken ct = default)
            => Task.FromResult<MalditoProductGalleryDto?>(null);
        public Task<MalditoCatalogPageResultDto> ExtractCatalogPageAsync(int page = 1, CancellationToken ct = default)
            => Task.FromResult(new MalditoCatalogPageResultDto([], false));
    }

    private class FakeArrakisExtractor : IArrakisReleasesExtractor
    {
        public List<EditorialReleaseItem> ItemsToReturn { get; set; } = [];
        public Task<IReadOnlyList<EditorialReleaseItem>> ExtractReleasesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<EditorialReleaseItem>>(ItemsToReturn);
        public IReadOnlyList<EditorialReleaseItem> ParseHtml(string homeHtml, string? catalogHtml = null) => ItemsToReturn;
        public Task<ArrakisProductGalleryDto?> ExtractProductGalleryAsync(string productUrl, CancellationToken ct = default)
            => Task.FromResult<ArrakisProductGalleryDto?>(null);
        public ArrakisProductGalleryDto? ParseProductFichaHtml(string html, string productUrl) => null;
        public Task<IReadOnlyList<ArrakisCatalogItemDto>> ExtractFullCatalogAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ArrakisCatalogItemDto>>([]);
        public IReadOnlyList<ArrakisCatalogItemDto> ParseCatalogHtml(string html) => [];
    }

    private class FakeWeeklyReleaseRepository : IWeeklyReleaseRepository
    {
        public List<WeeklyRelease> Releases { get; } = [];

        public Task AddAsync(WeeklyRelease release, CancellationToken ct = default)
        {
            Releases.Add(release);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            Releases.RemoveAll(r => r.Id == id);
            return Task.CompletedTask;
        }

        public Task<WeeklyRelease?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Releases.FirstOrDefault(r => r.Id == id));

        public Task<IReadOnlyList<WeeklyRelease>> GetReleasesAsync(DateOnly? fromDate = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WeeklyRelease>>(Releases.ToList());

        public Task UpdateAsync(WeeklyRelease release, CancellationToken ct = default)
        {
            int idx = Releases.FindIndex(r => r.Id == release.Id);
            if (idx >= 0) Releases[idx] = release;
            return Task.CompletedTask;
        }
    }

    private class FakeGameRepository : IGameRepository
    {
        private readonly List<Game> _games = [];

        public void Add(Game game) => _games.Add(game);

        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default)
        {
            _games.AddRange(games);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Game>> GetAllGamesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Game>>(_games.ToList());

        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default)
            => Task.FromResult(_games.FirstOrDefault(g => g.BggId == bggId));

        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_games.FirstOrDefault(g => g.Id == id));

        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default)
            => Task.FromResult(_games.FirstOrDefault(g => g.Slug == slug));

        public Task<bool> HasAnyAsync(CancellationToken ct = default)
            => Task.FromResult(_games.Count > 0);

        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default)
            => Task.FromResult<(IReadOnlyList<Game>, int)>((_games.Take(pageSize).ToList(), _games.Count));

        public Task UpdateAsync(Game game, CancellationToken ct = default)
        {
            int idx = _games.FindIndex(g => g.Id == game.Id);
            if (idx >= 0) _games[idx] = game;
            return Task.CompletedTask;
        }
    }

    private class FakeBggClient : IBggClient
    {
        public Dictionary<string, List<BggSearchResultDto>> SearchResults { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<int, Game> GamesById { get; } = [];

        public Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default)
            => Task.FromResult(GamesById.TryGetValue(bggId, out var g) ? g : null);

        public Task<IReadOnlyList<BggTopGameDto>> FetchTopGamesAsync(int limit = 50, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BggTopGameDto>>([]);

        public Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BggSearchResultDto>>(
                SearchResults.TryGetValue(query, out var list) ? list : []);
    }

    private class FakeAiMatcherService : IReleaseAiMatcherService
    {
        public Dictionary<string, AiReleaseMatchResultDto> SuggestionsByTitle { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<AiReleaseMatchResultDto> SuggestMatchAsync(
            string rawTitle,
            string publisher,
            decimal? estimatedPvp,
            string? notes,
            CancellationToken ct = default)
        {
            if (SuggestionsByTitle.TryGetValue(rawTitle, out var match))
            {
                return Task.FromResult(match);
            }

            return Task.FromResult(new AiReleaseMatchResultDto(
                SuggestedBggId: null,
                SuggestedTitle: rawTitle,
                Reasoning: $"Sugerencia simulada para '{rawTitle}'"));
        }
    }
}
