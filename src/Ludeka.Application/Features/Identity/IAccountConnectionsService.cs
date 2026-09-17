using System;
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

    /// <summary>
    /// Invalida la caché de ámbito de este servicio (corrección del orquestador, INC-49 PR #6).
    /// La caché de <see cref="GetConnectionsAsync"/>/<see cref="HasVerifiedProviderEmailAsync"/> vive
    /// todo el circuito interactivo, no una sola petición (<c>App.razor</c> monta
    /// <c>InteractiveServer</c> global), así que una desvinculación dentro del mismo circuito debe
    /// invalidarla explícitamente: de lo contrario cualquier lector de este mismo ámbito —incluida
    /// esta misma instancia— seguiría viendo el estado anterior hasta una recarga completa de
    /// página. Dispara <see cref="Invalidated"/> para que un componente vivo del circuito (por
    /// ejemplo, el aviso de cabecera) pueda refrescarse sin depender de una navegación nueva.
    /// </summary>
    void InvalidateCache();

    /// <summary>
    /// Se dispara cada vez que <see cref="InvalidateCache"/> invalida la caché de este ámbito.
    /// </summary>
    event EventHandler? Invalidated;
}
