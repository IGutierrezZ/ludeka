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

public class BggMassIngestionSweepTests
{
    private (BggMassIngestionService Service, FakeGameRepo GameRepo, FakeStagingRepo StagingRepo, FakeBggClient BggClient) CreateSut()
    {
        var stagingRepo = new FakeStagingRepo();
        var bggClient = new FakeBggClient();
        var geekDo = new FakeGeekDoClient();
        var images = new FakeImageStorageService();
        var ai = new FakeAiSummaryService();
        var gameRepo = new FakeGameRepo();
        using var httpClient = new HttpClient();
        var options = Options.Create(new BggMassIngestionOptions { DelayBetweenBggCallsMs = 0 });

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

        return (service, gameRepo, stagingRepo, bggClient);
    }

    [Fact]
    public async Task SweepCatalogQualityBatchAsync_WhenCatalogEmpty_ReturnsZeroAndHasMoreFalse()
    {
        var (service, _, _, _) = CreateSut();

        var result = await service.RunScheduledSweepCatalogQualityBatchAsync(afterBggId: 0, batchSize: 50);

        Assert.Equal(0, result.EvaluatedCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.False(result.HasMore);
        Assert.Equal(0, result.LastBggIdProcessed);
    }

    [Fact]
    public async Task SweepCatalogQualityBatchAsync_PaginatesByCursor_AscendingAndStopsWhenExhausted()
    {
        var (service, gameRepo, stagingRepo, _) = CreateSut();

        var g1 = CreateGame(10, "Game 10", GameStyle.Eurogame);
        var g2 = CreateGame(20, "Game 20", GameStyle.Eurogame);
        var g3 = CreateGame(30, "Game 30", GameStyle.Eurogame);
        gameRepo.Games.AddRange([g2, g3, g1]); // Desordenados

        // Lote 1: batchSize = 2 desde afterBggId = 0
        var batch1 = await service.RunScheduledSweepCatalogQualityBatchAsync(afterBggId: 0, batchSize: 2);
        Assert.Equal(2, batch1.EvaluatedCount);
        Assert.Equal(20, batch1.LastBggIdProcessed);
        Assert.True(batch1.HasMore);

        // Lote 2: batchSize = 2 desde afterBggId = 20
        var batch2 = await service.RunScheduledSweepCatalogQualityBatchAsync(afterBggId: batch1.LastBggIdProcessed, batchSize: 2);
        Assert.Equal(1, batch2.EvaluatedCount);
        Assert.Equal(30, batch2.LastBggIdProcessed);
        Assert.False(batch2.HasMore);

        // Lote 3: batchSize = 2 desde afterBggId = 30
        var batch3 = await service.RunScheduledSweepCatalogQualityBatchAsync(afterBggId: batch2.LastBggIdProcessed, batchSize: 2);
        Assert.Equal(0, batch3.EvaluatedCount);
        Assert.False(batch3.HasMore);
    }

    [Fact]
    public async Task SweepCatalogQualityBatchAsync_WhenGameHasFalseEurogame_CorrectsFromStagingDna()
    {
        var (service, gameRepo, stagingRepo, _) = CreateSut();

        // Juego en catálogo erróneamente clasificado como Eurogame con duración colapsada
        var game = CreateGame(42, "Combat Commander: Europe", GameStyle.Eurogame, confrontation: ConfrontationType.Competitive);
        gameRepo.Games.Add(game);

        // Staging tiene el ADN correcto cacheado
        var staging = new BggCatalogStagingItem(42, "Combat Commander: Europe", 2006, 120, 15000, 7.8, 8.0);
        staging.MarkFetched(
            rawXml: "<dna style=\"Ameritrash\" confrontation=\"Competitive\" solo=\"false\" />",
            spanishTitle: "Combat Commander: Europa",
            designer: "Chad Jensen",
            publisher: "GMT Games",
            description: "Juego táctico de infantería en la 2GM.",
            minPlayers: 2,
            maxPlayers: 2,
            playingTimeMinutes: 120,
            minAge: 12,
            bggRating: 7.8,
            minPlayTimeMinutes: 60,
            maxPlayTimeMinutes: 180,
            inferredFootprint: TableFootprint.StandardTable,
            scalability: [new ScalabilityEntry(2, "2J", ScalabilityStatus.MustPlay, 80, 20, 0)],
            sleeves: [],
            spanishPublisher: "Devir",
            regionalPublishers: []
        );
        stagingRepo.Items.Add(staging);

        var result = await service.RunScheduledSweepCatalogQualityBatchAsync(afterBggId: 0, batchSize: 10);

        Assert.Equal(1, result.EvaluatedCount);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(GameStyle.Ameritrash, game.Style);
        Assert.Equal(60, game.Duration.MinMinutes);
        Assert.Equal(180, game.Duration.MaxMinutes);
        Assert.Equal(60, game.Duration.EstimatedPerPlayerMinutes);
        Assert.Equal("Devir", game.SpanishPublisher);
    }

    [Fact]
    public async Task SweepCatalogQualityBatchAsync_WhenGameIsAlreadyCorrect_CountsAsSkippedWithoutDbUpdate()
    {
        var (service, gameRepo, stagingRepo, _) = CreateSut();

        // Juego con ADN, duración y escalabilidad ya perfectamente alineados
        var game = CreateGame(100, "Concordia", GameStyle.Eurogame, durationMinutes: 90);
        game.UpdateScalability([new ScalabilityEntry(4, "4J", ScalabilityStatus.MustPlay, 200, 50, 2)]);
        game.UpdateDuration(new GameDuration(90, 90, 22));
        gameRepo.Games.Add(game);

        var staging = new BggCatalogStagingItem(100, "Concordia", 2013, 20, 45000, 8.1, 8.2);
        staging.MarkFetched(
            rawXml: "<dna style=\"Eurogame\" confrontation=\"Competitive\" solo=\"false\" />",
            spanishTitle: "Concordia",
            designer: "Mac Gerdts",
            publisher: "PD-Verlag",
            description: "Juego pacífico de desarrollo comercial.",
            minPlayers: 2,
            maxPlayers: 5,
            playingTimeMinutes: 90,
            minAge: 13,
            bggRating: 8.1,
            minPlayTimeMinutes: 90,
            maxPlayTimeMinutes: 90,
            inferredFootprint: TableFootprint.StandardTable,
            scalability: [new ScalabilityEntry(4, "4J", ScalabilityStatus.MustPlay, 200, 50, 2)],
            sleeves: [],
            spanishPublisher: null,
            regionalPublishers: []
        );
        stagingRepo.Items.Add(staging);

        var result = await service.RunScheduledSweepCatalogQualityBatchAsync(afterBggId: 0, batchSize: 10);

        Assert.Equal(1, result.EvaluatedCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(0, gameRepo.UpdateCallsCount); // Cero escrituras a base de datos
    }

    private static Game CreateGame(int bggId, string title, GameStyle style, ConfrontationType confrontation = ConfrontationType.Competitive, int durationMinutes = 30)
    {
        return new Game(
            bggId: bggId,
            originalTitle: title,
            spanishTitle: title,
            designer: "Autor",
            publisher: "Editorial",
            yearPublished: 2020,
            coverImageUrl: "https://cdn.ludeka.com/cover.jpg",
            thumbnailUrl: "https://cdn.ludeka.com/thumb.jpg",
            description: "Descripción",
            bggRating: 7.5,
            bggRank: 100,
            ludistRating: 7.5,
            confrontation: confrontation,
            style: style,
            isOfficialSolo: false,
            age: new AgeRating(10, 10),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(durationMinutes, durationMinutes, 15),
            scalability: [],
            sleeves: []
        );
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
                if (existing != null) Items.Remove(existing);
                Items.Add(item);
            }
            return Task.CompletedTask;
        }

        public Task<BggCatalogStagingItem?> GetByBggIdAsync(int bggId, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(i => i.BggId == bggId));

        public Task UpdateAsync(BggCatalogStagingItem item, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateBatchAsync(IEnumerable<BggCatalogStagingItem> items, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingFetchBatchAsync(int batchSize, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingImagesBatchAsync(int batchSize, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingAiBatchAsync(int batchSize, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingPromotionBatchAsync(int batchSize, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<BggStagingMetricsDto> GetMetricsAsync(CancellationToken ct = default) => throw new NotImplementedException();
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
        public Task<GameImageUploadResult> SaveGameCoverAsync(string slug, Stream contentStream, string originalFileName, string contentType, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<GameImageUploadResult> SaveEventPosterAsync(string eventSlugOrId, Stream contentStream, string originalFileName, string contentType, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<GameImageUploadResult> SaveCommunityImageAsync(string subfolder, string identifier, Stream contentStream, string originalFileName, string contentType, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<GameImageUploadResult> ValidateCoverUrlAsync(string imageUrl, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private class FakeAiSummaryService : IAiGameSummaryService
    {
        public Task<AiBatchResultDto> GenerateBatchSummariesAsync(IReadOnlyList<AiGameBatchInputDto> games, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AiGameSummaryDto> GenerateSummaryAsync(Game game, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AiGameSummaryDto> EnsureSummaryForGameAsync(Guid gameId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AiBatchProcessingResultDto> ProcessPendingSummariesBatchAsync(int batchSize = 20, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private class FakeGameRepo : IGameRepository
    {
        public List<Game> Games = [];
        public int UpdateCallsCount = 0;

        public Task<IReadOnlyList<Game>> GetGamesCursorPagedAsync(int afterBggId, int limit = 50, CancellationToken ct = default)
        {
            var paged = Games
                .Where(g => g.BggId > afterBggId)
                .OrderBy(g => g.BggId)
                .Take(limit)
                .ToList();
            return Task.FromResult((IReadOnlyList<Game>)paged);
        }

        public Task<int> GetTotalCatalogCountAsync(CancellationToken ct = default) => Task.FromResult(Games.Count);

        public Task UpdateAsync(Game game, CancellationToken ct = default)
        {
            UpdateCallsCount++;
            return Task.CompletedTask;
        }

        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Games.FirstOrDefault(g => g.Id == id));
        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(Games.FirstOrDefault(g => g.Slug == slug));
        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default) => Task.FromResult(Games.FirstOrDefault(g => g.BggId == bggId));
        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default) { Games.AddRange(games); return Task.CompletedTask; }
        public Task<bool> HasAnyAsync(CancellationToken ct = default) => Task.FromResult(Games.Count > 0);
        public Task<IReadOnlyList<Game>> GetGamesWithoutAiSummaryAsync(int limit = 20, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Game>> GetByPublisherAsync(string publisherName, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Game>> GetByDesignerAsync(string designerName, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Game>> GetAllGamesAsync(CancellationToken ct = default) => Task.FromResult((IReadOnlyList<Game>)Games);
        public Task<IReadOnlyList<Game>> GetGamesPendingQualityBackfillAsync(int limit = 50, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> GetGamesPendingQualityBackfillCountAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    #endregion
}
