using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Data;

public class SqliteStoreRepository : DbContextRepositoryBase, IStoreRepository
{
    public SqliteStoreRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteStoreRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Store>> GetAllAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.Stores
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync(ct);
    }

    public async Task<Store?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.Stores
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<Store?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var normalized = slug.Trim().ToLowerInvariant();

        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.Stores
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Slug == normalized, ct);
    }

    public async Task<Store?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var normalized = name.Trim();

        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.Stores
            .AsNoTracking()
            .FirstOrDefaultAsync(s => EF.Functions.Like(s.Name, normalized), ct);
    }

    public async Task AddAsync(Store store, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.Stores.AddAsync(store, ct);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Store store, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);

        await using var scope = await CreateScopeAsync(ct);
        var existing = await scope.Context.Stores.FirstOrDefaultAsync(s => s.Id == store.Id, ct);
        if (existing != null)
        {
            existing.UpdateDetails(
                store.Name,
                store.Type,
                store.City,
                store.Address,
                store.Description,
                store.LogoUrl,
                store.WebsiteUrl,
                store.AffiliateCode,
                store.HasLoyaltyProgram,
                store.Country,
                store.ShippingCountries
            );
            existing.SetSocialLinks(store.SocialLinks);
            await scope.Context.SaveChangesAsync(ct);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var existing = await scope.Context.Stores.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (existing != null)
        {
            scope.Context.Stores.Remove(existing);
            await scope.Context.SaveChangesAsync(ct);
        }
    }
}
