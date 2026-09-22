using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Data;

public class SqliteAuditLogRepository : DbContextRepositoryBase, IAuditLogRepository
{
    public SqliteAuditLogRepository(IDbContextFactory<LudekaDbContext> factory) : base(factory)
    {
    }

    internal SqliteAuditLogRepository(LudekaDbContext db) : base(db)
    {
    }

    public async Task AddAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await using var scope = await CreateScopeAsync(ct);
        await scope.Context.AuditLogs.AddAsync(entry, ct);
        await scope.Context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetLogsAsync(
        string? userId = null,
        AuditEntityType? entityType = null,
        AuditAction? action = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        int skip = 0,
        int take = 50,
        CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var query = BuildQuery(scope.Context, userId, entityType, action);

        // EF Core SQLite no traduce ORDER BY ni las comparaciones de rango sobre DateTimeOffset:
        // se materializa con los filtros traducibles y el rango de fechas y el orden se resuelven en memoria.
        var logs = await query.AsNoTracking().ToListAsync(ct);

        if (fromDate.HasValue)
        {
            logs = logs.Where(a => a.Timestamp >= fromDate.Value).ToList();
        }

        if (toDate.HasValue)
        {
            logs = logs.Where(a => a.Timestamp <= toDate.Value).ToList();
        }

        return logs
            .OrderByDescending(a => a.Timestamp)
            .ThenByDescending(a => a.Id)
            .Skip(skip)
            .Take(take)
            .ToList();
    }

    public async Task<int> CountLogsAsync(
        string? userId = null,
        AuditEntityType? entityType = null,
        AuditAction? action = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken ct = default)
    {
        await using var scope = await CreateScopeAsync(ct);
        var query = BuildQuery(scope.Context, userId, entityType, action);

        // Mismo defecto que GetLogsAsync: el rango de fechas sobre DateTimeOffset no es
        // traducible por SQLite y se resuelve en memoria.
        var logs = await query.AsNoTracking().ToListAsync(ct);

        if (fromDate.HasValue)
        {
            logs = logs.Where(a => a.Timestamp >= fromDate.Value).ToList();
        }

        if (toDate.HasValue)
        {
            logs = logs.Where(a => a.Timestamp <= toDate.Value).ToList();
        }

        return logs.Count;
    }

    private static IQueryable<AuditLogEntry> BuildQuery(
        LudekaDbContext db,
        string? userId,
        AuditEntityType? entityType,
        AuditAction? action)
    {
        var query = db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var cleanUserId = userId.Trim().ToLowerInvariant();
            query = query.Where(a => a.UserId == cleanUserId);
        }

        if (entityType.HasValue)
        {
            query = query.Where(a => a.EntityType == entityType.Value);
        }

        if (action.HasValue)
        {
            query = query.Where(a => a.Action == action.Value);
        }

        return query;
    }
}
