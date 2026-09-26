using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Data;

public class SqliteLeaderboardRepository : DbContextRepositoryBase, ILeaderboardRepository
{
    public SqliteLeaderboardRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteLeaderboardRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task<List<UserMonthlyPlaysAggregate>> GetMonthlyPlaysAsync(
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);

        // SQLite no traduce operaciones relacionales ni ordenación por DateTimeOffset directamente en SQL:
        // Proyectamos datos mínimos y filtramos en memoria.
        var plays = await scope.Context.GamePlayLogs
            .AsNoTracking()
            .Select(p => new { p.UserId, p.PlayDate })
            .ToListAsync(ct);

        var aggregates = plays
            .Where(p => p.PlayDate >= periodStart && p.PlayDate < periodEnd)
            .GroupBy(p => p.UserId, StringComparer.OrdinalIgnoreCase)
            .Select(g => new UserMonthlyPlaysAggregate(
                UserId: g.Key,
                PlayCount: g.Count(),
                FirstPlayDate: g.Min(p => p.PlayDate)))
            .ToList();

        return aggregates;
    }
}
