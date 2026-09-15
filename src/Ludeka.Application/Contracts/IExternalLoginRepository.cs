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
}
