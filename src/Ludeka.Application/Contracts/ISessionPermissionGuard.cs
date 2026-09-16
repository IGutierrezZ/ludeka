using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Revalidación de permiso sobre la identidad de la sesión (INC-46, hallazgo W1). Cada escritura
/// administrativa que se puede disparar desde la interfaz comprueba aquí que la sesión existe y que su
/// <c>AppUser</c> <b>actual</b> concede el permiso exigido; una cuenta suspendida o con permisos
/// revocados queda bloqueada en la siguiente operación aunque su cookie siga vigente. La denegación
/// es controlada (<see cref="UnauthorizedAccessException"/>) y nunca deja rastro en la base de datos.
/// </summary>
public interface ISessionPermissionGuard
{
    /// <summary>
    /// Exige una sesión autenticada y el permiso indicado, releyendo el <c>AppUser</c> sin rastreo.
    /// </summary>
    /// <param name="permission">Permiso granular exigido por la operación administrativa.</param>
    /// <param name="denialMessage">Mensaje de la denegación controlada.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <exception cref="UnauthorizedAccessException">
    /// No hay sesión, la cuenta ya no existe, está suspendida o no concede el permiso.
    /// </exception>
    Task RequireAsync(ModeratorPermission permission, string denialMessage, CancellationToken ct = default);
}
