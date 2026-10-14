using System;
using Ludeka.Core.Enums;

namespace Ludeka.Core.Entities;

/// <summary>
/// Representa un aviso personal dentro del centro de notificaciones de un usuario en Ludeka (INC-149).
/// Permanece sin leer hasta que el usuario interactúa con él.
/// </summary>
public class UserNotification
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public UserNotificationType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string TargetUrl { get; private set; } = string.Empty;
    public Guid? GameId { get; private set; }
    public string? ImageUrl { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }

    protected UserNotification()
    {
    }

    public UserNotification(
        string userId,
        UserNotificationType type,
        string title,
        string message,
        string targetUrl,
        Guid? gameId = null,
        string? imageUrl = null,
        DateTime? createdAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("El identificador de usuario no puede estar vacío.", nameof(userId));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("El título de la notificación no puede estar vacío.", nameof(title));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("El mensaje de la notificación no puede estar vacío.", nameof(message));

        Id = Guid.NewGuid();
        UserId = userId.Trim();
        Type = type;
        Title = title.Trim();
        Message = message.Trim();
        TargetUrl = string.IsNullOrWhiteSpace(targetUrl) ? "/cuenta/notificaciones" : targetUrl.Trim();
        GameId = gameId;
        ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        IsRead = false;
        CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow;
        ReadAtUtc = null;
    }

    public void MarkAsRead(DateTime? readAtUtc = null)
    {
        if (IsRead)
            return;

        IsRead = true;
        ReadAtUtc = readAtUtc ?? DateTime.UtcNow;
    }
}
