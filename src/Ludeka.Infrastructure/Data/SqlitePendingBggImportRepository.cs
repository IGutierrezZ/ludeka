using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Data;

public class SqlitePendingBggImportRepository : DbContextRepositoryBase, IPendingBggImportRepository
{
    public SqlitePendingBggImportRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqlitePendingBggImportRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task<PendingBggImport?> GetByBggIdAsync(int bggId, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.PendingBggImports
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.BggId == bggId, ct);
    }

    public async Task<IReadOnlyList<PendingBggImport>> GetTopPendingAsync(int limit = 50, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.PendingBggImports
            .AsNoTracking()
            .Where(p => p.Status == CatalogQueueStatus.Pending || p.Status == CatalogQueueStatus.Failed)
            .OrderBy(p => p.Status == CatalogQueueStatus.Failed ? 1 : 0)
            .ThenByDescending(p => p.RequestedCount)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PendingBggImport>> GetAllAsync(CatalogQueueStatus? status = null, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var query = scope.Context.PendingBggImports.AsNoTracking().AsQueryable();
        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        return await query
            .OrderByDescending(p => p.RequestedCount)
            .ToListAsync(ct);
    }

    public async Task<int> GetTotalPendingCountAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.PendingBggImports
            .CountAsync(p => p.Status == CatalogQueueStatus.Pending || p.Status == CatalogQueueStatus.Failed, ct);
    }

    public async Task AddAsync(PendingBggImport item, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.PendingBggImports.AddAsync(item, ct);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(PendingBggImport item, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        scope.Context.PendingBggImports.Update(item);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task ResetFailedToPendingAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var failedItems = await scope.Context.PendingBggImports
            .Where(p => p.Status == CatalogQueueStatus.Failed)
            .ToListAsync(ct);

        foreach (var item in failedItems)
        {
            item.ResetToPending();
        }

        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task<int> RecoverStaleProcessingToPendingAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var staleItems = await scope.Context.PendingBggImports
            .Where(p => p.Status == CatalogQueueStatus.Processing)
            .ToListAsync(ct);

        if (staleItems.Count == 0) return 0;

        foreach (var item in staleItems)
        {
            item.ResetToPending();
        }

        await scope.Context.SaveChangesAsync(ct);
        return staleItems.Count;
    }
}
