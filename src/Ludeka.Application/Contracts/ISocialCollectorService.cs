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
    /// </summary>
    Task<SocialCollectorRunResultDto> CollectAllAccountsAsync(int maxItemsPerAccount = 5, CancellationToken ct = default);

    /// <summary>
    /// Escanea un canal monitorizado concreto bajo demanda.
    /// </summary>
    Task<SocialCollectorRunResultDto> CollectAccountAsync(Guid accountId, int maxItems = 5, CancellationToken ct = default);
}
