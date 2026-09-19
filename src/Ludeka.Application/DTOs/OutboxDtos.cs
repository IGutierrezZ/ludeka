using System;
using Ludeka.Core.Enums;

namespace Ludeka.Application.DTOs;

/// <summary>
/// Proyección de un mensaje del outbox reclamado por <c>ClaimPendingAsync</c>
/// (INC-47, diseño §6.1). Nota de decisión de <c>sdd-apply</c>: el diseño no fija el fichero
/// exacto para este <c>record</c> dentro de <c>DTOs/</c>; se agrupa aquí junto con
/// <see cref="OutboxHealthSnapshot"/> en un único fichero <c>OutboxDtos.cs</c>, siguiendo el
/// mismo patrón de agrupación por funcionalidad que <c>CommunityNotificationDtos.cs</c> y
/// <c>AuditDtos.cs</c>, en vez de dos ficheros de un solo <c>record</c> cada uno.
/// </summary>
public record OutboxClaim(
    Guid Id,
    NotificationEventType EventType,
    string Title,
    string Summary,
    string? TargetUrl,
    string? ImageUrl,
    string FieldsJson,
    NotificationChannel? TargetChannel,
    int Attempts,
    DateTimeOffset CreatedAt);

/// <summary>Muestra de salud del outbox (INC-47, diseño §6.1 y §11): profundidad pendiente,
/// antigüedad del mensaje pendiente más antiguo y mensajes agotados (<c>Dead</c>).</summary>
public record OutboxHealthSnapshot(
    int PendingCount,
    DateTimeOffset? OldestPendingAt,
    int DeadCount,
    string Provider);
