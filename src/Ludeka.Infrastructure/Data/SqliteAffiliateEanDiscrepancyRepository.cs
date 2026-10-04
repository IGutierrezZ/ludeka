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

public class SqliteAffiliateEanDiscrepancyRepository : DbContextRepositoryBase, IAffiliateEanDiscrepancyRepository
{
    [ActivatorUtilitiesConstructor]
    public SqliteAffiliateEanDiscrepancyRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteAffiliateEanDiscrepancyRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task<List<AffiliateEanDiscrepancyLog>> GetPendingDiscrepanciesAsync(int limit = 50, CancellationToken ct = default)
    {
        if (limit <= 0) limit = 50;

        await using var scope = await CreateScopeAsync(ct);
        var list = await scope.Context.AffiliateEanDiscrepancies
            .AsNoTracking()
            .Where(d => !d.IsResolved)
            .ToListAsync(ct);

        return list
            .OrderByDescending(d => d.DetectedAtUtc)
            .Take(limit)
            .ToList();
    }

    public async Task<AffiliateEanDiscrepancyLog?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.AffiliateEanDiscrepancies
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public async Task<AffiliateEanDiscrepancyLog?> FindExistingPendingAsync(Guid gameId, string feedEan, string storeName, CancellationToken ct = default)
    {
        if (gameId == Guid.Empty || string.IsNullOrWhiteSpace(feedEan) || string.IsNullOrWhiteSpace(storeName))
            return null;

        var cleanEan = feedEan.Trim();
        var cleanStore = storeName.Trim();

        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.AffiliateEanDiscrepancies
            .AsNoTracking()
            .FirstOrDefaultAsync(d =>
                !d.IsResolved &&
                d.GameId == gameId &&
                d.FeedEan == cleanEan &&
                d.StoreName == cleanStore, ct);
    }

    public async Task<AffiliateEanDiscrepancyLog> AddAsync(AffiliateEanDiscrepancyLog log, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(log);

        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.AffiliateEanDiscrepancies.AddAsync(log, ct);
        await scope.Context.SaveChangesAsync(ct);
        return log;
    }

    public async Task UpdateAsync(AffiliateEanDiscrepancyLog log, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(log);

        await using var scope = await CreateScopeAsync(ct);
        scope.Context.AffiliateEanDiscrepancies.Update(log);
        await scope.Context.SaveChangesAsync(ct);
    }
}
