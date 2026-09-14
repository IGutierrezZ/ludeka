using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Contracts;

public interface IMonitoredAccountRepository
{
    Task<IReadOnlyList<MonitoredSocialAccount>> GetAllAsync(
        SocialPlatform? platform = null,
        MonitoredAccountType? type = null,
        bool? onlyEnabled = null,
        CancellationToken ct = default);

    Task<MonitoredSocialAccount?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<MonitoredSocialAccount> AddAsync(MonitoredSocialAccount account, CancellationToken ct = default);
    Task UpdateAsync(MonitoredSocialAccount account, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsAsync(SocialPlatform platform, string handleOrChannelId, CancellationToken ct = default);
}
