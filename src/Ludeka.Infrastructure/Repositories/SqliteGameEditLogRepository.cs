using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Ludeka.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Repositories;

public class SqliteGameEditLogRepository : DbContextRepositoryBase, IGameEditLogRepository
{
    public SqliteGameEditLogRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteGameEditLogRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task AddAsync(GameEditLog log, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.GameEditLogs.AddAsync(log, ct);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<GameEditLog>> GetByGameIdAsync(Guid gameId, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        // EF Core SQLite no traduce ORDER BY sobre DateTimeOffset: se materializa primero y se ordena en memoria.
        var logs = await scope.Context.GameEditLogs
            .AsNoTracking()
            .Where(l => l.GameId == gameId)
            .ToListAsync(ct);

        return logs
            .OrderByDescending(l => l.EditedAt)
            .ThenByDescending(l => l.Id)
            .ToList();
    }
}
