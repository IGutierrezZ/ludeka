using System;
using System.Collections.Concurrent;
using Ludeka.Application.Contracts;

namespace Ludeka.Web.Services;

/// <summary>
/// Implementación en memoria de <see cref="IUserSessionInvalidator"/> (INC-46 F3). Registrada como
/// Singleton para que el cambio hecho en un circuito alcance a los demás circuitos del proceso.
/// En un despliegue con varias instancias el aviso no cruza de instancia: la autorización sigue
/// siendo correcta porque el handler de permisos relee el <c>AppUser</c> en cada comprobación.
/// </summary>
public sealed class InMemoryUserSessionInvalidator : IUserSessionInvalidator
{
    private readonly ConcurrentDictionary<string, long> _versions = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public event EventHandler<UserSessionInvalidatedEventArgs>? Invalidated;

    /// <inheritdoc />
    public long GetVersion(string userId)
        => string.IsNullOrWhiteSpace(userId)
            ? 0
            : _versions.TryGetValue(Normalize(userId), out var version) ? version : 0;

    /// <inheritdoc />
    public void Invalidate(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var normalizedUserId = Normalize(userId);
        var version = _versions.AddOrUpdate(normalizedUserId, 1, (_, current) => current + 1);

        Invalidated?.Invoke(this, new UserSessionInvalidatedEventArgs(normalizedUserId, version));
    }

    private static string Normalize(string userId) => userId.Trim().ToLowerInvariant();
}
