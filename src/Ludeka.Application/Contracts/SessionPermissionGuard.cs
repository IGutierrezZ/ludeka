using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Implementación de la revalidación de permiso (INC-46, hallazgo W1). La identidad de la sesión solo
/// aporta el identificador; el permiso se decide con la relectura sin rastreo del <c>AppUser</c>, el
/// mismo mecanismo que usan las políticas de autorización y la invalidación en caliente. Así, la
/// suspensión o la revocación de permisos surte efecto en la operación siguiente sin depender de la
/// cookie ni del snapshot de interfaz.
/// </summary>
public sealed class SessionPermissionGuard : ISessionPermissionGuard
{
    private readonly ICurrentUserService _currentUser;
    private readonly IUserRepository _users;

    public SessionPermissionGuard(ICurrentUserService currentUser, IUserRepository users)
    {
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _users = users ?? throw new ArgumentNullException(nameof(users));
    }

    /// <inheritdoc />
    public async Task RequireAsync(
        ModeratorPermission permission,
        string denialMessage,
        CancellationToken ct = default)
    {
        // Invariante de anonimia: sin sesión no hay permiso que valga.
        var userId = SessionIdentity.Require(_currentUser);

        // Relectura sin rastreo: el estado actual de la cuenta manda (una rastreada devolvería
        // permisos obsoletos y la revocación en caliente no surtiría efecto).
        var user = await _users.GetByIdAsync(userId, ct);

        // HasPermission deniega a las cuentas suspendidas y solo concede a la Mesa Fundadora o a un
        // moderador con la bandera exacta.
        if (user is null || !user.HasPermission(permission))
        {
            throw new UnauthorizedAccessException(
                string.IsNullOrWhiteSpace(denialMessage) ? SessionIdentity.SessionRequiredMessage : denialMessage);
        }
    }
}
