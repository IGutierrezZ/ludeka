using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Data;

/// <summary>
/// Repositorio para la tabla satélite de payloads brutos de BGG con soporte para SQLite y PostgreSQL.
/// </summary>
public class SqliteBggRawSnapshotRepository : DbContextRepositoryBase, IBggRawSnapshotRepository
{
    public SqliteBggRawSnapshotRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteBggRawSnapshotRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task<BggRawSnapshot?> GetByBggIdAsync(int bggId, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.BggRawSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.BggId == bggId, ct);
    }

    public async Task UpsertAsync(BggRawSnapshot snapshot, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var existing = await scope.Context.BggRawSnapshots
            .FirstOrDefaultAsync(s => s.BggId == snapshot.BggId, ct);

        if (existing != null)
        {
            existing.UpdatePayload(snapshot.RawJson, snapshot.ApiVersion);
        }
        else
        {
            await scope.Context.BggRawSnapshots.AddAsync(snapshot, ct);
        }

        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<int>> GetMissingBggIdsAsync(int limit = 50, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);

        var existingSnapshotBggIds = scope.Context.BggRawSnapshots.Select(s => s.BggId);

        return await scope.Context.Games
            .AsNoTracking()
            .Where(g => g.BggId > 0 && !existingSnapshotBggIds.Contains(g.BggId))
            .OrderBy(g => g.BggRank.HasValue ? 0 : 1)
            .ThenBy(g => g.BggRank ?? int.MaxValue)
            .Select(g => g.BggId)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<int> GetCountAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.BggRawSnapshots.CountAsync(ct);
    }

    public async Task<int> GetTotalGamesWithBggIdCountAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.Games.CountAsync(g => g.BggId > 0, ct);
    }

    public async Task<IReadOnlyList<BggRawSnapshot>> GetAllSnapshotsAsync(int limit = 500, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.BggRawSnapshots
            .AsNoTracking()
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BggRawSnapshot>> GetSnapshotsAfterBggIdAsync(int lastBggId, int limit = 200, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        return await scope.Context.BggRawSnapshots
            .AsNoTracking()
            .Where(s => s.BggId > lastBggId)
            .OrderBy(s => s.BggId)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<int>> GetBggIdsMissingVersionsAsync(int limit = 50, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        if (scope.Context.Database.IsNpgsql())
        {
            var connection = scope.Context.Database.GetDbConnection();
            bool shouldClose = false;
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(ct);
                shouldClose = true;
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT "BggId"
                      FROM "BggRawSnapshots"
                     WHERE NOT jsonb_exists("RawJson", 'versions')
                       AND NOT jsonb_exists("RawJson", 'notFound')
                     ORDER BY "BggId"
                     LIMIT @limit;
                    """;
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@limit";
                parameter.Value = limit;
                command.Parameters.Add(parameter);

                var list = new List<int>();
                using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    list.Add(reader.GetInt32(0));
                }
                return list.AsReadOnly();
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }

        return await scope.Context.BggRawSnapshots
            .AsNoTracking()
            .Where(s => !s.RawJson.Contains("\"versions\"") && !s.RawJson.Contains("\"notFound\":true"))
            .OrderBy(s => s.BggId)
            .Select(s => s.BggId)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<int> GetCountWithVersionsAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        if (scope.Context.Database.IsNpgsql())
        {
            var connection = scope.Context.Database.GetDbConnection();
            bool shouldClose = false;
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(ct);
                shouldClose = true;
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*)::int FROM \"BggRawSnapshots\" WHERE jsonb_exists(\"RawJson\", 'versions');";
                var result = await command.ExecuteScalarAsync(ct);
                return result is int count ? count : Convert.ToInt32(result);
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }

        return await scope.Context.BggRawSnapshots
            .CountAsync(s => s.RawJson.Contains("\"versions\""), ct);
    }

    public async Task<IReadOnlyList<BggRawSnapshot>> GetSnapshotsByBggIdsAsync(IEnumerable<int> bggIds, CancellationToken ct = default)
    {
        var idList = bggIds.Distinct().ToList();
        if (idList.Count == 0) return [];
        await using var scope = await CreateScopeAsync(ct);

        return await scope.Context.BggRawSnapshots
            .AsNoTracking()
            .Where(s => idList.Contains(s.BggId))
            .ToListAsync(ct);
    }
}

