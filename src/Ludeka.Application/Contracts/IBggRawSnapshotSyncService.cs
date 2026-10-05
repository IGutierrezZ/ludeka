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
    Task<BggExpansionDiscoveryResultDto> DiscoverAndEnqueueMissingExpansionsAsync(int maxToEnqueue = 1200, CancellationToken ct = default);

    /// <summary>
    /// Re-escanea el catálogo buscando expansiones sin juego base asignado y las vincula deterministamente
    /// si su juego base correspondiente existe en el catálogo.
    /// </summary>
    Task<int> AutoLinkExistingExpansionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Versión de sistema para ejecutor en segundo plano (Ludeka.Jobs o background worker) sin guarda interactiva.
    /// </summary>
    Task<BggRawSnapshotSyncResultDto> RunScheduledSyncBatchAsync(int batchSize = 20, int delayMs = 1200, CancellationToken ct = default);

    /// <summary>
    /// Versión de sistema para descubrimiento y encolado de expansiones sin guarda interactiva.
    /// </summary>
    Task<BggExpansionDiscoveryResultDto> RunScheduledDiscoverAndEnqueueMissingExpansionsAsync(int maxToEnqueue = 1200, CancellationToken ct = default);

    /// <summary>
    /// Versión de sistema para auto-vinculación de expansiones sin guarda interactiva.
    /// </summary>
    Task<int> RunScheduledAutoLinkExistingExpansionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Asegura y sincroniza de forma puntual el snapshot crudo de un juego específico si no existe previamente.
    /// </summary>
    Task<bool> EnsureSnapshotAsync(int bggId, CancellationToken ct = default);

    /// <summary>
    /// Re-evalúa masivamente todos los snapshots crudos de BGG en el catálogo caliente:
    /// reclasifica como GameType.Expansion aquellos que en BGG son expansiones y los vincula bidireccionalmente a su juego base.
    /// </summary>
    Task<BggExpansionReconciliationResultDto> ReconcileAndLinkExpansionsFromSnapshotsAsync(int batchSize = 200, CancellationToken ct = default);

    /// <summary>
    /// Versión de sistema para reconciliación y vinculación de expansiones sin guarda interactiva.
    /// </summary>
    Task<BggExpansionReconciliationResultDto> RunScheduledReconcileAndLinkExpansionsFromSnapshotsAsync(int batchSize = 200, CancellationToken ct = default);

    /// <summary>
    /// Sincroniza versiones (&amp;versions=1) desde BGG para los snapshots existentes que carecen de ellas,
    /// actualizando en caliente el título en español, editorial y EAN de los juegos correspondientes.
    /// </summary>
    Task<BggRawSnapshotSyncResultDto> SyncVersionsBatchAsync(int batchSize = 20, int delayMs = 1200, CancellationToken ct = default)
        => Task.FromResult(new BggRawSnapshotSyncResultDto(0, 0, 0, [], []));

    /// <summary>
    /// Versión de sistema para sincronización de versiones sin guarda interactiva.
    /// </summary>
    Task<BggRawSnapshotSyncResultDto> RunScheduledSyncVersionsBatchAsync(int batchSize = 20, int delayMs = 1200, CancellationToken ct = default)
        => Task.FromResult(new BggRawSnapshotSyncResultDto(0, 0, 0, [], []));

    /// <summary>
    /// Barrido local (100% offline) de snapshots que contienen versiones para extraer y actualizar en catálogo
    /// títulos en español, editoriales y códigos de barras EAN-13 normalizados.
    /// </summary>
    Task<BggVersionCatalogSweepResultDto> SweepCatalogFromVersionsAsync(int batchSize = 200, int lastBggId = 0, CancellationToken ct = default)
        => Task.FromResult(new BggVersionCatalogSweepResultDto(0, 0, 0, 0, 0, lastBggId, false, string.Empty));

    /// <summary>
    /// Versión de sistema para barrido local de versiones sin guarda interactiva.
    /// </summary>
    Task<BggVersionCatalogSweepResultDto> RunScheduledSweepCatalogFromVersionsAsync(int batchSize = 200, int lastBggId = 0, CancellationToken ct = default)
        => Task.FromResult(new BggVersionCatalogSweepResultDto(0, 0, 0, 0, 0, lastBggId, false, string.Empty));
}
