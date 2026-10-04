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

public class SqliteAffiliateFeedSourceRepository : DbContextRepositoryBase, IAffiliateFeedSourceRepository
{
    [ActivatorUtilitiesConstructor]
    public SqliteAffiliateFeedSourceRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteAffiliateFeedSourceRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task<List<AffiliateFeedSource>> GetAllAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.AffiliateFeedSources
            .AsNoTracking()
            .OrderBy(s => s.StoreName)
            .ToListAsync(ct);
    }

    public async Task<List<AffiliateFeedSource>> GetActiveSourcesAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.AffiliateFeedSources
            .AsNoTracking()
            .Where(s => s.IsEnabled)
            .OrderBy(s => s.StoreName)
            .ToListAsync(ct);
    }

    public async Task<AffiliateFeedSource?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.AffiliateFeedSources
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<AffiliateFeedSource?> GetByStoreNameAsync(string storeName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storeName)) return null;
        var clean = storeName.Trim();

        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.AffiliateFeedSources
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.StoreName == clean, ct);
    }

    public async Task<AffiliateFeedSource> AddAsync(AffiliateFeedSource source, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.AffiliateFeedSources.AddAsync(source, ct);
        await scope.Context.SaveChangesAsync(ct);
        return source;
    }

    public async Task UpdateAsync(AffiliateFeedSource source, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        await using var scope = await CreateScopeAsync(ct);
        scope.Context.AffiliateFeedSources.Update(source);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var existing = await scope.Context.AffiliateFeedSources.FindAsync([id], ct);
        if (existing != null)
        {
            scope.Context.AffiliateFeedSources.Remove(existing);
            await scope.Context.SaveChangesAsync(ct);
        }
    }
}
