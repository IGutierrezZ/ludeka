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

public class SqliteMonitoredAccountRepository : DbContextRepositoryBase, IMonitoredAccountRepository
{
    public SqliteMonitoredAccountRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteMonitoredAccountRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<MonitoredSocialAccount>> GetAllAsync(
        SocialPlatform? platform = null,
        MonitoredAccountType? type = null,
        bool? onlyEnabled = null,
        CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var query = scope.Context.MonitoredSocialAccounts.AsNoTracking().AsQueryable();

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
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.MonitoredSocialAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<MonitoredSocialAccount> AddAsync(MonitoredSocialAccount account, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.MonitoredSocialAccounts.AddAsync(account, ct);
        await scope.Context.SaveChangesAsync(ct);
        return account;
    }

    public async Task UpdateAsync(MonitoredSocialAccount account, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        await using var scope = await CreateScopeAsync(ct);
        scope.Context.MonitoredSocialAccounts.Update(account);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var account = await scope.Context.MonitoredSocialAccounts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account != null)
        {
            scope.Context.MonitoredSocialAccounts.Remove(account);
            await scope.Context.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> ExistsAsync(SocialPlatform platform, string handleOrChannelId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(handleOrChannelId))
            return false;

        var clean = handleOrChannelId.Trim().ToLowerInvariant();
        await using var scope = await CreateScopeAsync(ct);
        var accounts = await scope.Context.MonitoredSocialAccounts
            .AsNoTracking()
            .Where(a => a.Platform == platform)
            .ToListAsync(ct);

        return accounts.Any(a => a.HandleOrChannelId.Trim().ToLowerInvariant() == clean);
    }
}
