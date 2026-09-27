using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Bgg;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class BggMassIngestionBackfillTests
{
    [Fact]
    public async Task PromoteReadyToCatalogBatchAsync_WhenGameAlreadyExists_UpdatesExistingAdditiveWithoutDuplicateKeyViolation()
    {
        // Arrange
        var stagingRepo = new FakeStagingRepo();
        var bggClient = new FakeBggClient();
        var geekDo = new FakeGeekDoClient();
        var images = new FakeImageStorageService();
        var ai = new FakeAiSummaryService();
        var gameRepo = new FakeGameRepo();
        using var httpClient = new HttpClient();
        var options = Options.Create(new BggMassIngestionOptions());

        var service = new BggMassIngestionService(
            stagingRepo,
            bggClient,
            geekDo,
            images,
            ai,
            gameRepo,
            httpClient,
            options,
            NullLogger<BggMassIngestionService>.Instance
        );

        // Ya existe un juego en el catálogo con BggId = 13 (ej. Catan básico antiguo sin fundas ni editorial española)
        var existingGame = new Game(
            bggId: 13,
            originalTitle: "Catan",
            spanishTitle: "Catan",
            designer: "Klaus Teuber",
            publisher: "Kosmos",
            yearPublished: 1995,
            coverImageUrl: "https://cdn.ludeka.com/catan.jpg",
            thumbnailUrl: "https://cdn.ludeka.com/catan_thumb.jpg",
            description: "Juego de comercio y colonización.",
            bggRating: 7.1,
            bggRank: 500,
            ludistRating: 7.5,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: false,
            age: new AgeRating(10, 10),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(60, 90, 20),
            scalability: new List<ScalabilityEntry>(),
            sleeves: new List<SleeveItem>()
        );
        gameRepo.Games.Add(existingGame);

        // El ítem en staging está listo para promoción con metadatos enriquecidos de INC-73
        var stagingItem = new BggCatalogStagingItem(13, "Catan", 1995, 500, 120000, 7.1, 7.2);
        stagingItem.MarkFetched(
            rawXml: "<item></item>",
            spanishTitle: "Catán",
            designer: "Klaus Teuber",
            publisher: "Kosmos",
            description: "Descripción enriquecida.",
            minPlayers: 3,
            maxPlayers: 4,
            playingTimeMinutes: 75,
            minAge: 10,
            bggRating: 7.1,
            minPlayTimeMinutes: 60,
            maxPlayTimeMinutes: 90,
            inferredFootprint: TableFootprint.StandardTable,
            scalability: new List<ScalabilityEntry>
            {
                new(3, "3", ScalabilityStatus.Recommended, 400, 800, 50),
                new(4, "4", ScalabilityStatus.MustPlay, 1200, 300, 20)
            },
            sleeves: new List<SleeveItem>
            {
                new("Estándar USA", 56, 87, 120, null)
            },
            spanishPublisher: "Devir",
            regionalPublishers: new List<RegionalPublisherEntry>
            {
                new("MX", "Devir México", "devir-mexico"),
                new("AR", "Buró de Juegos", "buro-de-juegos")
            }
        );
        stagingItem.MarkImagesCompleted("https://cdn.ludeka.com/catan.jpg", "https://cdn.ludeka.com/catan_thumb.jpg");
        stagingRepo.Items.Add(stagingItem);

        // Act
        int promotedCount = await service.PromoteReadyToCatalogBatchAsync(batchSize: 10);

        // Assert
        Assert.Equal(1, promotedCount);
        Assert.Single(gameRepo.Games); // No se duplicó el juego

        // Verificar enriquecimiento aditivo de campos de calidad y localización
        Assert.Equal("Devir", existingGame.SpanishPublisher);
        Assert.Equal(2, existingGame.RegionalPublishers.Count);
        Assert.NotEmpty(existingGame.Scalability);
        Assert.NotEmpty(existingGame.Sleeves);
    }

    [Fact]
    public async Task BackfillCatalogQualityBatchAsync_WhenNoGamesPending_ReturnsZeroProcessed()
    {
        // Arrange
        var stagingRepo = new FakeStagingRepo();
        var bggClient = new FakeBggClient();
        var geekDo = new FakeGeekDoClient();
        var images = new FakeImageStorageService();
        var ai = new FakeAiSummaryService();
        var gameRepo = new FakeGameRepo();
        using var httpClient = new HttpClient();
        var options = Options.Create(new BggMassIngestionOptions());

        var service = new BggMassIngestionService(
            stagingRepo,
            bggClient,
            geekDo,
            images,
            ai,
            gameRepo,
            httpClient,
            options,
            NullLogger<BggMassIngestionService>.Instance
        );

        // Act
        var result = await service.RunScheduledBackfillCatalogQualityBatchAsync(batchSize: 10);

        // Assert
        Assert.Equal(0, result.ProcessedCount);
        Assert.Equal(0, result.UpdatedCount);
    }

    [Fact]
    public async Task BackfillCatalogQualityBatchAsync_WhenGameHasNoCardsOrSpanishPublisher_EnrichesAndDoesNotRemainPending()
    {
        // Arrange
        var stagingRepo = new FakeStagingRepo();
        var bggClient = new FakeBggClient();
        var geekDo = new FakeGeekDoClient();
        var images = new FakeImageStorageService();
        var ai = new FakeAiSummaryService();
        var gameRepo = new FakeGameRepo();
        using var httpClient = new HttpClient();
        var options = Options.Create(new BggMassIngestionOptions());

        var service = new BggMassIngestionService(
            stagingRepo,
            bggClient,
            geekDo,
            images,
            ai,
            gameRepo,
            httpClient,
            options,
            NullLogger<BggMassIngestionService>.Instance
        );

        // Juego sin cartas (0 sleeves) ni editorial española en catálogo (ej. Hive)
        var hive = new Game(
            bggId: 2655,
            originalTitle: "Hive",
            spanishTitle: "Hive",
            designer: "John Yianni",
            publisher: "Gen42 Games",
            yearPublished: 2001,
            coverImageUrl: "https://cdn.ludeka.com/hive.jpg",
            thumbnailUrl: "https://cdn.ludeka.com/hive_thumb.jpg",
            description: "Juego de estrategia para 2 jugadores con fichas de insectos.",
            bggRating: 7.3,
            bggRank: 300,
            ludistRating: 7.6,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.FillerAbstract,
            isOfficialSolo: false,
            age: new AgeRating(9, 9),
            language: LanguageDependence.None,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(20, 20, 20),
            scalability: new List<ScalabilityEntry>(),
            sleeves: new List<SleeveItem>()
        );
        gameRepo.Games.Add(hive);

        // BGG devuelve escalabilidad pero 0 fundas y sin editorial española
        var fetchedHive = new Game(
            bggId: 2655,
            originalTitle: "Hive",
            spanishTitle: "Hive",
            designer: "John Yianni",
            publisher: "Gen42 Games",
            yearPublished: 2001,
            coverImageUrl: "https://cdn.ludeka.com/hive.jpg",
            thumbnailUrl: "https://cdn.ludeka.com/hive_thumb.jpg",
            description: "Descripción enriquecida",
            bggRating: 7.3,
            bggRank: 300,
            ludistRating: 7.6,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.FillerAbstract,
            isOfficialSolo: false,
            age: new AgeRating(9, 9),
            language: LanguageDependence.None,
            footprint: TableFootprint.SmallTable,
            duration: new GameDuration(15, 30, 20),
            scalability: new List<ScalabilityEntry>
            {
                new(2, "2", ScalabilityStatus.MustPlay, 850, 100, 10)
            },
            sleeves: new List<SleeveItem>() // 0 fundas
        );
        bggClient.Games[2655] = fetchedHive;

        // Act 1: Primer ciclo de enriquecimiento
        int pendingBefore = await service.GetPendingQualityBackfillCountAsync();
        Assert.Equal(1, pendingBefore);

        var firstResult = await service.RunScheduledBackfillCatalogQualityBatchAsync(batchSize: 10);

        // Assert 1: Se actualizó con éxito
        Assert.Equal(1, firstResult.EvaluatedCount);
        Assert.Equal(1, firstResult.UpdatedCount);
        Assert.Equal(0, firstResult.FailedCount);
        Assert.NotEmpty(hive.Scalability);
        Assert.Equal(TableFootprint.SmallTable, hive.Footprint);

        // Act 2: Segundo ciclo no debe re-procesar a Hive en un bucle infinito
        int pendingAfter = await service.GetPendingQualityBackfillCountAsync();
        Assert.Equal(0, pendingAfter);

        var secondResult = await service.RunScheduledBackfillCatalogQualityBatchAsync(batchSize: 10);
        Assert.Equal(0, secondResult.EvaluatedCount);
        Assert.Equal(0, secondResult.UpdatedCount);
    }

    #region Fakes

    private class FakeStagingRepo : IBggCatalogStagingRepository
    {
        public List<BggCatalogStagingItem> Items = [];

        public Task UpsertBatchAsync(IEnumerable<BggCatalogStagingItem> items, CancellationToken ct = default)
        {
            foreach (var item in items)
            {
                var existing = Items.FirstOrDefault(i => i.BggId == item.BggId);
                if (existing != null)
                {
                    Items.Remove(existing);
                }
                Items.Add(item);
            }
            return Task.CompletedTask;
        }

        public Task<BggCatalogStagingItem?> GetByBggIdAsync(int bggId, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(i => i.BggId == bggId));

        public Task UpdateAsync(BggCatalogStagingItem item, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateBatchAsync(IEnumerable<BggCatalogStagingItem> items, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingFetchBatchAsync(int batchSize, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<BggCatalogStagingItem>)Items.Where(i => i.FetchStatus == StagingFetchStatus.Pending).Take(batchSize).ToList());

        public Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingImagesBatchAsync(int batchSize, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<BggCatalogStagingItem>)Items.Where(i => i.ImagesStatus == StagingImagesStatus.Pending && i.FetchStatus == StagingFetchStatus.Fetched).Take(batchSize).ToList());

        public Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingAiBatchAsync(int batchSize, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<BggCatalogStagingItem>)Items.Where(i => i.AiStatus == StagingAiStatus.Pending && i.ImagesStatus == StagingImagesStatus.Completed).Take(batchSize).ToList());

        public Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingPromotionBatchAsync(int batchSize, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<BggCatalogStagingItem>)Items.Where(i => i.PromotionStatus == StagingPromotionStatus.Pending && i.ImagesStatus == StagingImagesStatus.Completed).Take(batchSize).ToList());

        public Task<BggStagingMetricsDto> GetMetricsAsync(CancellationToken ct = default)
            => Task.FromResult(new BggStagingMetricsDto(Items.Count, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        public Task ResetQuotaExceededStatusAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetTotalCountAsync(CancellationToken ct = default) => Task.FromResult(Items.Count);
        public Task ClearStagingAsync(CancellationToken ct = default) { Items.Clear(); return Task.CompletedTask; }
    }

    private class FakeBggClient : IBggClient
    {
        public Dictionary<int, Game> Games = [];
        public Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default) => Task.FromResult(Games.GetValueOrDefault(bggId));
        public Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<BggSearchResultDto>)Array.Empty<BggSearchResultDto>());
        public Task<IReadOnlyList<BggTopGameDto>> FetchTopGamesAsync(int limit = 50, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<BggTopGameDto>)Array.Empty<BggTopGameDto>());
    }

    private class FakeGeekDoClient : IGeekDoImagesClient
    {
        public Task<GeekDoGalleryImagesDto> GetTopVotedImagesAsync(int bggId, CancellationToken ct = default)
            => Task.FromResult(new GeekDoGalleryImagesDto(null, null, null));
    }

    private class FakeImageStorageService : IImageStorageService
    {
        public Task<string> UploadOptimizedImageAsync(Stream inputStream, string objectKey, int maxWidth = 1000, int quality = 82, CancellationToken ct = default)
            => Task.FromResult($"https://cdn.ludeka.com/{objectKey}");
        public Task<ImageVariantUrls> UploadGameImageVariantsAsync(Stream rawImageStream, int bggId, string imageType, CancellationToken ct = default)
            => Task.FromResult(new ImageVariantUrls($"https://cdn.ludeka.com/{bggId}/{imageType}.webp", $"https://cdn.ludeka.com/{bggId}/{imageType}_thumb.webp"));
        public Task<bool> DeleteImageAsync(string objectKey, CancellationToken ct = default) => Task.FromResult(true);
        public string GetPublicUrl(string objectKey) => $"https://cdn.ludeka.com/{objectKey}";
        public Task<GameImageUploadResult> SaveGameCoverAsync(string slug, Stream contentStream, string originalFileName, string contentType, CancellationToken ct = default)
            => Task.FromResult(new GameImageUploadResult(true, "url", null));
        public Task<GameImageUploadResult> SaveEventPosterAsync(string eventSlugOrId, Stream contentStream, string originalFileName, string contentType, CancellationToken ct = default)
            => Task.FromResult(new GameImageUploadResult(true, "url", null));
        public Task<GameImageUploadResult> SaveCommunityImageAsync(string subfolder, string identifier, Stream contentStream, string originalFileName, string contentType, CancellationToken ct = default)
            => Task.FromResult(new GameImageUploadResult(true, "url", null));
        public Task<GameImageUploadResult> ValidateCoverUrlAsync(string imageUrl, CancellationToken ct = default)
            => Task.FromResult(new GameImageUploadResult(true, imageUrl, null));
    }

    private class FakeAiSummaryService : IAiGameSummaryService
    {
        public Task<AiBatchResultDto> GenerateBatchSummariesAsync(IReadOnlyList<AiGameBatchInputDto> games, CancellationToken ct = default)
            => Task.FromResult(new AiBatchResultDto(true, false, new Dictionary<int, AiGameSummaryDto>(), null));
        public Task<AiGameSummaryDto> GenerateSummaryAsync(Game game, CancellationToken ct = default)
            => Task.FromResult(new AiGameSummaryDto(game.Id, game.SpanishTitle, "Ideal", "10+", "Mesa", "Resumen", "Model", DateTime.UtcNow));
        public Task<AiGameSummaryDto> EnsureSummaryForGameAsync(Guid gameId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AiBatchProcessingResultDto> ProcessPendingSummariesBatchAsync(int batchSize = 20, CancellationToken ct = default)
            => Task.FromResult(new AiBatchProcessingResultDto(0, 0, 0, []));
    }

    private class FakeGameRepo : IGameRepository
    {
        public List<Game> Games = [];
        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Games.FirstOrDefault(g => g.Id == id));
        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(Games.FirstOrDefault(g => g.Slug == slug));
        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default) => Task.FromResult(Games.FirstOrDefault(g => g.BggId == bggId));
        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default) => Task.FromResult(((IReadOnlyList<Game>)Games, Games.Count));
        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default) { Games.AddRange(games); return Task.CompletedTask; }
        public Task UpdateAsync(Game game, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> HasAnyAsync(CancellationToken ct = default) => Task.FromResult(Games.Count > 0);
        public Task<IReadOnlyList<Game>> GetGamesWithoutAiSummaryAsync(int limit = 20, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<Game>)Array.Empty<Game>());
        public Task<IReadOnlyList<Game>> GetByPublisherAsync(string publisherName, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<Game>)Array.Empty<Game>());
        public Task<IReadOnlyList<Game>> GetByDesignerAsync(string designerName, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<Game>)Array.Empty<Game>());
        public Task<IReadOnlyList<Game>> GetAllGamesAsync(CancellationToken ct = default) => Task.FromResult((IReadOnlyList<Game>)Games);
        public Task<IReadOnlyList<Game>> GetGamesPendingQualityBackfillAsync(int limit = 50, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<Game>)Games.Where(g => g.Scalability.Count == 0).Take(limit).ToList());
        public Task<int> GetGamesPendingQualityBackfillCountAsync(CancellationToken ct = default)
            => Task.FromResult(Games.Count(g => g.Scalability.Count == 0));
    }

    #endregion
}
