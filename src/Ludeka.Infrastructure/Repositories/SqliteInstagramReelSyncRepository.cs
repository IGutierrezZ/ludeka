using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Repositories;

public class SqliteInstagramReelSyncRepository : IInstagramReelSyncRepository
{
    private readonly Ludeka.Infrastructure.Data.LudekaDbContext _db;

    public SqliteInstagramReelSyncRepository(Ludeka.Infrastructure.Data.LudekaDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<InstagramReelSyncItem?> GetByInstagramMediaIdAsync(string instagramMediaId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(instagramMediaId))
            return null;

        var cleanId = instagramMediaId.Trim();
        return await _db.InstagramReelSyncItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.InstagramMediaId == cleanId, ct);
    }

    public async Task<InstagramReelSyncItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.InstagramReelSyncItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<IReadOnlyList<InstagramReelSyncItem>> GetPendingForUploadAsync(
        int maxItems,
        int maxAttempts = 3,
        CancellationToken ct = default)
    {
        var effectiveMax = Math.Clamp(maxItems, 1, 50);

        // Materializamos los candidatos pendientes/fallidos reintentables y ordenamos en memoria
        // por DateTimeOffset (INC-34: evitar ORDER BY DateTimeOffset en SQLite)
        var candidates = await _db.InstagramReelSyncItems
            .AsNoTracking()
            .Where(x => x.Status == InstagramReelSyncStatus.Pending ||
                        (x.Status == InstagramReelSyncStatus.Failed && x.AttemptCount < maxAttempts))
            .ToListAsync(ct);

        return candidates
            .OrderBy(x => x.MediaPublishedAtUtc)
            .ThenBy(x => x.CreatedAtUtc)
            .Take(effectiveMax)
            .ToList();
    }

    public async Task<IReadOnlyList<InstagramReelSyncItem>> GetAllAsync(
        InstagramReelSyncStatus? statusFilter = null,
        int limit = 100,
        CancellationToken ct = default)
    {
        var query = _db.InstagramReelSyncItems.AsNoTracking().AsQueryable();

        if (statusFilter.HasValue)
        {
            query = query.Where(x => x.Status == statusFilter.Value);
        }

        var list = await query.ToListAsync(ct);

        return list
            .OrderByDescending(x => x.MediaPublishedAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(Math.Clamp(limit, 1, 500))
            .ToList();
    }

    public async Task AddAsync(InstagramReelSyncItem item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        await _db.InstagramReelSyncItems.AddAsync(item, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(InstagramReelSyncItem item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        var existing = await _db.InstagramReelSyncItems.FirstOrDefaultAsync(x => x.Id == item.Id, ct);
        if (existing != null)
        {
            _db.Entry(existing).CurrentValues.SetValues(item);
            await _db.SaveChangesAsync(ct);
        }
    }
}
