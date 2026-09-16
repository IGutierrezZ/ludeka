using System.Collections.Generic;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Identidad del usuario de la sesión actual (INC-46). Sin sesión autenticada, <see cref="UserId"/>
/// es vacío, <see cref="Roles"/> no contiene roles y las comprobaciones devuelven <c>false</c>:
/// no existe ninguna identidad simulada ni conmutador de rol o usuario.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>Identificador del <c>AppUser</c> de la sesión, o vacío cuando no hay sesión.</summary>
    string UserId { get; }

    /// <summary>Nombre visible del usuario de la sesión, o vacío cuando no hay sesión.</summary>
    string UserName { get; }

    /// <summary>Roles del usuario de la sesión, o lista vacía cuando no hay sesión.</summary>
    IReadOnlyList<string> Roles { get; }

    /// <summary>Indica si la sesión pertenece a la Mesa Fundadora activa.</summary>
    bool IsFoundingTeam { get; }

    /// <summary>Comprueba si la sesión pertenece a un rol concreto.</summary>
    bool IsInRole(string role);

    /// <summary>
    /// Comprueba un permiso granular de moderación de la sesión. Sin sesión siempre devuelve
    /// <c>false</c>; la decisión de autorización real la ejerce el pipeline y la revalidación
    /// de los servicios de escritura, nunca este contrato por sí solo.
    /// </summary>
    bool HasPermission(ModeratorPermission permission);
}
