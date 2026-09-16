using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Orquestador del servicio de recolección automática de publicaciones desde canales sociales monitorizados.
/// </summary>
public interface ISocialCollectorService
{
    /// <summary>
    /// Escanea todos los canales monitorizados habilitados e ingesta las publicaciones no duplicadas en la bandeja de moderación.
    /// Exige sesión y permiso de moderación de medios (INC-46, W1).
    /// </summary>
    Task<SocialCollectorRunResultDto> CollectAllAccountsAsync(int maxItemsPerAccount = 5, CancellationToken ct = default);

    /// <summary>
    /// Ciclo programado del servicio hospedado (INC-46, W1): ruta de sistema sin sesión que ejecuta el
    /// mismo sondeo sin pasar por la guarda de la interfaz.
    /// </summary>
    Task<SocialCollectorRunResultDto> RunScheduledCollectionAsync(int maxItemsPerAccount = 5, CancellationToken ct = default);

    /// <summary>
    /// Escanea un canal monitorizado concreto bajo demanda.
    /// </summary>
    Task<SocialCollectorRunResultDto> CollectAccountAsync(Guid accountId, int maxItems = 5, CancellationToken ct = default);
}
