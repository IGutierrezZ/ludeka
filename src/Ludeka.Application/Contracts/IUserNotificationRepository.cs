using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Repositorio de persistencia para avisos personales, suscripciones de alertas de juegos y Web Push (INC-149).
/// </summary>
public interface IUserNotificationRepository
{
    Task<IReadOnlyList<UserNotification>> GetByUserIdAsync(string userId, int limit = 100, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(string userId, CancellationToken ct = default);
    Task<UserNotification?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddNotificationAsync(UserNotification notification, CancellationToken ct = default);
    Task AddNotificationsBatchAsync(IReadOnlyList<UserNotification> notifications, CancellationToken ct = default);
    Task<bool> MarkAsReadAsync(Guid id, string userId, CancellationToken ct = default);
    Task<int> MarkAllAsReadAsync(string userId, CancellationToken ct = default);

    Task<IReadOnlyList<UserGameAlertSubscription>> GetUserGameAlertsAsync(string userId, Guid? gameId = null, CancellationToken ct = default);
    Task<IReadOnlyList<UserGameAlertSubscription>> GetSubscribersForGameAlertAsync(Guid gameId, GameAlertType alertType, CancellationToken ct = default);
    Task<UserGameAlertSubscription?> GetGameAlertAsync(string userId, Guid gameId, GameAlertType alertType, CancellationToken ct = default);
    Task UpsertGameAlertAsync(UserGameAlertSubscription subscription, CancellationToken ct = default);
    Task<bool> RemoveGameAlertAsync(string userId, Guid gameId, GameAlertType alertType, CancellationToken ct = default);

    Task<IReadOnlyList<UserPushSubscription>> GetPushSubscriptionsByUserIdAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<UserPushSubscription>> GetPushSubscriptionsByUserIdsAsync(IReadOnlyCollection<string> userIds, CancellationToken ct = default);
    Task UpsertPushSubscriptionAsync(UserPushSubscription subscription, CancellationToken ct = default);
    Task<bool> RemovePushSubscriptionAsync(string userId, string? endpoint = null, CancellationToken ct = default);
}
