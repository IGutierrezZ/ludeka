using System;
using System.Collections.Generic;
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
/// Persistencia del outbox de notificaciones (INC-47, R4a, diseño §6). Todos los métodos de
/// esta rebanada usan LINQ/EF Core normal. La reclamación exclusiva por lotes
/// (<c>ClaimPendingAsync</c>) llega en el PR 6c de esta partición: es el único método del
/// contrato que necesita un comando ADO.NET crudo y parametrizado sobre
/// <c>Database.GetDbConnection()</c>, con rama por proveedor, porque ni <c>FromSqlRaw</c> ni
/// <c>SqlQueryRaw</c> componen un <c>UPDATE ... RETURNING</c> desde LINQ (diseño §6.4).
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
}
