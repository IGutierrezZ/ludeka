using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ludeka.Infrastructure.Data;

public class SqliteGiveawayRepository : DbContextRepositoryBase, IGiveawayRepository
{
    public SqliteGiveawayRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteGiveawayRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Giveaway>> GetGiveawaysAsync(bool includeExpired = false, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var now = DateTimeOffset.UtcNow;

        if (scope.Context.Database.IsSqlite())
        {
            var list = await scope.Context.Giveaways
                .AsNoTracking()
                .ToListAsync(ct);

            if (!includeExpired)
            {
                list = list.Where(g => g.DeadlineAt >= now).ToList();
            }

            return list.OrderBy(g => g.DeadlineAt).ToList();
        }

        var query = scope.Context.Giveaways.AsNoTracking().AsQueryable();

        if (!includeExpired)
        {
            query = query.Where(g => g.DeadlineAt >= now);
        }

        return await query
            .OrderBy(g => g.DeadlineAt)
            .ToListAsync(ct);
    }

    public async Task<Giveaway?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.Giveaways
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id, ct);
    }

    public async Task<Giveaway?> FindDuplicateOrCollaborativeAsync(string title, string organizer, DateTimeOffset deadline, CancellationToken ct = default)
    {
        var cleanTitle = title.Trim().ToLowerInvariant();
        var cleanOrganizer = organizer.Trim().ToLowerInvariant();

        await using var scope = await CreateScopeAsync(ct);
        var candidates = await scope.Context.Giveaways
            .AsNoTracking()
            .ToListAsync(ct);

        return candidates.FirstOrDefault(g =>
            !g.IsExpired &&
            g.Title.Trim().ToLowerInvariant().Equals(cleanTitle, StringComparison.OrdinalIgnoreCase) &&
            (g.Organizer.Trim().ToLowerInvariant().Contains(cleanOrganizer) ||
             cleanOrganizer.Contains(g.Organizer.Trim().ToLowerInvariant()) ||
             (g.Collaborator != null && g.Collaborator.Trim().ToLowerInvariant().Contains(cleanOrganizer))));
    }

    public async Task AddAsync(Giveaway giveaway, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(giveaway);
        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.Giveaways.AddAsync(giveaway, ct);
        if (giveaway.Game != null)
        {
            scope.Context.Entry(giveaway.Game).State = EntityState.Unchanged;
        }
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Giveaway giveaway, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(giveaway);
        await using var scope = await CreateScopeAsync(ct);
        var existing = await scope.Context.Giveaways.FirstOrDefaultAsync(g => g.Id == giveaway.Id, ct);
        if (existing != null)
        {
            scope.Context.Entry(existing).CurrentValues.SetValues(giveaway);
            await scope.Context.SaveChangesAsync(ct);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var item = await scope.Context.Giveaways.FindAsync(new object[] { id }, ct);
        if (item != null)
        {
            scope.Context.Giveaways.Remove(item);
            await scope.Context.SaveChangesAsync(ct);
        }
    }
}
