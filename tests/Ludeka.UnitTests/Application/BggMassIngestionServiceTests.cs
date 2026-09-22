using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
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

public class BggMassIngestionServiceTests
{
    [Fact]
    public async Task IngestRanksDumpAsync_InsertsOnlyGamesMeetingVoteThresholdIntoStaging()
    {
        // Arrange
        var stagingRepo = new FakeStagingRepo();
        var bggClient = new FakeBggClient();
        var geekDo = new FakeGeekDoClient();
        var images = new FakeImageStorageService();
        var ai = new FakeAiSummaryService();
        var gameRepo = new FakeGameRepo();
        using var httpClient = new HttpClient();
        var options = Options.Create(new BggMassIngestionOptions { MinUsersRated = 30 });

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

        string csv = """
            id,name,yearpublished,rank,bayesaverage,average,usersrated
            174430,"Gloomhaven",2017,1,8.42,8.61,62000
            224517,"Brass: Birmingham",2018,2,8.41,8.60,48000
            999999,"Bajo en votos",2025,12000,5.0,5.1,12
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        int count = await service.IngestRanksDumpAsync(stream, minUsersRated: 30);

        // Assert
        Assert.Equal(2, count);
        Assert.Equal(2, stagingRepo.Items.Count);
        Assert.Contains(stagingRepo.Items, i => i.BggId == 174430);
        Assert.Contains(stagingRepo.Items, i => i.BggId == 224517);
        Assert.DoesNotContain(stagingRepo.Items, i => i.BggId == 999999);
    }

    [Fact]
    public async Task IngestRanksDumpAsync_WithDuplicateBggIdsInStream_DeduplicatesAndInsertsOnce()
    {
        // Arrange
        var stagingRepo = new FakeStagingRepo();
        var bggClient = new FakeBggClient();
        var geekDo = new FakeGeekDoClient();
        var images = new FakeImageStorageService();
        var ai = new FakeAiSummaryService();
        var gameRepo = new FakeGameRepo();
        using var httpClient = new HttpClient();
        var options = Options.Create(new BggMassIngestionOptions { MinUsersRated = 30 });

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

        string csv = """
            id,name,yearpublished,rank,bayesaverage,average,usersrated
            174430,"Gloomhaven",2017,1,8.42,8.61,62000
            174430,"Gloomhaven Duplicado",2017,1,8.42,8.61,62000
            224517,"Brass: Birmingham",2018,2,8.41,8.60,48000
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        int count = await service.IngestRanksDumpAsync(stream, minUsersRated: 30);

        // Assert: solo 2 items insertados
        Assert.Equal(2, count);
        Assert.Equal(2, stagingRepo.Items.Count);
    }

    [Fact]
    public async Task ProcessPendingDetailsBatchAsync_FetchesFromBggAndMarksFetched()
    {
        // Arrange
        var stagingRepo = new FakeStagingRepo();
        var stagingItem = new BggCatalogStagingItem(174430, "Gloomhaven");
        stagingRepo.Items.Add(stagingItem);

        var fetchedGame = new Game(
            bggId: 174430,
            originalTitle: "Gloomhaven",
            spanishTitle: "Gloomhaven",
            designer: "Isaac Childres",
            publisher: "Cephalofair",
            yearPublished: 2017,
            coverImageUrl: "https://cf.geekdo-images.com/cover.jpg",
            thumbnailUrl: "https://cf.geekdo-images.com/thumb.jpg",
            description: "Juego de mazmorras cooperativo",
            bggRating: 8.7,
            bggRank: 1,
            ludistRating: 9.0,
            confrontation: ConfrontationType.Cooperative,
            style: GameStyle.Ameritrash,
            isOfficialSolo: true,
            age: new AgeRating(14, 14),
            language: LanguageDependence.High,
            footprint: TableFootprint.TableMonster,
            duration: new GameDuration(120, 180, 45)
        );

        var bggClient = new FakeBggClient();
        bggClient.Games[174430] = fetchedGame;

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
        int processed = await service.ProcessPendingDetailsBatchAsync(10);

        // Assert
        Assert.Equal(1, processed);
        Assert.Equal(StagingFetchStatus.Fetched, stagingItem.FetchStatus);
        Assert.Equal("Isaac Childres", stagingItem.Designer);
        Assert.Equal("Cephalofair", stagingItem.Publisher);
        Assert.True(stagingRepo.UpdateBatchCallCount > 0);
    }

    [Fact]
    public async Task ProcessPendingAiBatchAsync_WhenQuotaExhausted_MarksItemsAndStopsCleanly()
    {
        // Arrange
        var stagingRepo = new FakeStagingRepo();
        var stagingItem = new BggCatalogStagingItem(174430, "Gloomhaven");
        stagingItem.MarkFetched("xml", "Gloomhaven", "Isaac Childres", "Cephalofair", "Mazmorras", 1, 4, 120, 14, 8.7);
        stagingRepo.Items.Add(stagingItem);

        var bggClient = new FakeBggClient();
        var geekDo = new FakeGeekDoClient();
        var images = new FakeImageStorageService();
        var ai = new FakeAiSummaryService
        {
            NextBatchResult = new AiBatchResultDto(
                Success: false,
                QuotaExhausted: true,
                Summaries: new Dictionary<int, AiGameSummaryDto>(),
                ErrorMessage: "HTTP 429"
            )
        };
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
        var result = await service.ProcessPendingAiBatchAsync(gamesPerBatch: 5, maxBatches: 1);

        // Assert
        Assert.Equal(StagingAiStatus.QuotaExceeded, stagingItem.AiStatus);
        Assert.True(stagingRepo.UpdateBatchCallCount > 0);
    }

    [Fact]
    public async Task PromoteReadyToCatalogBatchAsync_CreatesGameEntityAndMarksPromoted()
    {
        // Arrange
        var stagingRepo = new FakeStagingRepo();
        var stagingItem = new BggCatalogStagingItem(224517, "Brass: Birmingham", 2018, 1, 45000, 8.42, 8.61, "Brass: Birmingham");
        stagingItem.MarkFetched("xml", "Brass: Birmingham", "Martin Wallace", "Roxley", "Revolución industrial", 2, 4, 120, 14, 8.6);
        stagingItem.MarkImagesCompleted("https://cdn.ludeka.com/games/224517/cover.webp", "https://cdn.ludeka.com/games/224517/cover_thumb.webp");
        stagingItem.MarkAiCompleted("{\"generalVerdict\":\"Imprescindible\",\"scalabilitySummary\":\"Brilla a 3-4\",\"ageSummary\":\"14+\",\"footprintSummary\":\"Mesa estándar\"}");
        stagingRepo.Items.Add(stagingItem);

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
        int promoted = await service.PromoteReadyToCatalogBatchAsync(10);

        // Assert
        Assert.Equal(1, promoted);
        Assert.Equal(StagingPromotionStatus.Promoted, stagingItem.PromotionStatus);
        Assert.Single(gameRepo.Games);
        Assert.Equal(224517, gameRepo.Games[0].BggId);
        Assert.Equal("Brass: Birmingham", gameRepo.Games[0].SpanishTitle);
        Assert.NotNull(gameRepo.Games[0].AiSummary);
    }

    // --- FAKES ---

    private class FakeStagingRepo : IBggCatalogStagingRepository
    {
        public List<BggCatalogStagingItem> Items = [];
        public int UpdateBatchCallCount { get; private set; }

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

        public Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingFetchBatchAsync(int batchSize = 20, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BggCatalogStagingItem>>(Items.Where(i => i.FetchStatus == StagingFetchStatus.Pending).Take(batchSize).ToList());

        public Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingImagesBatchAsync(int batchSize = 10, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BggCatalogStagingItem>>(Items.Where(i => i.ImagesStatus == StagingImagesStatus.Pending && i.FetchStatus == StagingFetchStatus.Fetched).Take(batchSize).ToList());

        public Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingAiBatchAsync(int batchSize = 10, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BggCatalogStagingItem>>(Items.Where(i => i.AiStatus == StagingAiStatus.Pending && i.FetchStatus == StagingFetchStatus.Fetched).Take(batchSize).ToList());

        public Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingPromotionBatchAsync(int batchSize = 50, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BggCatalogStagingItem>>(Items.Where(i => i.PromotionStatus == StagingPromotionStatus.Pending && i.FetchStatus == StagingFetchStatus.Fetched).Take(batchSize).ToList());

        public Task UpdateAsync(BggCatalogStagingItem item, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateBatchAsync(IEnumerable<BggCatalogStagingItem> items, CancellationToken ct = default)
        {
            UpdateBatchCallCount++;
            return Task.CompletedTask;
        }

        public Task<BggStagingMetricsDto> GetMetricsAsync(CancellationToken ct = default)
        {
            int total = Items.Count;
            int pendingFetch = Items.Count(i => i.FetchStatus == StagingFetchStatus.Pending);
            int fetched = Items.Count(i => i.FetchStatus == StagingFetchStatus.Fetched);
            int pendingImages = Items.Count(i => i.ImagesStatus == StagingImagesStatus.Pending);
            int imagesCompleted = Items.Count(i => i.ImagesStatus == StagingImagesStatus.Completed);
            int pendingAi = Items.Count(i => i.AiStatus == StagingAiStatus.Pending);
            int aiCompleted = Items.Count(i => i.AiStatus == StagingAiStatus.Completed);
            int quota = Items.Count(i => i.AiStatus == StagingAiStatus.QuotaExceeded);
            int pendingPromo = Items.Count(i => i.PromotionStatus == StagingPromotionStatus.Pending);
            int promoted = Items.Count(i => i.PromotionStatus == StagingPromotionStatus.Promoted);
            int failed = Items.Count(i => i.FetchStatus == StagingFetchStatus.Failed || i.ImagesStatus == StagingImagesStatus.Failed || i.AiStatus == StagingAiStatus.Failed || i.PromotionStatus == StagingPromotionStatus.Failed);

            return Task.FromResult(new BggStagingMetricsDto(total, pendingFetch, fetched, pendingImages, imagesCompleted, pendingAi, aiCompleted, quota, pendingPromo, promoted, failed));
        }

        public Task<BggCatalogStagingItem?> GetByBggIdAsync(int bggId, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(i => i.BggId == bggId));

        public Task ResetQuotaExceededStatusAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<int> GetTotalCountAsync(CancellationToken ct = default) => Task.FromResult(Items.Count);

        public Task ClearStagingAsync(CancellationToken ct = default)
        {
            Items.Clear();
            return Task.CompletedTask;
        }
    }

    private class FakeBggClient : IBggClient
    {
        public Dictionary<int, Game> Games = [];

        public Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default)
            => Task.FromResult(Games.GetValueOrDefault(bggId));

        public Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<BggSearchResultDto>)Array.Empty<BggSearchResultDto>());

        public Task<IReadOnlyList<BggTopGameDto>> FetchTopGamesAsync(int limit = 50, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<BggTopGameDto>)Array.Empty<BggTopGameDto>());
    }

    private class FakeGeekDoClient : IGeekDoImagesClient
    {
        public Dictionary<int, GeekDoGalleryImagesDto> Galleries = [];

        public Task<GeekDoGalleryImagesDto> GetTopVotedImagesAsync(int bggId, CancellationToken ct = default)
            => Task.FromResult(Galleries.GetValueOrDefault(bggId, new GeekDoGalleryImagesDto(null, null, null)));
    }

    private class FakeImageStorageService : IImageStorageService
    {
        public Task<string> UploadOptimizedImageAsync(Stream inputStream, string objectKey, int maxWidth = 1000, int quality = 82, CancellationToken ct = default)
            => Task.FromResult($"https://cdn.ludeka.com/{objectKey}");

        public Task<ImageVariantUrls> UploadGameImageVariantsAsync(Stream rawImageStream, int bggId, string imageType, CancellationToken ct = default)
            => Task.FromResult(new ImageVariantUrls($"https://cdn.ludeka.com/games/{bggId}/{imageType}.webp", $"https://cdn.ludeka.com/games/{bggId}/{imageType}_thumb.webp"));

        public Task<bool> DeleteImageAsync(string objectKey, CancellationToken ct = default)
            => Task.FromResult(true);

        public string GetPublicUrl(string objectKey)
            => $"https://cdn.ludeka.com/{objectKey}";

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
        public AiBatchResultDto NextBatchResult { get; set; } = new(true, false, new Dictionary<int, AiGameSummaryDto>(), null);

        public Task<AiBatchResultDto> GenerateBatchSummariesAsync(IReadOnlyList<AiGameBatchInputDto> games, CancellationToken ct = default)
            => Task.FromResult(NextBatchResult);

        public Task<AiGameSummaryDto> GenerateSummaryAsync(Game game, CancellationToken ct = default)
            => Task.FromResult(new AiGameSummaryDto(game.Id, game.SpanishTitle, "Ideal 4", "10+", "Mesa", "Síntesis", "FakeModel", DateTime.UtcNow));

        public Task<AiGameSummaryDto> EnsureSummaryForGameAsync(Guid gameId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<AiBatchProcessingResultDto> ProcessPendingSummariesBatchAsync(int batchSize = 20, CancellationToken ct = default)
            => Task.FromResult(new AiBatchProcessingResultDto(0, 0, 0, []));
    }

    private class FakeGameRepo : IGameRepository
    {
        public List<Game> Games = [];

        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Games.FirstOrDefault(g => g.Id == id));

        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult(Games.FirstOrDefault(g => g.Slug == slug));

        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default) =>
            Task.FromResult(Games.FirstOrDefault(g => g.BggId == bggId));

        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
            Task.FromResult(((IReadOnlyList<Game>)Games, Games.Count));

        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default)
        {
            Games.AddRange(games);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Game game, CancellationToken ct = default) => Task.CompletedTask;

        public Task<bool> HasAnyAsync(CancellationToken ct = default) => Task.FromResult(Games.Count > 0);
    }
}
