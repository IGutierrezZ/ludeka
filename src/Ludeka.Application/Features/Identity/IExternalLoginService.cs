using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Entities;

namespace Ludeka.Application.Features.Identity;

/// <summary>
/// Resuelve la identidad de Ludeka a partir de un acceso social correcto: reutiliza el vínculo
/// existente, vincula por correo verificado o aprueba una cuenta comunitaria nueva.
/// </summary>
public interface IExternalLoginService
{
    /// <summary>
    /// Resuelve la cuenta de Ludeka que corresponde al acceso del proveedor indicado, en cascada:
    /// (1) por el par <c>(provider, providerKey)</c>; (2) por correo verificado contra <c>AppUser.Email</c>,
    /// creando entonces el vínculo; (3) alta de una cuenta <c>CommunityUser</c> sin permisos.
    /// La cuenta fundadora nunca se auto-concede y los correos sin verificar no fusionan cuentas.
    /// </summary>
    /// <param name="provider">Proveedor de identidad (<c>Google</c>, <c>Discord</c>, <c>Facebook</c>).</param>
    /// <param name="providerKey">Identificador único del usuario dentro del proveedor.</param>
    /// <param name="email">Correo entregado por el proveedor, si existe.</param>
    /// <param name="emailVerified">Indica si el proveedor verificó el correo.</param>
    /// <param name="displayName">Nombre visible entregado por el proveedor, si existe.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>La cuenta de Ludeka resuelta o aprovisionada.</returns>
    Task<AppUser> ResolveAsync(
        string provider,
        string providerKey,
        string? email,
        bool emailVerified,
        string? displayName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Vincula la identidad externa entrante a la cuenta indicada por la sesión. NUNCA aprovisiona un
    /// <c>AppUser</c> nuevo, sea cual sea el resultado del desafío.
    /// </summary>
    /// <param name="userId">Identificador de la cuenta de la sesión activa (nunca del cliente).</param>
    /// <param name="provider">Proveedor de identidad (<c>Google</c>, <c>Discord</c>, <c>Facebook</c>).</param>
    /// <param name="providerKey">Identificador único del usuario dentro del proveedor.</param>
    /// <param name="email">Correo entregado por el proveedor, si existe.</param>
    /// <param name="emailVerified">Indica si el proveedor verificó el correo.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <exception cref="UnauthorizedAccessException">No hay sesión, o la cuenta no existe o está suspendida.</exception>
    Task<ExternalLoginLinkResult> LinkAsync(
        string userId,
        string provider,
        string providerKey,
        string? email,
        bool emailVerified,
        CancellationToken cancellationToken = default);
}
