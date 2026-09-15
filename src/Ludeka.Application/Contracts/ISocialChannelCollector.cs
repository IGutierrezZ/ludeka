using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Contrato para recolectores especializados por plataforma o medio social.
/// </summary>
public interface ISocialChannelCollector
{
    /// <summary>
    /// Comprueba si este recolector da soporte a la plataforma indicada.
    /// </summary>
    bool CanHandle(SocialPlatform platform);

    /// <summary>
    /// Extrae las publicaciones o vídeos recientes de una cuenta o canal monitorizado.
    /// </summary>
    Task<IReadOnlyList<DiscoveredSocialPostDto>> CollectRecentPostsAsync(
        MonitoredSocialAccount account,
        int maxItems = 5,
        CancellationToken ct = default);
}
