using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Contrato de persistencia para la tabla intermedia de staging de catálogo BGG.
/// </summary>
public interface IBggCatalogStagingRepository
{
    Task UpsertBatchAsync(IEnumerable<BggCatalogStagingItem> items, CancellationToken ct = default);

    Task<BggCatalogStagingItem?> GetByBggIdAsync(int bggId, CancellationToken ct = default);

    Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingFetchBatchAsync(int batchSize = 20, CancellationToken ct = default);

    Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingImagesBatchAsync(int batchSize = 10, CancellationToken ct = default);

    Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingAiBatchAsync(int batchSize = 10, CancellationToken ct = default);

    Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingPromotionBatchAsync(int batchSize = 50, CancellationToken ct = default);

    Task UpdateAsync(BggCatalogStagingItem item, CancellationToken ct = default);

    Task UpdateBatchAsync(IEnumerable<BggCatalogStagingItem> items, CancellationToken ct = default);

    Task<BggStagingMetricsDto> GetMetricsAsync(CancellationToken ct = default);

    Task ResetQuotaExceededStatusAsync(CancellationToken ct = default);

    Task<int> GetTotalCountAsync(CancellationToken ct = default);
}
