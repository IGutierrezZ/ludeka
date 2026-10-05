using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Contracts;

public interface IPendingBggImportRepository
{
    Task<PendingBggImport?> GetByBggIdAsync(int bggId, CancellationToken ct = default);
    Task<IReadOnlyList<PendingBggImport>> GetTopPendingAsync(int limit = 50, CancellationToken ct = default);
    Task<IReadOnlyList<PendingBggImport>> GetAllAsync(CatalogQueueStatus? status = null, CancellationToken ct = default);
    Task<int> GetTotalPendingCountAsync(CancellationToken ct = default);
    Task AddAsync(PendingBggImport item, CancellationToken ct = default);
    Task UpdateAsync(PendingBggImport item, CancellationToken ct = default);
    Task ResetFailedToPendingAsync(CancellationToken ct = default);
    Task<int> RecoverStaleProcessingToPendingAsync(CancellationToken ct = default);
}
