using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Community;
using Ludeka.Application.Features.Instagram;
using Ludeka.Application.Options;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Repositories;
using Ludeka.Infrastructure.YouTube;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Application;

/// <summary>Dobles compartidos por las pruebas de las escrituras de comunidad.</summary>
internal sealed class StaticMetadataExtractor : ISocialMetadataExtractor
{
    public Task<SocialMetadataResultDto?> ExtractFromUrlAsync(string url, CancellationToken ct = default)
        => Task.FromResult<SocialMetadataResultDto?>(null);
}

internal sealed class StaticAiAnalysisService : ISocialAiAnalysisService
{
    public Task<SocialAiAnalysisResultDto> AnalyzeTextAsync(string text, string? authorOrChannel = null, CancellationToken ct = default)
        => Task.FromResult(new SocialAiAnalysisResultDto(
            DetectedType: SocialSubmissionType.Giveaway,
            Title: "Sorteo detectado",
            OrganizerOrAuthor: "Editorial Local",
            Collaborator: null,
            SuggestedGameTitle: null,
            EventOrReleaseDate: DateTimeOffset.UtcNow.AddDays(5),
            EventEndDate: null,
            Location: null,
            EstimatedPvp: null,
            MediaCategory: null,
            PlayerCountBadge: null,
            Notes: null));

    public Task<SocialAiAnalysisResultDto> AnalyzeMultimodalAsync(
        string? rulesText = null,
        byte[]? rulesImageBytes = null,
        string? rulesImageMimeType = null,
        byte[]? coverImageBytes = null,
        string? coverImageMimeType = null,
        string? authorOrChannel = null,
        CancellationToken ct = default)
        => Task.FromResult(new SocialAiAnalysisResultDto(
            DetectedType: SocialSubmissionType.Giveaway,
            Title: "Sorteo detectado",
            OrganizerOrAuthor: "Editorial Local",
            Collaborator: null,
            SuggestedGameTitle: null,
            EventOrReleaseDate: DateTimeOffset.UtcNow.AddDays(5),
            EventEndDate: null,
            Location: null,
            EstimatedPvp: null,
            MediaCategory: null,
            PlayerCountBadge: null,
            Notes: null));
}

internal sealed class NoopImageStorageService : IImageStorageService
{
    public Task<string> UploadOptimizedImageAsync(Stream inputStream, string objectKey, int maxWidth = 1000, int quality = 82, CancellationToken ct = default)
        => Task.FromResult($"https://cdn.ludeka.test/{objectKey}");

    public Task<ImageVariantUrls> UploadGameImageVariantsAsync(Stream rawImageStream, int bggId, string imageType, CancellationToken ct = default)
        => Task.FromResult(new ImageVariantUrls($"https://cdn.ludeka.test/games/{bggId}/{imageType}.webp", string.Empty));

    public Task<bool> DeleteImageAsync(string objectKey, CancellationToken ct = default) => Task.FromResult(true);

    public string GetPublicUrl(string objectKey) => $"https://cdn.ludeka.test/{objectKey}";

    public Task<GameImageUploadResult> SaveGameCoverAsync(string slug, Stream contentStream, string originalFileName, string contentType, CancellationToken ct = default)
        => Task.FromResult(new GameImageUploadResult(true, "https://cdn.ludeka.test/cover.webp", null));

    public Task<GameImageUploadResult> SaveEventPosterAsync(string eventSlugOrId, Stream contentStream, string originalFileName, string contentType, CancellationToken ct = default)
        => Task.FromResult(new GameImageUploadResult(true, "https://cdn.ludeka.test/poster.webp", null));

    public Task<GameImageUploadResult> SaveCommunityImageAsync(string folder, string entityId, Stream contentStream, string originalFileName, string contentType, CancellationToken ct = default)
        => Task.FromResult(new GameImageUploadResult(true, "https://cdn.ludeka.test/community.webp", null));

    public Task<GameImageUploadResult> ValidateCoverUrlAsync(string coverUrl, CancellationToken ct = default)
        => Task.FromResult(new GameImageUploadResult(true, coverUrl, null));
}

internal sealed class NoopDiscordWebhookClient : IDiscordWebhookClient
{
    public Task<NotificationDispatchResult> SendAsync(CommunityNotificationMessage message, CancellationToken ct = default)
        => Task.FromResult(new NotificationDispatchResult(false, NotificationChannel.Discord, NotificationStatus.Failed, "no configurado"));
}

internal sealed class NoopTelegramBotClient : ITelegramBotClient
{
    public Task<NotificationDispatchResult> SendAsync(CommunityNotificationMessage message, CancellationToken ct = default)
        => Task.FromResult(new NotificationDispatchResult(false, NotificationChannel.Telegram, NotificationStatus.Failed, "no configurado"));
}

internal sealed class RecordingChannelCollector : ISocialChannelCollector
{
    public int CallCount { get; private set; }

    public IReadOnlyList<DiscoveredSocialPostDto> PostsToReturn { get; set; } = [];

    public bool CanHandle(SocialPlatform platform) => true;

    public Task<IReadOnlyList<DiscoveredSocialPostDto>> CollectRecentPostsAsync(
        MonitoredSocialAccount account,
        int maxItems,
        CancellationToken ct = default)
    {
        CallCount++;
        return Task.FromResult(PostsToReturn);
    }
}

internal sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
{
    public TestOptionsMonitor(T value) => CurrentValue = value;

    public T CurrentValue { get; }

    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

internal sealed class StubChannelFocusProvider : IChannelFocusProvider
{
    public IReadOnlyList<ChannelFocusEntry> GetReferenceChannels() => [];

    public bool IsReferenceChannel(string channelTitle, out ChannelCategory category, out int priorityBonus)
    {
        category = ChannelCategory.Creator;
        priorityBonus = 0;
        return false;
    }
}

/// <summary>Bandeja de moderación social: alta exprés, edición, aprobación y descarte.</summary>
public class SocialIngestionWriteGuardTests : AdministrativeWriteGuardTestBase
{
    private SocialIngestionService CreateService(ISessionPermissionGuard? guard = null)
        => new(
            new SqliteSocialInboxRepository(Context),
            new StaticMetadataExtractor(),
            new StaticAiAnalysisService(),
            new NoopImageStorageService(),
            new SqliteGameRepository(Context),
            new SqliteGiveawayRepository(Context),
            new SqliteWeeklyReleaseRepository(Context),
            new SqliteBoardGameEventRepository(Context),
            new SqliteMediaRepository(Context),
            new HttpClient(),
            NullLogger<SocialIngestionService>.Instance,
            guard);

    private async Task<SocialInboxItem> SeedPendingItemAsync()
    {
        var item = new SocialInboxItem(
            sourceUrl: "https://instagram.com/p/sorteo-de-prueba",
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Sorteo de prueba",
            organizerOrAuthor: "Editorial Local");

        Context.SocialInboxItems.Add(item);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return item;
    }

    [Fact]
    public async Task IngestFromUrlAsync_WithoutSession_DeniesAndPersistsNothing()
    {
        var service = CreateService(CreateGuard(Anonymous()));

        await AssertDenied(() => service.IngestFromUrlAsync("https://instagram.com/p/nuevo-sorteo"));

        Assert.Empty(await Context.SocialInboxItems.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task ApproveAndPublishAsync_WithSuspendedAccount_DeniesEvenWithLiveCookie()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanApproveMedia));
        await SuspendAsync(ModeratorId);
        var item = await SeedPendingItemAsync();
        var service = CreateService(CreateGuard(LiveCookie()));

        await AssertDenied(() => service.ApproveAndPublishAsync(item.Id, ModeratorId));

        var stored = await Context.SocialInboxItems.AsNoTracking().SingleAsync(i => i.Id == item.Id);
        Assert.Equal(SocialInboxStatus.PendingReview, stored.Status);
        Assert.Empty(await Context.Giveaways.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task IngestFromUrlAsync_WithThePermission_PersistsTheInboxItem()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanApproveMedia));
        var service = CreateService(CreateGuard(LiveCookie()));

        var created = await service.IngestFromUrlAsync("https://instagram.com/p/nuevo-sorteo");

        Assert.Equal("Sorteo detectado", created.Title);
        Assert.Equal(1, await Context.SocialInboxItems.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task IngestFromCollectorAsync_WithoutSession_IngestsAsTheSystemPath()
    {
        var service = CreateService();

        var created = await service.IngestFromCollectorAsync("https://instagram.com/p/descubierto", "descripción");

        Assert.Equal("Sorteo detectado", created.Title);
        Assert.Equal(1, await Context.SocialInboxItems.AsNoTracking().CountAsync());
    }
}

/// <summary>Cuentas monitorizadas: alta, edición, estado, borrado y sincronización con el directorio.</summary>
public class MonitoredAccountWriteGuardTests : AdministrativeWriteGuardTestBase
{
    private MonitoredAccountService CreateService(ISessionPermissionGuard? guard = null)
        => new(
            new SqliteMonitoredAccountRepository(Context),
            new SqlitePublisherRepository(Context),
            new SqliteCreatorRepository(Context),
            new SqliteStoreRepository(Context),
            NullLogger<MonitoredAccountService>.Instance,
            guard);

    private static MonitoredAccountDto CreateAccountDto(string name = "Canal de prueba") => new(
        Id: Guid.Empty,
        Name: name,
        Platform: SocialPlatform.Instagram,
        HandleOrChannelId: "@canal-de-prueba",
        AccountType: MonitoredAccountType.Creator,
        ProfileUrl: "https://instagram.com/canal-de-prueba",
        Notes: null,
        IsEnabled: true,
        LastCheckedAt: null,
        CreatedAt: DateTimeOffset.UtcNow);

    private async Task<MonitoredSocialAccount> SeedAccountAsync()
    {
        var account = new MonitoredSocialAccount(
            name: "Canal existente",
            platform: SocialPlatform.Instagram,
            handleOrChannelId: "@canal-existente",
            accountType: MonitoredAccountType.Creator,
            profileUrl: "https://instagram.com/canal-existente");

        Context.MonitoredSocialAccounts.Add(account);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return account;
    }

    [Fact]
    public async Task CreateAccountAsync_WithoutSession_DeniesAndPersistsNothing()
    {
        var service = CreateService(CreateGuard(Anonymous()));

        await AssertDenied(() => service.CreateAccountAsync(CreateAccountDto()));

        Assert.Empty(await Context.MonitoredSocialAccounts.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CreateAccountAsync_WithSuspendedAccount_DeniesEvenWithLiveCookie()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanApproveMedia));
        await SuspendAsync(ModeratorId);
        var service = CreateService(CreateGuard(LiveCookie()));

        await AssertDenied(() => service.CreateAccountAsync(CreateAccountDto()));

        Assert.Empty(await Context.MonitoredSocialAccounts.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CreateAccountAsync_WithThePermission_PersistsTheAccount()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanApproveMedia));
        var service = CreateService(CreateGuard(LiveCookie()));

        var created = await service.CreateAccountAsync(CreateAccountDto());

        Assert.Equal("Canal de prueba", created.Name);
        Assert.Equal(1, await Context.MonitoredSocialAccounts.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task DeleteAccountAsync_WithoutThePermission_DeniesAndKeepsTheAccount()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanEditGames));
        var account = await SeedAccountAsync();
        var service = CreateService(CreateGuard(LiveCookie()));

        await AssertDenied(() => service.DeleteAccountAsync(account.Id));

        Assert.True(await Context.MonitoredSocialAccounts.AsNoTracking().AnyAsync(a => a.Id == account.Id));
    }
}

/// <summary>Recolector social: sondeo bajo demanda desde el panel y ciclo programado del servicio hospedado.</summary>
public class SocialCollectorWriteGuardTests : AdministrativeWriteGuardTestBase
{
    private readonly RecordingChannelCollector _collector = new();

    private SocialCollectorService CreateService(
        ISessionPermissionGuard? guard = null,
        SocialIngestionService? ingestion = null)
        => new(
            new SqliteMonitoredAccountRepository(Context),
            new SqliteSocialInboxRepository(Context),
            ingestion ?? new SocialIngestionService(
                new SqliteSocialInboxRepository(Context),
                new StaticMetadataExtractor(),
                new StaticAiAnalysisService(),
                new NoopImageStorageService(),
                new SqliteGameRepository(Context),
                new SqliteGiveawayRepository(Context),
                new SqliteWeeklyReleaseRepository(Context),
                new SqliteBoardGameEventRepository(Context),
                new SqliteMediaRepository(Context),
                new HttpClient(),
                NullLogger<SocialIngestionService>.Instance),
            [_collector],
            new TestOptionsMonitor<SocialCollectorOptions>(new SocialCollectorOptions()),
            NullLogger<SocialCollectorService>.Instance,
            guard);

    private async Task<MonitoredSocialAccount> SeedAccountAsync()
    {
        var account = new MonitoredSocialAccount(
            name: "Canal existente",
            platform: SocialPlatform.YouTube,
            handleOrChannelId: "@canal-existente",
            accountType: MonitoredAccountType.Creator,
            profileUrl: "https://youtube.com/@canal-existente");

        Context.MonitoredSocialAccounts.Add(account);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return account;
    }

    [Fact]
    public async Task CollectAllAccountsAsync_WithoutSession_DeniesAndDoesNotProbeChannels()
    {
        var account = await SeedAccountAsync();
        var service = CreateService(CreateGuard(Anonymous()));

        await AssertDenied(() => service.CollectAllAccountsAsync());

        Assert.Equal(0, _collector.CallCount);
        var stored = await Context.MonitoredSocialAccounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
        Assert.Null(stored.LastCheckedAt);
    }

    [Fact]
    public async Task CollectAllAccountsAsync_WithSuspendedAccount_DeniesEvenWithLiveCookie()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanApproveMedia));
        await SuspendAsync(ModeratorId);
        var account = await SeedAccountAsync();
        var service = CreateService(CreateGuard(LiveCookie()));

        await AssertDenied(() => service.CollectAllAccountsAsync());

        Assert.Equal(0, _collector.CallCount);
        var stored = await Context.MonitoredSocialAccounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
        Assert.Null(stored.LastCheckedAt);
    }

    [Fact]
    public async Task CollectAllAccountsAsync_WithThePermission_CollectsAndMarksTheAccount()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanApproveMedia));
        var account = await SeedAccountAsync();
        var service = CreateService(CreateGuard(LiveCookie()));

        var result = await service.CollectAllAccountsAsync();

        Assert.Equal(1, _collector.CallCount);
        Assert.Equal(1, result.AccountsScanned);
        var stored = await Context.MonitoredSocialAccounts.AsNoTracking().SingleAsync(a => a.Id == account.Id);
        Assert.NotNull(stored.LastCheckedAt);
    }

    [Fact]
    public async Task RunScheduledCollectionAsync_WithoutSession_CollectsAsTheSystemPath()
    {
        await SeedAccountAsync();
        _collector.PostsToReturn =
        [
            new DiscoveredSocialPostDto
            {
                SourceUrl = "https://youtube.com/watch?v=descubierto",
                Title = "Vídeo descubierto",
                Description = "descripción",
                Platform = SocialPlatform.YouTube,
                PublishedAt = DateTimeOffset.UtcNow
            }
        ];

        var service = CreateService();

        var result = await service.RunScheduledCollectionAsync(maxItemsPerAccount: 5);

        Assert.Equal(1, _collector.CallCount);
        Assert.Equal(1, result.ItemsImported);
        Assert.Equal(1, await Context.SocialInboxItems.AsNoTracking().CountAsync());
    }
}

/// <summary>Notificaciones comunitarias: ping de prueba, reintentos y disparos manuales del panel.</summary>
public class CommunityNotificationWriteGuardTests : AdministrativeWriteGuardTestBase
{
    private static IOptions<CommunityNotificationOptions> Options()
        => Microsoft.Extensions.Options.Options.Create(new CommunityNotificationOptions
        {
            Enabled = true,
            DryRun = true,
            DiscordEnabled = true,
            TelegramEnabled = false
        });

    private CommunityNotificationService CreateService(ISessionPermissionGuard? guard = null)
        => new(
            Options(),
            new SqliteCommunityNotificationRepository(Context),
            new NoopDiscordWebhookClient(),
            new NoopTelegramBotClient(),
            new SqliteGiveawayRepository(Context),
            new SqliteWeeklyReleaseRepository(Context),
            NullLogger<CommunityNotificationService>.Instance,
            guard);

    private async Task<Giveaway> SeedExpiringGiveawayAsync()
    {
        var giveaway = new Giveaway(
            title: "Sorteo a punto de expirar",
            organizer: "Editorial Local",
            url: "https://example.test/sorteo-expirando",
            platform: GiveawayPlatform.Instagram,
            deadlineAt: DateTimeOffset.UtcNow.AddHours(2));

        Context.Giveaways.Add(giveaway);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return giveaway;
    }

    [Fact]
    public async Task SendTestPingAsync_WithoutSession_DeniesAndWritesNoLog()
    {
        var service = CreateService(CreateGuard(Anonymous()));

        await AssertDenied(() => service.SendTestPingAsync(NotificationChannel.Discord));

        Assert.Empty(await Context.NotificationLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task TriggerExpiringGiveawaysScanAsync_WithSuspendedAccount_DeniesEvenWithLiveCookie()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanManageNotifications));
        await SuspendAsync(ModeratorId);
        await SeedExpiringGiveawayAsync();
        var service = CreateService(CreateGuard(LiveCookie()));

        await AssertDenied(() => service.TriggerExpiringGiveawaysScanAsync());

        Assert.Empty(await Context.NotificationLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task SendTestPingAsync_WithThePermission_WritesTheDryRunLog()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanManageNotifications));
        var service = CreateService(CreateGuard(LiveCookie()));

        await service.SendTestPingAsync(NotificationChannel.Discord);

        var log = await Context.NotificationLogs.AsNoTracking().SingleAsync();
        Assert.Equal(NotificationChannel.Discord, log.Channel);
        Assert.Equal(NotificationStatus.DryRun, log.Status);
    }

    [Fact]
    public async Task RunExpiringGiveawaysScanAsync_WithoutSession_DispatchesAsTheSystemPath()
    {
        await SeedExpiringGiveawayAsync();
        var service = CreateService();

        await service.RunExpiringGiveawaysScanAsync();

        var log = await Context.NotificationLogs.AsNoTracking().SingleAsync();
        Assert.Contains("Sorteo a punto de expirar", log.Title);
    }
}

/// <summary>Búsqueda de YouTube: ingestión de vídeos desde el panel de moderación multimedia.</summary>
public class YouTubeSearchWriteGuardTests : AdministrativeWriteGuardTestBase
{
    private YouTubeSearchService CreateService(ISessionPermissionGuard? guard = null)
        => new(
            new HttpClient(),
            Microsoft.Extensions.Options.Options.Create(new YouTubeOptions()),
            new StubChannelFocusProvider(),
            new SqliteGameRepository(Context),
            new SqliteMediaRepository(Context),
            NullLogger<YouTubeSearchService>.Instance,
            guard);

    private YouTubeIngestRequestDto CreateRequest() => new(
        GameId: Game.Id,
        VideoId: "abc123",
        Type: MediaType.Tutorial,
        Title: "Tutorial de prueba",
        Url: "https://youtube.com/watch?v=abc123",
        ThumbnailUrl: "https://i.ytimg.com/vi/abc123/hqdefault.jpg",
        ChannelTitle: "Canal de Prueba");

    [Fact]
    public async Task IngestVideoAsync_WithoutSession_DeniesAndPersistsNothing()
    {
        var service = CreateService(CreateGuard(Anonymous()));

        await AssertDenied(() => service.IngestVideoAsync(CreateRequest()));

        Assert.Empty(await Context.MediaItems.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task IngestVideoAsync_WithSuspendedAccount_DeniesEvenWithLiveCookie()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanApproveMedia));
        await SuspendAsync(ModeratorId);
        var service = CreateService(CreateGuard(LiveCookie()));

        await AssertDenied(() => service.IngestVideoAsync(CreateRequest()));

        Assert.Empty(await Context.MediaItems.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task IngestVideoAsync_WithThePermission_PersistsTheMediaItem()
    {
        SeedUser(ModeratorWith(ModeratorPermission.CanApproveMedia));
        var service = CreateService(CreateGuard(LiveCookie()));

        var created = await service.IngestVideoAsync(CreateRequest());

        Assert.Equal("Tutorial de prueba", created.Title);
        var stored = await Context.MediaItems.AsNoTracking().SingleAsync();
        Assert.Equal("https://youtube.com/watch?v=abc123", stored.Url);
    }
}
