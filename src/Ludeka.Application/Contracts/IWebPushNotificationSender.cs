using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Entities;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Despachador de notificaciones nativas Web Push (W3C Push API + VAPID) para navegador y PWA móvil (INC-149).
/// </summary>
public interface IWebPushNotificationSender
{
    string GetVapidPublicKey();

    Task SendNotificationAsync(
        UserPushSubscription subscription,
        string title,
        string message,
        string targetUrl,
        string? imageUrl = null,
        CancellationToken ct = default);

    Task SendNotificationsBatchAsync(
        IReadOnlyCollection<UserPushSubscription> subscriptions,
        string title,
        string message,
        string targetUrl,
        string? imageUrl = null,
        CancellationToken ct = default);
}
