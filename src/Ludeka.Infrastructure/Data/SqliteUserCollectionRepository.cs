using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ludeka.Infrastructure.Data;

public class SqliteUserCollectionRepository : DbContextRepositoryBase, IUserCollectionRepository
{
    public SqliteUserCollectionRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteUserCollectionRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task<UserCollectionItem?> GetByUserAndGameAsync(string userId, Guid gameId, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        return await scope.Context.CollectionItems
            .Include(c => c.Game)
            .FirstOrDefaultAsync(c => c.UserId == userId && c.GameId == gameId, cancellationToken);
    }

    public async Task<UserCollectionItem?> GetByUserAndBggIdAsync(string userId, int bggId, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        return await scope.Context.CollectionItems
            .Include(c => c.Game)
            .FirstOrDefaultAsync(c => c.UserId == userId && (c.BggId == bggId || (c.Game != null && c.Game.BggId == bggId)), cancellationToken);
    }

    public async Task<List<UserCollectionItem>> GetByUserIdAsync(string userId, CollectionStatus? status = null, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        var query = scope.Context.CollectionItems
            .Include(c => c.Game)
            .Where(c => c.UserId == userId);

        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        var list = await query.ToListAsync(cancellationToken);
        return list.OrderByDescending(c => c.AddedAt).ToList();
    }

    public async Task<List<UserCollectionItem>> GetPendingItemsByBggIdAsync(int bggId, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        return await scope.Context.CollectionItems
            .Where(c => c.BggId == bggId && c.GameId == null)
            .ToListAsync(cancellationToken);
    }

    public async Task PromotePendingItemsAsync(int bggId, Guid gameId, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        var pendingItems = await scope.Context.CollectionItems
            .Where(c => c.BggId == bggId && c.GameId == null)
            .ToListAsync(cancellationToken);

        foreach (var item in pendingItems)
        {
            item.PromoteToCataloged(gameId);
        }

        if (pendingItems.Count > 0)
        {
            await scope.Context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<Dictionary<CollectionStatus, int>> GetCountsByStatusAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        var counts = await scope.Context.CollectionItems
            .Where(c => c.UserId == userId && c.Status != null)
            .GroupBy(c => c.Status!.Value)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

        return counts;
    }

    public async Task<List<UserCollectionItem>> GetPlayedByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        // EF Core SQLite no traduce ORDER BY sobre DateTimeOffset: se materializa primero y se ordena en memoria.
        await using var scope = await CreateScopeAsync(cancellationToken);
        var items = await scope.Context.CollectionItems
            .Include(c => c.Game)
            .Where(c => c.UserId == userId && c.IsPlayed)
            .ToListAsync(cancellationToken);

        return items
            .OrderByDescending(c => c.AddedAt)
            .ThenByDescending(c => c.Id)
            .ToList();
    }

    public async Task<int> GetPlayedCountAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        return await scope.Context.CollectionItems
            .CountAsync(c => c.UserId == userId && c.IsPlayed, cancellationToken);
    }

    public async Task AddAsync(UserCollectionItem item, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        await scope.Context.CollectionItems.AddAsync(item, cancellationToken);
        await scope.Context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(UserCollectionItem item, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        scope.Context.CollectionItems.Update(item);
        await scope.Context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(UserCollectionItem item, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        scope.Context.CollectionItems.Remove(item);
        await scope.Context.SaveChangesAsync(cancellationToken);
    }
}
