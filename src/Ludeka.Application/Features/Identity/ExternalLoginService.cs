using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Application.Features.Identity;

/// <inheritdoc />
public sealed class ExternalLoginService : IExternalLoginService
{
    /// <summary>
    /// Dominio reservado para los correos sintéticos de las cuentas cuyo proveedor no entrega
    /// correo (por ejemplo, Facebook sin permiso de correo aprobado). Nunca es enrutable.
    /// </summary>
    public const string PlaceholderEmailDomain = "ludeka.invalid";

    private readonly IExternalLoginRepository _externalLogins;
    private readonly IUserRepository _users;

    public ExternalLoginService(IExternalLoginRepository externalLogins, IUserRepository users)
    {
        _externalLogins = externalLogins ?? throw new ArgumentNullException(nameof(externalLogins));
        _users = users ?? throw new ArgumentNullException(nameof(users));
    }

    /// <inheritdoc />
    public async Task<AppUser> ResolveAsync(
        string provider,
        string providerKey,
        string? email,
        bool emailVerified,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);

        var providerName = provider.Trim();
        var key = providerKey.Trim();
        var normalizedEmail = NormalizeEmail(email);

        // (1) Vía primaria: el par (Provider, ProviderKey) ya vinculado.
        var existingLink = await _externalLogins.GetByProviderKeyAsync(providerName, key, cancellationToken);
        if (existingLink is not null)
        {
            var linkedUser = await _users.GetByIdAsync(existingLink.UserId, cancellationToken);
            if (linkedUser is not null)
            {
                return linkedUser;
            }
        }

        // (2) Vía secundaria: correo verificado que coincide con una cuenta existente.
        if (emailVerified && normalizedEmail is not null)
        {
            var match = await _users.GetByEmailAsync(normalizedEmail, cancellationToken);
            if (match is not null)
            {
                await _externalLogins.AddAsync(
                    new ExternalLogin(match.Id, providerName, key, normalizedEmail), cancellationToken);
                return match;
            }
        }

        // (3) Alta de una cuenta comunitaria nueva: nunca hereda roles ni permisos.
        // El correo sin verificar no se persiste en AppUser (el índice único de Email es la
        // identidad verificada de la cuenta); queda únicamente como pista en ExternalLogin.ProviderEmail.
        var verifiedEmail = emailVerified ? normalizedEmail : null;
        var newUser = new AppUser(
            BuildUserId(providerName, key),
            BuildUserName(displayName, providerName, key),
            verifiedEmail ?? BuildPlaceholderEmail(providerName, key));

        await _users.AddAsync(newUser, cancellationToken);
        await _externalLogins.AddAsync(
            new ExternalLogin(newUser.Id, providerName, key, normalizedEmail), cancellationToken);

        return newUser;
    }

    /// <inheritdoc />
    public async Task<ExternalLoginLinkResult> LinkAsync(
        string userId,
        string provider,
        string providerKey,
        string? email,
        bool emailVerified,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);

        var id = SessionIdentity.Require(userId);
        var providerName = provider.Trim();
        var key = providerKey.Trim();
        var normalizedEmail = NormalizeEmail(email);

        // Relectura sin rastreo: el estado actual de la cuenta manda (SessionPermissionGuard.cs:37).
        var user = await _users.GetByIdAsync(id, cancellationToken);
        if (user is null || user.Status == UserStatus.Suspended)
        {
            throw new UnauthorizedAccessException(SessionIdentity.SessionRequiredMessage);
        }

        // Doble barrera (INC-49, sección 3.1): esta comprobación previa existe para poder redactar
        // un mensaje honesto; el índice único (Provider, ProviderKey) es la garantía real.
        var existing = await _externalLogins.GetByProviderKeyAsync(providerName, key, cancellationToken);
        if (existing is not null)
        {
            return string.Equals(existing.UserId, user.Id, StringComparison.OrdinalIgnoreCase)
                ? new ExternalLoginLinkResult(ExternalLoginLinkOutcome.AlreadyLinkedToThisAccount, providerName, user)
                : new ExternalLoginLinkResult(ExternalLoginLinkOutcome.RejectedOwnedByAnotherAccount, providerName, user);
        }

        try
        {
            await _externalLogins.AddAsync(
                new ExternalLogin(user.Id, providerName, key, normalizedEmail, providerEmailVerified: emailVerified),
                cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Misma barrera que arriba, pero disparada por el índice único ante una carrera real
            // entre dos intentos casi simultáneos: mismo mensaje, dispare quien dispare.
            return new ExternalLoginLinkResult(ExternalLoginLinkOutcome.RejectedOwnedByAnotherAccount, providerName, user);
        }

        return new ExternalLoginLinkResult(ExternalLoginLinkOutcome.Linked, providerName, user);
    }

    internal static string BuildUserId(string provider, string providerKey)
        => $"{provider}:{providerKey}".Trim().ToLowerInvariant();

    internal static string BuildUserName(string? displayName, string provider, string providerKey)
        => string.IsNullOrWhiteSpace(displayName) ? $"{provider} {providerKey}".Trim() : displayName.Trim();

    internal static string BuildPlaceholderEmail(string provider, string providerKey)
        => $"{providerKey}@{provider.ToLowerInvariant()}.{PlaceholderEmailDomain}";

    private static string? NormalizeEmail(string? email)
        => string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
}
