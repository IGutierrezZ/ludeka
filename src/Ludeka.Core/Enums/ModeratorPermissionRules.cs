using System;

namespace Ludeka.Core.Enums;

/// <summary>
/// Reglas de concesión de los permisos granulares de moderación (INC-20, extraídas en INC-46 F3).
/// Las comparten el agregado <c>AppUser</c> y la proyección de la identidad de la sesión, de modo
/// que la cookie y la base de datos nunca discrepen sobre qué concede cada rol.
/// </summary>
public static class ModeratorPermissionRules
{
    /// <summary>
    /// Determina si un rol con una máscara de permisos concreta concede el permiso solicitado.
    /// Una cuenta suspendida nunca concede; la Mesa Fundadora concede siempre; un moderador activo
    /// evalúa su máscara exacta; los usuarios comunitarios no tienen permisos de moderación.
    /// </summary>
    /// <param name="role">Rol del usuario evaluado.</param>
    /// <param name="status">Estado de la cuenta, que anula cualquier privilegio si está suspendida.</param>
    /// <param name="mask">Máscara granular de permisos del usuario.</param>
    /// <param name="required">Permiso o conjunción de permisos requeridos.</param>
    /// <returns><c>true</c> cuando el permiso queda concedido.</returns>
    public static bool Grants(
        UserRole role,
        UserStatus status,
        ModeratorPermission mask,
        ModeratorPermission required)
    {
        if (status == UserStatus.Suspended)
        {
            return false;
        }

        if (role == UserRole.FoundingTeam)
        {
            return true;
        }

        if (role != UserRole.Moderator)
        {
            return false;
        }

        if (required == ModeratorPermission.None)
        {
            return true;
        }

        return (mask & required) == required;
    }
}
