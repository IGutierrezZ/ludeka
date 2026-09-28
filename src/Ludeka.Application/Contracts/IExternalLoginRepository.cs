using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Entities;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Acceso a los vínculos de identidad externa que unen un proveedor (Google, Discord, Facebook) con una cuenta de Ludeka.
/// </summary>
public interface IExternalLoginRepository
{
    /// <summary>
    /// Recupera el vínculo de identidad externa que corresponde al par proveedor-clave indicado.
    /// </summary>
    /// <param name="provider">Nombre del proveedor de identidad (por ejemplo, <c>Google</c>).</param>
    /// <param name="providerKey">Identificador único del usuario dentro del proveedor.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>El vínculo existente, o <c>null</c> si el par no está registrado.</returns>
    Task<ExternalLogin?> GetByProviderKeyAsync(string provider, string providerKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste un vínculo de identidad externa nuevo.
    /// </summary>
    /// <param name="externalLogin">Vínculo a persistir.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task AddAsync(ExternalLogin externalLogin, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera todos los vínculos de identidad externa de la cuenta indicada.
    /// </summary>
    /// <param name="userId">Identificador de la cuenta cuyos vínculos se consultan.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<IReadOnlyList<ExternalLogin>> ListByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina un vínculo de identidad externa existente. No existe ningún método de actualización:
    /// reasignar una fila entre cuentas es estructuralmente imposible por diseño (INC-49, sección 3.1).
    /// </summary>
    /// <param name="externalLogin">Vínculo a eliminar.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task RemoveAsync(ExternalLogin externalLogin, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina todos los vínculos de identidad externa asociados a una cuenta de usuario (INC-75).
    /// Permite liberar de forma atómica los proveedores OAuth tras la baja o supresión de la cuenta.
    /// </summary>
    /// <param name="userId">Identificador de la cuenta cuyos vínculos se eliminan.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task DeleteByUserIdAsync(string userId, CancellationToken cancellationToken = default);
}

