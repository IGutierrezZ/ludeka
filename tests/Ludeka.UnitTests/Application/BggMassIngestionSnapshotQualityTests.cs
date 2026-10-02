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

public class BggMassIngestionSnapshotQualityTests
{
    [Fact]
    public async Task BackfillCatalogQualityBatch_WhenSnapshotExists_ParsesFromSnapshotAndBypassesNetwork()
    {
        // Arrange
        var stagingRepo = new FakeStagingRepo();
        var bggClient = new CountingBggClient();
        var geekDo = new FakeGeekDoClient();
        var images = new FakeImageStorageService();
        var ai = new FakeAiSummaryService();
        var gameRepo = new FakeGameRepo();
        var snapshotRepo = new FakeSnapshotRepo();
        using var httpClient = new HttpClient();
        var options = Options.Create(new BggMassIngestionOptions());

        var existingGame = CreateTestGame(bggId: 42, title: "Galaxy Trucker", withEmptyScalability: true);
        gameRepo.Games.Add(existingGame);

        // Snapshot satélite disponible en base de datos local
        var snapshot = new BggRawSnapshot(42, "{\"item\":{\"id\":\"42\",\"name\":\"Galaxy Trucker\"}}");
        snapshotRepo.Snapshots[42] = snapshot;

        // El parser recrea el juego con escalabilidad y fundas enriquecidas
        bggClient.ParsedGameFactory = json =>
        {
            var g = CreateTestGame(bggId: 42, title: "Galaxy Trucker", withEmptyScalability: false);
            g.UpdateScalability([
                new ScalabilityEntry(2, "2J", ScalabilityStatus.Recommended, BestVotes: 10, RecommendedVotes: 40, NotRecommendedVotes: 5),
                new ScalabilityEntry(4, "4J", ScalabilityStatus.MustPlay, BestVotes: 80, RecommendedVotes: 20, NotRecommendedVotes: 2)
            ]);
            g.UpdateSleeves([
                new SleeveItem("Mini Euro", 45, 68, 120, null)
            ]);
            return g;
        };

        var service = new BggMassIngestionService(
            stagingRepo,
            bggClient,
            geekDo,
            images,
            ai,
            gameRepo,
            httpClient,
            options,
            NullLogger<BggMassIngestionService>.Instance,
            snapshotRepo: snapshotRepo
        );

        // Act
        var result = await service.BackfillCatalogQualityBatchAsync(afterBggId: 0, batchSize: 10);

        // Assert
        Assert.Equal(1, result.EvaluatedCount);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, bggClient.NetworkFetchCount); // CERO llamadas de red HTTP a BGG
        Assert.Equal(1, bggClient.ParseFromJsonCount); // Reconstituido desde snapshot local

        Assert.Equal(2, existingGame.Scalability.Count);
        Assert.Equal(ScalabilityStatus.MustPlay, existingGame.Scalability.First(s => s.PlayerCount == 4).Status);
        Assert.Single(existingGame.Sleeves);
        Assert.Equal("Mini Euro", existingGame.Sleeves[0].FormatName);
    }

    [Fact]
    public async Task BackfillCatalogQualityBatch_WhenBothLackCommunityVotes_DoesNotMarkEnrichedPreventingInfiniteLoop()
    {
        // Arrange
        var stagingRepo = new FakeStagingRepo();
        var bggClient = new CountingBggClient();
        var geekDo = new FakeGeekDoClient();
        var images = new FakeImageStorageService();
        var ai = new FakeAiSummaryService();
        var gameRepo = new FakeGameRepo();
        var snapshotRepo = new FakeSnapshotRepo();
        using var httpClient = new HttpClient();
        var options = Options.Create(new BggMassIngestionOptions());

        // Juego con fallback (1J y 4J con 0 votos)
        var existingGame = CreateTestGame(bggId: 100, title: "Juego Sin Votos", withEmptyScalability: false);
        existingGame.UpdateScalability([
            new ScalabilityEntry(1, "1J", ScalabilityStatus.Recommended, BestVotes: 0, RecommendedVotes: 0, NotRecommendedVotes: 0),
            new ScalabilityEntry(4, "4J", ScalabilityStatus.Recommended, BestVotes: 0, RecommendedVotes: 0, NotRecommendedVotes: 0)
        ]);
        gameRepo.Games.Add(existingGame);

        // El snapshot también devuelve 1J y 4J con 0 votos (sin votos en BGG)
        var snapshot = new BggRawSnapshot(100, "{\"item\":{\"id\":\"100\"}}");
        snapshotRepo.Snapshots[100] = snapshot;

        bggClient.ParsedGameFactory = json =>
        {
            var g = CreateTestGame(bggId: 100, title: "Juego Sin Votos", withEmptyScalability: false);
            g.UpdateScalability([
                new ScalabilityEntry(1, "1J", ScalabilityStatus.Recommended, BestVotes: 0, RecommendedVotes: 0, NotRecommendedVotes: 0),
                new ScalabilityEntry(4, "4J", ScalabilityStatus.Recommended, BestVotes: 0, RecommendedVotes: 0, NotRecommendedVotes: 0)
            ]);
            return g;
        };

        var service = new BggMassIngestionService(
            stagingRepo,
            bggClient,
            geekDo,
            images,
            ai,
            gameRepo,
            httpClient,
            options,
            NullLogger<BggMassIngestionService>.Instance,
            snapshotRepo: snapshotRepo
        );

        // Act
        var result = await service.BackfillCatalogQualityBatchAsync(afterBggId: 0, batchSize: 10);

        // Assert
        Assert.Equal(1, result.EvaluatedCount);
        Assert.Equal(0, result.UpdatedCount); // No se modificó innecesariamente
        Assert.Equal(100, result.LastBggIdProcessed);
    }

    [Fact]
    public async Task BackfillCatalogQualityBatch_WithCursor_PaginatesMonotonicallyAndCompletesWithoutLooping()
    {
        // Arrange
        var stagingRepo = new FakeStagingRepo();
        var bggClient = new CountingBggClient();
        var geekDo = new FakeGeekDoClient();
        var images = new FakeImageStorageService();
        var ai = new FakeAiSummaryService();
        var gameRepo = new FakeGameRepo();
        var snapshotRepo = new FakeSnapshotRepo();
        using var httpClient = new HttpClient();
        var options = Options.Create(new BggMassIngestionOptions());

        // 3 juegos pendientes
        gameRepo.Games.Add(CreateTestGame(bggId: 10, title: "Juego 10", withEmptyScalability: true));
        gameRepo.Games.Add(CreateTestGame(bggId: 20, title: "Juego 20", withEmptyScalability: true));
        gameRepo.Games.Add(CreateTestGame(bggId: 30, title: "Juego 30", withEmptyScalability: true));

        var service = new BggMassIngestionService(
            stagingRepo,
            bggClient,
            geekDo,
            images,
            ai,
            gameRepo,
            httpClient,
            options,
            NullLogger<BggMassIngestionService>.Instance,
            snapshotRepo: snapshotRepo
        );

        // Act - Lote 1 con batchSize = 2
        var batch1 = await service.BackfillCatalogQualityBatchAsync(afterBggId: 0, batchSize: 2);

        // Assert Lote 1
        Assert.Equal(2, batch1.EvaluatedCount);
        Assert.Equal(20, batch1.LastBggIdProcessed);
        Assert.True(batch1.HasMore);

        // Act - Lote 2 continuando con cursor = 20
        var batch2 = await service.BackfillCatalogQualityBatchAsync(afterBggId: batch1.LastBggIdProcessed, batchSize: 2);

        // Assert Lote 2
        Assert.Equal(1, batch2.EvaluatedCount);
        Assert.Equal(30, batch2.LastBggIdProcessed);
        Assert.False(batch2.HasMore); // Ya no hay más títulos, bucle termina naturalmente
    }

    private static Game CreateTestGame(int bggId, string title, bool withEmptyScalability)
    {
        return new Game(
            bggId: bggId,
            originalTitle: title,
            spanishTitle: title,
            designer: "Autor Test",
            publisher: "Editorial Test",
            yearPublished: 2020,
            coverImageUrl: "https://cdn.ludeka.com/cover.jpg",
            thumbnailUrl: "https://cdn.ludeka.com/thumb.jpg",
            description: "Descripción de prueba",
            bggRating: 7.5,
            bggRank: 100,
            ludistRating: 7.8,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: false,
            age: new AgeRating(10, 10),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(30, 60, 15),
            scalability: withEmptyScalability ? [] : [
                new ScalabilityEntry(1, "1J", ScalabilityStatus.Recommended, 0, 0, 0)
            ],
            sleeves: []
        );
    }

    #region Fakes

    private class CountingBggClient : IBggClient
    {
        public int NetworkFetchCount { get; private set; }
        public int ParseFromJsonCount { get; private set; }
        public Func<string, Game?>? ParsedGameFactory { get; set; }

        public Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default)
        {
            NetworkFetchCount++;
            return Task.FromResult<Game?>(null);
        }

        public Game? ParseGameFromRawJson(string rawJson)
        {
            ParseFromJsonCount++;
            return ParsedGameFactory?.Invoke(rawJson);
        }

        public Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BggSearchResultDto>>([]);

        public Task<IReadOnlyList<BggTopGameDto>> FetchTopGamesAsync(int limit = 50, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BggTopGameDto>>([]);
    }

    private class FakeSnapshotRepo : IBggRawSnapshotRepository
    {
        public Dictionary<int, BggRawSnapshot> Snapshots = [];

        public Task<BggRawSnapshot?> GetByBggIdAsync(int bggId, CancellationToken ct = default)
            => Task.FromResult(Snapshots.GetValueOrDefault(bggId));

        public Task UpsertAsync(BggRawSnapshot snapshot, CancellationToken ct = default)
        {
            Snapshots[snapshot.BggId] = snapshot;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<int>> GetMissingBggIdsAsync(int limit = 50, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<int>)Array.Empty<int>());

        public Task<int> GetCountAsync(CancellationToken ct = default)
            => Task.FromResult(Snapshots.Count);

        public Task<int> GetTotalGamesWithBggIdCountAsync(CancellationToken ct = default)
            => Task.FromResult(Snapshots.Count);

        public Task<IReadOnlyList<BggRawSnapshot>> GetAllSnapshotsAsync(int limit = 500, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<BggRawSnapshot>)Snapshots.Values.Take(limit).ToList());

        public Task<IReadOnlyList<BggRawSnapshot>> GetSnapshotsAfterBggIdAsync(int lastBggId, int limit = 200, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<BggRawSnapshot>)Snapshots.Values.Where(s => s.BggId > lastBggId).OrderBy(s => s.BggId).Take(limit).ToList());
    }

    private class FakeGameRepo : IGameRepository
    {
        public List<Game> Games = [];

        public Task<IReadOnlyList<Game>> GetGamesPendingQualityBackfillAsync(int afterBggId, int limit, CancellationToken ct = default)
        {
            var query = Games
                .Where(g => g.BggId > afterBggId && (g.Scalability.Count == 0 || g.Scalability.All(s => s.BestVotes == 0 && s.RecommendedVotes == 0)))
                .OrderBy(g => g.BggId)
                .Take(limit)
                .ToList();
            return Task.FromResult((IReadOnlyList<Game>)query);
        }

        public Task<IReadOnlyList<Game>> GetGamesPendingQualityBackfillAsync(int limit = 50, CancellationToken ct = default)
            => GetGamesPendingQualityBackfillAsync(0, limit, ct);

        public Task<int> GetGamesPendingQualityBackfillCountAsync(CancellationToken ct = default)
            => Task.FromResult(Games.Count(g => g.Scalability.Count == 0 || g.Scalability.All(s => s.BestVotes == 0 && s.RecommendedVotes == 0)));

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
    }

    private class FakeStagingRepo : IBggCatalogStagingRepository
    {
        public List<BggCatalogStagingItem> Items = [];
        public Task UpsertBatchAsync(IEnumerable<BggCatalogStagingItem> items, CancellationToken ct = default) { Items.AddRange(items); return Task.CompletedTask; }
        public Task<BggCatalogStagingItem?> GetByBggIdAsync(int bggId, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(i => i.BggId == bggId));
        public Task UpdateAsync(BggCatalogStagingItem item, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateBatchAsync(IEnumerable<BggCatalogStagingItem> items, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingFetchBatchAsync(int batchSize = 50, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<BggCatalogStagingItem>)[]);
        public Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingImagesBatchAsync(int batchSize = 20, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<BggCatalogStagingItem>)[]);
        public Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingAiBatchAsync(int batchSize = 10, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<BggCatalogStagingItem>)[]);
        public Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingPromotionBatchAsync(int batchSize = 50, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<BggCatalogStagingItem>)[]);
        public Task<BggStagingMetricsDto> GetMetricsAsync(CancellationToken ct = default) => Task.FromResult(new BggStagingMetricsDto(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        public Task ResetQuotaExceededStatusAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetTotalCountAsync(CancellationToken ct = default) => Task.FromResult(Items.Count);
        public Task ClearStagingAsync(CancellationToken ct = default) { Items.Clear(); return Task.CompletedTask; }
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

    #endregion
}
