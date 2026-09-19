using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Persistencia y reclamación exclusiva del outbox de notificaciones comunitarias
/// (INC-47, diseño §6.1, decisión D3). Sustituye la lectura infinita de
/// <see cref="ICommunityNotificationQueue"/> por un contrato explícito de encolado,
/// reclamación por lotes y transición de estado, común a los dos proveedores soportados
/// (PostgreSQL/SQLite).
/// </summary>
public interface INotificationOutboxRepository
{
    /// <summary>Persiste el mensaje inmediatamente, sin depender de que el despachador
    /// llegue a ejecutarse (especificación <c>notification-outbox</c>, "El registro persiste
    /// inmediatamente al encolar").</summary>
    Task EnqueueAsync(NotificationOutboxMessage message, CancellationToken ct = default);

    /// <summary>Reclama hasta <paramref name="batchSize"/> mensajes de forma exclusiva y
    /// atómica. En PostgreSQL con <c>FOR UPDATE SKIP LOCKED</c> (diseño §6.3); en SQLite en
    /// modo degradado, sin garantía de exclusión entre procesos (diseño §6.4). La sentencia
    /// desplaza <c>NextAttemptAt</c> al futuro: el lote devuelto queda invisible para
    /// cualquier otro despachador durante la concesión de <paramref name="lease"/>.</summary>
    Task<IReadOnlyList<OutboxClaim>> ClaimPendingAsync(
        int batchSize, string claimedBy, TimeSpan lease, CancellationToken ct = default);

    /// <summary>Sub-entregas por canal ya creadas para un mensaje del outbox.</summary>
    Task<IReadOnlyList<CommunityNotificationLog>> GetDeliveriesAsync(Guid messageId, CancellationToken ct = default);

    /// <summary>Asegura la sub-entrega de <paramref name="channel"/> para el mensaje
    /// reclamado en <paramref name="claim"/>. Idempotente vía
    /// <c>UNIQUE(MessageId, Channel)</c>: si la sub-entrega ya existe, se reutiliza en vez
    /// de duplicarla.</summary>
    Task<CommunityNotificationLog> EnsureDeliveryAsync(
        Guid messageId, NotificationChannel channel, OutboxClaim claim, CancellationToken ct = default);

    /// <summary>Marca el mensaje como completado: ninguna de sus sub-entregas queda en
    /// estado no terminal (diseño §6.5, paso 5).</summary>
    Task CompleteMessageAsync(Guid messageId, CancellationToken ct = default);

    /// <summary>Libera el mensaje de vuelta a pendiente con un nuevo <paramref name="nextAttemptAt"/>
    /// (diseño §6.5, paso 2: fallo de envío o conjunto de canales vacío).</summary>
    Task ReleaseMessageAsync(
        Guid messageId, DateTimeOffset nextAttemptAt, string? lastError, CancellationToken ct = default);

    /// <summary>Agota los intentos de reclamación configurados: estado terminal, no vuelve
    /// a reclamarse.</summary>
    Task MarkMessageDeadAsync(Guid messageId, string reason, CancellationToken ct = default);

    /// <summary>Muestra de salud del outbox para el <em>health check</em> (R4c, diseño §11).</summary>
    Task<OutboxHealthSnapshot> GetHealthSnapshotAsync(CancellationToken ct = default);
}
