using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Repositories;

public class SqliteInstagramPostDraftRepository : DbContextRepositoryBase, IInstagramPostDraftRepository
{
    public SqliteInstagramPostDraftRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteInstagramPostDraftRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<InstagramPostDraft>> GetDraftsAsync(
        InstagramPostDraftStatus? status = null,
        CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        IQueryable<InstagramPostDraft> query = scope.Context.InstagramPostDrafts.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(d => d.Status == status.Value);
        }

        // EF Core SQLite no traduce ORDER BY sobre DateTimeOffset: se materializa primero y se ordena en memoria.
        var drafts = await query.ToListAsync(ct);

        return drafts
            .OrderByDescending(d => d.CreatedAt)
            .ThenByDescending(d => d.Id)
            .ToList();
    }

    public async Task<InstagramPostDraft?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.InstagramPostDrafts
            .FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public async Task<InstagramPostDraft?> GetBySourceAsync(
        InstagramPostSourceType sourceType,
        string sourceId,
        CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.InstagramPostDrafts
            .FirstOrDefaultAsync(d => d.SourceType == sourceType && d.SourceId == sourceId, ct);
    }

    public async Task AddAsync(InstagramPostDraft draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.InstagramPostDrafts.AddAsync(draft, ct);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(InstagramPostDraft draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        await using var scope = await CreateScopeAsync(ct);
        scope.Context.InstagramPostDrafts.Update(draft);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var draft = await scope.Context.InstagramPostDrafts.FindAsync(new object[] { id }, ct);
        if (draft != null)
        {
            scope.Context.InstagramPostDrafts.Remove(draft);
            await scope.Context.SaveChangesAsync(ct);
        }
    }
}
