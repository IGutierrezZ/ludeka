using System;
using System.Collections.Generic;
using Ludeka.Core.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Ludeka.Web.Authentication;

/// <summary>
/// Nombres, mapeo y configuración de las 11 políticas de permiso de INC-46 —una por
/// <see cref="ModeratorPermission"/>— y de la política de rol conservada por compatibilidad.
/// </summary>
public static class AuthorizationPolicies
{
    public const string PermisoEditarFichas = nameof(PermisoEditarFichas);
    public const string PermisoAprobarMedios = nameof(PermisoAprobarMedios);
    public const string PermisoResolverReportes = nameof(PermisoResolverReportes);
    public const string PermisoGestionarEditores = nameof(PermisoGestionarEditores);
    public const string PermisoGestionarCreadores = nameof(PermisoGestionarCreadores);
    public const string PermisoGestionarTiendas = nameof(PermisoGestionarTiendas);
    public const string PermisoPublicarInstagram = nameof(PermisoPublicarInstagram);
    public const string PermisoVerAuditoria = nameof(PermisoVerAuditoria);
    public const string PermisoGestionarUsuarios = nameof(PermisoGestionarUsuarios);
    public const string PermisoGestionarEventos = nameof(PermisoGestionarEventos);
    public const string PermisoGestionarNotificaciones = nameof(PermisoGestionarNotificaciones);

    /// <summary>
    /// Política de rol que conservaba el público de `/admin/eventos` y `/admin/notificaciones`
    /// mientras el maintainer no decidía un permiso específico (INC-46 §9.3). Ya no la declara
    /// ninguna página, pero se mantiene registrada porque otros incrementos pueden depender de ella.
    /// </summary>
    public const string RolModerador = nameof(RolModerador);

    /// <summary>Mapeo política de permiso → bandera granular que la concede.</summary>
    public static readonly IReadOnlyDictionary<string, ModeratorPermission> PermissionPolicies =
        new Dictionary<string, ModeratorPermission>(StringComparer.Ordinal)
        {
            [PermisoEditarFichas] = ModeratorPermission.CanEditGames,
            [PermisoAprobarMedios] = ModeratorPermission.CanApproveMedia,
            [PermisoResolverReportes] = ModeratorPermission.CanResolveReports,
            [PermisoGestionarEditores] = ModeratorPermission.CanManagePublishers,
            [PermisoGestionarCreadores] = ModeratorPermission.CanManageCreators,
            [PermisoGestionarTiendas] = ModeratorPermission.CanManageStoreLinks,
            [PermisoPublicarInstagram] = ModeratorPermission.CanPublishInstagram,
            [PermisoVerAuditoria] = ModeratorPermission.CanViewAuditLog,
            [PermisoGestionarUsuarios] = ModeratorPermission.CanManageUsers,
            [PermisoGestionarEventos] = ModeratorPermission.CanManageEvents,
            [PermisoGestionarNotificaciones] = ModeratorPermission.CanManageNotifications,
        };

    /// <summary>Registra las 11 políticas de permiso y la política de rol.</summary>
    public static void Configure(AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        foreach (var (name, permission) in PermissionPolicies)
        {
            options.AddPolicy(name, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission)));
        }

        options.AddPolicy(RolModerador, policy => policy
            .RequireAuthenticatedUser()
            .RequireRole(LudekaRoleNames.FoundingTeam, LudekaRoleNames.Moderator));
    }
}

/// <summary>Requisito de autorización que exige un permiso granular de moderación.</summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(ModeratorPermission permission) => Permission = permission;

    /// <summary>Permiso granular exigido por la política.</summary>
    public ModeratorPermission Permission { get; }
}

/// <summary>Nombres de rol usados en los claims de la cookie de sesión.</summary>
public static class LudekaRoleNames
{
    public const string FoundingTeam = nameof(UserRole.FoundingTeam);
    public const string Moderator = nameof(UserRole.Moderator);
    public const string CommunityUser = nameof(UserRole.CommunityUser);
}
