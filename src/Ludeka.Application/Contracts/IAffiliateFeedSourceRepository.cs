using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Entities;

namespace Ludeka.Application.Contracts;

public interface IAffiliateFeedSourceRepository
{
    Task<List<AffiliateFeedSource>> GetAllAsync(CancellationToken ct = default);
    Task<List<AffiliateFeedSource>> GetActiveSourcesAsync(CancellationToken ct = default);
    Task<AffiliateFeedSource?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AffiliateFeedSource?> GetByStoreNameAsync(string storeName, CancellationToken ct = default);
    Task<AffiliateFeedSource> AddAsync(AffiliateFeedSource source, CancellationToken ct = default);
    Task UpdateAsync(AffiliateFeedSource source, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
