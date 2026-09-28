using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Features.Admin;

public class UserManagementService : IUserManagementService
{
    private readonly IUserRepository _userRepository;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserSessionInvalidator? _sessionInvalidator;
    private readonly IExternalLoginRepository? _externalLoginRepository;

    public UserManagementService(
        IUserRepository userRepository,
        IAuditService auditService,
        ICurrentUserService currentUserService,
        IUserSessionInvalidator? sessionInvalidator = null,
        IExternalLoginRepository? externalLoginRepository = null)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _sessionInvalidator = sessionInvalidator;
        _externalLoginRepository = externalLoginRepository;
    }

    public async Task<IReadOnlyList<AppUserDto>> GetUsersAsync(UserFilterDto? filter = null, CancellationToken ct = default)
    {
        EnsureFoundingTeam();

        var users = await _userRepository.GetAllAsync(filter?.Search, filter?.Role, filter?.Status, ct);
        return users.Select(MapToDto).ToList();
    }

    public async Task<AppUserDto?> GetUserByIdAsync(string id, CancellationToken ct = default)
    {
        EnsureFoundingTeam();

        if (string.IsNullOrWhiteSpace(id))
            return null;

        var user = await _userRepository.GetByIdAsync(id.Trim().ToLowerInvariant(), ct);
        return user == null ? null : MapToDto(user);
    }

    public async Task<AppUserDto> CreateUserAsync(CreateUserCommand command, CancellationToken ct = default)
    {
        EnsureFoundingTeam();
        ArgumentNullException.ThrowIfNull(command);

        var existing = await _userRepository.GetByIdAsync(command.Id.Trim().ToLowerInvariant(), ct);
        if (existing != null)
            throw new InvalidOperationException($"Ya existe un usuario con el identificador '{command.Id}'.");

        var user = new AppUser(
            id: command.Id,
            userName: command.UserName,
            email: command.Email,
            role: command.Role,
            permissions: command.Permissions
        );

        await _userRepository.AddAsync(user, ct);

        // Auditoría
        await _auditService.RecordChangeAsync(new RecordAuditCommand(
            UserId: _currentUserService.UserId,
            UserName: _currentUserService.UserName,
            Action: AuditAction.Created,
            EntityType: AuditEntityType.User,
            EntityId: user.Id,
            EntityName: user.UserName,
            Summary: $"Alta de nuevo usuario '{user.UserName}' con rol {GetRoleDisplayName(user.Role)}",
            Changes: [
                new FieldChangeDto("Role", null, user.Role.ToString()),
                new FieldChangeDto("Status", null, user.Status.ToString()),
                new FieldChangeDto("Permissions", null, user.Permissions.ToString())
            ]
        ), ct);

        return MapToDto(user);
    }

    public async Task<AppUserDto> UpdateUserRoleAndPermissionsAsync(UpdateUserRoleAndPermissionsCommand command, CancellationToken ct = default)
    {
        EnsureFoundingTeam();
        ArgumentNullException.ThrowIfNull(command);

        var user = await _userRepository.GetByIdAsync(command.UserId.Trim().ToLowerInvariant(), ct)
            ?? throw new KeyNotFoundException($"No se encontró ningún usuario con ID '{command.UserId}'.");

        var oldRole = user.Role;
        var oldPermissions = user.Permissions;

        user.UpdateRoleAndPermissions(command.Role, command.Permissions);
        await _userRepository.UpdateAsync(user, ct);

        // Auditoría con diff estructurado
        var changes = new List<FieldChangeDto>();
        if (oldRole != user.Role)
            changes.Add(new FieldChangeDto("Role", oldRole.ToString(), user.Role.ToString()));
        if (oldPermissions != user.Permissions)
            changes.Add(new FieldChangeDto("Permissions", oldPermissions.ToString(), user.Permissions.ToString()));

        var action = oldRole != user.Role ? AuditAction.RoleChanged : AuditAction.PermissionsChanged;

        await _auditService.RecordChangeAsync(new RecordAuditCommand(
            UserId: _currentUserService.UserId,
            UserName: _currentUserService.UserName,
            Action: action,
            EntityType: AuditEntityType.User,
            EntityId: user.Id,
            EntityName: user.UserName,
            Summary: $"Actualización de privilegios para '{user.UserName}': Rol={GetRoleDisplayName(user.Role)}, Permisos={user.Permissions}",
            Changes: changes
        ), ct);

        InvalidateSession(user.Id);

        return MapToDto(user);
    }

    public async Task<AppUserDto> UpdateUserStatusAsync(UpdateUserStatusCommand command, CancellationToken ct = default)
    {
        EnsureFoundingTeam();
        ArgumentNullException.ThrowIfNull(command);

        var user = await _userRepository.GetByIdAsync(command.UserId.Trim().ToLowerInvariant(), ct)
            ?? throw new KeyNotFoundException($"No se encontró ningún usuario con ID '{command.UserId}'.");

        var oldStatus = user.Status;
        if (oldStatus == command.Status)
            return MapToDto(user);

        user.UpdateStatus(command.Status);
        await _userRepository.UpdateAsync(user, ct);

        // Auditoría
        await _auditService.RecordChangeAsync(new RecordAuditCommand(
            UserId: _currentUserService.UserId,
            UserName: _currentUserService.UserName,
            Action: AuditAction.StatusChanged,
            EntityType: AuditEntityType.User,
            EntityId: user.Id,
            EntityName: user.UserName,
            Summary: $"Estado del usuario '{user.UserName}' modificado a {GetStatusDisplayName(user.Status)}",
            Changes: [
                new FieldChangeDto("Status", oldStatus.ToString(), user.Status.ToString())
            ]
        ), ct);

        InvalidateSession(user.Id);

        return MapToDto(user);
    }

    public async Task<AppUserDto> AnonymizeUserAsync(string userId, string reason, CancellationToken ct = default)
    {
        EnsureFoundingTeam();

        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("El identificador de usuario no puede estar vacío.", nameof(userId));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Debe proporcionar un motivo o justificación para la baja del usuario.", nameof(reason));

        var user = await _userRepository.GetByIdAsync(userId.Trim().ToLowerInvariant(), ct)
            ?? throw new KeyNotFoundException($"No se encontró ningún usuario con ID '{userId}'.");

        if (user.Status == UserStatus.Deleted)
            return MapToDto(user);

        var oldUserName = user.UserName;
        var oldRole = user.Role;
        var oldStatus = user.Status;

        user.AnonymizeAndClose(reason);
        await _userRepository.UpdateAsync(user, ct);

        if (_externalLoginRepository != null)
        {
            await _externalLoginRepository.DeleteByUserIdAsync(user.Id, ct);
        }

        // Auditoría
        await _auditService.RecordChangeAsync(new RecordAuditCommand(
            UserId: _currentUserService.UserId,
            UserName: _currentUserService.UserName,
            Action: AuditAction.Deleted,
            EntityType: AuditEntityType.User,
            EntityId: user.Id,
            EntityName: "Usuario eliminado",
            Summary: $"Baja y anonimización del usuario '{oldUserName}' tramitada por {_currentUserService.UserName}. Motivo: {reason.Trim()}",
            Changes: [
                new FieldChangeDto("Status", oldStatus.ToString(), UserStatus.Deleted.ToString()),
                new FieldChangeDto("Role", oldRole.ToString(), UserRole.CommunityUser.ToString())
            ]
        ), ct);

        InvalidateSession(user.Id);

        return MapToDto(user);
    }

    /// <summary>
    /// Avisa a los circuitos del usuario afectado para que reevalúen su identidad: la suspensión o
    /// el cambio de permisos debe surtir efecto sin esperar al cierre de sesión.
    /// </summary>
    private void InvalidateSession(string userId) => _sessionInvalidator?.Invalidate(userId);

    private void EnsureFoundingTeam()
    {
        // Invariante de anonimia: sin sesión real no hay privilegios ni auditoría posible.
        SessionIdentity.Require(_currentUserService);

        if (!_currentUserService.IsFoundingTeam)
        {
            throw new UnauthorizedAccessException("Acceso denegado: Se requieren privilegios de la Mesa Fundadora para gestionar usuarios.");
        }
    }

    public static AppUserDto MapToDto(AppUser user)
    {
        return new AppUserDto(
            Id: user.Id,
            UserName: user.UserName,
            Email: user.Email,
            Role: user.Role,
            RoleDisplayName: GetRoleDisplayName(user.Role),
            Status: user.Status,
            StatusDisplayName: GetStatusDisplayName(user.Status),
            Permissions: user.Permissions,
            PermissionNames: GetPermissionNames(user.Permissions, user.Role),
            CreatedAt: user.CreatedAt,
            UpdatedAt: user.UpdatedAt
        );
    }

    public static string GetRoleDisplayName(UserRole role) => role switch
    {
        UserRole.FoundingTeam => "Mesa Fundadora",
        UserRole.Moderator => "Moderador",
        UserRole.CommunityUser => "Usuario Comunitario",
        _ => "Desconocido"
    };

    public static string GetStatusDisplayName(UserStatus status) => status switch
    {
        UserStatus.Active => "Activo",
        UserStatus.Suspended => "Suspendido",
        UserStatus.Deleted => "Eliminado",
        _ => "Desconocido"
    };

    public static IReadOnlyList<string> GetPermissionNames(ModeratorPermission permissions, UserRole role)
    {
        if (role == UserRole.FoundingTeam)
            return ["Acceso Total (Mesa Fundadora)"];

        if (role != UserRole.Moderator)
            return [];

        var list = new List<string>();
        if (permissions.HasFlag(ModeratorPermission.CanEditGames)) list.Add("Editar Juegos");
        if (permissions.HasFlag(ModeratorPermission.CanUploadImages)) list.Add("Subir Imágenes");
        if (permissions.HasFlag(ModeratorPermission.CanManagePublishers)) list.Add("Gestionar Editoriales");
        if (permissions.HasFlag(ModeratorPermission.CanManageCreators)) list.Add("Gestionar Autores");
        if (permissions.HasFlag(ModeratorPermission.CanApproveMedia)) list.Add("Moderar Vídeos");
        if (permissions.HasFlag(ModeratorPermission.CanResolveReports)) list.Add("Resolver Reportes");
        if (permissions.HasFlag(ModeratorPermission.CanManageStoreLinks)) list.Add("Gestionar Tiendas");
        if (permissions.HasFlag(ModeratorPermission.CanPublishInstagram)) list.Add("Publicar en Instagram");
        if (permissions.HasFlag(ModeratorPermission.CanManageUsers)) list.Add("Gestionar Usuarios");
        if (permissions.HasFlag(ModeratorPermission.CanViewAuditLog)) list.Add("Ver Auditoría");
        if (permissions.HasFlag(ModeratorPermission.CanManageEvents)) list.Add("Gestionar Eventos");
        if (permissions.HasFlag(ModeratorPermission.CanManageNotifications)) list.Add("Gestionar Notificaciones");

        return list;
    }
}
