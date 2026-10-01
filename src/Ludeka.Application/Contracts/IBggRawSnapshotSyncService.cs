using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Servicio de orquestación para la sincronización de snapshots brutos de BGG,
/// auto-vinculación de expansiones y descubrimiento de expansiones satélite.
/// </summary>
public interface IBggRawSnapshotSyncService
{
    /// <summary>
    /// Devuelve las métricas y estado global de snapshots frente al catálogo caliente.
    /// </summary>
    Task<BggRawSnapshotStatusDto> GetStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Sincroniza un lote de snapshots para juegos de catálogo que carecen de snapshot satélite,
    /// respetando la pausa de cortesía entre llamadas hacia BGG y auto-vinculando expansiones.
    /// </summary>
    Task<BggRawSnapshotSyncResultDto> SyncBatchAsync(int batchSize = 20, int delayMs = 1200, CancellationToken ct = default);

    /// <summary>
    /// Analiza los snapshots almacenados en la tabla satélite, identifica expansiones no catalogadas
    /// y las encola en la bandeja de importación pendiente para su procesamiento.
    /// </summary>
    Task<BggExpansionDiscoveryResultDto> DiscoverAndEnqueueMissingExpansionsAsync(int maxToEnqueue = 50, CancellationToken ct = default);

    /// <summary>
    /// Re-escanea el catálogo buscando expansiones sin juego base asignado y las vincula deterministamente
    /// si su juego base correspondiente existe en el catálogo.
    /// </summary>
    Task<int> AutoLinkExistingExpansionsAsync(CancellationToken ct = default);
}
