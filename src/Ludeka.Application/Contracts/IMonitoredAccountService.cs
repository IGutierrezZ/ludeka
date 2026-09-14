using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Contracts;

public interface IMonitoredAccountService
{
    Task<IReadOnlyList<MonitoredAccountDto>> GetAccountsAsync(
        SocialPlatform? platform = null,
        MonitoredAccountType? type = null,
        bool? onlyEnabled = null,
        CancellationToken ct = default);

    Task<MonitoredAccountDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<MonitoredAccountDto> CreateAccountAsync(MonitoredAccountDto dto, CancellationToken ct = default);
    Task UpdateAccountAsync(MonitoredAccountDto dto, CancellationToken ct = default);
    Task ToggleAccountStatusAsync(Guid id, bool isEnabled, CancellationToken ct = default);
    Task DeleteAccountAsync(Guid id, CancellationToken ct = default);
    Task<int> SyncFromDirectoryAsync(CancellationToken ct = default);
}
