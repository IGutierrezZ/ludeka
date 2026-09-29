using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Contracts;

public interface ISocialInboxRepository
{
    Task<IReadOnlyList<SocialInboxItem>> GetPendingAsync(SocialSubmissionType? typeFilter = null, CancellationToken ct = default);
    Task<IReadOnlyList<SocialInboxItem>> GetAllAsync(SocialInboxStatus? statusFilter = null, SocialSubmissionType? typeFilter = null, int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<SocialInboxItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<int> GetPendingCountAsync(CancellationToken ct = default);
    Task<SocialInboxItem> AddAsync(SocialInboxItem item, CancellationToken ct = default);
    Task UpdateAsync(SocialInboxItem item, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsBySourceUrlAsync(string sourceUrl, CancellationToken ct = default);
    Task<int> PurgeSimulatedAsync(CancellationToken ct = default);
}
