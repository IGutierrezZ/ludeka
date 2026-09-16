using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Web.Authentication;
using Microsoft.AspNetCore.Http;

namespace Ludeka.Web.Services;

/// <summary>
/// Identidad de la sesión para la interfaz (INC-46 F3). En el circuito lee el snapshot que puebla
/// <see cref="UserCircuitHandler"/> con el <c>AppUser</c> de la base de datos; en SSR proyecta los
/// claims de la cookie como snapshot de interfaz. Sin sesión devuelve identidad vacía, ningún rol y
/// ningún permiso, y nunca decide autorización: esa la ejercen las políticas y la revalidación de
/// los servicios de escritura.
/// </summary>
public sealed class AuthenticatedCurrentUserService : ICurrentUserService
{
    private readonly UserIdentitySnapshot _snapshot;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public AuthenticatedCurrentUserService(
        UserIdentitySnapshot snapshot,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public string UserId => Session.UserId;

    /// <inheritdoc />
    public string UserName => Session.UserName;

    /// <inheritdoc />
    public IReadOnlyList<string> Roles => Session.IsActive ? [Session.Role.ToString()] : [];

    /// <inheritdoc />
    public bool IsFoundingTeam => Session.IsActive && Session.Role == UserRole.FoundingTeam;

    /// <inheritdoc />
    public bool IsInRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role) || !Session.IsActive)
        {
            return false;
        }

        return string.Equals(Session.Role.ToString(), role.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public bool HasPermission(ModeratorPermission permission)
    {
        if (!Session.IsActive)
        {
            return false;
        }

        return ModeratorPermissionRules.Grants(Session.Role, UserStatus.Active, Session.Permissions, permission);
    }

    /// <summary>
    /// Identidad efectiva del ámbito: el <c>AppUser</c> resuelto en el circuito manda; después, el
    /// principal de la sesión (del snapshot o del <c>HttpContext</c> de la petición SSR); sin nada
    /// de lo anterior, sesión anónima. Una cuenta suspendida conserva su identidad pero pierde
    /// todos los privilegios.
    /// </summary>
    private SessionIdentity Session
    {
        get
        {
            if (_snapshot.User is { } user)
            {
                return new SessionIdentity(
                    user.Id,
                    user.UserName,
                    user.Role,
                    user.Permissions,
                    user.Status != UserStatus.Suspended);
            }

            var principal = _snapshot.Principal ?? _httpContextAccessor?.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return SessionIdentity.Anonymous;
            }

            var claims = new CookieClaimProjection(principal);
            return new SessionIdentity(
                claims.UserId,
                claims.UserName,
                claims.Role,
                claims.Permissions,
                IsActive: true);
        }
    }

    /// <summary>Identidad resuelta del ámbito actual, ya normalizada como roles y permisos.</summary>
    private readonly record struct SessionIdentity(
        string UserId,
        string UserName,
        UserRole Role,
        ModeratorPermission Permissions,
        bool IsActive)
    {
        /// <summary>Sesión inexistente o no autenticada: sin identidad, sin roles y sin permisos.</summary>
        public static SessionIdentity Anonymous => new(
            string.Empty,
            string.Empty,
            UserRole.CommunityUser,
            ModeratorPermission.None,
            IsActive: false);
    }

    /// <summary>
    /// Proyección de los claims que firma la cookie de sesión (<see cref="ExternalLoginEvents.BuildSessionPrincipal(AppUser)"/>).
    /// Es un snapshot de interfaz: sirve para renderizar en SSR y no sustituye la relectura del
    /// <c>AppUser</c> que hacen las políticas y los servicios de escritura.
    /// </summary>
    private readonly struct CookieClaimProjection
    {
        public CookieClaimProjection(ClaimsPrincipal principal)
        {
            UserId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            UserName = principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
            Role = ResolveRole(principal);
            Permissions = ResolvePermissions(principal);
        }

        public string UserId { get; }
        public string UserName { get; }
        public UserRole Role { get; }
        public ModeratorPermission Permissions { get; }

        private static UserRole ResolveRole(ClaimsPrincipal principal)
        {
            var roleNames = principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToList();

            if (roleNames.Any(name => string.Equals(name, nameof(UserRole.FoundingTeam), StringComparison.OrdinalIgnoreCase)))
            {
                return UserRole.FoundingTeam;
            }

            if (roleNames.Any(name => string.Equals(name, nameof(UserRole.Moderator), StringComparison.OrdinalIgnoreCase)))
            {
                return UserRole.Moderator;
            }

            return UserRole.CommunityUser;
        }

        private static ModeratorPermission ResolvePermissions(ClaimsPrincipal principal)
        {
            var claim = principal.FindFirst(ExternalLoginEvents.PermissionsClaim);
            return claim is not null && Enum.TryParse<ModeratorPermission>(claim.Value, out var mask)
                ? mask
                : ModeratorPermission.None;
        }
    }
}
