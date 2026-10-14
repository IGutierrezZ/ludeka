using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Servicio de aplicación para el centro de avisos del usuario, gestión de alertas por juego y sorteos,
/// y disparo de avisos internos y Web Push (INC-149).
/// </summary>
public interface IUserNotificationService
{
    /// <summary>
    /// Evento disparado cuando cambia el estado de lectura o se generan nuevas notificaciones,
    /// permitiendo actualizar reactivamente el contador en <c>AccountMenu.razor</c>.
    /// </summary>
    event Action? OnNotificationsChanged;

    Task<IReadOnlyList<UserNotificationDto>> GetUserNotificationsAsync(string userId, int limit = 100, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(string userId, CancellationToken ct = default);
    Task<bool> MarkAsReadAsync(Guid notificationId, string userId, CancellationToken ct = default);
    Task<int> MarkAllAsReadAsync(string userId, CancellationToken ct = default);

    Task<IReadOnlyList<UserGameAlertSubscriptionDto>> GetUserGameAlertsAsync(string userId, Guid? gameId = null, CancellationToken ct = default);
    Task<bool> IsSubscribedToGameAlertAsync(string userId, Guid gameId, GameAlertType alertType, CancellationToken ct = default);
    Task SubscribeGameAlertAsync(string userId, Guid gameId, GameAlertType alertType, decimal? referenceBasePvp = null, CancellationToken ct = default);
    Task<bool> UnsubscribeGameAlertAsync(string userId, Guid gameId, GameAlertType alertType, CancellationToken ct = default);
    Task<bool> ToggleGameAlertAsync(string userId, Guid gameId, GameAlertType alertType, decimal? referenceBasePvp = null, CancellationToken ct = default);

    string GetVapidPublicKey();
    Task<bool> HasActivePushSubscriptionAsync(string userId, CancellationToken ct = default);
    Task RegisterPushSubscriptionAsync(string userId, WebPushSubscriptionRequestDto request, CancellationToken ct = default);
    Task<bool> UnregisterPushSubscriptionAsync(string userId, string? endpoint = null, CancellationToken ct = default);

    /// <summary>
    /// Evalúa una observación de precio/stock de un juego y dispara las alertas de bajada de precio
    /// (&gt;= 15% sobre PVP base) y/o reestock para los usuarios suscritos.
    /// </summary>
    Task EvaluatePriceAndStockAlertsAsync(
        Guid gameId,
        string storeName,
        decimal price,
        bool inStock,
        string currency = "€",
        decimal? referenceBasePvp = null,
        CancellationToken ct = default);

    /// <summary>
    /// Dispara las alertas de reimpresión para todos los usuarios suscritos al juego indicado.
    /// </summary>
    Task NotifyReprintDetectedAsync(
        Guid gameId,
        string releaseTitle,
        string publisher,
        DateOnly? releaseDate = null,
        CancellationToken ct = default);

    /// <summary>
    /// Dispara las alertas de nuevo sorteo para todos los usuarios que activaron el aviso de sorteos en su panel.
    /// </summary>
    Task NotifyNewGiveawayPublishedAsync(
        Guid giveawayId,
        string giveawayTitle,
        string organizer,
        string? thumbnailUrl = null,
        CancellationToken ct = default);
}
