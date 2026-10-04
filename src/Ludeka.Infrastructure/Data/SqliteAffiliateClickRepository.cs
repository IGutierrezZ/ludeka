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

public class SqliteAffiliateClickRepository : DbContextRepositoryBase, IAffiliateClickRepository
{
    [ActivatorUtilitiesConstructor]
    public SqliteAffiliateClickRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteAffiliateClickRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task RecordClickAsync(AffiliateClickLog click, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(click);

        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.AffiliateClicks.AddAsync(click, ct);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task<int> GetClickCountAsync(Guid? gameId = null, string? storeName = null, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var query = scope.Context.AffiliateClicks.AsNoTracking().AsQueryable();

        if (gameId.HasValue && gameId.Value != Guid.Empty)
        {
            query = query.Where(c => c.GameId == gameId.Value);
        }

        if (!string.IsNullOrWhiteSpace(storeName))
        {
            var cleanStore = storeName.Trim();
            query = query.Where(c => c.StoreName == cleanStore);
        }

        return await query.CountAsync(ct);
    }

    public async Task<IReadOnlyList<AffiliateClickLog>> GetRecentClicksAsync(int limit = 50, CancellationToken ct = default)
    {
        if (limit <= 0) limit = 50;

        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.AffiliateClicks
            .AsNoTracking()
            .OrderByDescending(c => c.ClickedAtUtc)
            .Take(limit)
            .ToListAsync(ct);
    }
}
