using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Community;
using Ludeka.Application.Options;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class CommunityNotificationServiceTests
{
    private class FakeNotificationRepository : ICommunityNotificationRepository
    {
        public readonly List<CommunityNotificationLog> Logs = [];

        public Task AddLogAsync(CommunityNotificationLog log, CancellationToken ct = default)
        {
            Logs.Add(log);
            return Task.CompletedTask;
        }

        public Task<CommunityNotificationLog?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return Task.FromResult(Logs.Find(l => l.Id == id));
        }

        public Task<IReadOnlyList<CommunityNotificationLog>> GetRecentLogsAsync(int take = 50, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<CommunityNotificationLog>>(Logs);
        }

        public Task UpdateLogAsync(CommunityNotificationLog log, CancellationToken ct = default)
        {
            var index = Logs.FindIndex(l => l.Id == log.Id);
            if (index >= 0) Logs[index] = log;
            return Task.CompletedTask;
        }
    }

    private class FakeDiscordClient : IDiscordWebhookClient
    {
        public int SendCalls { get; private set; }
        public bool ShouldFail { get; set; }

        public Task<NotificationDispatchResult> SendAsync(CommunityNotificationMessage message, CancellationToken ct = default)
        {
            SendCalls++;
            if (ShouldFail)
            {
                return Task.FromResult(new NotificationDispatchResult(false, NotificationChannel.Discord, NotificationStatus.Failed, "Discord 500"));
            }

            return Task.FromResult(new NotificationDispatchResult(true, NotificationChannel.Discord, NotificationStatus.Sent));
        }
    }

    private class FakeTelegramClient : ITelegramBotClient
    {
        public int SendCalls { get; private set; }

        public Task<NotificationDispatchResult> SendAsync(CommunityNotificationMessage message, CancellationToken ct = default)
        {
            SendCalls++;
            return Task.FromResult(new NotificationDispatchResult(true, NotificationChannel.Telegram, NotificationStatus.Sent));
        }
    }

    private class FakeGiveawayRepository : IGiveawayRepository
    {
        public List<Giveaway> Items = [];

        public Task<IReadOnlyList<Giveaway>> GetGiveawaysAsync(bool includeExpired = false, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Giveaway>>(Items.FindAll(g => includeExpired || !g.IsExpired));

        public Task<Giveaway?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.Find(g => g.Id == id));
        public Task<Giveaway?> FindDuplicateOrCollaborativeAsync(string title, string organizer, DateTimeOffset deadline, CancellationToken ct = default) => Task.FromResult<Giveaway?>(null);
        public Task AddAsync(Giveaway giveaway, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Giveaway giveaway, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private class FakeWeeklyReleaseRepository : IWeeklyReleaseRepository
    {
        public List<WeeklyRelease> Items = [];

        public Task<IReadOnlyList<WeeklyRelease>> GetReleasesAsync(DateOnly? fromDate = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WeeklyRelease>>(Items.FindAll(r => !fromDate.HasValue || r.ReleaseDate >= fromDate.Value));

        public Task<WeeklyRelease?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.Find(r => r.Id == id));
        public Task AddAsync(WeeklyRelease release, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(WeeklyRelease release, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task BroadcastAsync_WithDryRun_RecordsDryRunLogsAndDoesNotCallHttp()
    {
        var options = Options.Create(new CommunityNotificationOptions
        {
            Enabled = true,
            DryRun = true,
            DiscordEnabled = true,
            TelegramEnabled = true
        });

        var repo = new FakeNotificationRepository();
        var discord = new FakeDiscordClient();
        var telegram = new FakeTelegramClient();
        var giveawayRepo = new FakeGiveawayRepository();
        var weeklyRepo = new FakeWeeklyReleaseRepository();

        var service = new CommunityNotificationService(
            options,
            repo,
            discord,
            telegram,
            giveawayRepo,
            weeklyRepo,
            NullLogger<CommunityNotificationService>.Instance);

        var message = new CommunityNotificationMessage(
            NotificationEventType.CustomTestPing,
            "Ping de prueba",
            "Mensaje de test");

        var results = await service.BroadcastAsync(message);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(NotificationStatus.DryRun, r.Status));
        Assert.Equal(0, discord.SendCalls);
        Assert.Equal(0, telegram.SendCalls);
        Assert.Equal(2, repo.Logs.Count);
        Assert.All(repo.Logs, l => Assert.Equal(NotificationStatus.DryRun, l.Status));
    }

    [Fact]
    public async Task BroadcastAsync_WhenDisabled_ReturnsEmptyAndDoesNotLog()
    {
        var options = Options.Create(new CommunityNotificationOptions
        {
            Enabled = false
        });

        var repo = new FakeNotificationRepository();
        var service = new CommunityNotificationService(
            options,
            repo,
            new FakeDiscordClient(),
            new FakeTelegramClient(),
            new FakeGiveawayRepository(),
            new FakeWeeklyReleaseRepository(),
            NullLogger<CommunityNotificationService>.Instance);

        var results = await service.BroadcastAsync(new CommunityNotificationMessage(
            NotificationEventType.CustomTestPing,
            "Ping",
            "Test"));

        Assert.Empty(results);
        Assert.Empty(repo.Logs);
    }

    [Fact]
    public async Task TriggerExpiringGiveawaysScanAsync_FindsExpiringGiveaway_DispatchesNotification()
    {
        var options = Options.Create(new CommunityNotificationOptions
        {
            Enabled = true,
            DryRun = true,
            DiscordEnabled = true,
            TelegramEnabled = false
        });

        var repo = new FakeNotificationRepository();
        var giveawayRepo = new FakeGiveawayRepository();
        var weeklyRepo = new FakeWeeklyReleaseRepository();

        var expiringGiveaway = new Giveaway(
            "Sorteo Wingspan",
            "Maldito Games",
            "https://instagram.com/p/123",
            GiveawayPlatform.Instagram,
            DateTimeOffset.UtcNow.AddHours(5),
            gameTitle: "Wingspan");

        var safeGiveaway = new Giveaway(
            "Sorteo Lejano",
            "Devir",
            "https://instagram.com/p/456",
            GiveawayPlatform.Instagram,
            DateTimeOffset.UtcNow.AddDays(5),
            gameTitle: "Carcassonne");

        giveawayRepo.Items.Add(expiringGiveaway);
        giveawayRepo.Items.Add(safeGiveaway);

        var service = new CommunityNotificationService(
            options,
            repo,
            new FakeDiscordClient(),
            new FakeTelegramClient(),
            giveawayRepo,
            weeklyRepo,
            NullLogger<CommunityNotificationService>.Instance);

        await service.TriggerExpiringGiveawaysScanAsync();

        Assert.Single(repo.Logs);
        Assert.Contains("Sorteo Wingspan", repo.Logs[0].Title);
        Assert.Equal(NotificationEventType.GiveawayExpiring, repo.Logs[0].EventType);
    }

    [Fact]
    public async Task TriggerFridayReleasesBulletinAsync_WithReleases_ConsolidatesAndDispatches()
    {
        var options = Options.Create(new CommunityNotificationOptions
        {
            Enabled = true,
            DryRun = true,
            DiscordEnabled = true,
            TelegramEnabled = false
        });

        var repo = new FakeNotificationRepository();
        var giveawayRepo = new FakeGiveawayRepository();
        var weeklyRepo = new FakeWeeklyReleaseRepository();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        weeklyRepo.Items.Add(new WeeklyRelease("Dune: Imperium Uprising", "Asmodee", today, estimatedPvp: 59.99m));
        weeklyRepo.Items.Add(new WeeklyRelease("Harmonies", "Maldito Games", today, estimatedPvp: 34.99m));

        var service = new CommunityNotificationService(
            options,
            repo,
            new FakeDiscordClient(),
            new FakeTelegramClient(),
            giveawayRepo,
            weeklyRepo,
            NullLogger<CommunityNotificationService>.Instance);

        await service.TriggerFridayReleasesBulletinAsync();

        Assert.Single(repo.Logs);
        Assert.Equal(NotificationEventType.FridayReleasesSummary, repo.Logs[0].EventType);
        Assert.Contains("Dune: Imperium", repo.Logs[0].Summary);
        Assert.Contains("Harmonies", repo.Logs[0].Summary);
    }

    [Fact]
    public async Task RetryFailedNotificationAsync_WhenLogNotFound_ReturnsFailedResult()
    {
        var options = Options.Create(new CommunityNotificationOptions());
        var service = new CommunityNotificationService(
            options,
            new FakeNotificationRepository(),
            new FakeDiscordClient(),
            new FakeTelegramClient(),
            new FakeGiveawayRepository(),
            new FakeWeeklyReleaseRepository(),
            NullLogger<CommunityNotificationService>.Instance);

        var result = await service.RetryFailedNotificationAsync(Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Equal(NotificationStatus.Failed, result.Status);
    }

    // --- INC-47 (R4b, diseño §6.6, tasks.md 7.8): DeliverAsync envía sobre una sub-entrega del
    // outbox ya existente y actualiza ESA fila, sin crear una fila nueva. La orquestación del
    // fan-out por canal (7.1-7.5) se prueba de forma aislada en NotificationOutboxDispatcherTests
    // con un doble de este servicio; aquí se prueba la propia mecánica de DeliverAsync. ---

    [Fact]
    public async Task DeliverAsync_ConEnvioExitoso_MarcaLaSubentregaComoSentYPersisteElCambio()
    {
        var options = Options.Create(new CommunityNotificationOptions
        {
            Enabled = true,
            DryRun = false,
            DiscordEnabled = true,
            DiscordWebhookUrl = "https://discord.example/webhook"
        });

        var repo = new FakeNotificationRepository();
        var discord = new FakeDiscordClient();
        var service = new CommunityNotificationService(
            options,
            repo,
            discord,
            new FakeTelegramClient(),
            new FakeGiveawayRepository(),
            new FakeWeeklyReleaseRepository(),
            NullLogger<CommunityNotificationService>.Instance);

        var delivery = CommunityNotificationLog.ForDelivery(
            Guid.NewGuid(), NotificationEventType.CustomTestPing, NotificationChannel.Discord,
            "Ping de prueba", "Resumen del ping", null, null);
        repo.Logs.Add(delivery);

        var message = new CommunityNotificationMessage(NotificationEventType.CustomTestPing, "Ping de prueba", "Resumen del ping");

        var result = await service.DeliverAsync(delivery, message);

        Assert.True(result.Success);
        Assert.Equal(1, discord.SendCalls);
        Assert.Equal(NotificationStatus.Sent, delivery.Status);
        Assert.Equal(NotificationStatus.Sent, repo.Logs[0].Status);
    }

    [Fact]
    public async Task DeliverAsync_AgotandoMaxDeliveryAttempts_MarcaLaSubentregaComoFailedTerminal()
    {
        var options = Options.Create(new CommunityNotificationOptions
        {
            Enabled = true,
            DryRun = false,
            DiscordEnabled = true,
            DiscordWebhookUrl = "https://discord.example/webhook"
        });

        var repo = new FakeNotificationRepository();
        var discord = new FakeDiscordClient { ShouldFail = true };
        var service = new CommunityNotificationService(
            options,
            repo,
            discord,
            new FakeTelegramClient(),
            new FakeGiveawayRepository(),
            new FakeWeeklyReleaseRepository(),
            NullLogger<CommunityNotificationService>.Instance,
            outboxOptions: Options.Create(new OutboxOptions { MaxDeliveryAttempts = 1 }));

        var delivery = CommunityNotificationLog.ForDelivery(
            Guid.NewGuid(), NotificationEventType.CustomTestPing, NotificationChannel.Discord,
            "Ping de prueba", "Resumen del ping", null, null);
        repo.Logs.Add(delivery);

        var message = new CommunityNotificationMessage(NotificationEventType.CustomTestPing, "Ping de prueba", "Resumen del ping");

        var result = await service.DeliverAsync(delivery, message);

        Assert.False(result.Success);
        Assert.Equal(1, delivery.Attempts);
        Assert.Equal(NotificationStatus.Failed, delivery.Status);
        Assert.Equal(NotificationStatus.Failed, repo.Logs[0].Status);
    }
}
