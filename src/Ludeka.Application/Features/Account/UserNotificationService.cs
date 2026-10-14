using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Features.Account;

/// <summary>
/// Implementación del centro de notificaciones de usuario, suscripciones a alertas de juegos (bajada de precio >= 15%,
/// reimpresión, reestock), avisos de sorteos y despacho Web Push (INC-149).
/// </summary>
public class UserNotificationService : IUserNotificationService
{
    public const decimal MinimumPriceDropRatio = 0.15m; // Al menos 15% de descuento sobre el PVP base

    private readonly IUserNotificationRepository _repository;
    private readonly IGameRepository _gameRepository;
    private readonly IUserPreferenceService _preferenceService;
    private readonly IWebPushNotificationSender? _webPushSender;

    public event Action? OnNotificationsChanged;

    public UserNotificationService(
        IUserNotificationRepository repository,
        IGameRepository gameRepository,
        IUserPreferenceService preferenceService,
        IWebPushNotificationSender? webPushSender = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _preferenceService = preferenceService ?? throw new ArgumentNullException(nameof(preferenceService));
        _webPushSender = webPushSender;
    }

    public async Task<IReadOnlyList<UserNotificationDto>> GetUserNotificationsAsync(string userId, int limit = 100, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return [];

        var items = await _repository.GetByUserIdAsync(userId.Trim(), limit, ct);
        return items.Select(MapToDto).ToList();
    }

    public async Task<int> GetUnreadCountAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return 0;

        return await _repository.GetUnreadCountAsync(userId.Trim(), ct);
    }

    public async Task<bool> MarkAsReadAsync(Guid notificationId, string userId, CancellationToken ct = default)
    {
        if (notificationId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
            return false;

        var updated = await _repository.MarkAsReadAsync(notificationId, userId.Trim(), ct);
        if (updated)
        {
            OnNotificationsChanged?.Invoke();
        }
        return updated;
    }

    public async Task<int> MarkAllAsReadAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return 0;

        var count = await _repository.MarkAllAsReadAsync(userId.Trim(), ct);
        if (count > 0)
        {
            OnNotificationsChanged?.Invoke();
        }
        return count;
    }

    public async Task<IReadOnlyList<UserGameAlertSubscriptionDto>> GetUserGameAlertsAsync(string userId, Guid? gameId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return [];

        var subscriptions = await _repository.GetUserGameAlertsAsync(userId.Trim(), gameId, ct);
        var result = new List<UserGameAlertSubscriptionDto>(subscriptions.Count);

        foreach (var sub in subscriptions)
        {
            var game = sub.Game ?? await _gameRepository.GetByIdAsync(sub.GameId, ct);
            var title = game?.SpanishTitle ?? game?.OriginalTitle ?? "Juego del catálogo";
            var slug = game?.Slug ?? string.Empty;
            var cover = game?.CoverImageUrl;

            result.Add(new UserGameAlertSubscriptionDto(
                sub.Id,
                sub.UserId,
                sub.GameId,
                title,
                slug,
                cover,
                sub.AlertType,
                sub.ReferenceBasePvp,
                sub.LastNotifiedPrice,
                sub.LastNotifiedAtUtc,
                sub.CreatedAtUtc));
        }

        return result;
    }

    public async Task<bool> IsSubscribedToGameAlertAsync(string userId, Guid gameId, GameAlertType alertType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || gameId == Guid.Empty)
            return false;

        var sub = await _repository.GetGameAlertAsync(userId.Trim(), gameId, alertType, ct);
        return sub is not null;
    }

    public async Task SubscribeGameAlertAsync(string userId, Guid gameId, GameAlertType alertType, decimal? referenceBasePvp = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("Se requiere un usuario autenticado para activar alertas.", nameof(userId));
        if (gameId == Guid.Empty)
            throw new ArgumentException("El identificador del juego no es válido.", nameof(gameId));

        decimal? effectiveBasePvp = referenceBasePvp;
        if (!effectiveBasePvp.HasValue || effectiveBasePvp.Value <= 0)
        {
            var game = await _gameRepository.GetByIdAsync(gameId, ct);
            effectiveBasePvp = ResolveEffectiveBasePvp(game, null);
        }

        var subscription = new UserGameAlertSubscription(userId.Trim(), gameId, alertType, effectiveBasePvp);
        await _repository.UpsertGameAlertAsync(subscription, ct);
    }

    public async Task<bool> UnsubscribeGameAlertAsync(string userId, Guid gameId, GameAlertType alertType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || gameId == Guid.Empty)
            return false;

        return await _repository.RemoveGameAlertAsync(userId.Trim(), gameId, alertType, ct);
    }

    public async Task<bool> ToggleGameAlertAsync(string userId, Guid gameId, GameAlertType alertType, decimal? referenceBasePvp = null, CancellationToken ct = default)
    {
        if (await IsSubscribedToGameAlertAsync(userId, gameId, alertType, ct))
        {
            await UnsubscribeGameAlertAsync(userId, gameId, alertType, ct);
            return false;
        }

        await SubscribeGameAlertAsync(userId, gameId, alertType, referenceBasePvp, ct);
        return true;
    }

    public string GetVapidPublicKey() => _webPushSender?.GetVapidPublicKey() ?? string.Empty;

    public async Task<bool> HasActivePushSubscriptionAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        var subs = await _repository.GetPushSubscriptionsByUserIdAsync(userId.Trim(), ct);
        return subs.Count > 0;
    }

    public async Task RegisterPushSubscriptionAsync(string userId, WebPushSubscriptionRequestDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("Se requiere iniciar sesión para activar notificaciones en el dispositivo.", nameof(userId));
        ArgumentNullException.ThrowIfNull(request);

        var entity = new UserPushSubscription(
            userId.Trim(),
            request.Endpoint,
            request.P256dh,
            request.Auth,
            request.UserAgent);

        await _repository.UpsertPushSubscriptionAsync(entity, ct);
    }

    public async Task<bool> UnregisterPushSubscriptionAsync(string userId, string? endpoint = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        return await _repository.RemovePushSubscriptionAsync(userId.Trim(), endpoint, ct);
    }

    public async Task EvaluatePriceAndStockAlertsAsync(
        Guid gameId,
        string storeName,
        decimal price,
        bool inStock,
        string currency = "€",
        decimal? referenceBasePvp = null,
        CancellationToken ct = default)
    {
        if (gameId == Guid.Empty || !inStock || price <= 0)
            return;

        var game = await _gameRepository.GetByIdAsync(gameId, ct);
        var gameTitle = game?.SpanishTitle ?? game?.OriginalTitle ?? "Juego seguido";
        var targetUrl = game != null && !string.IsNullOrWhiteSpace(game.Slug)
            ? $"/juegos/{game.Slug}"
            : "/explorar";
        var coverUrl = game?.CoverImageUrl;

        var notificationsToInsert = new List<UserNotification>();
        var notifiedUserIds = new HashSet<string>(StringComparer.Ordinal);

        // 1. Evaluar alertas de Reestock (cuando entra stock de este juego)
        var restockSubscribers = await _repository.GetSubscribersForGameAlertAsync(gameId, GameAlertType.Restock, ct);
        foreach (var sub in restockSubscribers)
        {
            // Evitar re-notificar reestock en las últimas 24h al mismo usuario para el mismo juego
            if (sub.LastNotifiedAtUtc.HasValue && (DateTime.UtcNow - sub.LastNotifiedAtUtc.Value).TotalHours < 24)
                continue;

            var priceFormatted = price.ToString("0.00", CultureInfo.GetCultureInfo("es-ES"));
            var title = $"Reestock disponible: {gameTitle}";
            var message = $"Ya hay unidades en stock de «{gameTitle}» en {storeName} por {priceFormatted} {currency}.";

            notificationsToInsert.Add(new UserNotification(
                sub.UserId,
                UserNotificationType.Restock,
                title,
                message,
                targetUrl,
                gameId,
                coverUrl));

            sub.RecordNotificationSent(price, DateTime.UtcNow);
            await _repository.UpsertGameAlertAsync(sub, ct);
            notifiedUserIds.Add(sub.UserId);
        }

        // 2. Evaluar alertas de Bajada de Precio (al menos 15% sobre el PVP base)
        var priceDropSubscribers = await _repository.GetSubscribersForGameAlertAsync(gameId, GameAlertType.PriceDrop, ct);
        decimal? catalogBasePvp = ResolveEffectiveBasePvp(game, referenceBasePvp);

        foreach (var sub in priceDropSubscribers)
        {
            var effectiveBasePvp = sub.ReferenceBasePvp is > 0
                ? (catalogBasePvp.HasValue ? Math.Max(sub.ReferenceBasePvp.Value, catalogBasePvp.Value) : sub.ReferenceBasePvp.Value)
                : catalogBasePvp;

            if (!effectiveBasePvp.HasValue || effectiveBasePvp.Value <= 0)
                continue;

            decimal maxAllowedPrice = Math.Round(effectiveBasePvp.Value * (1m - MinimumPriceDropRatio), 2);
            if (price > maxAllowedPrice)
                continue;

            // Si ya le notificamos este mismo precio o uno más bajo, no repetir aviso
            if (sub.LastNotifiedPrice.HasValue && price >= sub.LastNotifiedPrice.Value)
                continue;

            var discountPercent = (int)Math.Round((1m - (price / effectiveBasePvp.Value)) * 100m, 0);
            var priceFormatted = price.ToString("0.00", CultureInfo.GetCultureInfo("es-ES"));
            var baseFormatted = effectiveBasePvp.Value.ToString("0.00", CultureInfo.GetCultureInfo("es-ES"));

            var title = $"Oferta (-{discountPercent}%): {gameTitle}";
            var message = $"«{gameTitle}» ha bajado a {priceFormatted} {currency} en {storeName} ({discountPercent}% sobre su PVP base de {baseFormatted} {currency}).";

            notificationsToInsert.Add(new UserNotification(
                sub.UserId,
                UserNotificationType.PriceDrop,
                title,
                message,
                targetUrl,
                gameId,
                coverUrl));

            sub.RecordNotificationSent(price, DateTime.UtcNow);
            await _repository.UpsertGameAlertAsync(sub, ct);
            notifiedUserIds.Add(sub.UserId);
        }

        if (notificationsToInsert.Count > 0)
        {
            await _repository.AddNotificationsBatchAsync(notificationsToInsert, ct);
            OnNotificationsChanged?.Invoke();
            await DispatchWebPushForNotificationsAsync(notificationsToInsert, ct);
        }
    }

    public async Task NotifyReprintDetectedAsync(
        Guid gameId,
        string releaseTitle,
        string publisher,
        DateOnly? releaseDate = null,
        CancellationToken ct = default)
    {
        if (gameId == Guid.Empty)
            return;

        var subscribers = await _repository.GetSubscribersForGameAlertAsync(gameId, GameAlertType.Reprint, ct);
        if (subscribers.Count == 0)
            return;

        var game = await _gameRepository.GetByIdAsync(gameId, ct);
        var gameTitle = game?.SpanishTitle ?? game?.OriginalTitle ?? releaseTitle;
        var targetUrl = game != null && !string.IsNullOrWhiteSpace(game.Slug)
            ? $"/juegos/{game.Slug}"
            : "/novedades";
        var coverUrl = game?.CoverImageUrl;
        var dateSuffix = releaseDate.HasValue
            ? $" (fecha estimada: {releaseDate.Value:dd/MM/yyyy})"
            : string.Empty;

        var notifications = new List<UserNotification>();
        foreach (var sub in subscribers)
        {
            if (sub.LastNotifiedAtUtc.HasValue && (DateTime.UtcNow - sub.LastNotifiedAtUtc.Value).TotalHours < 24)
                continue;

            var title = $"Reimpresión anunciada: {gameTitle}";
            var message = $"{publisher} ha confirmado una reimpresión de «{gameTitle}»{dateSuffix}.";

            notifications.Add(new UserNotification(
                sub.UserId,
                UserNotificationType.Reprint,
                title,
                message,
                targetUrl,
                gameId,
                coverUrl));

            sub.RecordNotificationSent(null, DateTime.UtcNow);
            await _repository.UpsertGameAlertAsync(sub, ct);
        }

        if (notifications.Count > 0)
        {
            await _repository.AddNotificationsBatchAsync(notifications, ct);
            OnNotificationsChanged?.Invoke();
            await DispatchWebPushForNotificationsAsync(notifications, ct);
        }
    }

    public async Task NotifyNewGiveawayPublishedAsync(
        Guid giveawayId,
        string giveawayTitle,
        string organizer,
        string? thumbnailUrl = null,
        CancellationToken ct = default)
    {
        var userIds = await _preferenceService.GetUserIdsSubscribedToGiveawaysAsync(ct);
        if (userIds.Count == 0)
            return;

        var title = $"Nuevo sorteo: {giveawayTitle}";
        var message = $"Se ha publicado un nuevo sorteo organizado por {organizer}. ¡Revisa las bases y participa!";
        var targetUrl = giveawayId != Guid.Empty ? $"/sorteos/{giveawayId}" : "/sorteos";

        var notifications = userIds
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct(StringComparer.Ordinal)
            .Select(userId => new UserNotification(
                userId,
                UserNotificationType.NewGiveaway,
                title,
                message,
                targetUrl,
                 null,
                thumbnailUrl))
            .ToList();

        if (notifications.Count > 0)
        {
            await _repository.AddNotificationsBatchAsync(notifications, ct);
            OnNotificationsChanged?.Invoke();
            await DispatchWebPushForNotificationsAsync(notifications, ct);
        }
    }

    public static decimal? ResolveEffectiveBasePvp(Game? game, decimal? explicitReferencePvp)
    {
        decimal maxCandidate = explicitReferencePvp is > 0 ? explicitReferencePvp.Value : 0m;

        if (game?.PurchaseLinks != null && game.PurchaseLinks.Count > 0)
        {
            var maxOfferPrice = game.PurchaseLinks
                .Where(o => o.Price.HasValue && o.Price.Value > 0)
                .Select(o => o.Price!.Value)
                .DefaultIfEmpty(0m)
                .Max();

            if (maxOfferPrice > maxCandidate)
            {
                maxCandidate = maxOfferPrice;
            }
        }

        return maxCandidate > 0 ? maxCandidate : null;
    }

    private async Task DispatchWebPushForNotificationsAsync(IReadOnlyList<UserNotification> notifications, CancellationToken ct)
    {
        if (_webPushSender is null || notifications.Count == 0)
            return;

        var userIds = notifications.Select(n => n.UserId).Distinct(StringComparer.Ordinal).ToList();
        var pushSubs = await _repository.GetPushSubscriptionsByUserIdsAsync(userIds, ct);
        if (pushSubs.Count == 0)
            return;

        var subsByUser = pushSubs
            .GroupBy(s => s.UserId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<UserPushSubscription>)g.ToList(), StringComparer.Ordinal);

        foreach (var notification in notifications)
        {
            if (subsByUser.TryGetValue(notification.UserId, out var userSubs) && userSubs.Count > 0)
            {
                await _webPushSender.SendNotificationsBatchAsync(
                    userSubs,
                    notification.Title,
                    notification.Message,
                    notification.TargetUrl,
                    notification.ImageUrl,
                    ct);
            }
        }
    }

    private static UserNotificationDto MapToDto(UserNotification n) =>
        new(
            n.Id,
            n.UserId,
            n.Type,
            n.Title,
            n.Message,
            n.TargetUrl,
            n.GameId,
            n.ImageUrl,
            n.IsRead,
            n.CreatedAtUtc,
            n.ReadAtUtc);
}
