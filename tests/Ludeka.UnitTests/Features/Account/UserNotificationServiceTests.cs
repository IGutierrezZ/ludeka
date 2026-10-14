using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Account;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Xunit;

namespace Ludeka.UnitTests.Features.Account;

public class UserNotificationServiceTests
{
    private sealed class InMemoryNotificationRepository : IUserNotificationRepository
    {
        public readonly List<UserNotification> Notifications = [];
        public readonly List<UserGameAlertSubscription> Subscriptions = [];
        public readonly List<UserPushSubscription> PushSubscriptions = [];

        public Task<IReadOnlyList<UserNotification>> GetByUserIdAsync(string userId, int limit = 100, CancellationToken ct = default)
        {
            var q = Notifications.Where(n => n.UserId == userId);
            return Task.FromResult<IReadOnlyList<UserNotification>>(q.OrderByDescending(n => n.CreatedAtUtc).Take(limit).ToList());
        }

        public Task<int> GetUnreadCountAsync(string userId, CancellationToken ct = default)
            => Task.FromResult(Notifications.Count(n => n.UserId == userId && !n.IsRead));

        public Task<UserNotification?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Notifications.FirstOrDefault(n => n.Id == id));

        public Task AddNotificationAsync(UserNotification notification, CancellationToken ct = default)
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }

        public Task AddNotificationsBatchAsync(IReadOnlyList<UserNotification> notifications, CancellationToken ct = default)
        {
            Notifications.AddRange(notifications);
            return Task.CompletedTask;
        }

        public Task<bool> MarkAsReadAsync(Guid id, string userId, CancellationToken ct = default)
        {
            var item = Notifications.FirstOrDefault(n => n.Id == id && n.UserId == userId);
            if (item == null) return Task.FromResult(false);
            item.MarkAsRead();
            return Task.FromResult(true);
        }

        public Task<int> MarkAllAsReadAsync(string userId, CancellationToken ct = default)
        {
            int count = 0;
            foreach (var n in Notifications.Where(n => n.UserId == userId && !n.IsRead))
            {
                n.MarkAsRead();
                count++;
            }
            return Task.FromResult(count);
        }

        public Task<IReadOnlyList<UserGameAlertSubscription>> GetUserGameAlertsAsync(string userId, Guid? gameId = null, CancellationToken ct = default)
        {
            var q = Subscriptions.Where(s => s.UserId == userId);
            if (gameId.HasValue && gameId.Value != Guid.Empty)
            {
                q = q.Where(s => s.GameId == gameId.Value);
            }
            return Task.FromResult<IReadOnlyList<UserGameAlertSubscription>>(q.ToList());
        }

        public Task<IReadOnlyList<UserGameAlertSubscription>> GetSubscribersForGameAlertAsync(Guid gameId, GameAlertType alertType, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<UserGameAlertSubscription>>(Subscriptions.Where(s => s.GameId == gameId && s.AlertType == alertType).ToList());

        public Task<UserGameAlertSubscription?> GetGameAlertAsync(string userId, Guid gameId, GameAlertType alertType, CancellationToken ct = default)
            => Task.FromResult(Subscriptions.FirstOrDefault(s => s.UserId == userId && s.GameId == gameId && s.AlertType == alertType));

        public Task UpsertGameAlertAsync(UserGameAlertSubscription subscription, CancellationToken ct = default)
        {
            var existing = Subscriptions.FirstOrDefault(s => s.Id == subscription.Id);
            if (existing == null)
            {
                Subscriptions.Add(subscription);
            }
            return Task.CompletedTask;
        }

        public Task<bool> RemoveGameAlertAsync(string userId, Guid gameId, GameAlertType alertType, CancellationToken ct = default)
        {
            var removed = Subscriptions.RemoveAll(s => s.UserId == userId && s.GameId == gameId && s.AlertType == alertType);
            return Task.FromResult(removed > 0);
        }

        public Task<IReadOnlyList<UserPushSubscription>> GetPushSubscriptionsByUserIdAsync(string userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<UserPushSubscription>>(PushSubscriptions.Where(p => p.UserId == userId).ToList());

        public Task<IReadOnlyList<UserPushSubscription>> GetPushSubscriptionsByUserIdsAsync(IReadOnlyCollection<string> userIds, CancellationToken ct = default)
        {
            var set = userIds.ToHashSet();
            return Task.FromResult<IReadOnlyList<UserPushSubscription>>(PushSubscriptions.Where(p => set.Contains(p.UserId)).ToList());
        }

        public Task UpsertPushSubscriptionAsync(UserPushSubscription subscription, CancellationToken ct = default)
        {
            PushSubscriptions.RemoveAll(p => p.Endpoint == subscription.Endpoint);
            PushSubscriptions.Add(subscription);
            return Task.CompletedTask;
        }

        public Task<bool> RemovePushSubscriptionAsync(string userId, string? endpoint = null, CancellationToken ct = default)
        {
            int removed = string.IsNullOrWhiteSpace(endpoint)
                ? PushSubscriptions.RemoveAll(p => p.UserId == userId)
                : PushSubscriptions.RemoveAll(p => p.UserId == userId && p.Endpoint == endpoint);
            return Task.FromResult(removed > 0);
        }
    }

    private sealed class FakeGameRepository : IGameRepository
    {
        private readonly Dictionary<Guid, Game> _games = new();

        public void Seed(Game game) => _games[game.Id] = game;

        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_games.TryGetValue(id, out var g) ? g : null);

        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult<Game?>(null);
        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default) => Task.FromResult<Game?>(null);
        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default)
            => Task.FromResult<(IReadOnlyList<Game>, int)>((_games.Values.ToList(), _games.Count));
        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default)
        {
            foreach (var g in games) _games[g.Id] = g;
            return Task.CompletedTask;
        }
        public Task UpdateAsync(Game game, CancellationToken ct = default) { _games[game.Id] = game; return Task.CompletedTask; }
        public Task<bool> HasAnyAsync(CancellationToken ct = default) => Task.FromResult(_games.Count > 0);
    }

    private sealed class FakePreferenceService : IUserPreferenceService
    {
        public List<string> GiveawaySubscribers { get; set; } = [];

        public Task<UserPreferenceDto> GetUserPreferenceAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult(new UserPreferenceDto(userId, "charcoal", DateTime.UtcNow, "España", false, false, false, null, GiveawaySubscribers.Contains(userId)));
        public Task<string> GetUserThemeAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult("charcoal");
        public Task SetUserThemeAsync(string userId, string themeKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string?> GetUserCountryAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult<string?>("España");
        public Task SetUserCountryAsync(string userId, string? country, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> IsPublicProfileHiddenAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task SetPublicProfileHiddenAsync(string userId, bool hidePublicProfile, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetLeaderboardPreferencesAsync(string userId, bool optIn, bool anonymous, string? customPseudonym = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetNotifyNewGiveawaysAsync(string userId, bool notifyNewGiveaways, CancellationToken cancellationToken = default)
        {
            if (notifyNewGiveaways && !GiveawaySubscribers.Contains(userId)) GiveawaySubscribers.Add(userId);
            else if (!notifyNewGiveaways) GiveawaySubscribers.Remove(userId);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<string>> GetUserIdsSubscribedToGiveawaysAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(GiveawaySubscribers);
    }

    private sealed class FakeWebPushSender : IWebPushNotificationSender
    {
        public int SentCount { get; private set; }
        public string GetVapidPublicKey() => "BEl62iUYgUivxIkv69yViEuiBIa-Ib9-SkvMeAtA3LFgDzkrxZJjSgSnfckjBJuBkr3qBUYIHBQFLXYp5Nksh8U";

        public Task SendNotificationAsync(UserPushSubscription subscription, string title, string message, string targetUrl, string? imageUrl = null, CancellationToken ct = default)
        {
            SentCount++;
            return Task.CompletedTask;
        }

        public Task SendNotificationsBatchAsync(IReadOnlyCollection<UserPushSubscription> subscriptions, string title, string message, string targetUrl, string? imageUrl = null, CancellationToken ct = default)
        {
            SentCount += subscriptions.Count;
            return Task.CompletedTask;
        }
    }

    private static Game CreateGameWithBasePvp(decimal pvp = 50.00m)
    {
        return new Game(
            bggId: 266192,
            originalTitle: "Wingspan",
            spanishTitle: "Wingspan",
            designer: "Elizabeth Hargrave",
            publisher: "Maldito Games",
            yearPublished: 2019,
            coverImageUrl: "https://example.com/wingspan.jpg",
            thumbnailUrl: "https://example.com/wingspan-thumb.jpg",
            description: "Juego de aves.",
            bggRating: 8.1,
            bggRank: 28,
            ludistRating: 8.4,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: true,
            age: new AgeRating(10, 10),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(40, 70, 15),
            purchaseLinks: [new GamePurchaseLink("Zacatrus", "https://zacatrus.es/wingspan", pvp, "€", true)]);
    }

    [Fact]
    public async Task EvaluatePriceAndStockAlertsAsync_WhenDiscountIsBelow15Percent_DoesNotNotify()
    {
        var repo = new InMemoryNotificationRepository();
        var gameRepo = new FakeGameRepository();
        var prefService = new FakePreferenceService();
        var pushSender = new FakeWebPushSender();

        var game = CreateGameWithBasePvp(50.00m);
        gameRepo.Seed(game);

        var service = new UserNotificationService(repo, gameRepo, prefService, pushSender);
        await service.SubscribeGameAlertAsync("user-1", game.Id, GameAlertType.PriceDrop);

        // 10% de descuento (45,00 € sobre 50,00 € PVP base) -> NO debe disparar aviso
        await service.EvaluatePriceAndStockAlertsAsync(game.Id, "Zacatrus", 45.00m, true, "€");

        Assert.Empty(repo.Notifications);
    }

    [Fact]
    public async Task EvaluatePriceAndStockAlertsAsync_WhenDiscountIsAtLeast15Percent_CreatesUnreadNotificationAndSendsPush()
    {
        var repo = new InMemoryNotificationRepository();
        var gameRepo = new FakeGameRepository();
        var prefService = new FakePreferenceService();
        var pushSender = new FakeWebPushSender();

        var game = CreateGameWithBasePvp(50.00m);
        gameRepo.Seed(game);

        var service = new UserNotificationService(repo, gameRepo, prefService, pushSender);
        await service.SubscribeGameAlertAsync("user-1", game.Id, GameAlertType.PriceDrop);
        await service.RegisterPushSubscriptionAsync("user-1", new WebPushSubscriptionRequestDto("https://push.example/1", "p256dh-key", "auth-key", "PWA Mobile"));

        // 20% de descuento (40,00 € sobre 50,00 € PVP base) -> SÍ debe disparar aviso (>= 15%)
        await service.EvaluatePriceAndStockAlertsAsync(game.Id, "Zacatrus", 40.00m, true, "€");

        Assert.Single(repo.Notifications);
        var notification = repo.Notifications[0];
        Assert.False(notification.IsRead);
        Assert.Equal(UserNotificationType.PriceDrop, notification.Type);
        Assert.Contains("-20%", notification.Title);
        Assert.Equal(1, pushSender.SentCount);

        // No debe duplicar si se vuelve a observar el mismo precio
        await service.EvaluatePriceAndStockAlertsAsync(game.Id, "Zacatrus", 40.00m, true, "€");
        Assert.Single(repo.Notifications);
    }

    [Fact]
    public async Task DispatchReprintAndRestockAndGiveawayNotifications_CreateUnreadNotificationsUntilExplicitClick()
    {
        var repo = new InMemoryNotificationRepository();
        var gameRepo = new FakeGameRepository();
        var prefService = new FakePreferenceService { GiveawaySubscribers = ["user-1"] };
        var pushSender = new FakeWebPushSender();

        var game = CreateGameWithBasePvp(60.00m);
        gameRepo.Seed(game);

        var service = new UserNotificationService(repo, gameRepo, prefService, pushSender);
        await service.SubscribeGameAlertAsync("user-1", game.Id, GameAlertType.Reprint);
        await service.SubscribeGameAlertAsync("user-1", game.Id, GameAlertType.Restock);

        // 1. Reimpresión
        await service.NotifyReprintDetectedAsync(game.Id, "Wingspan", "Maldito Games", new DateOnly(2026, 11, 15));

        // 2. Reestock
        await service.EvaluatePriceAndStockAlertsAsync(game.Id, "Dracotienda", 54.95m, true, "€");

        // 3. Sorteo
        await service.NotifyNewGiveawayPublishedAsync(Guid.NewGuid(), "Sorteo Aniversario", "Maldito Games");

        // Consultar notificaciones NO las marca como leídas automáticamente
        var list = await service.GetUserNotificationsAsync("user-1");
        Assert.Equal(3, list.Count);
        Assert.All(list, n => Assert.False(n.IsRead));
        Assert.Equal(3, await service.GetUnreadCountAsync("user-1"));

        // Al pulsar explícitamente en una notificación, solo esa pasa a leída
        await service.MarkAsReadAsync(list[0].Id, "user-1");
        Assert.Equal(2, await service.GetUnreadCountAsync("user-1"));
    }
}
