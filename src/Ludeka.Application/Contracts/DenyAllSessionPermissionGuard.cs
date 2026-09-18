using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Guarda de permiso para hosts sin sesión de usuario (procesos de trabajo, INC-47). Un trabajo
/// programado solo debe invocar los puntos de entrada de sistema de cada servicio (los que releen
/// el estado persistido, no los que dependen de un <c>AppUser</c> autenticado), así que esta guarda
/// deniega SIEMPRE en lugar de imitar el valor por defecto <c>null</c> que usan los seis servicios
/// que ya toleran la guarda ausente (INC-46, hallazgo W1). Denegar en vez de "permitir todo" o
/// "dejar en null" convierte una llamada futura a un método con guarda en una excepción ruidosa,
/// nunca en una autorización eludida en silencio.
/// </summary>
public sealed class DenyAllSessionPermissionGuard : ISessionPermissionGuard
{
    private const string DenialMessage =
        "Este proceso de trabajo no tiene sesión de usuario: solo puede ejecutar puntos de entrada de sistema.";

    /// <inheritdoc />
    public Task RequireAsync(ModeratorPermission permission, string denialMessage, CancellationToken ct = default)
    {
        throw new UnauthorizedAccessException(
            string.IsNullOrWhiteSpace(denialMessage) ? DenialMessage : denialMessage);
    }
}
