using System;
using System.Security.Claims;
using Ludeka.Core.Entities;

namespace Ludeka.Web.Services;

/// <summary>
/// Snapshot de identidad del ámbito actual (petición SSR o circuito interactivo). Lo puebla
/// <see cref="UserCircuitHandler"/> al abrir el circuito con el <c>AppUser</c> de la sesión y lo
/// consume <see cref="AuthenticatedCurrentUserService"/>. Sin resolución, la identidad se proyecta
/// desde los claims de la cookie; nunca se inventa un usuario.
/// </summary>
public sealed class UserIdentitySnapshot
{
    /// <summary>Indica si el ámbito ya intentó resolver la identidad (autenticada o anónima).</summary>
    public bool IsResolved { get; private set; }

    /// <summary>
    /// Principal de la sesión del circuito. Un principal anónimo es una resolución válida: evita
    /// caer al <c>HttpContext</c> de la petición que ya no existe dentro del circuito.
    /// </summary>
    public ClaimsPrincipal? Principal { get; private set; }

    /// <summary><c>AppUser</c> de la sesión, o <c>null</c> cuando no hay sesión o no existe la fila.</summary>
    public AppUser? User { get; private set; }

    /// <summary>Fija la identidad resuelta del ámbito (principal y usuario, cualquiera puede ser nulo).</summary>
    public void Resolve(ClaimsPrincipal? principal, AppUser? user)
    {
        Principal = principal;
        User = user;
        IsResolved = true;
    }

    /// <summary>Restablece el snapshot al estado sin resolver (uso en pruebas y re-resoluciones).</summary>
    public void Clear()
    {
        Principal = null;
        User = null;
        IsResolved = false;
    }
}
