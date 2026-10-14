using System;
using Ludeka.Core.Enums;

namespace Ludeka.Core.Entities;

/// <summary>
/// Suscripción de un usuario a un tipo específico de alerta sobre un juego del catálogo (INC-149):
/// bajada de precio (>= 15% sobre PVP base), reimpresión o reestock.
/// </summary>
public class UserGameAlertSubscription
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public Guid GameId { get; private set; }
    public Game? Game { get; private set; }
    public GameAlertType AlertType { get; private set; }
    public decimal? ReferenceBasePvp { get; private set; }
    public decimal? LastNotifiedPrice { get; private set; }
    public DateTime? LastNotifiedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    protected UserGameAlertSubscription()
    {
    }

    public UserGameAlertSubscription(
        string userId,
        Guid gameId,
        GameAlertType alertType,
        decimal? referenceBasePvp = null,
        DateTime? createdAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("El identificador de usuario no puede estar vacío.", nameof(userId));
        if (gameId == Guid.Empty)
            throw new ArgumentException("El identificador del juego no puede ser vacío.", nameof(gameId));

        Id = Guid.NewGuid();
        UserId = userId.Trim();
        GameId = gameId;
        AlertType = alertType;
        ReferenceBasePvp = referenceBasePvp is > 0 ? referenceBasePvp : null;
        CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow;
    }

    public void UpdateReferenceBasePvp(decimal? referenceBasePvp)
    {
        if (referenceBasePvp is > 0)
        {
            ReferenceBasePvp = referenceBasePvp;
        }
    }

    public void RecordNotificationSent(decimal? notifiedPrice = null, DateTime? notifiedAtUtc = null)
    {
        LastNotifiedPrice = notifiedPrice;
        LastNotifiedAtUtc = notifiedAtUtc ?? DateTime.UtcNow;
    }
}
