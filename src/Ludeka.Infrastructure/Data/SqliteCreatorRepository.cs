using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Data;

public class SqliteCreatorRepository : DbContextRepositoryBase, ICreatorRepository
{
    public SqliteCreatorRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteCreatorRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Creator>> GetAllAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.Creators
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }

    public async Task<Creator?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.Creators
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<Creator?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var normalized = slug.Trim().ToLowerInvariant();

        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.Creators
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == normalized, ct);
    }

    public async Task<Creator?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var normalized = name.Trim();

        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.Creators
            .AsNoTracking()
            .FirstOrDefaultAsync(c => EF.Functions.Like(c.Name, normalized), ct);
    }

    public async Task AddAsync(Creator creator, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(creator);
        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.Creators.AddAsync(creator, ct);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Creator creator, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(creator);

        await using var scope = await CreateScopeAsync(ct);
        var existing = await scope.Context.Creators.FirstOrDefaultAsync(c => c.Id == creator.Id, ct);
        if (existing != null)
        {
            existing.UpdateDetails(
                creator.Name,
                creator.Nationality,
                creator.Bio,
                creator.AvatarUrl,
                creator.BggPersonId,
                creator.WebsiteUrl
            );
            existing.SetSocialLinks(creator.SocialLinks);
            await scope.Context.SaveChangesAsync(ct);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var existing = await scope.Context.Creators.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (existing != null)
        {
            scope.Context.Creators.Remove(existing);
            await scope.Context.SaveChangesAsync(ct);
        }
    }
}
