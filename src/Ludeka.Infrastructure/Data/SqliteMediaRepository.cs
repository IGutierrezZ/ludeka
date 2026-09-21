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

public class SqliteMediaRepository : DbContextRepositoryBase, IMediaRepository
{
    public SqliteMediaRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteMediaRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task<MediaItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.MediaItems
            .Include(m => m.Game)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<IReadOnlyList<MediaItem>> GetApprovedByGameIdAsync(Guid gameId, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var items = await scope.Context.MediaItems
            .Include(m => m.Game)
            .Where(m => m.GameId == gameId && m.Status == ModerationStatus.Approved)
            .ToListAsync(ct);

        // En SQLite el ordenamiento por DateTimeOffset se ejecuta en memoria
        return items.OrderByDescending(m => m.PublishedAt).ToList();
    }

    public async Task<IReadOnlyList<MediaItem>> GetPendingModerationAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var items = await scope.Context.MediaItems
            .Include(m => m.Game)
            .Where(m => m.Status == ModerationStatus.PendingApproval)
            .ToListAsync(ct);

        return items.OrderByDescending(m => m.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<MediaItem>> GetOrphansAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var items = await scope.Context.MediaItems
            .Where(m => m.GameId == null)
            .ToListAsync(ct);

        return items.OrderByDescending(m => m.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<MediaItem>> GetApprovedAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var items = await scope.Context.MediaItems
            .Include(m => m.Game)
            .Where(m => m.Status == ModerationStatus.Approved)
            .ToListAsync(ct);

        return items.OrderByDescending(m => m.PublishedAt).ToList();
    }

    public async Task<IReadOnlyList<MediaItem>> GetAllAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var items = await scope.Context.MediaItems
            .Include(m => m.Game)
            .ToListAsync(ct);

        return items.OrderByDescending(m => m.CreatedAt).ToList();
    }

    public async Task<bool> ExistsByUrlAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var trimmed = url.Trim();
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.MediaItems.AnyAsync(m => m.Url == trimmed, ct);
    }

    public async Task AddAsync(MediaItem item, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.MediaItems.AddAsync(item, ct);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(MediaItem item, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        scope.Context.MediaItems.Update(item);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var item = await scope.Context.MediaItems.FindAsync([id], ct);
        if (item != null)
        {
            scope.Context.MediaItems.Remove(item);
            await scope.Context.SaveChangesAsync(ct);
        }
    }
}
