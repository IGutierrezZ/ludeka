using System;
using Ludeka.Core.Enums;

namespace Ludeka.Core.Entities;

/// <summary>
/// Mensaje lógico del outbox de notificaciones (INC-47, diseño §5.2, decisión D2). Una fila por
/// mensaje; las sub-entregas por canal viven en <see cref="CommunityNotificationLog"/>. El
/// <em>fan-out</em> por canal se decide al despachar, nunca al encolar.
/// </summary>
public class NotificationOutboxMessage
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public NotificationEventType EventType { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public string? TargetUrl { get; private set; }
    public string? ImageUrl { get; private set; }

    /// <summary>Campos del mensaje serializados. Sin esta columna, cada notificación que
    /// sobreviva a un reinicio llegaría sin los <c>fields</c> del embed de Discord
    /// (DiscordWebhookClient.cs:64-68) ni las líneas de Telegram (TelegramBotClient.cs:107-113).
    /// Resuelve C1 (diseño §2).</summary>
    public string FieldsJson { get; private set; } = "{}";

    /// <summary>Canal exclusivo solicitado al encolar, o <see langword="null"/> para difusión.
    /// El despachador nunca resuelve el fan-out aquí: lo decide releyendo las opciones
    /// (diseño §1, decisión 4).</summary>
    public NotificationChannel? TargetChannel { get; private set; }

    public OutboxMessageStatus Status { get; private set; } = OutboxMessageStatus.Pending;
    public int Attempts { get; private set; }

    /// <summary>Momento a partir del cual el mensaje es reclamable. La reclamación lo desplaza
    /// al futuro (concesión temporal): es lo que hace invisible el mensaje a otros despachadores
    /// durante el envío y lo que devuelve a la cola una ejecución que muriese a mitad.</summary>
    public DateTimeOffset NextAttemptAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; private set; }

    // Observabilidad de la concesión. NO forman parte del predicado de reclamación (R4a).
    public DateTimeOffset? ClaimedAt { get; private set; }
    public string? ClaimedBy { get; private set; }

    public string? LastError { get; private set; }

    // Constructor privado para EF Core
    private NotificationOutboxMessage() { }

    public NotificationOutboxMessage(
        NotificationEventType eventType,
        string title,
        string summary,
        string? targetUrl = null,
        string? imageUrl = null,
        string fieldsJson = "{}",
        NotificationChannel? targetChannel = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("El título del mensaje del outbox no puede estar vacío.", nameof(title));

        if (string.IsNullOrWhiteSpace(summary))
            throw new ArgumentException("El resumen del mensaje del outbox no puede estar vacío.", nameof(summary));

        EventType = eventType;
        Title = title.Trim();
        Summary = summary.Trim();
        TargetUrl = string.IsNullOrWhiteSpace(targetUrl) ? null : targetUrl.Trim();
        ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        FieldsJson = string.IsNullOrWhiteSpace(fieldsJson) ? "{}" : fieldsJson;
        TargetChannel = targetChannel;
        Status = OutboxMessageStatus.Pending;
        Attempts = 0;
        NextAttemptAt = DateTimeOffset.UtcNow;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Marca el mensaje como completado: ninguna de sus sub-entregas por canal queda
    /// en estado no terminal (INC-47, diseño §6.5, paso 5). No vuelve a ser reclamado.</summary>
    public void MarkCompleted()
    {
        Status = OutboxMessageStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Libera el mensaje de vuelta a pendiente con un nuevo momento de reintento,
    /// tras un fallo de envío o un conjunto de canales vacío (INC-47, diseño §6.5, paso 2).
    /// Permanece <see cref="OutboxMessageStatus.Pending"/>: sigue siendo reclamable.</summary>
    public void Release(DateTimeOffset nextAttemptAt, string? lastError)
    {
        NextAttemptAt = nextAttemptAt;
        LastError = string.IsNullOrWhiteSpace(lastError) ? null : lastError.Trim();
    }

    /// <summary>Agota <c>MaxClaimAttempts</c>: estado terminal, no vuelve a reclamarse
    /// (INC-47, diseño §6.5, paso 2).</summary>
    public void MarkDead(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            reason = "Motivo no especificado al agotar los intentos de reclamación.";

        Status = OutboxMessageStatus.Dead;
        LastError = reason.Trim();
    }
}
