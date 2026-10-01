using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Data;

/// <summary>
/// Repositorio de persistencia de tendencias diarias con soporte para SQLite y PostgreSQL vía EF Core.
/// </summary>
public class SqliteDailyTrendingGameRepository : DbContextRepositoryBase, IDailyTrendingGameRepository
{
    public SqliteDailyTrendingGameRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteDailyTrendingGameRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task<DateOnly?> GetLatestDateAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.DailyTrendingGames
            .AsNoTracking()
            .Select(t => (DateOnly?)t.DateUtc)
            .MaxAsync(ct);
    }

    public async Task<DateOnly?> GetPreviousDateAsync(DateOnly dateUtc, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.DailyTrendingGames
            .AsNoTracking()
            .Where(t => t.DateUtc < dateUtc)
            .Select(t => (DateOnly?)t.DateUtc)
            .MaxAsync(ct);
    }

    public async Task<IReadOnlyList<DailyTrendingGame>> GetTrendingByDateAsync(DateOnly dateUtc, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.DailyTrendingGames
            .Include(t => t.Game)
            .AsNoTracking()
            .Where(t => t.DateUtc == dateUtc)
            .OrderBy(t => t.Rank)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DailyTrendingGame>> GetLatestTrendingAsync(int limit = 50, CancellationToken ct = default)
    {
        if (limit <= 0) limit = 50;

        await using var scope = await CreateScopeAsync(ct);
        var latestDate = await scope.Context.DailyTrendingGames
            .AsNoTracking()
            .Select(t => (DateOnly?)t.DateUtc)
            .MaxAsync(ct);

        if (!latestDate.HasValue)
        {
            return Array.Empty<DailyTrendingGame>();
        }

        return await scope.Context.DailyTrendingGames
            .Include(t => t.Game)
            .AsNoTracking()
            .Where(t => t.DateUtc == latestDate.Value)
            .OrderBy(t => t.Rank)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task UpsertDailyTrendingBatchAsync(IEnumerable<DailyTrendingGame> items, CancellationToken ct = default)
    {
        var itemList = items.ToList();
        if (itemList.Count == 0) return;

        await using var scope = await CreateScopeAsync(ct);

        var dates = itemList.Select(i => i.DateUtc).Distinct().ToList();
        var existing = await scope.Context.DailyTrendingGames
            .Where(t => dates.Contains(t.DateUtc))
            .ToListAsync(ct);

        foreach (var item in itemList)
        {
            var match = existing.FirstOrDefault(e => e.DateUtc == item.DateUtc && (e.Rank == item.Rank || e.BggId == item.BggId));
            if (match != null)
            {
                // Si el item nuevo ya trae GameId y el match no lo tenía, actualizarlo
                if (item.GameId.HasValue && !match.GameId.HasValue)
                {
                    match.LinkToGame(item.GameId.Value);
                }
            }
            else
            {
                await scope.Context.DailyTrendingGames.AddAsync(item, ct);
                existing.Add(item);
            }
        }

        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task LinkGameAsync(int bggId, Guid gameId, CancellationToken ct = default)
    {
        if (bggId <= 0 || gameId == Guid.Empty) return;

        await using var scope = await CreateScopeAsync(ct);
        var records = await scope.Context.DailyTrendingGames
            .Where(t => t.BggId == bggId && t.GameId == null)
            .ToListAsync(ct);

        if (records.Count == 0) return;

        foreach (var record in records)
        {
            record.LinkToGame(gameId);
        }

        await scope.Context.SaveChangesAsync(ct);
    }
}
