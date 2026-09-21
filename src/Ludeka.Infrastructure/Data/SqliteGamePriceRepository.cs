using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Ludeka.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ludeka.Infrastructure.Data;

/// <summary>
/// Repositorio de persistencia de histórico de precios y métricas de ofertas sobre EF Core / SQLite / PostgreSQL.
/// En SQLite, el filtrado de rango y ordenamiento por DateTimeOffset se evalúa en memoria para compatibilidad de proveedores.
/// </summary>
public class SqliteGamePriceRepository : DbContextRepositoryBase, IGamePriceRepository
{
    public SqliteGamePriceRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteGamePriceRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task RecordSnapshotAsync(GamePriceSnapshot snapshot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        await using var scope = await CreateScopeAsync(ct);
        // Regla anti-saturación: evitar duplicados idénticos en las últimas 6 horas
        // EF Core SQLite no traduce comparaciones de rango sobre DateTimeOffset: se evalúa en memoria
        var candidates = await scope.Context.GamePriceSnapshots
            .Where(s => s.GameId == snapshot.GameId &&
                        s.StoreName == snapshot.StoreName &&
                        s.Price == snapshot.Price &&
                        s.InStock == snapshot.InStock)
            .ToListAsync(ct);

        var threshold = DateTimeOffset.UtcNow.AddHours(-6);
        bool isRedundant = candidates.Any(s => s.RecordedAtUtc >= threshold);

        if (isRedundant)
        {
            return;
        }

        await scope.Context.GamePriceSnapshots.AddAsync(snapshot, ct);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task RecordSnapshotsBatchAsync(IEnumerable<GamePriceSnapshot> snapshots, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        var snapshotList = snapshots.ToList();
        if (snapshotList.Count == 0) return;

        var gameIds = snapshotList.Select(s => s.GameId).Distinct().ToList();
        await using var scope = await CreateScopeAsync(ct);
        var candidates = await scope.Context.GamePriceSnapshots
            .Where(s => gameIds.Contains(s.GameId))
            .ToListAsync(ct);

        var threshold = DateTimeOffset.UtcNow.AddHours(-6);
        var recentSnapshots = candidates.Where(s => s.RecordedAtUtc >= threshold).ToList();

        var toAdd = new List<GamePriceSnapshot>();
        foreach (var candidate in snapshotList)
        {
            bool isRedundant = recentSnapshots.Any(r =>
                r.GameId == candidate.GameId &&
                r.StoreName.Equals(candidate.StoreName, StringComparison.OrdinalIgnoreCase) &&
                r.Price == candidate.Price &&
                r.InStock == candidate.InStock);

            if (!isRedundant)
            {
                toAdd.Add(candidate);
                recentSnapshots.Add(candidate); // Evitar duplicados dentro del mismo lote
            }
        }

        if (toAdd.Count > 0)
        {
            await scope.Context.GamePriceSnapshots.AddRangeAsync(toAdd, ct);
            await scope.Context.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<GamePriceSnapshot>> GetHistoryAsync(Guid gameId, int limit = 50, CancellationToken ct = default)
    {
        if (limit <= 0) limit = 50;

        await using var scope = await CreateScopeAsync(ct);
        var list = await scope.Context.GamePriceSnapshots
            .Where(s => s.GameId == gameId)
            .ToListAsync(ct);

        return list
            .OrderByDescending(s => s.RecordedAtUtc)
            .Take(limit)
            .ToList();
    }

    public async Task<GamePriceMetrics> GetMetricsAsync(Guid gameId, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var snapshots = await scope.Context.GamePriceSnapshots
            .Where(s => s.GameId == gameId)
            .ToListAsync(ct);

        var game = await scope.Context.Games
            .FirstOrDefaultAsync(g => g.Id == gameId, ct);

        return GamePriceMetrics.Calculate(gameId, snapshots, game?.PurchaseLinks);
    }

    public async Task<IReadOnlyDictionary<Guid, GamePriceMetrics>> GetMetricsBatchAsync(IEnumerable<Guid> gameIds, CancellationToken ct = default)
    {
        var idList = gameIds?.Distinct().ToList() ?? [];
        if (idList.Count == 0) return new Dictionary<Guid, GamePriceMetrics>();

        await using var scope = await CreateScopeAsync(ct);
        var snapshots = await scope.Context.GamePriceSnapshots
            .Where(s => idList.Contains(s.GameId))
            .ToListAsync(ct);

        var games = await scope.Context.Games
            .Where(g => idList.Contains(g.Id))
            .ToListAsync(ct);

        var gamesById = games.ToDictionary(g => g.Id);
        var snapshotsByGame = snapshots.GroupBy(s => s.GameId).ToDictionary(g => g.Key, g => g.ToList());

        var result = new Dictionary<Guid, GamePriceMetrics>();
        foreach (var id in idList)
        {
            gamesById.TryGetValue(id, out var game);
            snapshotsByGame.TryGetValue(id, out var gameSnapshots);

            result[id] = GamePriceMetrics.Calculate(id, gameSnapshots, game?.PurchaseLinks);
        }

        return result;
    }

    public async Task<IReadOnlyList<GamePriceSnapshot>> GetRecentSnapshotsAsync(int limit = 100, CancellationToken ct = default)
    {
        if (limit <= 0) limit = 100;

        await using var scope = await CreateScopeAsync(ct);
        var list = await scope.Context.GamePriceSnapshots
            .ToListAsync(ct);

        return list
            .OrderByDescending(s => s.RecordedAtUtc)
            .Take(limit)
            .ToList();
    }
}
