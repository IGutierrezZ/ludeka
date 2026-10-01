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

public class SqliteBoardGameEventRepository : DbContextRepositoryBase, IBoardGameEventRepository
{
    public SqliteBoardGameEventRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteBoardGameEventRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<BoardGameEvent>> GetUpcomingEventsAsync(int limit = 20, CancellationToken ct = default)
    {
        if (limit < 1) limit = 20;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await using var scope = await CreateScopeAsync(ct);
        // Solo eventos vigentes o futuros (EndDate >= hoy) ordenados por StartDate ascendente
        return await scope.Context.BoardGameEvents
            .AsNoTracking()
            .Where(e => e.EndDate >= today)
            .OrderBy(e => e.StartDate)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BoardGameEvent>> GetAllEventsAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.BoardGameEvents
            .AsNoTracking()
            .OrderBy(e => e.StartDate)
            .ToListAsync(ct);
    }

    public async Task<BoardGameEvent?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.BoardGameEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task AddAsync(BoardGameEvent boardGameEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(boardGameEvent);
        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.BoardGameEvents.AddAsync(boardGameEvent, ct);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(BoardGameEvent boardGameEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(boardGameEvent);
        await using var scope = await CreateScopeAsync(ct);
        scope.Context.BoardGameEvents.Update(boardGameEvent);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var entity = await scope.Context.BoardGameEvents.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entity != null)
        {
            scope.Context.BoardGameEvents.Remove(entity);
            await scope.Context.SaveChangesAsync(ct);
        }
    }
}
