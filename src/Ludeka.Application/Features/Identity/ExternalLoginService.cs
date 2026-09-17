using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

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
    private readonly IAuditService? _audit;

    /// <param name="audit">
    /// Dependencia OPCIONAL (INC-49): así <c>ExternalLoginServiceTests.cs:35</c> —construida con solo
    /// dos argumentos— sigue compilando sin tocarla. En producción, <c>Program.cs</c> la inyecta siempre.
    /// </param>
    public ExternalLoginService(IExternalLoginRepository externalLogins, IUserRepository users, IAuditService? audit = null)
    {
        _externalLogins = externalLogins ?? throw new ArgumentNullException(nameof(externalLogins));
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _audit = audit;
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

        // (2) Vía secundaria: correo verificado que coincide con una cuenta existente. INC-49 parte
        // esta rama en 2a/2b (diseño §2.2): la excepción segura solo aplica cuando la cuenta destino
        // no tiene todavía ningún proveedor vinculado.
        if (emailVerified && normalizedEmail is not null)
        {
            var match = await _users.GetByEmailAsync(normalizedEmail, cancellationToken);
            if (match is not null)
            {
                var matchLinks = await _externalLogins.ListByUserIdAsync(match.Id, cancellationToken);
                if (matchLinks.Count > 0)
                {
                    // (2b) Colisión: la cuenta ya tiene al menos un proveedor vinculado. Nunca se
                    // fusiona en silencio (INC-49, diseño §2.2): cero escrituras. Este mensaje es
                    // diagnóstico interno; el aviso que ve la persona lo traduce ExternalLoginEvents
                    // a partir del código cerrado de "?aviso=", nunca de este texto (§3.1 del diseño).
                    throw new ExternalLoginCollisionException(
                        $"El correo verificado coincide con una cuenta que ya tiene otro proveedor vinculado ({providerName}).");
                }

                // (2a) Excepción segura: la cuenta no tiene ninguna identidad externa vinculada
                // todavía. Conducta idéntica a la rama 2 original de INC-46.
                await _externalLogins.AddAsync(
                    new ExternalLogin(match.Id, providerName, key, normalizedEmail, providerEmailVerified: emailVerified),
                    cancellationToken);
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
            new ExternalLogin(newUser.Id, providerName, key, normalizedEmail, providerEmailVerified: emailVerified),
            cancellationToken);

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

        var providerName = provider.Trim();
        var key = providerKey.Trim();
        var normalizedEmail = NormalizeEmail(email);

        var user = await RequireActiveUserAsync(userId, cancellationToken);

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
        catch (DuplicateExternalLoginException)
        {
            // Misma barrera que arriba, pero disparada por el índice único ante una carrera real
            // entre dos intentos casi simultáneos: mismo mensaje, dispare quien dispare.
            return new ExternalLoginLinkResult(ExternalLoginLinkOutcome.RejectedOwnedByAnotherAccount, providerName, user);
        }

        // Reemplazo del correo sintético (INC-49, diseño §3.4): de mejor esfuerzo y nunca fatal,
        // después de crear la fila y solo en el camino de éxito.
        var previousEmail = user.Email;
        var emailReplaced = await TryReplacePlaceholderEmailAsync(user, normalizedEmail, emailVerified, cancellationToken);

        // La auditoría solo se registra en el ÚNICO camino de éxito: nunca antes de una excepción
        // ni en un resultado distinto de Linked (INC-49, diseño §D7).
        await RecordAuditAsync(
            user, AuditAction.LinkedProvider,
            $"Vinculación del proveedor de acceso {providerName}",
            oldProviderValue: null, newProviderValue: providerName,
            cancellationToken,
            emailChange: emailReplaced ? new FieldChangeDto("Email", previousEmail, user.Email) : null);

        return new ExternalLoginLinkResult(ExternalLoginLinkOutcome.Linked, providerName, user, emailReplaced);
    }

    /// <summary>
    /// Reemplaza el correo sintético de la cuenta por el correo verificado del proveedor recién
    /// vinculado (INC-49, diseño §3.4). Es de MEJOR ESFUERZO: si el correo ya pertenece a otra
    /// cuenta, no se reemplaza nada y la vinculación que lo invoca sigue siendo un éxito.
    /// </summary>
    private async Task<bool> TryReplacePlaceholderEmailAsync(
        AppUser user, string? normalizedEmail, bool emailVerified, CancellationToken cancellationToken)
    {
        if (!emailVerified || normalizedEmail is null)
        {
            return false;
        }

        if (!IsPlaceholderEmail(user.Email))
        {
            return false; // (a) la cuenta ya tiene un correo real: no se toca.
        }

        var owner = await _users.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (owner is not null)
        {
            return false; // (b) colisión: el correo ya pertenece a otra cuenta. No se reemplaza.
        }

        user.UpdateProfile(user.UserName, normalizedEmail);
        try
        {
            await _users.UpdateAsync(user, cancellationToken);
            return true;
        }
        catch (DuplicateUserEmailException)
        {
            // (c) carrera contra el índice único de AppUsers.Email: el vínculo ya está creado y es
            // válido, así que no se revierte. La cuenta conserva su correo sintético.
            return false;
        }
    }

    /// <summary>
    /// Indica si <paramref name="email"/> pertenece al dominio reservado y no enrutable de los
    /// correos sintéticos (INC-49, diseño §3.4), en cualquiera de sus dos formas:
    /// <c>{clave}@{proveedor}.ludeka.invalid</c> (la que genera <see cref="BuildPlaceholderEmail"/>)
    /// o la forma corta histórica <c>@ludeka.invalid</c>.
    /// </summary>
    public static bool IsPlaceholderEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var at = email.LastIndexOf('@');
        if (at < 0)
        {
            return false;
        }

        var host = email[(at + 1)..];
        return host.Equals(PlaceholderEmailDomain, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + PlaceholderEmailDomain, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task UnlinkAsync(
        string userId,
        string provider,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);

        var user = await RequireActiveUserAsync(userId, cancellationToken);

        // Solo entre las filas PROPIAS: reasignar o borrar la de otro es estructuralmente imposible.
        var links = await _externalLogins.ListByUserIdAsync(user.Id, cancellationToken);
        var target = links.FirstOrDefault(l => string.Equals(l.Provider, provider.Trim(), StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            return; // idempotente: ya no estaba vinculado
        }

        // LA GUARDA: sin esto, la cuenta se quedaría sin ninguna forma de volver a entrar.
        if (links.Count <= 1)
        {
            throw new LastAccessMethodException(AccountConnectionMessages.LastAccessMethodDenied);
        }

        await _externalLogins.RemoveAsync(target, cancellationToken);

        await RecordAuditAsync(
            user, AuditAction.UnlinkedProvider,
            $"Desvinculación del proveedor de acceso {target.Provider}",
            oldProviderValue: target.Provider, newProviderValue: null,
            cancellationToken);
    }

    /// <summary>
    /// Invariante compartido de <see cref="LinkAsync"/> y <see cref="UnlinkAsync"/> (INC-49, diseño
    /// §D5): exige sesión y relee la cuenta sin rastreo, igual que <c>SessionPermissionGuard.cs:37</c>,
    /// de modo que la suspensión surte efecto en la operación siguiente sin depender de la cookie.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">No hay sesión, o la cuenta no existe o está suspendida.</exception>
    private async Task<AppUser> RequireActiveUserAsync(string userId, CancellationToken cancellationToken)
    {
        var id = SessionIdentity.Require(userId);
        var user = await _users.GetByIdAsync(id, cancellationToken);
        if (user is null || user.Status == UserStatus.Suspended)
        {
            throw new UnauthorizedAccessException(SessionIdentity.SessionRequiredMessage);
        }

        return user;
    }

    /// <summary>
    /// Registra la entrada de auditoría de una vinculación o desvinculación completada con éxito.
    /// Sin operación si no se suministró <see cref="IAuditService"/> (dependencia opcional, INC-49).
    /// </summary>
    private async Task RecordAuditAsync(
        AppUser user,
        AuditAction action,
        string summary,
        string? oldProviderValue,
        string? newProviderValue,
        CancellationToken cancellationToken,
        FieldChangeDto? emailChange = null)
    {
        if (_audit is null)
        {
            return;
        }

        // El cambio de correo (INC-49, tarea 7.5) solo se añade cuando LinkAsync reemplazó el
        // correo sintético; UnlinkAsync nunca lo pasa, así que su auditoría queda igual que antes.
        FieldChangeDto[] changes = emailChange is null
            ? [new FieldChangeDto("Provider", oldProviderValue, newProviderValue)]
            : [new FieldChangeDto("Provider", oldProviderValue, newProviderValue), emailChange];

        await _audit.RecordChangeAsync(
            new RecordAuditCommand(
                UserId: user.Id,
                UserName: user.UserName,
                Action: action,
                EntityType: AuditEntityType.User,
                EntityId: user.Id,
                EntityName: user.UserName,
                Summary: summary,
                Changes: changes),
            cancellationToken);
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
