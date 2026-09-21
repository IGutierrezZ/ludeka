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

public class SqliteWeeklyReleaseRepository : DbContextRepositoryBase, IWeeklyReleaseRepository
{
    public SqliteWeeklyReleaseRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteWeeklyReleaseRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<WeeklyRelease>> GetReleasesAsync(DateOnly? fromDate = null, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var query = scope.Context.WeeklyReleases
            .Include(r => r.Game)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(r => r.ReleaseDate >= fromDate.Value);
        }

        return await query
            .OrderBy(r => r.ReleaseDate)
            .ThenBy(r => r.Title)
            .ToListAsync(ct);
    }

    public async Task<WeeklyRelease?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.WeeklyReleases
            .Include(r => r.Game)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task AddAsync(WeeklyRelease release, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.WeeklyReleases.AddAsync(release, ct);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(WeeklyRelease release, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        scope.Context.WeeklyReleases.Update(release);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var item = await scope.Context.WeeklyReleases.FindAsync(new object[] { id }, ct);
        if (item != null)
        {
            scope.Context.WeeklyReleases.Remove(item);
            await scope.Context.SaveChangesAsync(ct);
        }
    }
}
