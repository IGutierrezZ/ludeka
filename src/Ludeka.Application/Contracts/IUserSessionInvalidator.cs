using System;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Notifica la invalidación de la sesión de un usuario (INC-46 F3). Cuando una cuenta pasa a
/// suspendida o cambian sus permisos, los circuitos interactivos activos deben recargarse para
/// reevaluar la identidad sin esperar a que caduque la cookie.
/// </summary>
public interface IUserSessionInvalidator
{
    /// <summary>
    /// Versión monótona de invalidaciones de un usuario (cero si nunca se invalidó). Permite a un
    /// consumidor distinguir una invalidación nueva de una ya atendida y no recargar en bucle.
    /// </summary>
    long GetVersion(string userId);

    /// <summary>Invalida la sesión del usuario indicado y notifica a los circuitos suscritos.</summary>
    void Invalidate(string userId);

    /// <summary>Se dispara después de invalidar la sesión de un usuario.</summary>
    event EventHandler<UserSessionInvalidatedEventArgs>? Invalidated;
}
