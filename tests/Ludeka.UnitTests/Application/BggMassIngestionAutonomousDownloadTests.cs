using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
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

public class BggMassIngestionAutonomousDownloadTests
{
    private const string SampleCsv = """
        id,name,yearpublished,rank,bayesaverage,average,usersrated
        174430,"Gloomhaven",2017,1,8.42,8.61,62000
        224517,"Brass: Birmingham",2018,2,8.41,8.60,48000
        342942,"Ark Nova",2021,3,8.35,8.53,42000
        999999,"Sin Suficientes Votos",2024,15000,5.1,5.2,14
        """;

    [Fact]
    public async Task DownloadAndIngest_UsesTodayDate_WhenAvailable()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var handler = new MockHttpMessageHandler();
        handler.RegisterResponse(
            $"https://raw.githubusercontent.com/beefsack/bgg-ranking-historicals/master/{today:yyyy-MM-dd}.csv",
            HttpStatusCode.OK,
            SampleCsv
        );

        var stagingRepo = new FakeStagingRepo();
        var service = CreateService(stagingRepo, handler);

        // Act
        int count = await service.RunScheduledDownloadAndIngestLatestRanksAsync(minUsersRated: 30);

        // Assert
        Assert.Equal(3, count);
        Assert.Equal(3, stagingRepo.Items.Count);
        Assert.Contains(stagingRepo.Items, i => i.BggId == 174430);
        Assert.Contains(stagingRepo.Items, i => i.BggId == 224517);
        Assert.Contains(stagingRepo.Items, i => i.BggId == 342942);
        Assert.DoesNotContain(stagingRepo.Items, i => i.BggId == 999999);
    }

    [Fact]
    public async Task DownloadAndIngest_FallsBackToPreviousDate_WhenTodayIs404()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);
        var handler = new MockHttpMessageHandler();

        // Hoy devuelve 404
        handler.RegisterResponse(
            $"https://raw.githubusercontent.com/beefsack/bgg-ranking-historicals/master/{today:yyyy-MM-dd}.csv",
            HttpStatusCode.NotFound,
            "Not Found"
        );

        // Ayer devuelve 200 OK
        handler.RegisterResponse(
            $"https://raw.githubusercontent.com/beefsack/bgg-ranking-historicals/master/{yesterday:yyyy-MM-dd}.csv",
            HttpStatusCode.OK,
            SampleCsv
        );

        var stagingRepo = new FakeStagingRepo();
        var service = CreateService(stagingRepo, handler);

        // Act
        int count = await service.RunScheduledDownloadAndIngestLatestRanksAsync(minUsersRated: 30);

        // Assert
        Assert.Equal(3, count);
        Assert.Equal(3, stagingRepo.Items.Count);
        Assert.Contains(handler.RequestedUrls, u => u.Contains(today.ToString("yyyy-MM-dd")));
        Assert.Contains(handler.RequestedUrls, u => u.Contains(yesterday.ToString("yyyy-MM-dd")));
    }

    [Fact]
    public async Task DownloadAndIngest_ThrowsException_WhenAllFallbackDaysFail()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(); // Todo devuelve 404 por defecto
        var stagingRepo = new FakeStagingRepo();
        var options = new BggMassIngestionOptions { MaxFallbackDays = 2 };
        var service = CreateService(stagingRepo, handler, options: options);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RunScheduledDownloadAndIngestLatestRanksAsync()
        );

        Assert.Contains("No se pudo obtener ningún volcado", ex.Message);
        Assert.Empty(stagingRepo.Items);
    }

    [Fact]
    public async Task DownloadAndIngest_FiltersByMinUsersRated()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        string customCsv = """
            id,name,yearpublished,rank,bayesaverage,average,usersrated
            1,"Juego Pocos Votos A",2020,10,6.0,6.1,10
            2,"Juego Pocos Votos B",2020,11,6.0,6.1,29
            3,"Juego Justo Umbral",2020,12,6.0,6.1,30
            4,"Juego Muchos Votos",2020,1,8.0,8.1,500
            """;

        var handler = new MockHttpMessageHandler();
        handler.RegisterResponse(
            $"https://raw.githubusercontent.com/beefsack/bgg-ranking-historicals/master/{today:yyyy-MM-dd}.csv",
            HttpStatusCode.OK,
            customCsv
        );

        var stagingRepo = new FakeStagingRepo();
        var service = CreateService(stagingRepo, handler);

        // Act
        int count = await service.RunScheduledDownloadAndIngestLatestRanksAsync(minUsersRated: 30);

        // Assert
        Assert.Equal(2, count);
        Assert.Contains(stagingRepo.Items, i => i.BggId == 3);
        Assert.Contains(stagingRepo.Items, i => i.BggId == 4);
        Assert.DoesNotContain(stagingRepo.Items, i => i.BggId == 1);
        Assert.DoesNotContain(stagingRepo.Items, i => i.BggId == 2);
    }

    [Fact]
    public async Task DownloadAndIngest_RequiresPermission_ForInteractiveMethod()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var handler = new MockHttpMessageHandler();
        handler.RegisterResponse(
            $"https://raw.githubusercontent.com/beefsack/bgg-ranking-historicals/master/{today:yyyy-MM-dd}.csv",
            HttpStatusCode.OK,
            SampleCsv
        );

        var stagingRepo = new FakeStagingRepo();
        var permissionGuard = new DenyingPermissionGuard();
        var service = CreateService(stagingRepo, handler, permissionGuard: permissionGuard);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.DownloadAndIngestLatestRanksAsync(minUsersRated: 30)
        );

        Assert.Empty(stagingRepo.Items);
    }

    [Fact]
    public async Task DownloadAndIngest_InSimulateMode_DoesNotUseNetwork()
    {
        // Arrange
        var handler = new ThrowingHttpMessageHandler(); // Lanza si se intenta usar red
        var stagingRepo = new FakeStagingRepo();
        var options = new BggMassIngestionOptions { Simulate = true, MinUsersRated = 30 };
        var service = CreateService(stagingRepo, handler, options: options);

        // Act
        int count = await service.RunScheduledDownloadAndIngestLatestRanksAsync();

        // Assert
        Assert.True(count > 0);
        Assert.NotEmpty(stagingRepo.Items);
        Assert.Contains(stagingRepo.Items, i => i.BggId == 174430); // Gloomhaven en el sample sintético
    }

    [Fact]
    public async Task BggDumpParser_HandlesNonSeekableStream()
    {
        // Arrange
        var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(SampleCsv));
        using var nonSeekableStream = new NonSeekableStreamWrapper(memoryStream);

        Assert.False(nonSeekableStream.CanSeek);

        // Act
        var results = new List<BggRanksDumpRowDto>();
        await foreach (var row in BggDumpParser.ParseRanksDumpAsync(nonSeekableStream, minUsersRated: 30))
        {
            results.Add(row);
        }

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Contains(results, r => r.BggId == 174430);
        Assert.Contains(results, r => r.BggId == 224517);
        Assert.Contains(results, r => r.BggId == 342942);
    }

    #region Helpers & Fakes

    private static BggMassIngestionService CreateService(
        FakeStagingRepo stagingRepo,
        HttpMessageHandler handler,
        ISessionPermissionGuard? permissionGuard = null,
        BggMassIngestionOptions? options = null)
    {
        var bggClient = new FakeBggClient();
        var geekDo = new FakeGeekDoClient();
        var images = new FakeImageStorageService();
        var ai = new FakeAiSummaryService();
        var gameRepo = new FakeGameRepo();
        var httpClient = new HttpClient(handler);
        var optionsWrapper = Options.Create(options ?? new BggMassIngestionOptions());

        return new BggMassIngestionService(
            stagingRepo,
            bggClient,
            geekDo,
            images,
            ai,
            gameRepo,
            httpClient,
            optionsWrapper,
            NullLogger<BggMassIngestionService>.Instance,
            permissionGuard
        );
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode StatusCode, string Content)> _responses = new();
        public List<string> RequestedUrls { get; } = new();

        public void RegisterResponse(string url, HttpStatusCode code, string content)
        {
            _responses[url] = (code, content);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string url = request.RequestUri?.ToString() ?? string.Empty;
            RequestedUrls.Add(url);

            if (_responses.TryGetValue(url, out var resp))
            {
                var responseMessage = new HttpResponseMessage(resp.StatusCode)
                {
                    Content = new StringContent(resp.Content, Encoding.UTF8, "text/plain")
                };
                return Task.FromResult(responseMessage);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("No se debe realizar tráfico de red en modo simulado.");
        }
    }

    private class NonSeekableStreamWrapper : Stream
    {
        private readonly Stream _inner;

        public NonSeekableStreamWrapper(Stream inner) => _inner = inner;

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private class DenyingPermissionGuard : ISessionPermissionGuard
    {
        public Task RequireAsync(ModeratorPermission permission, string? customDenialMessage = null, CancellationToken ct = default)
            => throw new UnauthorizedAccessException(customDenialMessage ?? "Acceso denegado.");

        public Task<bool> HasPermissionAsync(ModeratorPermission permission, CancellationToken ct = default)
            => Task.FromResult(false);
    }

    private class FakeStagingRepo : IBggCatalogStagingRepository
    {
        public List<BggCatalogStagingItem> Items = new();

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

        public Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingFetchBatchAsync(int batchSize, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BggCatalogStagingItem>>(new List<BggCatalogStagingItem>());

        public Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingImagesBatchAsync(int batchSize, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BggCatalogStagingItem>>(new List<BggCatalogStagingItem>());

        public Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingAiBatchAsync(int batchSize, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BggCatalogStagingItem>>(new List<BggCatalogStagingItem>());

        public Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingPromotionBatchAsync(int batchSize, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BggCatalogStagingItem>>(new List<BggCatalogStagingItem>());

        public Task UpdateAsync(BggCatalogStagingItem item, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateBatchAsync(IEnumerable<BggCatalogStagingItem> items, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<BggStagingMetricsDto> GetMetricsAsync(CancellationToken ct = default)
            => Task.FromResult(new BggStagingMetricsDto(Items.Count, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        public Task<BggCatalogStagingItem?> GetByBggIdAsync(int bggId, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(i => i.BggId == bggId));

        public Task ResetQuotaExceededStatusAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<int> GetTotalCountAsync(CancellationToken ct = default) => Task.FromResult(Items.Count);
    }

    private class FakeBggClient : IBggClient
    {
        public Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default) => Task.FromResult<Game?>(null);
        public Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BggSearchResultDto>>(Array.Empty<BggSearchResultDto>());
        public Task<IReadOnlyList<BggTopGameDto>> FetchTopGamesAsync(int limit = 50, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BggTopGameDto>>(Array.Empty<BggTopGameDto>());
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
            => Task.FromResult(new ImageVariantUrls("", ""));
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
            => Task.FromResult(new AiGameSummaryDto(game.Id, game.SpanishTitle, "Ideal 4", "10+", "Mesa", "Síntesis", "FakeModel", DateTime.UtcNow));
        public Task<AiGameSummaryDto> EnsureSummaryForGameAsync(Guid gameId, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<AiBatchProcessingResultDto> ProcessPendingSummariesBatchAsync(int batchSize = 20, CancellationToken ct = default)
            => Task.FromResult(new AiBatchProcessingResultDto(0, 0, 0, []));
    }

    private class FakeGameRepo : IGameRepository
    {
        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Game?>(null);
        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult<Game?>(null);
        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default) => Task.FromResult<Game?>(null);
        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default) => Task.FromResult(((IReadOnlyList<Game>)new List<Game>(), 0));
        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Game game, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> HasAnyAsync(CancellationToken ct = default) => Task.FromResult(false);
    }

    #endregion
}
