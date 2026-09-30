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

public class SqliteFoundingVerdictRepository : DbContextRepositoryBase, IFoundingVerdictRepository
{
    public SqliteFoundingVerdictRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteFoundingVerdictRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task<FoundingVerdict?> GetByGameIdAsync(Guid gameId, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.FoundingVerdicts
            .FirstOrDefaultAsync(v => v.GameId == gameId, ct);
    }

    public async Task<FoundingVerdict?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.FoundingVerdicts
            .FirstOrDefaultAsync(v => v.Id == id, ct);
    }

    public async Task AddAsync(FoundingVerdict verdict, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.FoundingVerdicts.AddAsync(verdict, ct);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(FoundingVerdict verdict, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        scope.Context.FoundingVerdicts.Update(verdict);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var item = await scope.Context.FoundingVerdicts.FindAsync([id], ct);
        if (item != null)
        {
            scope.Context.FoundingVerdicts.Remove(item);
            await scope.Context.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<FoundingVerdict>> GetAllAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        if (scope.Context.Database.IsNpgsql())
        {
            return await scope.Context.FoundingVerdicts
                .AsNoTracking()
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync(ct);
        }

        // En SQLite, el ordenamiento por DateTimeOffset se realiza en memoria defensivamente
        var list = await scope.Context.FoundingVerdicts
            .AsNoTracking()
            .ToListAsync(ct);
        return list.OrderByDescending(v => v.CreatedAt).ToList().AsReadOnly();
    }
}
