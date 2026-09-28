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

public class SqliteSocialInboxRepository : DbContextRepositoryBase, ISocialInboxRepository
{
    public SqliteSocialInboxRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteSocialInboxRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<SocialInboxItem>> GetPendingAsync(SocialSubmissionType? typeFilter = null, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var items = await scope.Context.SocialInboxItems
            .AsNoTracking()
            .Where(i => i.Status == SocialInboxStatus.PendingReview)
            .ToListAsync(ct);

        if (typeFilter.HasValue)
        {
            items = items.Where(i => i.DetectedType == typeFilter.Value).ToList();
        }

        return items.OrderByDescending(i => i.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<SocialInboxItem>> GetAllAsync(
        SocialInboxStatus? statusFilter = null,
        SocialSubmissionType? typeFilter = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var items = await scope.Context.SocialInboxItems
            .AsNoTracking()
            .ToListAsync(ct);

        if (statusFilter.HasValue)
        {
            items = items.Where(i => i.Status == statusFilter.Value).ToList();
        }

        if (typeFilter.HasValue)
        {
            items = items.Where(i => i.DetectedType == typeFilter.Value).ToList();
        }

        return items
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }

    public async Task<SocialInboxItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.SocialInboxItems
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<int> GetPendingCountAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.SocialInboxItems
            .AsNoTracking()
            .CountAsync(i => i.Status == SocialInboxStatus.PendingReview, ct);
    }

    public async Task<SocialInboxItem> AddAsync(SocialInboxItem item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.SocialInboxItems.AddAsync(item, ct);
        if (item.Game != null)
        {
            scope.Context.Entry(item.Game).State = EntityState.Unchanged;
        }
        await scope.Context.SaveChangesAsync(ct);
        return item;
    }

    public async Task UpdateAsync(SocialInboxItem item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        await using var scope = await CreateScopeAsync(ct);
        var existing = await scope.Context.SocialInboxItems.FirstOrDefaultAsync(i => i.Id == item.Id, ct);
        if (existing != null)
        {
            scope.Context.Entry(existing).CurrentValues.SetValues(item);
            await scope.Context.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> ExistsBySourceUrlAsync(string sourceUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
            return false;

        var clean = sourceUrl.Trim();
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.SocialInboxItems
            .AsNoTracking()
            .AnyAsync(i => i.SourceUrl == clean, ct);
    }
}
