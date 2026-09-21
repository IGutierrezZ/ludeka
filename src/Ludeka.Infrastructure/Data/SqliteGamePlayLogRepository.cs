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

public class SqliteGamePlayLogRepository : DbContextRepositoryBase, IGamePlayLogRepository
{
    public SqliteGamePlayLogRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteGamePlayLogRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task AddAsync(GamePlayLog play, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        await scope.Context.GamePlayLogs.AddAsync(play, cancellationToken);
        await scope.Context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        var play = await scope.Context.GamePlayLogs.FindAsync([id], cancellationToken);
        if (play != null)
        {
            scope.Context.GamePlayLogs.Remove(play);
            await scope.Context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<GamePlayLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        return await scope.Context.GamePlayLogs
            .Include(p => p.Game)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<List<GamePlayLog>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        // EF Core SQLite no traduce ORDER BY sobre DateTimeOffset: se materializa primero y se ordena en memoria.
        await using var scope = await CreateScopeAsync(cancellationToken);
        var logs = await scope.Context.GamePlayLogs
            .Include(p => p.Game)
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);

        return logs
            .OrderByDescending(p => p.PlayDate)
            .ThenByDescending(p => p.CreatedAt)
            .ToList();
    }

    public async Task<List<GamePlayLog>> GetByUserAndGameAsync(string userId, Guid gameId, CancellationToken cancellationToken = default)
    {
        // EF Core SQLite no traduce ORDER BY sobre DateTimeOffset: se materializa primero y se ordena en memoria.
        await using var scope = await CreateScopeAsync(cancellationToken);
        var logs = await scope.Context.GamePlayLogs
            .Include(p => p.Game)
            .Where(p => p.UserId == userId && p.GameId == gameId)
            .ToListAsync(cancellationToken);

        return logs
            .OrderByDescending(p => p.PlayDate)
            .ThenByDescending(p => p.CreatedAt)
            .ToList();
    }

    public async Task<int> GetCountByUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        return await scope.Context.GamePlayLogs
            .CountAsync(p => p.UserId == userId, cancellationToken);
    }

    public async Task<int> GetCountByUserAndGameAsync(string userId, Guid gameId, CancellationToken cancellationToken = default)
    {
        await using var scope = await CreateScopeAsync(cancellationToken);
        return await scope.Context.GamePlayLogs
            .CountAsync(p => p.UserId == userId && p.GameId == gameId, cancellationToken);
    }
}
