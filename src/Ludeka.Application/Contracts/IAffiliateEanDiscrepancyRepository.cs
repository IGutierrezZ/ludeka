using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Entities;

namespace Ludeka.Application.Contracts;

public interface IAffiliateEanDiscrepancyRepository
{
    Task<List<AffiliateEanDiscrepancyLog>> GetPendingDiscrepanciesAsync(int limit = 50, CancellationToken ct = default);
    Task<AffiliateEanDiscrepancyLog?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AffiliateEanDiscrepancyLog?> FindExistingPendingAsync(Guid gameId, string feedEan, string storeName, CancellationToken ct = default);
    Task<AffiliateEanDiscrepancyLog> AddAsync(AffiliateEanDiscrepancyLog log, CancellationToken ct = default);
    Task UpdateAsync(AffiliateEanDiscrepancyLog log, CancellationToken ct = default);
}
