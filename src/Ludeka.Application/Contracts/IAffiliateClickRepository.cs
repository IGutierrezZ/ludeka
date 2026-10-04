using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Entities;

namespace Ludeka.Application.Contracts;

public interface IAffiliateClickRepository
{
    Task RecordClickAsync(AffiliateClickLog click, CancellationToken ct = default);
    Task<int> GetClickCountAsync(Guid? gameId = null, string? storeName = null, CancellationToken ct = default);
    Task<IReadOnlyList<AffiliateClickLog>> GetRecentClicksAsync(int limit = 50, CancellationToken ct = default);
}
