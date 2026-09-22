using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Ludeka.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Repositories;

public class SqliteCommunityNotificationRepository : DbContextRepositoryBase, ICommunityNotificationRepository
{
    public SqliteCommunityNotificationRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteCommunityNotificationRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<CommunityNotificationLog>> GetRecentLogsAsync(
        int take = 50,
        CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var logs = await scope.Context.NotificationLogs
            .AsNoTracking()
            .ToListAsync(ct);

        return logs
            .OrderByDescending(l => l.CreatedAt)
            .Take(take)
            .ToList();
    }

    public async Task<CommunityNotificationLog?> GetByIdAsync(
        Guid id,
        CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.NotificationLogs
            .FirstOrDefaultAsync(l => l.Id == id, ct);
    }

    public async Task AddLogAsync(
        CommunityNotificationLog log,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.NotificationLogs.AddAsync(log, ct);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task UpdateLogAsync(
        CommunityNotificationLog log,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        await using var scope = await CreateScopeAsync(ct);
        scope.Context.NotificationLogs.Update(log);
        await scope.Context.SaveChangesAsync(ct);
    }
}
