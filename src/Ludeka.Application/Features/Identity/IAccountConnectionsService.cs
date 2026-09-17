using System.Threading;
using System.Threading.Tasks;

namespace Ludeka.Application.Features.Identity;

/// <summary>
/// Conexiones de identidad externa de la propia cuenta de la sesión (INC-49, diseño §D6). Es una
/// lectura de estado de cuenta para la interfaz: nunca decide autorización, que la ejercen el
/// pipeline y la revalidación de los servicios de escritura.
/// </summary>
public interface IAccountConnectionsService
{
    /// <summary>Vínculos de la cuenta de la sesión. Sin sesión devuelve una vista vacía.</summary>
    Task<AccountConnectionsView> GetConnectionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica si la cuenta de la sesión conserva al menos una identidad externa cuyo correo entregó
    /// el proveedor como verificado. Sin sesión devuelve <c>false</c>: no hay cuenta que avisar.
    /// </summary>
    Task<bool> HasVerifiedProviderEmailAsync(CancellationToken cancellationToken = default);
}
