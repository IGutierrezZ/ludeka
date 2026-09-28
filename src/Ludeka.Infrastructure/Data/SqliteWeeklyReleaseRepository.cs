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
            .AsNoTracking()
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
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task AddAsync(WeeklyRelease release, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(release);
        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.WeeklyReleases.AddAsync(release, ct);
        if (release.Game != null)
        {
            scope.Context.Entry(release.Game).State = EntityState.Unchanged;
        }
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(WeeklyRelease release, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(release);
        await using var scope = await CreateScopeAsync(ct);
        var existing = await scope.Context.WeeklyReleases.FirstOrDefaultAsync(r => r.Id == release.Id, ct);
        if (existing != null)
        {
            scope.Context.Entry(existing).CurrentValues.SetValues(release);
            await scope.Context.SaveChangesAsync(ct);
        }
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
