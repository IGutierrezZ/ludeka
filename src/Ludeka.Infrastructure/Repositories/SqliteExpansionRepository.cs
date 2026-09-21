using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Ludeka.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ludeka.Infrastructure.Repositories;

public class SqliteExpansionRepository : DbContextRepositoryBase, IExpansionRepository
{
    public SqliteExpansionRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteExpansionRepository(LudekaDbContext db) : base(db)
    {
    }

    public async Task<IReadOnlyList<Game>> GetExpansionsByBaseGameIdAsync(Guid baseGameId, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var items = await scope.Context.Games
            .AsNoTracking()
            .Where(g => g.BaseGameId == baseGameId)
            .OrderBy(g => g.YearPublished)
            .ThenBy(g => g.SpanishTitle)
            .ToListAsync(ct);

        return items.AsReadOnly();
    }

    public async Task<Game?> GetExpansionWithBaseGameAsync(Guid expansionId, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.Games
            .AsNoTracking()
            .Include(g => g.BaseGame)
            .FirstOrDefaultAsync(g => g.Id == expansionId, ct);
    }

    public async Task<IReadOnlyList<ExpansionSynergy>> GetSynergiesByBaseGameIdAsync(Guid baseGameId, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var items = await scope.Context.ExpansionSynergies
            .AsNoTracking()
            .Where(s => s.BaseGameId == baseGameId)
            .ToListAsync(ct);

        return items.AsReadOnly();
    }

    public async Task<IReadOnlyList<ExpansionRecipe>> GetRecipesByBaseGameIdAsync(Guid baseGameId, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var items = await scope.Context.ExpansionRecipes
            .AsNoTracking()
            .Where(r => r.BaseGameId == baseGameId)
            .ToListAsync(ct);

        return items.AsReadOnly();
    }

    public async Task AddSynergyAsync(ExpansionSynergy synergy, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.ExpansionSynergies.AddAsync(synergy, ct);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task AddRecipeAsync(ExpansionRecipe recipe, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.ExpansionRecipes.AddAsync(recipe, ct);
        await scope.Context.SaveChangesAsync(ct);
    }
}
