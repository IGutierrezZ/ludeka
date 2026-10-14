using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Repositories;

/// <summary>
/// Implementación de persistencia EF Core para notificaciones de usuario, alertas por juego y suscripciones Web Push (INC-149).
/// </summary>
public class SqliteUserNotificationRepository : DbContextRepositoryBase, IUserNotificationRepository
{
    public SqliteUserNotificationRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteUserNotificationRepository(LudekaDbContext db) : base(db)
    {
    }

    public async Task<IReadOnlyList<UserNotification>> GetByUserIdAsync(string userId, int limit = 100, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return [];

        if (limit <= 0)
            limit = 100;

        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.UserNotifications
            .AsNoTracking()
            .Where(n => n.UserId == userId.Trim())
            .OrderBy(n => n.IsRead)
            .ThenByDescending(n => n.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<int> GetUnreadCountAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return 0;

        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.UserNotifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId.Trim() && !n.IsRead, ct);
    }

    public async Task<UserNotification?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            return null;

        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.UserNotifications
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id, ct);
    }

    public async Task AddNotificationAsync(UserNotification notification, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await using var scope = await CreateScopeAsync(ct);
        scope.Context.UserNotifications.Add(notification);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task AddNotificationsBatchAsync(IReadOnlyList<UserNotification> notifications, CancellationToken ct = default)
    {
        if (notifications == null || notifications.Count == 0)
            return;

        await using var scope = await CreateScopeAsync(ct);
        scope.Context.UserNotifications.AddRange(notifications);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task<bool> MarkAsReadAsync(Guid id, string userId, CancellationToken ct = default)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(userId))
            return false;

        await using var scope = await CreateScopeAsync(ct);
        var entity = await scope.Context.UserNotifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId.Trim(), ct);

        if (entity is null || entity.IsRead)
            return false;

        entity.MarkAsRead(DateTime.UtcNow);
        await scope.Context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> MarkAllAsReadAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return 0;

        await using var scope = await CreateScopeAsync(ct);
        var unread = await scope.Context.UserNotifications
            .Where(n => n.UserId == userId.Trim() && !n.IsRead)
            .ToListAsync(ct);

        if (unread.Count == 0)
            return 0;

        var now = DateTime.UtcNow;
        foreach (var item in unread)
        {
            item.MarkAsRead(now);
        }

        await scope.Context.SaveChangesAsync(ct);
        return unread.Count;
    }

    public async Task<IReadOnlyList<UserGameAlertSubscription>> GetUserGameAlertsAsync(string userId, Guid? gameId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return [];

        await using var scope = await CreateScopeAsync(ct);
        var query = scope.Context.UserGameAlertSubscriptions
            .AsNoTracking()
            .Include(s => s.Game)
            .Where(s => s.UserId == userId.Trim());

        if (gameId.HasValue && gameId.Value != Guid.Empty)
        {
            query = query.Where(s => s.GameId == gameId.Value);
        }

        return await query
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<UserGameAlertSubscription>> GetSubscribersForGameAlertAsync(Guid gameId, GameAlertType alertType, CancellationToken ct = default)
    {
        if (gameId == Guid.Empty)
            return [];

        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.UserGameAlertSubscriptions
            .AsNoTracking()
            .Where(s => s.GameId == gameId && s.AlertType == alertType)
            .ToListAsync(ct);
    }

    public async Task<UserGameAlertSubscription?> GetGameAlertAsync(string userId, Guid gameId, GameAlertType alertType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || gameId == Guid.Empty)
            return null;

        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.UserGameAlertSubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.UserId == userId.Trim() && s.GameId == gameId && s.AlertType == alertType,
                ct);
    }

    public async Task UpsertGameAlertAsync(UserGameAlertSubscription subscription, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        await using var scope = await CreateScopeAsync(ct);
        var existing = await scope.Context.UserGameAlertSubscriptions
            .FirstOrDefaultAsync(
                s => s.UserId == subscription.UserId &&
                     s.GameId == subscription.GameId &&
                     s.AlertType == subscription.AlertType,
                ct);

        if (existing is null)
        {
            scope.Context.UserGameAlertSubscriptions.Add(subscription);
        }
        else
        {
            existing.UpdateReferenceBasePvp(subscription.ReferenceBasePvp);
            if (subscription.LastNotifiedAtUtc.HasValue)
            {
                existing.RecordNotificationSent(subscription.LastNotifiedPrice, subscription.LastNotifiedAtUtc);
            }
        }

        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task<bool> RemoveGameAlertAsync(string userId, Guid gameId, GameAlertType alertType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || gameId == Guid.Empty)
            return false;

        await using var scope = await CreateScopeAsync(ct);
        var existing = await scope.Context.UserGameAlertSubscriptions
            .FirstOrDefaultAsync(
                s => s.UserId == userId.Trim() && s.GameId == gameId && s.AlertType == alertType,
                ct);

        if (existing is null)
            return false;

        scope.Context.UserGameAlertSubscriptions.Remove(existing);
        await scope.Context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<UserPushSubscription>> GetPushSubscriptionsByUserIdAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return [];

        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.UserPushSubscriptions
            .AsNoTracking()
            .Where(s => s.UserId == userId.Trim())
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<UserPushSubscription>> GetPushSubscriptionsByUserIdsAsync(IReadOnlyCollection<string> userIds, CancellationToken ct = default)
    {
        if (userIds == null || userIds.Count == 0)
            return [];

        var cleanIds = userIds.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).Distinct().ToList();
        if (cleanIds.Count == 0)
            return [];

        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.UserPushSubscriptions
            .AsNoTracking()
            .Where(s => cleanIds.Contains(s.UserId))
            .ToListAsync(ct);
    }

    public async Task UpsertPushSubscriptionAsync(UserPushSubscription subscription, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        await using var scope = await CreateScopeAsync(ct);
        var existing = await scope.Context.UserPushSubscriptions
            .FirstOrDefaultAsync(
                s => s.UserId == subscription.UserId && s.Endpoint == subscription.Endpoint,
                ct);

        if (existing is null)
        {
            scope.Context.UserPushSubscriptions.Add(subscription);
        }
        else
        {
            existing.UpdateKeys(subscription.P256dh, subscription.Auth, subscription.UserAgent);
        }

        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task<bool> RemovePushSubscriptionAsync(string userId, string? endpoint = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        await using var scope = await CreateScopeAsync(ct);
        var query = scope.Context.UserPushSubscriptions
            .Where(s => s.UserId == userId.Trim());

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            query = query.Where(s => s.Endpoint == endpoint.Trim());
        }

        var items = await query.ToListAsync(ct);
        if (items.Count == 0)
            return false;

        scope.Context.UserPushSubscriptions.RemoveRange(items);
        await scope.Context.SaveChangesAsync(ct);
        return true;
    }
}
