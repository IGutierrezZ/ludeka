using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Entities;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Métrica y resultado resumido del proceso de sincronización de un feed de afiliado.
/// </summary>
public record FeedSyncResult(
    Guid SourceId,
    string StoreName,
    bool Success,
    int ItemsRead,
    int MatchedCount,
    int AutoAssignedEanCount,
    int DiscrepanciesCount,
    string? ErrorMessage = null);

/// <summary>
/// Orquestador del ciclo de vida de ingesta, cruce por código de barras y actualización de ofertas para feeds comerciales.
/// </summary>
public interface ICatalogFeedSyncService
{
    /// <summary>
    /// Sincroniza una fuente específica de feed comercial.
    /// </summary>
    Task<FeedSyncResult> SyncFeedSourceAsync(AffiliateFeedSource source, CancellationToken ct = default);

    /// <summary>
    /// Busca la fuente por ID y ejecuta su sincronización si está registrada.
    /// </summary>
    Task<FeedSyncResult> SyncFeedSourceByIdAsync(Guid sourceId, CancellationToken ct = default);

    /// <summary>
    /// Sincroniza secuencialmente todas las fuentes activas configuradas.
    /// </summary>
    Task<List<FeedSyncResult>> SyncAllActiveFeedsAsync(CancellationToken ct = default);
}
