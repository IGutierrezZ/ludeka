using System;

namespace Ludeka.Core.Entities;

/// <summary>
/// Representa un token criptográfico de un solo uso para acceso por correo (Magic Link).
/// Almacena únicamente el hash SHA-256 del token para garantizar que nunca se persista en claro.
/// </summary>
public class MagicLinkToken
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Email { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public string? TargetUserId { get; private set; }

    // Constructor privado para EF Core
    private MagicLinkToken() { }

    public MagicLinkToken(
        string email,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        string? targetUserId = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("El correo electrónico no puede estar vacío.", nameof(email));

        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("El hash del token no puede estar vacío.", nameof(tokenHash));

        if (expiresAt <= createdAt)
            throw new ArgumentException("La fecha de expiración debe ser posterior a la fecha de creación.", nameof(expiresAt));

        Id = Guid.NewGuid();
        Email = email.Trim().ToLowerInvariant();
        TokenHash = tokenHash.Trim();
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        TargetUserId = string.IsNullOrWhiteSpace(targetUserId) ? null : targetUserId.Trim();
    }

    /// <summary>
    /// Determina si el token es válido en el instante indicado (no ha sido consumido y no ha expirado).
    /// </summary>
    public bool IsValid(DateTimeOffset nowUtc)
    {
        return ConsumedAt is null && nowUtc <= ExpiresAt;
    }

    /// <summary>
    /// Marca el token como consumido en el instante especificado.
    /// </summary>
    public void Consume(DateTimeOffset consumedAt)
    {
        if (ConsumedAt is not null)
            throw new InvalidOperationException("El token de enlace mágico ya ha sido consumido previamente.");

        if (consumedAt < CreatedAt)
            throw new ArgumentException("La fecha de consumo no puede ser anterior a la de creación.", nameof(consumedAt));

        ConsumedAt = consumedAt;
    }
}
