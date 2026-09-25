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

public class SqliteUserMilestoneRepository : DbContextRepositoryBase, IUserMilestoneRepository
{
    public SqliteUserMilestoneRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteUserMilestoneRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task<List<UserMilestone>> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return new List<UserMilestone>();

        string normalizedUserId = userId.Trim();
        await using var scope = await CreateScopeAsync(ct);
        var items = await scope.Context.UserMilestones
            .AsNoTracking()
            .Where(m => m.UserId == normalizedUserId)
            .ToListAsync(ct);

        return items.OrderBy(m => m.UnlockedAt).ToList();
    }

    public async Task<bool> HasMilestoneAsync(string userId, MilestoneType type, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        string normalizedUserId = userId.Trim();
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.UserMilestones
            .AsNoTracking()
            .AnyAsync(m => m.UserId == normalizedUserId && m.Type == type, ct);
    }

    public async Task<bool> UnlockMilestoneAsync(UserMilestone milestone, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(milestone);

        await using var scope = await CreateScopeAsync(ct);
        bool alreadyExists = await scope.Context.UserMilestones
            .AnyAsync(m => m.UserId == milestone.UserId && m.Type == milestone.Type, ct);

        if (alreadyExists)
            return false;

        await scope.Context.UserMilestones.AddAsync(milestone, ct);
        await scope.Context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> UnlockMilestonesAsync(IEnumerable<UserMilestone> milestones, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(milestones);

        var list = milestones.ToList();
        if (list.Count == 0)
            return 0;

        await using var scope = await CreateScopeAsync(ct);
        var userIds = list.Select(m => m.UserId).Distinct().ToList();

        var existing = await scope.Context.UserMilestones
            .Where(m => userIds.Contains(m.UserId))
            .Select(m => new { m.UserId, m.Type })
            .ToListAsync(ct);

        var existingSet = new HashSet<(string UserId, MilestoneType Type)>(
            existing.Select(e => (e.UserId, e.Type))
        );

        var toAdd = new List<UserMilestone>();
        foreach (var m in list)
        {
            if (existingSet.Add((m.UserId, m.Type)))
            {
                toAdd.Add(m);
            }
        }

        if (toAdd.Count == 0)
            return 0;

        await scope.Context.UserMilestones.AddRangeAsync(toAdd, ct);
        await scope.Context.SaveChangesAsync(ct);
        return toAdd.Count;
    }
}
