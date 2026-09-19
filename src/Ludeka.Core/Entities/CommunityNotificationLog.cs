using System;
using Ludeka.Core.Enums;

namespace Ludeka.Core.Entities;

public class CommunityNotificationLog
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public NotificationEventType EventType { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public string? TargetUrl { get; private set; }
    public string? ImageUrl { get; private set; }
    public NotificationStatus Status { get; private set; }
    public string? ErrorDetails { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; private set; }

    // --- INC-47 (R3a, diseño §5.2): sub-entrega por canal del outbox. NULL en lo histórico y
    // en escrituras directas (panel, ping de prueba, reintento manual). ---
    public Guid? MessageId { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }

    // Constructor privado para EF Core
    private CommunityNotificationLog() { }

    public CommunityNotificationLog(
        NotificationEventType eventType,
        NotificationChannel channel,
        string title,
        string summary,
        string? targetUrl = null,
        string? imageUrl = null,
        NotificationStatus initialStatus = NotificationStatus.Queued,
        Guid? messageId = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("El título de la notificación no puede estar vacío.", nameof(title));

        if (string.IsNullOrWhiteSpace(summary))
            throw new ArgumentException("El resumen de la notificación no puede estar vacío.", nameof(summary));

        EventType = eventType;
        Channel = channel;
        Title = title.Trim();
        Summary = summary.Trim();
        TargetUrl = string.IsNullOrWhiteSpace(targetUrl) ? null : targetUrl.Trim();
        ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        Status = initialStatus;
        CreatedAt = DateTimeOffset.UtcNow;
        MessageId = messageId;
        Attempts = 0;

        if (initialStatus == NotificationStatus.Sent || initialStatus == NotificationStatus.DryRun)
        {
            SentAt = DateTimeOffset.UtcNow;
        }
    }

    public void MarkAsSent()
    {
        Status = NotificationStatus.Sent;
        SentAt = DateTimeOffset.UtcNow;
        ErrorDetails = null;
    }

    public void MarkAsFailed(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
            error = "Error no especificado al emitir la notificación.";

        Status = NotificationStatus.Failed;
        ErrorDetails = error.Trim();
    }

    public void MarkAsDryRun()
    {
        Status = NotificationStatus.DryRun;
        SentAt = DateTimeOffset.UtcNow;
        ErrorDetails = null;
    }

    /// <summary>Crea la sub-entrega por canal de un mensaje del outbox ya reclamado
    /// (INC-47, diseño §5.2). El despachador (R4a/R4b) la usa una vez por canal derivado.</summary>
    public static CommunityNotificationLog ForDelivery(
        Guid messageId,
        NotificationEventType eventType,
        NotificationChannel channel,
        string title,
        string summary,
        string? targetUrl,
        string? imageUrl) =>
        new(eventType, channel, title, summary, targetUrl, imageUrl, NotificationStatus.Queued, messageId);

    /// <summary>Registra un intento de entrega fallido: incrementa el contador y programa un
    /// reintento en un estado no terminal (INC-47, diseño §5.2).</summary>
    public void RegisterFailedAttempt(string error, DateTimeOffset nextAttemptAt)
    {
        if (string.IsNullOrWhiteSpace(error))
            error = "Error no especificado al reintentar la notificación.";

        Attempts++;
        NextAttemptAt = nextAttemptAt;
        Status = NotificationStatus.Queued;
        ErrorDetails = error.Trim();
    }

    /// <summary>Agota el número máximo de intentos: marca la sub-entrega en estado terminal
    /// <c>Failed</c>, que no vuelve a reclamarse (INC-47, diseño §5.2).</summary>
    public void MarkAsPermanentlyFailed(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
            error = "Error no especificado al agotar los intentos de entrega.";

        Status = NotificationStatus.Failed;
        ErrorDetails = error.Trim();
    }
}
