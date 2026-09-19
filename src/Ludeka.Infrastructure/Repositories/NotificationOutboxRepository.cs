using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Repositories;

/// <summary>
/// Persistencia y reclamación exclusiva del outbox de notificaciones (INC-47, R4a, diseño
/// §6). <see cref="ClaimPendingAsync"/> es el único método que usa un comando ADO.NET crudo y
/// parametrizado sobre <c>Database.GetDbConnection()</c>, con rama por proveedor — mismo
/// patrón que <see cref="SqliteSchemaMigrator"/> (<c>SqliteSchemaMigrator.cs:19-40</c>).
/// Ni <c>FromSqlRaw</c> ni <c>SqlQueryRaw</c> componen un <c>UPDATE ... RETURNING</c> desde
/// LINQ (diseño §6.4), así que esa es la única excepción; el resto de métodos usa LINQ/EF
/// Core normal.
/// </summary>
public class NotificationOutboxRepository : INotificationOutboxRepository
{
    private readonly LudekaDbContext _db;

    public NotificationOutboxRepository(LudekaDbContext db)
    {
        _db = db;
    }

    public async Task EnqueueAsync(NotificationOutboxMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        await _db.NotificationOutboxMessages.AddAsync(message, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<OutboxClaim>> ClaimPendingAsync(
        int batchSize, string claimedBy, TimeSpan lease, CancellationToken ct = default)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = false;

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
            shouldClose = true;
        }

        try
        {
            var isSqlite = _db.Database.IsSqlite();
            var now = DateTimeOffset.UtcNow;

            using var command = connection.CreateCommand();
            command.CommandText = isSqlite ? SqliteClaimSql : NpgsqlClaimSql;

            // Solo DbParameter, nunca interpolación de cadenas (matriz de amenazas §14,
            // "Inyección SQL en la reclamación").
            AddParameter(command, "@ahora", now);
            AddParameter(command, "@tamanoLote", batchSize);
            AddParameter(command, "@reclamadoPor", claimedBy);

            if (isSqlite)
            {
                // SQLite (modo degradado, diseño §6.4): sin aritmética de fechas en texto, el
                // "hasta cuándo" de la concesión llega ya calculado desde C#.
                AddParameter(command, "@hasta", now.Add(lease));
            }
            else
            {
                // PostgreSQL: aritmética de intervalo en la propia sentencia (diseño §6.3);
                // @concesion viaja como TimeSpan -> interval.
                AddParameter(command, "@concesion", lease);
            }

            var claims = new List<OutboxClaim>();
            using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                claims.Add(MapClaim(reader, isSqlite));
            }

            return claims;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<IReadOnlyList<CommunityNotificationLog>> GetDeliveriesAsync(
        Guid messageId, CancellationToken ct = default)
    {
        return await _db.NotificationLogs
            .AsNoTracking()
            .Where(l => l.MessageId == messageId)
            .ToListAsync(ct);
    }

    public async Task<CommunityNotificationLog> EnsureDeliveryAsync(
        Guid messageId, NotificationChannel channel, OutboxClaim claim, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(claim);

        var existing = await _db.NotificationLogs
            .FirstOrDefaultAsync(l => l.MessageId == messageId && l.Channel == channel, ct);

        if (existing is not null)
        {
            return existing;
        }

        var delivery = CommunityNotificationLog.ForDelivery(
            messageId, claim.EventType, channel, claim.Title, claim.Summary, claim.TargetUrl, claim.ImageUrl);

        await _db.NotificationLogs.AddAsync(delivery, ct);
        await _db.SaveChangesAsync(ct);
        return delivery;
    }

    public async Task CompleteMessageAsync(Guid messageId, CancellationToken ct = default)
    {
        var message = await GetTrackedMessageAsync(messageId, ct);
        message.MarkCompleted();
        await _db.SaveChangesAsync(ct);
    }

    public async Task ReleaseMessageAsync(
        Guid messageId, DateTimeOffset nextAttemptAt, string? lastError, CancellationToken ct = default)
    {
        var message = await GetTrackedMessageAsync(messageId, ct);
        message.Release(nextAttemptAt, lastError);
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkMessageDeadAsync(Guid messageId, string reason, CancellationToken ct = default)
    {
        var message = await GetTrackedMessageAsync(messageId, ct);
        message.MarkDead(reason);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<OutboxHealthSnapshot> GetHealthSnapshotAsync(CancellationToken ct = default)
    {
        var pendingCount = await _db.NotificationOutboxMessages
            .CountAsync(m => m.Status == OutboxMessageStatus.Pending, ct);

        DateTimeOffset? oldestPendingAt;
        if (_db.Database.IsSqlite())
        {
            // El proveedor de SQLite para EF Core no traduce ORDER BY/MIN sobre columnas
            // DateTimeOffset (NotSupportedException). Modo degradado, igual que la
            // reclamación (diseño §6.4): se resuelve en cliente sobre la columna proyectada.
            var pendingCreatedAtValues = await _db.NotificationOutboxMessages
                .Where(m => m.Status == OutboxMessageStatus.Pending)
                .Select(m => m.CreatedAt)
                .ToListAsync(ct);
            oldestPendingAt = pendingCreatedAtValues.Count == 0 ? null : pendingCreatedAtValues.Min();
        }
        else
        {
            // PostgreSQL: agregado cubierto por el índice (Status, CreatedAt) del diseño §5.3.
            oldestPendingAt = await _db.NotificationOutboxMessages
                .Where(m => m.Status == OutboxMessageStatus.Pending)
                .OrderBy(m => m.CreatedAt)
                .Select(m => (DateTimeOffset?)m.CreatedAt)
                .FirstOrDefaultAsync(ct);
        }

        var deadCount = await _db.NotificationOutboxMessages
            .CountAsync(m => m.Status == OutboxMessageStatus.Dead, ct);

        var provider = _db.Database.IsNpgsql() ? "PostgreSQL" : _db.Database.IsSqlite() ? "SQLite" : "Desconocido";

        return new OutboxHealthSnapshot(pendingCount, oldestPendingAt, deadCount, provider);
    }

    private async Task<NotificationOutboxMessage> GetTrackedMessageAsync(Guid messageId, CancellationToken ct)
    {
        var message = await _db.NotificationOutboxMessages.FindAsync(new object[] { messageId }, ct);
        if (message is null)
        {
            throw new InvalidOperationException($"No existe ningún mensaje del outbox con Id '{messageId}'.");
        }

        return message;
    }

    private static OutboxClaim MapClaim(DbDataReader reader, bool isSqlite)
    {
        var id = isSqlite ? Guid.Parse(reader.GetString(0)) : reader.GetGuid(0);
        var eventType = (NotificationEventType)reader.GetInt32(1);
        var title = reader.GetString(2);
        var summary = reader.GetString(3);
        var targetUrl = reader.IsDBNull(4) ? null : reader.GetString(4);
        var imageUrl = reader.IsDBNull(5) ? null : reader.GetString(5);
        var fieldsJson = reader.GetString(6);
        var targetChannel = reader.IsDBNull(7) ? (NotificationChannel?)null : (NotificationChannel)reader.GetInt32(7);
        var attempts = reader.GetInt32(8);
        var createdAt = reader.GetFieldValue<DateTimeOffset>(9);

        return new OutboxClaim(id, eventType, title, summary, targetUrl, imageUrl, fieldsJson, targetChannel, attempts, createdAt);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    // PostgreSQL (diseño §6.3): una sola sentencia — FOR UPDATE SKIP LOCKED solo bloquea
    // durante ella, así que no hace falta abrir una transacción explícita. "Status" = 0 es
    // OutboxMessageStatus.Pending.
    private const string NpgsqlClaimSql = """
        WITH reclamados AS (
            SELECT "Id"
              FROM "NotificationOutboxMessages"
             WHERE "Status" = 0
               AND "NextAttemptAt" <= @ahora
             ORDER BY "NextAttemptAt", "CreatedAt"
             LIMIT @tamanoLote
               FOR UPDATE SKIP LOCKED
        )
        UPDATE "NotificationOutboxMessages" AS m
           SET "Attempts"      = m."Attempts" + 1,
               "NextAttemptAt" = @ahora + @concesion,
               "ClaimedAt"     = @ahora,
               "ClaimedBy"     = @reclamadoPor
          FROM reclamados r
         WHERE m."Id" = r."Id"
        RETURNING m."Id", m."EventType", m."Title", m."Summary", m."TargetUrl",
                  m."ImageUrl", m."FieldsJson", m."TargetChannel", m."Attempts", m."CreatedAt";
        """;

    // SQLite (diseño §6.4): modo degradado, sin cláusula de bloqueo — SQLite ya serializa a
    // los escritores. "@hasta" llega precalculado desde C# (ver ClaimPendingAsync).
    private const string SqliteClaimSql = """
        UPDATE "NotificationOutboxMessages"
           SET "Attempts" = "Attempts" + 1,
               "NextAttemptAt" = @hasta,
               "ClaimedAt" = @ahora,
               "ClaimedBy" = @reclamadoPor
         WHERE "Id" IN (
             SELECT "Id" FROM "NotificationOutboxMessages"
              WHERE "Status" = 0 AND "NextAttemptAt" <= @ahora
              ORDER BY "NextAttemptAt", "CreatedAt"
              LIMIT @tamanoLote
         )
        RETURNING "Id", "EventType", "Title", "Summary", "TargetUrl", "ImageUrl", "FieldsJson",
                  "TargetChannel", "Attempts", "CreatedAt";
        """;
}
