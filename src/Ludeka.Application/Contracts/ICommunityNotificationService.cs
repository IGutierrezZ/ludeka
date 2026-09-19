using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;

namespace Ludeka.Application.Contracts;

public interface ICommunityNotificationService
{
    Task<IReadOnlyList<NotificationDispatchResult>> BroadcastAsync(CommunityNotificationMessage message, CancellationToken ct = default);
    Task<NotificationDispatchResult> SendToDiscordAsync(CommunityNotificationMessage message, CancellationToken ct = default);
    Task<NotificationDispatchResult> SendToTelegramAsync(CommunityNotificationMessage message, CancellationToken ct = default);

    /// <summary>Envía por <c>delivery.Channel</c> y actualiza ESA sub-entrega del outbox
    /// (Attempts, Status, NextAttemptAt); no crea ninguna fila nueva (INC-47, R4b, diseño §6.6).
    /// Exclusivo del despachador del outbox (<c>NotificationOutboxDispatcher</c>).</summary>
    Task<NotificationDispatchResult> DeliverAsync(CommunityNotificationLog delivery, CommunityNotificationMessage message, CancellationToken ct = default);
    Task<IReadOnlyList<CommunityNotificationLogDto>> GetHistoryAsync(int limit = 50, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationChannelStatusDto>> GetChannelStatusesAsync(CancellationToken ct = default);
    Task<NotificationDispatchResult> RetryFailedNotificationAsync(Guid logId, CancellationToken ct = default);

    /// <summary>Disparo manual del panel: exige sesión y permiso de notificaciones (INC-46, W1).</summary>
    Task TriggerExpiringGiveawaysScanAsync(CancellationToken ct = default);

    /// <summary>Ciclo programado del despachador en segundo plano: ruta de sistema sin sesión.</summary>
    Task RunExpiringGiveawaysScanAsync(CancellationToken ct = default);

    /// <summary>Disparo manual del panel: exige sesión y permiso de notificaciones (INC-46, W1).</summary>
    Task TriggerFridayReleasesBulletinAsync(CancellationToken ct = default);

    /// <summary>Ciclo programado del despachador en segundo plano: ruta de sistema sin sesión.</summary>
    Task RunFridayReleasesBulletinAsync(CancellationToken ct = default);

    Task SendTestPingAsync(NotificationChannel? channel = null, CancellationToken ct = default);
}
