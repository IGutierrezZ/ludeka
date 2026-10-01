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
    /// Escaneo de sistema del ciclo nocturno (INC-46, W1): misma lógica sin la guarda de la interfaz.
    /// </summary>
    Task<BggDiscoveryResultDto> RunBggTrendsDiscoveryAsync(int maxItems = 50, CancellationToken ct = default);

    /// <summary>
    /// Escanea y encola específicamente lanzamientos de un año objetivo (por defecto año actual y previo).
    /// </summary>
    Task<BggDiscoveryResultDto> DiscoverAndEnqueueNewReleasesAsync(int? targetYear = null, int maxItems = 50, CancellationToken ct = default);

    /// <summary>
    /// Sincroniza la foto de tendencias (Hotness 1..50) del día en la base de datos e ingiere de forma
    /// inmediata en el catálogo y tabla satélite de snapshots crudos todos aquellos títulos que falten.
    /// Exige el permiso de moderación 'CanEditGames'.
    /// </summary>
    Task<BggTrendingSyncResultDto> SyncDailyTrendingAsync(int maxItems = 50, CancellationToken ct = default);

    /// <summary>
    /// Ejecución del sistema para la sincronización de tendencias del lote nocturno (sin guarda de sesión).
    /// </summary>
    Task<BggTrendingSyncResultDto> RunDailyTrendingSyncAsync(int maxItems = 50, CancellationToken ct = default);
}
