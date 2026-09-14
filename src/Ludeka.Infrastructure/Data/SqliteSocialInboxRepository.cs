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

public class SqliteSocialInboxRepository : ISocialInboxRepository
{
    private readonly LudekaDbContext _context;

    public SqliteSocialInboxRepository(LudekaDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<SocialInboxItem>> GetPendingAsync(SocialSubmissionType? typeFilter = null, CancellationToken ct = default)
    {
        var items = await _context.SocialInboxItems
            .Include(i => i.Game)
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
        var items = await _context.SocialInboxItems
            .Include(i => i.Game)
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
        return await _context.SocialInboxItems
            .Include(i => i.Game)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<int> GetPendingCountAsync(CancellationToken ct = default)
    {
        return await _context.SocialInboxItems
            .CountAsync(i => i.Status == SocialInboxStatus.PendingReview, ct);
    }

    public async Task<SocialInboxItem> AddAsync(SocialInboxItem item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        await _context.SocialInboxItems.AddAsync(item, ct);
        await _context.SaveChangesAsync(ct);
        return item;
    }

    public async Task UpdateAsync(SocialInboxItem item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        _context.SocialInboxItems.Update(item);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> ExistsBySourceUrlAsync(string sourceUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
            return false;

        var clean = sourceUrl.Trim();
        return await _context.SocialInboxItems
            .AnyAsync(i => i.SourceUrl == clean, ct);
    }
}
