using System;
using Ludeka.Core.Enums;

namespace Ludeka.Core.Entities;

/// <summary>
/// Representa una interacción de «me gusta» de un usuario identificado sobre una entidad de la comunidad.
/// </summary>
public class UserLike
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public LikeTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // Constructor privado para EF Core
    private UserLike() { }

    public UserLike(
        Guid userId,
        LikeTargetType targetType,
        Guid targetId,
        Guid? id = null,
        DateTimeOffset? createdAt = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("El identificador del usuario no puede estar vacío.", nameof(userId));

        if (targetId == Guid.Empty)
            throw new ArgumentException("El identificador del destino no puede estar vacío.", nameof(targetId));

        Id = id ?? Guid.NewGuid();
        UserId = userId;
        TargetType = targetType;
        TargetId = targetId;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
    }
}
