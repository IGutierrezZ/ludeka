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
/// Repositorio para la persistencia y consulta de la bitácora de ejecuciones de catalogación nocturna con ámbitos efímeros.
/// </summary>
public class SqliteNightlyCatalogingLogRepository : DbContextRepositoryBase, INightlyCatalogingLogRepository
{
    public SqliteNightlyCatalogingLogRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteNightlyCatalogingLogRepository(LudekaDbContext context) : base(context)
    {
    }

    public async Task AddAsync(NightlyCatalogingExecutionLog log, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.NightlyCatalogingExecutionLogs.AddAsync(log, ct);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(NightlyCatalogingExecutionLog log, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        await using var scope = await CreateScopeAsync(ct);
        scope.Context.NightlyCatalogingExecutionLogs.Update(log);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<NightlyCatalogingExecutionLog>> GetRecentLogsAsync(int limit = 20, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var logs = await scope.Context.NightlyCatalogingExecutionLogs
            .AsNoTracking()
            .ToListAsync(ct);

        return logs
            .OrderByDescending(l => l.StartedAt)
            .ThenByDescending(l => l.Id)
            .Take(limit)
            .ToList();
    }

    public async Task<NightlyCatalogingExecutionLog?> GetLatestLogAsync(CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var logs = await scope.Context.NightlyCatalogingExecutionLogs
            .AsNoTracking()
            .ToListAsync(ct);

        return logs
            .OrderByDescending(l => l.StartedAt)
            .ThenByDescending(l => l.Id)
            .FirstOrDefault();
    }

    public async Task<int> FailStaleRunningLogsAsync(DateTimeOffset startedBefore, CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var runningLogs = await scope.Context.NightlyCatalogingExecutionLogs
            .Where(l => l.Status == "Running")
            .ToListAsync(ct);

        var staleLogs = runningLogs
            .Where(l => l.StartedAt <= startedBefore)
            .ToList();

        if (staleLogs.Count == 0) return 0;

        foreach (var log in staleLogs)
        {
            log.Fail("Ejecución interrumpida (timeout o reinicio del host)");
        }

        await scope.Context.SaveChangesAsync(ct);
        return staleLogs.Count;
    }
}
