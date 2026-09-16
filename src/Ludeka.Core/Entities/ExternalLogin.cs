using System;

namespace Ludeka.Core.Entities;

/// <summary>
/// Representa un vínculo de identidad externa (Google, Discord, Facebook) con una cuenta de Ludeka.
/// El par <see cref="Provider"/> + <see cref="ProviderKey"/> es único y actúa como puente hacia <see cref="AppUser"/>.
/// </summary>
public class ExternalLogin
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string UserId { get; private set; } = string.Empty;
    public string Provider { get; private set; } = string.Empty;
    public string ProviderKey { get; private set; } = string.Empty;
    public string? ProviderEmail { get; private set; }
    public DateTimeOffset LinkedAt { get; private set; }

    // Constructor privado para EF Core
    private ExternalLogin() { }

    public ExternalLogin(
        string userId,
        string provider,
        string providerKey,
        string? providerEmail = null,
        DateTimeOffset? linkedAt = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("El identificador de usuario no puede estar vacío.", nameof(userId));

        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("El proveedor de identidad no puede estar vacío.", nameof(provider));

        if (string.IsNullOrWhiteSpace(providerKey))
            throw new ArgumentException("La clave del proveedor no puede estar vacía.", nameof(providerKey));

        UserId = userId.Trim();
        Provider = provider.Trim();
        ProviderKey = providerKey.Trim();
        ProviderEmail = string.IsNullOrWhiteSpace(providerEmail) ? null : providerEmail.Trim();
        LinkedAt = linkedAt ?? DateTimeOffset.UtcNow;
    }
}
