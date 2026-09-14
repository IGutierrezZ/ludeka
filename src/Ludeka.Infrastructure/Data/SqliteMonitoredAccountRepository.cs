using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Data;

public class SqliteMonitoredAccountRepository : IMonitoredAccountRepository
{
    private readonly LudekaDbContext _context;

    public SqliteMonitoredAccountRepository(LudekaDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<MonitoredSocialAccount>> GetAllAsync(
        SocialPlatform? platform = null,
        MonitoredAccountType? type = null,
        bool? onlyEnabled = null,
        CancellationToken ct = default)
    {
        var query = _context.MonitoredSocialAccounts.AsQueryable();

        if (platform.HasValue)
        {
            query = query.Where(a => a.Platform == platform.Value);
        }

        if (type.HasValue)
        {
            query = query.Where(a => a.AccountType == type.Value);
        }

        if (onlyEnabled.HasValue)
        {
            query = query.Where(a => a.IsEnabled == onlyEnabled.Value);
        }

        var list = await query.ToListAsync(ct);
        return list.OrderBy(a => a.Name).ToList();
    }

    public async Task<MonitoredSocialAccount?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.MonitoredSocialAccounts
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<MonitoredSocialAccount> AddAsync(MonitoredSocialAccount account, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        await _context.MonitoredSocialAccounts.AddAsync(account, ct);
        await _context.SaveChangesAsync(ct);
        return account;
    }

    public async Task UpdateAsync(MonitoredSocialAccount account, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        _context.MonitoredSocialAccounts.Update(account);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var account = await GetByIdAsync(id, ct);
        if (account != null)
        {
            _context.MonitoredSocialAccounts.Remove(account);
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> ExistsAsync(SocialPlatform platform, string handleOrChannelId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(handleOrChannelId))
            return false;

        var clean = handleOrChannelId.Trim().ToLowerInvariant();
        var accounts = await _context.MonitoredSocialAccounts
            .Where(a => a.Platform == platform)
            .ToListAsync(ct);

        return accounts.Any(a => a.HandleOrChannelId.Trim().ToLowerInvariant() == clean);
    }
}
