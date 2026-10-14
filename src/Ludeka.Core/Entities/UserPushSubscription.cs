using System;

namespace Ludeka.Core.Entities;

/// <summary>
/// Suscripción Web Push (W3C Push API / VAPID) asociada al navegador o PWA móvil de un usuario (INC-149).
/// </summary>
public class UserPushSubscription
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string Endpoint { get; private set; } = string.Empty;
    public string P256dh { get; private set; } = string.Empty;
    public string Auth { get; private set; } = string.Empty;
    public string? UserAgent { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    protected UserPushSubscription()
    {
    }

    public UserPushSubscription(
        string userId,
        string endpoint,
        string p256dh,
        string auth,
        string? userAgent = null,
        DateTime? createdAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("El identificador de usuario no puede estar vacío.", nameof(userId));
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentException("El endpoint de la suscripción push no puede estar vacío.", nameof(endpoint));
        if (string.IsNullOrWhiteSpace(p256dh))
            throw new ArgumentException("La clave pública P256dh no puede estar vacía.", nameof(p256dh));
        if (string.IsNullOrWhiteSpace(auth))
            throw new ArgumentException("El secreto de autenticación Auth no puede estar vacío.", nameof(auth));

        Id = Guid.NewGuid();
        UserId = userId.Trim();
        Endpoint = endpoint.Trim();
        P256dh = p256dh.Trim();
        Auth = auth.Trim();
        UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent.Trim();
        CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow;
    }

    public void UpdateKeys(string p256dh, string auth, string? userAgent = null)
    {
        if (!string.IsNullOrWhiteSpace(p256dh))
            P256dh = p256dh.Trim();
        if (!string.IsNullOrWhiteSpace(auth))
            Auth = auth.Trim();
        if (!string.IsNullOrWhiteSpace(userAgent))
            UserAgent = userAgent.Trim();
    }
}
