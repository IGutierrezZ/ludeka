using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Data;

public class SqlitePublisherRepository : DbContextRepositoryBase, IPublisherRepository
{
    public SqlitePublisherRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqlitePublisherRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Publisher>> GetAllAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.Publishers
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<Publisher?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.Publishers
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<Publisher?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var normalized = slug.Trim().ToLowerInvariant();

        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.Publishers
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Slug == normalized, ct);
    }

    public async Task<Publisher?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var normalized = name.Trim();

        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.Publishers
            .AsNoTracking()
            .FirstOrDefaultAsync(p => EF.Functions.Like(p.Name, normalized), ct);
    }

    public async Task AddAsync(Publisher publisher, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(publisher);
        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.Publishers.AddAsync(publisher, ct);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Publisher publisher, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(publisher);

        await using var scope = await CreateScopeAsync(ct);
        var existing = await scope.Context.Publishers.FirstOrDefaultAsync(p => p.Id == publisher.Id, ct);
        if (existing != null)
        {
            existing.UpdateDetails(
                publisher.Name,
                publisher.Country,
                publisher.City,
                publisher.Description,
                publisher.LogoUrl,
                publisher.WebsiteUrl
            );
            existing.SetSocialLinks(publisher.SocialLinks);
            await scope.Context.SaveChangesAsync(ct);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var existing = await scope.Context.Publishers.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (existing != null)
        {
            scope.Context.Publishers.Remove(existing);
            await scope.Context.SaveChangesAsync(ct);
        }
    }
}
