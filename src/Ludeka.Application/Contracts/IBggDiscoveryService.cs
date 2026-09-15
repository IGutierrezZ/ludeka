using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Contrato para el servicio de auto-descubrimiento continuo de tendencias y lanzamientos en BoardGameGeek.
/// </summary>
public interface IBggDiscoveryService
{
    /// <summary>
    /// Escanea las tendencias mundiales actuales (Hotness) de BGG y encola en la cola de catalogación
    /// aquellos títulos que aún no existan en el catálogo, en la cola ni en la tabla de staging.
    /// Si el año de publicación corresponde al año actual o inmediatamente anterior, se clasifica como BggNewReleases;
    /// de lo contrario, como BggHotness.
    /// </summary>
    Task<BggDiscoveryResultDto> DiscoverAndEnqueueBggTrendsAsync(int maxItems = 50, CancellationToken ct = default);

    /// <summary>
    /// Escanea y encola específicamente lanzamientos de un año objetivo (por defecto año actual y previo).
    /// </summary>
    Task<BggDiscoveryResultDto> DiscoverAndEnqueueNewReleasesAsync(int? targetYear = null, int maxItems = 50, CancellationToken ct = default);
}
