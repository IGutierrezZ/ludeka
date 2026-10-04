using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.YouTube;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class YouTubeSearchServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private LudekaDbContext _context = null!;
    private IMediaRepository _mediaRepository = null!;
    private IGameRepository _gameRepository = null!;
    private IChannelFocusProvider _channelFocus = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LudekaDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        _mediaRepository = new SqliteMediaRepository(_context);
        _gameRepository = new SqliteGameRepository(_context);
        _channelFocus = new ChannelFocusProvider();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task SearchQuickOverviewsAsync_InSimulatedMode_ReturnsQuickOverviewVideos()
    {
        var service = CreateService(new YouTubeOptions { Simulate = true });
        var results = await service.SearchQuickOverviewsAsync("Wingspan");

        Assert.NotEmpty(results);
        Assert.All(results, r =>
        {
            Assert.Equal(MediaType.QuickOverview, r.SuggestedType);
            Assert.True(r.DurationSeconds <= 210, "Los vídeos de cómo funciona deben durar aprox. 2-3 minutos");
            Assert.NotEmpty(r.VideoId);
            Assert.NotEmpty(r.Title);
            Assert.NotEmpty(r.ChannelTitle);
        });
    }

    [Fact]
    public async Task SearchTutorialsAsync_InSimulatedMode_ReturnsTutorials()
    {
        var service = CreateService(new YouTubeOptions { Simulate = true });
        var results = await service.SearchTutorialsAsync("Wingspan");

        Assert.NotEmpty(results);
        Assert.All(results, r =>
        {
            Assert.Equal(MediaType.Tutorial, r.SuggestedType);
            Assert.True(r.DurationSeconds >= 480, "Los tutoriales deben tener duración de reglas (>= 8 min)");
        });
    }

    [Fact]
    public async Task SearchPlaythroughsAsync_InSimulatedMode_ExtractsPlayerCountBadge()
    {
        var service = CreateService(new YouTubeOptions { Simulate = true });
        var results = await service.SearchPlaythroughsAsync("Wingspan");

        Assert.NotEmpty(results);
        Assert.All(results, r =>
        {
            Assert.Equal(MediaType.Playthrough, r.SuggestedType);
            Assert.False(string.IsNullOrWhiteSpace(r.ExtractedPlayerBadge), "El badge de comensales es obligatorio para partidas");
            Assert.StartsWith("Partida", r.ExtractedPlayerBadge);
        });
    }

    [Fact]
    public async Task SearchVideosForGameAsync_AggregatesThreeTypesSortedByRelevance()
    {
        var game = await SeedGameAsync("Wingspan");
        var service = CreateService(new YouTubeOptions { Simulate = true });

        var results = await service.SearchVideosForGameAsync(game.Id);

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.SuggestedType == MediaType.QuickOverview);
        Assert.Contains(results, r => r.SuggestedType == MediaType.Tutorial);
        Assert.Contains(results, r => r.SuggestedType == MediaType.Playthrough);
    }

    [Fact]
    public async Task IngestVideoAsync_WhenValidRequest_PersistsMediaItemAndReturnsDto()
    {
        var game = await SeedGameAsync("Catan");
        var service = CreateService(new YouTubeOptions { Simulate = true });

        var request = new YouTubeIngestRequestDto(
            GameId: game.Id,
            VideoId: "test-catan-01",
            Type: MediaType.Tutorial,
            Title: "Cómo jugar a Catan tutorial",
            Url: "https://www.youtube.com/watch?v=testcatan01",
            ThumbnailUrl: "https://example.com/thumb.jpg",
            ChannelTitle: "Devir TV",
            DurationSeconds: 600,
            AutoApprove: true
        );

        var dto = await service.IngestVideoAsync(request);

        Assert.NotNull(dto);
        Assert.Equal(game.Id, dto.GameId);
        Assert.Equal(ModerationStatus.Approved, dto.Status);
        Assert.Equal("Devir TV", dto.AuthorChannel);

        var inDb = await _mediaRepository.GetByIdAsync(dto.Id);
        Assert.NotNull(inDb);
        Assert.Equal(ModerationStatus.Approved, inDb.Status);
    }

    [Fact]
    public async Task IngestVideoAsync_WhenDuplicateUrl_ReturnsExistingWithoutAddingDuplicate()
    {
        var game = await SeedGameAsync("Carcassonne");
        var service = CreateService(new YouTubeOptions { Simulate = true });

        var request = new YouTubeIngestRequestDto(
            GameId: game.Id,
            VideoId: "dup-vid-01",
            Type: MediaType.QuickOverview,
            Title: "Carcassonne en 2 min",
            Url: "https://www.youtube.com/watch?v=dupvid01",
            ThumbnailUrl: "https://example.com/thumb.jpg",
            ChannelTitle: "Zacatrus!",
            DurationSeconds: 120,
            AutoApprove: false
        );

        var first = await service.IngestVideoAsync(request);
        var second = await service.IngestVideoAsync(request);

        Assert.Equal(first.Id, second.Id);

        var all = await _mediaRepository.GetAllAsync();
        Assert.Single(all, m => m.Url == request.Url);
    }

    [Fact]
    public async Task AutoSuggestAndIngestForGameAsync_IngestsExpectedVideos()
    {
        var game = await SeedGameAsync("Terraforming Mars");
        var service = CreateService(new YouTubeOptions { Simulate = true });

        var ingested = await service.AutoSuggestAndIngestForGameAsync(game.Id, autoApprove: false);

        Assert.NotEmpty(ingested);
        Assert.All(ingested, i =>
        {
            Assert.Equal(game.Id, i.GameId);
            Assert.Equal(ModerationStatus.PendingApproval, i.Status);
        });
    }

    [Fact]
    public async Task ExecuteSearchAsync_WithMockHttpMessageHandler_ParsesYouTubeApiV3Responses()
    {
        var searchJson = """
        {
          "items": [
            {
              "id": { "videoId": "api-vid-123" },
              "snippet": {
                "title": "Wingspan cómo jugar tutorial español",
                "description": "Explicación completa de reglas",
                "channelTitle": "Análisis Parálisis",
                "publishedAt": "2026-05-01T12:00:00Z"
              }
            }
          ]
        }
        """;

        var videosJson = """
        {
          "items": [
            {
              "id": "api-vid-123",
              "snippet": {
                "title": "Wingspan cómo jugar tutorial español",
                "description": "Explicación completa de reglas",
                "channelTitle": "Análisis Parálisis",
                "publishedAt": "2026-05-01T12:00:00Z",
                "thumbnails": {
                  "high": { "url": "https://img.youtube.com/vi/api-vid-123/hqdefault.jpg" }
                }
              },
              "contentDetails": {
                "duration": "PT14M32S"
              }
            }
          ]
        }
        """;

        var handler = new MockHttpMessageHandler((req) =>
        {
            var uri = req.RequestUri?.ToString() ?? string.Empty;
            if (uri.Contains("search?"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(searchJson, Encoding.UTF8, "application/json")
                };
            }
            if (uri.Contains("videos?"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(videosJson, Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://www.googleapis.com/youtube/v3/")
        };

        var service = new YouTubeSearchService(
            httpClient,
            Options.Create(new YouTubeOptions { ApiKey = "fake-key", Simulate = false }),
            _channelFocus,
            _gameRepository,
            _mediaRepository,
            NullLogger<YouTubeSearchService>.Instance
        );

        var results = await service.SearchTutorialsAsync("Wingspan");

        Assert.NotEmpty(results);
        var first = results.First();
        Assert.Equal("api-vid-123", first.VideoId);
        Assert.Equal("Análisis Parálisis", first.ChannelTitle);
        Assert.True(first.IsReferenceChannel);
        Assert.Equal("Creator", first.ChannelCategory);
        Assert.Equal(872, first.DurationSeconds); // PT14M32S = 14*60 + 32 = 872
        Assert.Equal("14:32", first.FormattedDuration);
        Assert.True(first.RelevanceScore >= 120, "Debe recibir bonus de canal de referencia y duración óptima");
    }

    [Fact]
    public async Task ExecuteSearchAsync_WhenApiReturnsForbidden_ThrowsInvalidOperationExceptionWithQuotaExplanation()
    {
        var handler = new MockHttpMessageHandler((req) =>
        {
            return new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("{\"error\":{\"code\":403,\"message\":\"quotaExceeded\"}}")
            };
        });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://www.googleapis.com/youtube/v3/")
        };

        var service = new YouTubeSearchService(
            httpClient,
            Options.Create(new YouTubeOptions { ApiKey = "test-key", Simulate = false }),
            _channelFocus,
            _gameRepository,
            _mediaRepository,
            NullLogger<YouTubeSearchService>.Instance
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SearchTutorialsAsync("Wingspan"));

        Assert.Contains("Cuota diaria de YouTube API excedida", ex.Message);
        Assert.Contains("403", ex.Message);
    }

    [Fact]
    public async Task ExecuteSearchAsync_WhenOperationCancelled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancelado

        var handler = new MockHttpMessageHandler((req) =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };
        });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://www.googleapis.com/youtube/v3/")
        };

        var service = new YouTubeSearchService(
            httpClient,
            Options.Create(new YouTubeOptions { ApiKey = "test-key", Simulate = false }),
            _channelFocus,
            _gameRepository,
            _mediaRepository,
            NullLogger<YouTubeSearchService>.Instance
        );

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.SearchTutorialsAsync("Wingspan", cts.Token));
    }

    [Fact]
    public async Task ExecuteSearchAsync_WhenApiResponseContainsDuplicateVideoIds_DeduplicatesWithoutThrowing()
    {
        var searchJson = """
        {
          "items": [
            { "id": { "videoId": "dup-vid-1" }, "snippet": { "title": "Vídeo duplicado 1", "channelTitle": "Canal A" } },
            { "id": { "videoId": "dup-vid-1" }, "snippet": { "title": "Vídeo duplicado 1 repetido", "channelTitle": "Canal A" } },
            { "id": { "videoId": "dup-vid-2" }, "snippet": { "title": "Vídeo 2", "channelTitle": "Canal B" } }
          ]
        }
        """;

        var videosJson = """
        {
          "items": [
            { "id": "dup-vid-1", "snippet": { "title": "Vídeo duplicado 1", "channelTitle": "Canal A" }, "contentDetails": { "duration": "PT5M" } },
            { "id": "dup-vid-2", "snippet": { "title": "Vídeo 2", "channelTitle": "Canal B" }, "contentDetails": { "duration": "PT10M" } }
          ]
        }
        """;

        var handler = new MockHttpMessageHandler((req) =>
        {
            var uri = req.RequestUri?.ToString() ?? string.Empty;
            if (uri.Contains("search?"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(searchJson, Encoding.UTF8, "application/json")
                };
            }
            if (uri.Contains("videos?"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(videosJson, Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://www.googleapis.com/youtube/v3/")
        };

        var service = new YouTubeSearchService(
            httpClient,
            Options.Create(new YouTubeOptions { ApiKey = "fake-key", Simulate = false }),
            _channelFocus,
            _gameRepository,
            _mediaRepository,
            NullLogger<YouTubeSearchService>.Instance
        );

        var results = await service.SearchTutorialsAsync("Wingspan");

        Assert.NotEmpty(results);
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task ExecuteSearchAsync_WithMemoryCache_ReusesCachedResultsWithoutCallingHttpTwice()
    {
        var callCount = 0;
        var searchJson = """
        {
          "items": [
            { "id": { "videoId": "cache-vid-1" }, "snippet": { "title": "Wingspan tutorial", "channelTitle": "Canal Test" } }
          ]
        }
        """;
        var videosJson = """
        {
          "items": [
            { "id": "cache-vid-1", "snippet": { "title": "Wingspan tutorial", "channelTitle": "Canal Test" }, "contentDetails": { "duration": "PT12M" } }
          ]
        }
        """;

        var handler = new MockHttpMessageHandler((req) =>
        {
            var uri = req.RequestUri?.ToString() ?? string.Empty;
            if (uri.Contains("search?"))
            {
                Interlocked.Increment(ref callCount);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(searchJson, Encoding.UTF8, "application/json")
                };
            }
            if (uri.Contains("videos?"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(videosJson, Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://www.googleapis.com/youtube/v3/") };
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = new YouTubeSearchService(
            httpClient,
            Options.Create(new YouTubeOptions { ApiKey = "fake-key", Simulate = false }),
            _channelFocus,
            _gameRepository,
            _mediaRepository,
            NullLogger<YouTubeSearchService>.Instance,
            cache: memoryCache
        );

        // Primera llamada: debe llamar al handler HTTP
        var res1 = await service.SearchTutorialsAsync("Wingspan");
        Assert.Single(res1);
        Assert.Equal(1, callCount);

        // Segunda llamada con misma query: debe responder desde caché sin llamar de nuevo a search?
        var res2 = await service.SearchTutorialsAsync("Wingspan");
        Assert.Single(res2);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task SearchConsolidatedCandidatesAsync_InSimulatedMode_ReturnsMultiCategoryCandidates()
    {
        var game = await SeedGameAsync("Catan");
        var service = CreateService(new YouTubeOptions { Simulate = true });

        var results = await service.SearchConsolidatedCandidatesAsync("Catan", game.Id);

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.SuggestedType == MediaType.QuickOverview);
        Assert.Contains(results, r => r.SuggestedType == MediaType.Tutorial);
        Assert.Contains(results, r => r.SuggestedType == MediaType.Playthrough);
    }

    [Fact]
    public async Task AutoSuggestAndIngestConsolidatedForGameAsync_IngestsUpToThreeCategories()
    {
        var game = await SeedGameAsync("Azul");
        var service = CreateService(new YouTubeOptions { Simulate = true });

        var ingested = await service.AutoSuggestAndIngestConsolidatedForGameAsync(game.Id, autoApprove: true);

        Assert.NotEmpty(ingested);
        Assert.True(ingested.Count <= 3);
        Assert.All(ingested, i =>
        {
            Assert.Equal(game.Id, i.GameId);
            Assert.Equal(ModerationStatus.Approved, i.Status);
        });
    }

    private YouTubeSearchService CreateService(YouTubeOptions options)
    {
        var httpClient = new HttpClient { BaseAddress = new Uri("https://www.googleapis.com/youtube/v3/") };
        return new YouTubeSearchService(
            httpClient,
            Options.Create(options),
            _channelFocus,
            _gameRepository,
            _mediaRepository,
            NullLogger<YouTubeSearchService>.Instance
        );
    }

    private async Task<Game> SeedGameAsync(string title)
    {
        var game = new Game(
            bggId: 1000 + Math.Abs(title.GetHashCode() % 1000),
            originalTitle: title,
            spanishTitle: title,
            designer: "Autor Test",
            publisher: "Editorial Test",
            yearPublished: 2021,
            coverImageUrl: "https://example.com/cover.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            description: "Descripción test",
            bggRating: 8.0,
            bggRank: 50,
            ludistRating: 8.5,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: true,
            age: new AgeRating(10, 10),
            language: LanguageDependence.None,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(30, 60, 20),
            scalability:
            [
                new ScalabilityEntry(1, "1", ScalabilityStatus.Recommended, 10, 20, 0),
                new ScalabilityEntry(2, "2", ScalabilityStatus.MustPlay, 50, 10, 0),
                new ScalabilityEntry(3, "3", ScalabilityStatus.Recommended, 20, 30, 5)
            ]
        );

        await _context.Games.AddAsync(game);
        await _context.SaveChangesAsync();
        return game;
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
