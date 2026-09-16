using System;

namespace Ludeka.Application.Contracts;

/// <summary>Datos de una invalidación de sesión: el usuario afectado y la versión resultante.</summary>
public sealed class UserSessionInvalidatedEventArgs : EventArgs
{
    public UserSessionInvalidatedEventArgs(string userId, long version)
    {
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        Version = version;
    }

    /// <summary>Identificador normalizado del usuario cuya sesión se invalidó.</summary>
    public string UserId { get; }

    /// <summary>Versión monótona de invalidaciones de ese usuario.</summary>
    public long Version { get; }
}
