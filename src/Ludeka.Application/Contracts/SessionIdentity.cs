using System;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Invariante de sesión (INC-46): ninguna escritura con identidad se ejecuta sin una sesión
/// autenticada. Es el punto único donde se decide la frontera de anonimia, de modo que la denegación
/// es siempre controlada (<see cref="UnauthorizedAccessException"/>) y nunca una excepción de
/// argumento lanzada por una entidad de dominio al recibir un <c>UserId</c> vacío, ni una fila
/// atribuida a un usuario centinela.
/// </summary>
public static class SessionIdentity
{
    /// <summary>Mensaje único de denegación por falta de sesión.</summary>
    public const string SessionRequiredMessage = "Se requiere una sesión iniciada para realizar esta acción.";

    /// <summary>
    /// Devuelve el identificador de la sesión actual o lanza la denegación controlada cuando no hay
    /// sesión autenticada. Un servicio que recibe la dependencia como opcional no debe invocar este
    /// método cuando carece de contexto de identidad (pruebas y ejecuciones sin web).
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">No hay identidad de sesión.</exception>
    public static string Require(ICurrentUserService? currentUser)
        => Require(currentUser?.UserId);

    /// <summary>
    /// Devuelve la identidad indicada, ya normalizada, o lanza la denegación controlada cuando está
    /// vacía. Lo usan los servicios que reciben la identidad como parámetro en lugar de resolverla
    /// por <see cref="ICurrentUserService"/>.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">La identidad es nula o vacía.</exception>
    public static string Require(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException(SessionRequiredMessage);
        }

        return userId.Trim();
    }
}
