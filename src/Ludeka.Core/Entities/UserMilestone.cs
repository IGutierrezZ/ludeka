using System;
using Ludeka.Core.Enums;

namespace Ludeka.Core.Entities;

/// <summary>
/// Representa un hito o logro alcanzado y desbloqueado por un usuario en Ludeka.
/// </summary>
public class UserMilestone
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public MilestoneType Type { get; private set; }
    public DateTimeOffset UnlockedAt { get; private set; }

    // Constructor privado para EF Core
    private UserMilestone() { }

    public UserMilestone(
        string userId,
        MilestoneType type,
        DateTimeOffset? unlockedAt = null,
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("El identificador de usuario no puede estar vacío.", nameof(userId));

        if (!Enum.IsDefined(typeof(MilestoneType), type))
            throw new ArgumentOutOfRangeException(nameof(type), "El tipo de hito no es válido.");

        Id = id ?? Guid.NewGuid();
        UserId = userId.Trim();
        Type = type;
        UnlockedAt = unlockedAt ?? DateTimeOffset.UtcNow;
    }
}
