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
using Ludeka.Application.Features.Identity;
using Ludeka.Application.Features.Instagram;
using Ludeka.Application.Options;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Ludeka.Infrastructure.Services;
using Ludeka.Infrastructure.YouTube;
using Ludeka.Jobs.Runners;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Features.Instagram;

public class InstagramYouTubeSyncTests
{
    [Fact]
    public async Task CaptionAdapter_ResolvesPublisherWithYouTubeAndStoreWithoutYouTube_AndAppendsLudekaFooter()
    {
        // Arrange
        var devir = new Publisher(
            name: "Devir Iberia",
            slug: "devir-iberia",
            country: "España",
            socialLinks: [
                new SocialNetworkLink(SocialPlatform.Instagram, "https://www.instagram.com/deviriberia/", "@deviriberia"),
                new SocialNetworkLink(SocialPlatform.YouTube, "https://www.youtube.com/@DevirTV", "@DevirTV")
            ]);

        var zacatrus = new Store(
            name: "Zacatrus",
            slug: "zacatrus",
            type: StoreType.Hybrid,
            city: "Madrid",
            websiteUrl: "https://zacatrus.es",
            socialLinks: [
                new SocialNetworkLink(SocialPlatform.Instagram, "https://www.instagram.com/zacatrus/", "@zacatrus")
            ]);

        var adapter = new InstagramToYouTubeCaptionAdapter(
            new FakePublisherRepository([devir]),
            new FakeStoreRepository([zacatrus]),
            new FakeCreatorRepository([]),
            new FakeMonitoredAccountRepository([]),
            new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))),
            Options.Create(new GeminiOptions { ApiKey = string.Empty }), // Modo fallback determinista sin API key
            Options.Create(new InstagramOptions { PublicBaseUrl = "https://ludeka.es" }),
            NullLogger<InstagramToYouTubeCaptionAdapter>.Instance);

        var rawCaption = """
            ¡Repasamos en 90 segundos las novedades de @deviriberia que ya están disponibles en @zacatrus y también junto a @meeple_factory_es!
            Tienes el link en bio para ver todas las fichas.
            #juegosdemesa #ludeka
            """;

        // Act
        var adapted = await adapter.AdaptCaptionForYouTubeShortAsync(rawCaption);

        // Assert
        Assert.NotNull(adapted);
        Assert.True(adapted.Title.Length <= 100);
        Assert.Contains("#Shorts", adapted.Title, StringComparison.OrdinalIgnoreCase);

        // Menciones resueltas en el directorio de Ludeka: Devir tiene YouTube (@DevirTV) y Zacatrus está en Ludeka sin YouTube
        Assert.Equal(2, adapted.ResolvedMentions.Count);
        var devirMention = Assert.Single(adapted.ResolvedMentions, m => m.InstagramHandle.Equals("@deviriberia", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Devir Iberia", devirMention.OfficialName);
        Assert.Equal("@DevirTV", devirMention.YouTubeHandle);
        Assert.Equal("https://ludeka.es/editoriales/devir-iberia", devirMention.LudekaProfileUrl);

        var zacaMention = Assert.Single(adapted.ResolvedMentions, m => m.InstagramHandle.Equals("@zacatrus", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Zacatrus", zacaMention.OfficialName);
        Assert.Null(zacaMention.YouTubeHandle);
        Assert.Equal("https://ludeka.es/tiendas/zacatrus", zacaMention.LudekaProfileUrl);

        // El cuerpo adaptado incluye @DevirTV para Devir, el nombre limpio Zacatrus sin @ muerta, y humaniza la cuenta desconocida (@meeple_factory_es -> Meeple Factory)
        Assert.Contains("@DevirTV", adapted.Description);
        Assert.Contains("Zacatrus", adapted.Description);
        Assert.DoesNotContain("@zacatrus", adapted.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Meeple Factory", adapted.Description);
        Assert.DoesNotContain("@meeple_factory_es", adapted.Description, StringComparison.OrdinalIgnoreCase);

        // Limpia "link en bio"
        Assert.DoesNotContain("link en bio", adapted.Description, StringComparison.OrdinalIgnoreCase);

        // Pie determinista de Ludeka con fichas y ludeka.es
        Assert.Contains("https://ludeka.es/editoriales/devir-iberia", adapted.Description);
        Assert.Contains("https://ludeka.es/tiendas/zacatrus", adapted.Description);
        Assert.Contains("🎲 Descubre más juegos de mesa, compara precios en tiendas, sorteos y eventos en tu mesa:", adapted.Description);
        Assert.Contains("👉 https://ludeka.es", adapted.Description);
        Assert.Contains("#Shorts #JuegosDeMesa #BoardGames #Ludeka", adapted.Description);
    }

    [Fact]
    public async Task CaptionAdapter_UsesGeminiFlashWhenConfigured_AndEnforcesMentionSafeguards()
    {
        // Arrange
        var geminiJsonResponse = """
        {
          "candidates": [
            {
              "content": {
                "parts": [
                  {
                    "text": "{\"title\": \"Top 3 Euros Duros de 2026\", \"cleanDescription\": \"Analizamos las novedades con Editorial Tranjis en menos de dos minutos.\"}"
                  }
                ]
              }
            }
          ]
        }
        """;

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(geminiJsonResponse, Encoding.UTF8, "application/json")
        });

        var adapter = new InstagramToYouTubeCaptionAdapter(
            new FakePublisherRepository([]),
            new FakeStoreRepository([]),
            new FakeCreatorRepository([]),
            new FakeMonitoredAccountRepository([]),
            new HttpClient(handler),
            Options.Create(new GeminiOptions { Simulate = false, ApiKey = "test-gemini-key", Model = "gemini-2.5-flash" }),
            Options.Create(new InstagramOptions { PublicBaseUrl = "https://ludeka.es" }),
            NullLogger<InstagramToYouTubeCaptionAdapter>.Instance);

        // Act
        var result = await adapter.AdaptCaptionForYouTubeShortAsync("Analizamos novedades con @tranjisgames #ludeka");

        // Assert
        Assert.Equal("Top 3 Euros Duros de 2026 #Shorts", result.Title);
        Assert.Contains("Analizamos las novedades con Editorial Tranjis en menos de dos minutos.", result.Description);
        Assert.Contains("👉 https://ludeka.es", result.Description);
    }

    [Fact]
    public async Task InstagramApiClient_GetAccountReelsAsync_ReturnsFiveSimulatedReelsWhenInSimulateMode()
    {
        // Arrange
        var client = new InstagramApiClient(
            new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))),
            Options.Create(new InstagramOptions { Simulate = true }),
            NullLogger<InstagramApiClient>.Instance);

        // Act
        var reels = await client.GetAccountReelsAsync(limit: 25);

        // Assert
        Assert.Equal(5, reels.Count);
        Assert.All(reels, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.Id));
            Assert.False(string.IsNullOrWhiteSpace(r.MediaUrl));
            Assert.False(string.IsNullOrWhiteSpace(r.Permalink));
        });
    }

    [Fact]
    public async Task YouTubeShortsUploadClient_ExecutesOAuthRefreshAndResumableUpload_WhenConfigured()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(req =>
        {
            var url = req.RequestUri?.ToString() ?? string.Empty;
            if (url.Contains("oauth2.googleapis.com/token", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"ya29.test_access_token\",\"expires_in\":3600}", Encoding.UTF8, "application/json")
                };
            }

            if (url.Contains("cdn.ludeka.test/reel1.mp4", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70])
                };
            }

            if (url.Contains("uploadType=resumable", StringComparison.OrdinalIgnoreCase) && req.Method == HttpMethod.Post)
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Headers.Location = new Uri("https://www.googleapis.com/upload/youtube/v3/videos?uploadType=resumable&upload_id=session_123");
                return response;
            }

            if (url.Contains("upload_id=session_123", StringComparison.OrdinalIgnoreCase) && req.Method == HttpMethod.Put)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"id\":\"yt_short_abc123\"}", Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var ytOptions = Options.Create(new YouTubeOptions
        {
            Simulate = false,
            OAuthClientId = "client-id.apps.googleusercontent.com",
            OAuthClientSecret = "client-secret",
            OAuthRefreshToken = "1//refresh-token-test"
        });

        var client = new YouTubeShortsUploadClient(
            new HttpClient(handler),
            ytOptions,
            Options.Create(new AuthenticationOptions()),
            NullLogger<YouTubeShortsUploadClient>.Instance);

        // Act
        var result = await client.UploadShortFromUrlAsync(
            "https://cdn.ludeka.test/reel1.mp4",
            "Nuevo Reel Ludeka #Shorts",
            "Descripción con 👉 https://ludeka.es");

        // Assert
        Assert.False(result.IsQuotaExceeded);
        Assert.Equal("yt_short_abc123", result.VideoId);
        Assert.Equal("https://www.youtube.com/shorts/yt_short_abc123", result.ShortUrl);
    }

    [Fact]
    public async Task InstagramYouTubeSyncService_UploadsFiveExistingReelsChronologically_AndIsIdempotentOnSecondRun()
    {
        // Arrange
        var simulatedIgClient = new InstagramApiClient(
            new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))),
            Options.Create(new InstagramOptions { Simulate = true }),
            NullLogger<InstagramApiClient>.Instance);

        var syncRepo = new InMemoryInstagramReelSyncRepository();
        var captionAdapter = new FakeCaptionAdapter();
        var ytUploadClient = new RecordingYouTubeShortsUploadClient();

        var service = new InstagramYouTubeSyncService(
            simulatedIgClient,
            syncRepo,
            captionAdapter,
            ytUploadClient,
            Options.Create(new InstagramYouTubeSyncOptions { MaxUploadsPerRun = 5 }),
            NullLogger<InstagramYouTubeSyncService>.Instance);

        // Act 1: Primera carga masiva de los 5 Reels existentes
        var firstRun = await service.SyncReelsToYouTubeAsync();

        // Assert 1
        Assert.Equal(5, firstRun.DiscoveredReelsCount);
        Assert.Equal(5, firstRun.NewlyEnqueuedCount);
        Assert.Equal(5, firstRun.UploadedCount);
        Assert.Equal(0, firstRun.FailedCount);
        Assert.False(firstRun.StoppedByQuotaLimit);
        Assert.All(syncRepo.Items, item => Assert.Equal(InstagramReelSyncStatus.Uploaded, item.Status));

        // Verificar que se subieron en estricto orden cronológico (del más antiguo reel-101 al más reciente reel-105)
        Assert.EndsWith("reel-101.mp4", ytUploadClient.UploadedMediaUrls[0]);
        Assert.EndsWith("reel-105.mp4", ytUploadClient.UploadedMediaUrls[4]);

        // Act 2: Ejecución diaria subsiguiente sin nuevos Reels (Idempotencia)
        var secondRun = await service.SyncReelsToYouTubeAsync();

        // Assert 2
        Assert.Equal(5, secondRun.DiscoveredReelsCount);
        Assert.Equal(0, secondRun.NewlyEnqueuedCount);
        Assert.Equal(0, secondRun.UploadedCount);
        Assert.Equal(5, ytUploadClient.UploadedMediaUrls.Count); // No se invocó ninguna subida adicional
    }

    [Fact]
    public async Task InstagramYouTubeSyncJobRunner_CompletesWindowLease_WhenStoppedByQuotaLimit()
    {
        // Arrange
        var syncService = new StubInstagramYouTubeSyncService(new InstagramYouTubeSyncResultDto(
            DiscoveredReelsCount: 5,
            NewlyEnqueuedCount: 2,
            UploadedCount: 1,
            FailedCount: 1,
            SkippedCount: 0,
            StoppedByQuotaLimit: true));

        var coordinator = new PassThroughJobExecutionCoordinator();
        var runner = new InstagramYouTubeSyncJobRunner(
            coordinator,
            syncService,
            Options.Create(new InstagramYouTubeSyncOptions { MaxUploadsPerRun = 5 }));

        // Act
        var outcome = await runner.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(JobLeaseOutcome.Completed, outcome);
        Assert.NotNull(coordinator.LastWorkResult);
        Assert.Equal(1, coordinator.LastWorkResult.Processed);
        Assert.Equal(1, coordinator.LastWorkResult.Failed);
    }

    private sealed class PassThroughJobExecutionCoordinator : IJobExecutionCoordinator
    {
        public JobWorkResult? LastWorkResult { get; private set; }

        public async Task<JobLeaseOutcome> ExecuteWithWindowLeaseAsync(
            string jobName,
            string windowKey,
            Func<IJobHeartbeat, CancellationToken, Task<JobWorkResult>> work,
            CancellationToken ct = default)
        {
            LastWorkResult = await work(new NoopJobHeartbeat(), ct);
            return JobLeaseOutcome.Completed;
        }
    }

    private sealed class NoopJobHeartbeat : IJobHeartbeat
    {
        public Task BeatAsync(CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }

    private sealed class FakePublisherRepository(IReadOnlyList<Publisher> items) : IPublisherRepository
    {
        public Task<IReadOnlyList<Publisher>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(items);
        public Task<Publisher?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(items.FirstOrDefault(x => x.Id == id));
        public Task<Publisher?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(items.FirstOrDefault(x => x.Slug == slug));
        public Task<Publisher?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult(items.FirstOrDefault(x => x.Name == name));
        public Task AddAsync(Publisher publisher, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Publisher publisher, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeStoreRepository(IReadOnlyList<Store> items) : IStoreRepository
    {
        public Task<IReadOnlyList<Store>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(items);
        public Task<IReadOnlyList<Store>> GetApprovedAsync(CancellationToken ct = default) => Task.FromResult(items);
        public Task<IReadOnlyList<Store>> GetByCityAsync(string city, CancellationToken ct = default) => Task.FromResult(items);
        public Task<Store?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(items.FirstOrDefault(x => x.Id == id));
        public Task<Store?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(items.FirstOrDefault(x => x.Slug == slug));
        public Task<Store?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult(items.FirstOrDefault(x => x.Name == name));
        public Task AddAsync(Store store, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Store store, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeCreatorRepository(IReadOnlyList<Creator> items) : ICreatorRepository
    {
        public Task<IReadOnlyList<Creator>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(items);
        public Task<Creator?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(items.FirstOrDefault(x => x.Id == id));
        public Task<Creator?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(items.FirstOrDefault(x => x.Slug == slug));
        public Task<Creator?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult(items.FirstOrDefault(x => x.Name == name));
        public Task<Creator?> GetByChannelIdAsync(string channelId, CancellationToken ct = default) => Task.FromResult<Creator?>(null);
        public Task AddAsync(Creator creator, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Creator creator, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeMonitoredAccountRepository(IReadOnlyList<MonitoredSocialAccount> items) : IMonitoredAccountRepository
    {
        public Task<IReadOnlyList<MonitoredSocialAccount>> GetAllAsync(SocialPlatform? platform = null, MonitoredAccountType? type = null, bool? activeOnly = null, CancellationToken ct = default)
            => Task.FromResult(items);
        public Task<MonitoredSocialAccount?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(items.FirstOrDefault(x => x.Id == id));
        public Task<MonitoredSocialAccount?> GetByHandleAsync(SocialPlatform platform, string handle, CancellationToken ct = default)
            => Task.FromResult<MonitoredSocialAccount?>(null);
        public Task<bool> ExistsAsync(SocialPlatform platform, string handle, CancellationToken ct = default)
            => Task.FromResult(false);
        public Task<MonitoredSocialAccount> AddAsync(MonitoredSocialAccount account, CancellationToken ct = default) => Task.FromResult(account);
        public Task UpdateAsync(MonitoredSocialAccount account, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class InMemoryInstagramReelSyncRepository : IInstagramReelSyncRepository
    {
        public List<InstagramReelSyncItem> Items { get; } = [];

        public Task<InstagramReelSyncItem?> GetByInstagramMediaIdAsync(string instagramMediaId, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(x => x.InstagramMediaId.Equals(instagramMediaId, StringComparison.OrdinalIgnoreCase)));

        public Task<InstagramReelSyncItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<InstagramReelSyncItem>> GetPendingForUploadAsync(int maxItems, int maxAttempts = 3, CancellationToken ct = default)
        {
            IReadOnlyList<InstagramReelSyncItem> pending = Items
                .Where(x => (x.Status == InstagramReelSyncStatus.Pending || x.Status == InstagramReelSyncStatus.Failed) && x.AttemptCount < maxAttempts)
                .OrderBy(x => x.MediaPublishedAtUtc)
                .Take(maxItems)
                .ToList();
            return Task.FromResult(pending);
        }

        public Task<IReadOnlyList<InstagramReelSyncItem>> GetAllAsync(InstagramReelSyncStatus? statusFilter = null, int limit = 100, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<InstagramReelSyncItem>>(Items.Take(limit).ToList());

        public Task AddAsync(InstagramReelSyncItem item, CancellationToken ct = default)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(InstagramReelSyncItem item, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeCaptionAdapter : IInstagramToYouTubeCaptionAdapter
    {
        public Task<YouTubeAdaptedMetadataDto> AdaptCaptionForYouTubeShortAsync(string? instagramCaption, CancellationToken ct = default)
            => Task.FromResult(new YouTubeAdaptedMetadataDto(
                Title: "Reel Ludeka #Shorts",
                Description: $"{instagramCaption}\n👉 https://ludeka.es",
                ResolvedMentions: []));
    }

    private sealed class RecordingYouTubeShortsUploadClient : IYouTubeShortsUploadClient
    {
        public bool IsConfigured => true;
        public List<string> UploadedMediaUrls { get; } = [];

        public Task<YouTubeShortUploadResultDto> UploadShortFromUrlAsync(string mediaUrl, string title, string description, CancellationToken ct = default)
        {
            UploadedMediaUrls.Add(mediaUrl);
            var vid = $"yt_{UploadedMediaUrls.Count}";
            return Task.FromResult(new YouTubeShortUploadResultDto(vid, $"https://www.youtube.com/shorts/{vid}"));
        }
    }

    private sealed class StubInstagramYouTubeSyncService(InstagramYouTubeSyncResultDto result) : IInstagramYouTubeSyncService
    {
        public Task<InstagramYouTubeSyncResultDto> SyncReelsToYouTubeAsync(int? maxUploads = null, CancellationToken ct = default)
            => Task.FromResult(result);

        public Task<IReadOnlyList<InstagramReelSyncItemDto>> GetSyncItemsAsync(InstagramReelSyncStatus? statusFilter = null, int limit = 50, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<InstagramReelSyncItemDto>>([]);
    }
}
