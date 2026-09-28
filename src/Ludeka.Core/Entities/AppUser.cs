using System;
using Ludeka.Core.Enums;

namespace Ludeka.Core.Entities;

/// <summary>
/// Representa a un usuario en Ludeka con roles formales y permisos granulares de moderación.
/// </summary>
public class AppUser
{
    public string Id { get; private set; } = string.Empty;
    public string UserName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? Country { get; private set; }
    public UserRole Role { get; private set; } = UserRole.CommunityUser;
    public UserStatus Status { get; private set; } = UserStatus.Active;
    public ModeratorPermission Permissions { get; private set; } = ModeratorPermission.None;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; private set; }

    // Constructor privado para EF Core
    private AppUser() { }

    public AppUser(
        string id,
        string userName,
        string email,
        UserRole role = UserRole.CommunityUser,
        ModeratorPermission permissions = ModeratorPermission.None,
        UserStatus status = UserStatus.Active,
        DateTimeOffset? createdAt = null,
        string? country = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("El identificador de usuario no puede estar vacío.", nameof(id));

        if (string.IsNullOrWhiteSpace(userName))
            throw new ArgumentException("El nombre de usuario no puede estar vacío.", nameof(userName));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("El correo electrónico no puede estar vacío.", nameof(email));

        Id = id.Trim().ToLowerInvariant();
        UserName = userName.Trim();
        Email = email.Trim().ToLowerInvariant();
        Country = string.IsNullOrWhiteSpace(country) ? null : ValueObjects.CountryCatalog.Normalize(country);
        Role = role;
        Status = status;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;

        // Si es FoundingTeam, se le asignan todos los permisos de forma inherente
        Permissions = role switch
        {
            UserRole.FoundingTeam => ModeratorPermission.All,
            UserRole.Moderator => permissions,
            _ => ModeratorPermission.None
        };
    }

    public void SetCountry(string? country)
    {
        Country = string.IsNullOrWhiteSpace(country) ? null : ValueObjects.CountryCatalog.Normalize(country);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Determina si el usuario posee un permiso de moderación concreto considerando su estado y rol.
    /// La regla vive en <see cref="ModeratorPermissionRules"/> para compartirla con la proyección
    /// de la identidad de la sesión.
    /// </summary>
    public bool HasPermission(ModeratorPermission permission)
        => ModeratorPermissionRules.Grants(Role, Status, Permissions, permission);

    /// <summary>
    /// Actualiza el rol y la máscara granular de permisos del usuario.
    /// </summary>
    public void UpdateRoleAndPermissions(UserRole newRole, ModeratorPermission newPermissions)
    {
        Role = newRole;
        Permissions = newRole switch
        {
            UserRole.FoundingTeam => ModeratorPermission.All,
            UserRole.Moderator => newPermissions,
            _ => ModeratorPermission.None
        };
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Modifica el estado de actividad de la cuenta (activo o suspendido).
    /// </summary>
    public void UpdateStatus(UserStatus newStatus)
    {
        Status = newStatus;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Actualiza los datos de perfil básico.
    /// </summary>
    public void UpdateProfile(string userName, string email)
    {
        if (string.IsNullOrWhiteSpace(userName))
            throw new ArgumentException("El nombre de usuario no puede estar vacío.", nameof(userName));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("El correo electrónico no puede estar vacío.", nameof(email));

        UserName = userName.Trim();
        Email = email.Trim().ToLowerInvariant();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Concede un permiso de moderación adicional a un usuario moderador.
    /// </summary>
    public void GrantPermission(ModeratorPermission permission)
    {
        if (Role == UserRole.Moderator)
        {
            Permissions |= permission;
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Revoca un permiso de moderación a un usuario moderador.
    /// </summary>
    public void RevokePermission(ModeratorPermission permission)
    {
        if (Role == UserRole.Moderator)
        {
            Permissions &= ~permission;
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Anonimiza de forma irreversible los datos personales identificables (PII) del usuario,
    /// revoca todos los permisos y asigna el estado Deleted en cumplimiento del derecho al olvido (RGPD).
    /// </summary>
    public void AnonymizeAndClose(string? reason = null)
    {
        if (Role == UserRole.FoundingTeam)
            throw new InvalidOperationException("No está permitido dar de baja a un miembro de la Mesa Fundadora.");

        Status = UserStatus.Deleted;
        Role = UserRole.CommunityUser;
        Permissions = ModeratorPermission.None;
        UserName = "Usuario eliminado";
        Email = $"deleted-{Guid.NewGuid():N}@deleted.ludeka.es";
        Country = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
