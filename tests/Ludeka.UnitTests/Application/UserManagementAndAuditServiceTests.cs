using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Admin;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class UserManagementAndAuditServiceTests
{
    private class FakeUserRepository : IUserRepository
    {
        public List<AppUser> Users = [];

        public Task AddAsync(AppUser user, CancellationToken ct = default)
        {
            Users.Add(user);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id, CancellationToken ct = default)
        {
            Users.RemoveAll(u => u.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AppUser>> GetAllAsync(string? search = null, UserRole? role = null, UserStatus? status = null, CancellationToken ct = default)
        {
            var q = Users.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(u => u.UserName.Contains(search, StringComparison.OrdinalIgnoreCase) || u.Email.Contains(search, StringComparison.OrdinalIgnoreCase));
            if (role.HasValue)
                q = q.Where(u => u.Role == role.Value);
            if (status.HasValue)
                q = q.Where(u => u.Status == status.Value);

            return Task.FromResult<IReadOnlyList<AppUser>>(q.ToList());
        }

        public Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default)
        {
            return Task.FromResult(Users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));
        }

        public Task<AppUser?> GetByIdAsync(string id, CancellationToken ct = default)
        {
            return Task.FromResult(Users.FirstOrDefault(u => u.Id.Equals(id, StringComparison.OrdinalIgnoreCase)));
        }

        public Task UpdateAsync(AppUser user, CancellationToken ct = default)
        {
            var idx = Users.FindIndex(u => u.Id.Equals(user.Id, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) Users[idx] = user;
            return Task.CompletedTask;
        }
    }

    private class FakeAuditLogRepository : IAuditLogRepository
    {
        public List<AuditLogEntry> Entries = [];

        public Task AddAsync(AuditLogEntry entry, CancellationToken ct = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<int> CountLogsAsync(string? userId = null, AuditEntityType? entityType = null, AuditAction? action = null, DateTimeOffset? fromDate = null, DateTimeOffset? toDate = null, CancellationToken ct = default)
        {
            var q = Filter(userId, entityType, action, fromDate, toDate);
            return Task.FromResult(q.Count());
        }

        public Task<IReadOnlyList<AuditLogEntry>> GetLogsAsync(string? userId = null, AuditEntityType? entityType = null, AuditAction? action = null, DateTimeOffset? fromDate = null, DateTimeOffset? toDate = null, int skip = 0, int take = 50, CancellationToken ct = default)
        {
            var q = Filter(userId, entityType, action, fromDate, toDate);
            var res = q.OrderByDescending(e => e.Timestamp).Skip(skip).Take(take).ToList();
            return Task.FromResult<IReadOnlyList<AuditLogEntry>>(res);
        }

        private IEnumerable<AuditLogEntry> Filter(string? userId, AuditEntityType? entityType, AuditAction? action, DateTimeOffset? fromDate, DateTimeOffset? toDate)
        {
            var q = Entries.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(userId))
                q = q.Where(e => e.UserId.Equals(userId, StringComparison.OrdinalIgnoreCase));
            if (entityType.HasValue)
                q = q.Where(e => e.EntityType == entityType.Value);
            if (action.HasValue)
                q = q.Where(e => e.Action == action.Value);
            if (fromDate.HasValue)
                q = q.Where(e => e.Timestamp >= fromDate.Value);
            if (toDate.HasValue)
                q = q.Where(e => e.Timestamp <= toDate.Value);
            return q;
        }
    }

    private class FakeCurrentUserService : ICurrentUserService
    {
        public string UserId { get; set; } = "carlos_fundador";
        public string UserName { get; set; } = "Carlos Fundador";
        public List<string> RolesList { get; set; } = ["FoundingTeam"];
        public ModeratorPermission Permissions { get; set; } = ModeratorPermission.All;

        public IReadOnlyList<string> Roles => RolesList;
        public bool IsFoundingTeam => RolesList.Contains("FoundingTeam", StringComparer.OrdinalIgnoreCase);

        public bool IsInRole(string role) => RolesList.Any(r => r.Equals(role, StringComparison.OrdinalIgnoreCase));

        public bool HasPermission(ModeratorPermission permission)
        {
            if (IsFoundingTeam) return true;
            return (Permissions & permission) == permission;
        }
    }

    private class FakeExternalLoginRepository : IExternalLoginRepository
    {
        public List<string> DeletedUserIds = [];

        public Task AddAsync(ExternalLogin externalLogin, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ExternalLogin?> GetByProviderKeyAsync(string provider, string providerKey, CancellationToken cancellationToken = default) => Task.FromResult<ExternalLogin?>(null);
        public Task<IReadOnlyList<ExternalLogin>> ListByUserIdAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ExternalLogin>>([]);
        public Task RemoveAsync(ExternalLogin externalLogin, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteByUserIdAsync(string userId, CancellationToken cancellationToken = default)
        {
            DeletedUserIds.Add(userId);
            return Task.CompletedTask;
        }
    }


    [Fact]
    public async Task UserManagementService_NonFounder_ThrowsUnauthorized()
    {
        var userRepo = new FakeUserRepository();
        var auditRepo = new FakeAuditLogRepository();
        var currentUser = new FakeCurrentUserService();
        currentUser.RolesList = ["User"]; // No es fundador

        var auditService = new AuditService(auditRepo, currentUser);
        var userService = new UserManagementService(userRepo, auditService, currentUser);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => userService.GetUsersAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => userService.CreateUserAsync(new CreateUserCommand("test", "Test", "test@test.es")));
    }

    [Fact]
    public async Task UserManagementService_Founder_CanCreateAndListUsers()
    {
        var userRepo = new FakeUserRepository();
        var auditRepo = new FakeAuditLogRepository();
        var currentUser = new FakeCurrentUserService(); // FoundingTeam

        var auditService = new AuditService(auditRepo, currentUser);
        var userService = new UserManagementService(userRepo, auditService, currentUser);

        var created = await userService.CreateUserAsync(new CreateUserCommand(
            Id: "maria_mod",
            UserName: "María Moderadora",
            Email: "maria@ludeka.es",
            Role: UserRole.Moderator,
            Permissions: ModeratorPermission.CanEditGames | ModeratorPermission.CanUploadImages
        ));

        Assert.Equal("maria_mod", created.Id);
        Assert.Equal("María Moderadora", created.UserName);
        Assert.Equal(UserRole.Moderator, created.Role);
        Assert.Contains("Editar Juegos", created.PermissionNames);
        Assert.Contains("Subir Imágenes", created.PermissionNames);

        var users = await userService.GetUsersAsync();
        Assert.Single(users);

        // Verifica que se haya generado log de auditoría
        Assert.Single(auditRepo.Entries);
        Assert.Equal(AuditAction.Created, auditRepo.Entries[0].Action);
        Assert.Equal(AuditEntityType.User, auditRepo.Entries[0].EntityType);
        Assert.Equal("maria_mod", auditRepo.Entries[0].EntityId);
    }

    [Fact]
    public async Task UserManagementService_UpdateRoleAndPermissions_RecordsAuditDiff()
    {
        var userRepo = new FakeUserRepository();
        var auditRepo = new FakeAuditLogRepository();
        var currentUser = new FakeCurrentUserService();

        var initialUser = new AppUser("juan", "Juan", "juan@test.es", UserRole.CommunityUser);
        userRepo.Users.Add(initialUser);

        var auditService = new AuditService(auditRepo, currentUser);
        var userService = new UserManagementService(userRepo, auditService, currentUser);

        var updated = await userService.UpdateUserRoleAndPermissionsAsync(new UpdateUserRoleAndPermissionsCommand(
            UserId: "juan",
            Role: UserRole.Moderator,
            Permissions: ModeratorPermission.CanManagePublishers | ModeratorPermission.CanManageCreators
        ));

        Assert.Equal(UserRole.Moderator, updated.Role);
        Assert.True(updated.Permissions.HasFlag(ModeratorPermission.CanManagePublishers));

        Assert.Single(auditRepo.Entries);
        var log = auditRepo.Entries[0];
        Assert.Equal(AuditAction.RoleChanged, log.Action);
        Assert.Equal("juan", log.EntityId);
        Assert.Contains(log.Changes, c => c.FieldName == "Role" && c.OldValue == "CommunityUser" && c.NewValue == "Moderator");
    }

    [Fact]
    public async Task UserManagementService_UpdateStatus_SuspendsUserAndAudits()
    {
        var userRepo = new FakeUserRepository();
        var auditRepo = new FakeAuditLogRepository();
        var currentUser = new FakeCurrentUserService();

        var initialUser = new AppUser("ana", "Ana", "ana@test.es", UserRole.Moderator, ModeratorPermission.CanEditGames);
        userRepo.Users.Add(initialUser);

        var auditService = new AuditService(auditRepo, currentUser);
        var userService = new UserManagementService(userRepo, auditService, currentUser);

        var result = await userService.UpdateUserStatusAsync(new UpdateUserStatusCommand("ana", UserStatus.Suspended));

        Assert.Equal(UserStatus.Suspended, result.Status);
        Assert.Single(auditRepo.Entries);
        Assert.Equal(AuditAction.StatusChanged, auditRepo.Entries[0].Action);
        Assert.Contains(auditRepo.Entries[0].Changes, c => c.FieldName == "Status" && c.OldValue == "Active" && c.NewValue == "Suspended");
    }

    private class RecordingSessionInvalidator : IUserSessionInvalidator
    {
        public List<string> InvalidatedUserIds { get; } = [];

        public event EventHandler<UserSessionInvalidatedEventArgs>? Invalidated;

        public long GetVersion(string userId) => 0;

        public void Invalidate(string userId)
        {
            InvalidatedUserIds.Add(userId);
            Invalidated?.Invoke(this, new UserSessionInvalidatedEventArgs(userId, InvalidatedUserIds.Count));
        }
    }

    [Fact]
    public async Task UserManagementService_UpdateStatus_ShouldInvalidateTheTargetSession()
    {
        var userRepo = new FakeUserRepository();
        var auditRepo = new FakeAuditLogRepository();
        var currentUser = new FakeCurrentUserService();
        var invalidator = new RecordingSessionInvalidator();

        userRepo.Users.Add(new AppUser("ana", "Ana", "ana@test.es", UserRole.Moderator, ModeratorPermission.CanEditGames));

        var userService = new UserManagementService(
            userRepo,
            new AuditService(auditRepo, currentUser),
            currentUser,
            invalidator);

        await userService.UpdateUserStatusAsync(new UpdateUserStatusCommand("ana", UserStatus.Suspended));

        Assert.Equal(["ana"], invalidator.InvalidatedUserIds);
    }

    [Fact]
    public async Task UserManagementService_UpdateRoleAndPermissions_ShouldInvalidateTheTargetSession()
    {
        var userRepo = new FakeUserRepository();
        var auditRepo = new FakeAuditLogRepository();
        var currentUser = new FakeCurrentUserService();
        var invalidator = new RecordingSessionInvalidator();

        userRepo.Users.Add(new AppUser("juan", "Juan", "juan@test.es", UserRole.CommunityUser));

        var userService = new UserManagementService(
            userRepo,
            new AuditService(auditRepo, currentUser),
            currentUser,
            invalidator);

        await userService.UpdateUserRoleAndPermissionsAsync(new UpdateUserRoleAndPermissionsCommand(
            UserId: "juan",
            Role: UserRole.Moderator,
            Permissions: ModeratorPermission.CanApproveMedia));

        Assert.Equal(["juan"], invalidator.InvalidatedUserIds);
    }

    [Fact]
    public async Task UserManagementService_UpdateStatus_WhenNothingChanges_ShouldNotInvalidate()
    {
        var userRepo = new FakeUserRepository();
        var auditRepo = new FakeAuditLogRepository();
        var currentUser = new FakeCurrentUserService();
        var invalidator = new RecordingSessionInvalidator();

        userRepo.Users.Add(new AppUser("ana", "Ana", "ana@test.es", UserRole.Moderator, ModeratorPermission.CanEditGames));

        var userService = new UserManagementService(
            userRepo,
            new AuditService(auditRepo, currentUser),
            currentUser,
            invalidator);

        await userService.UpdateUserStatusAsync(new UpdateUserStatusCommand("ana", UserStatus.Active));

        Assert.Empty(invalidator.InvalidatedUserIds);
    }

    [Fact]
    public async Task AuditService_GetAuditLogs_PaginationAndFiltering()
    {
        var auditRepo = new FakeAuditLogRepository();
        var currentUser = new FakeCurrentUserService();
        var auditService = new AuditService(auditRepo, currentUser);

        for (int i = 1; i <= 15; i++)
        {
            await auditService.RecordChangeAsync(new RecordAuditCommand(
                UserId: i % 2 == 0 ? "mod_a" : "mod_b",
                UserName: i % 2 == 0 ? "Mod A" : "Mod B",
                Action: i % 2 == 0 ? AuditAction.Updated : AuditAction.Created,
                EntityType: AuditEntityType.Game,
                EntityId: $"game-{i}",
                EntityName: $"Juego {i}",
                Summary: $"Acción de prueba {i}"
            ));
        }

        // Página 1 con PageSize = 10
        var page1 = await auditService.GetAuditLogsAsync(new AuditLogFilterDto(Page: 1, PageSize: 10));
        Assert.Equal(15, page1.TotalCount);
        Assert.Equal(10, page1.Items.Count);
        Assert.Equal(2, page1.TotalPages);

        // Filtro por usuario
        var filteredByUser = await auditService.GetAuditLogsAsync(new AuditLogFilterDto(UserId: "mod_a"));
        Assert.Equal(7, filteredByUser.TotalCount);
    }

    [Fact]
    public void AuditService_GetActionDisplayName_ForLinkedProvider_ReturnsSpecificSpanishName()
    {
        var displayName = AuditService.GetActionDisplayName(AuditAction.LinkedProvider);

        Assert.Equal("Vinculación de Proveedor", displayName);
        Assert.NotEqual("Operación", displayName);
    }

    [Fact]
    public void AuditService_GetActionDisplayName_ForUnlinkedProvider_ReturnsSpecificSpanishName()
    {
        var displayName = AuditService.GetActionDisplayName(AuditAction.UnlinkedProvider);

        Assert.Equal("Desvinculación de Proveedor", displayName);
        Assert.NotEqual("Operación", displayName);
        Assert.NotEqual(AuditService.GetActionDisplayName(AuditAction.LinkedProvider), displayName);
    }

    [Fact]
    public async Task UserManagementService_AnonymizeUserAsync_ShouldAnonymizeUser_PurgeExternalLogins_AuditAndInvalidateSession()
    {
        var userRepo = new FakeUserRepository();
        var auditRepo = new FakeAuditLogRepository();
        var currentUser = new FakeCurrentUserService();
        var invalidator = new RecordingSessionInvalidator();
        var externalLoginRepo = new FakeExternalLoginRepository();

        var targetUser = new AppUser(
            id: "user_to_anonymize",
            userName: "Carlos Moderador",
            email: "carlos.mod@ludeka.es",
            role: UserRole.Moderator,
            permissions: ModeratorPermission.CanEditGames | ModeratorPermission.CanUploadImages
        );
        await userRepo.AddAsync(targetUser);

        var auditService = new AuditService(auditRepo, currentUser);
        var userService = new UserManagementService(userRepo, auditService, currentUser, invalidator, externalLoginRepo);

        var result = await userService.AnonymizeUserAsync(targetUser.Id, "Solicitud RGPD derecho al olvido");

        Assert.Equal(UserStatus.Deleted, result.Status);
        Assert.Equal("Usuario eliminado", result.UserName);
        Assert.Equal("Eliminado", result.StatusDisplayName);
        Assert.StartsWith("deleted-", result.Email);
        Assert.EndsWith("@deleted.ludeka.es", result.Email);
        Assert.Equal(UserRole.CommunityUser, result.Role);
        Assert.Empty(result.PermissionNames);

        // Verifica repositorio externo
        Assert.Contains(targetUser.Id, externalLoginRepo.DeletedUserIds);

        // Verifica invalidación de sesión
        Assert.Contains(targetUser.Id, invalidator.InvalidatedUserIds);

        // Verifica auditoría
        var audit = Assert.Single(auditRepo.Entries);
        Assert.Equal(AuditAction.Deleted, audit.Action);
        Assert.Equal(AuditEntityType.User, audit.EntityType);
        Assert.Equal(targetUser.Id, audit.EntityId);
        Assert.Contains("Carlos Moderador", audit.Summary);
        Assert.Contains("Solicitud RGPD derecho al olvido", audit.Summary);
    }

    [Fact]
    public async Task UserManagementService_AnonymizeUserAsync_NonFounder_ThrowsUnauthorized()
    {
        var userRepo = new FakeUserRepository();
        var auditRepo = new FakeAuditLogRepository();
        var currentUser = new FakeCurrentUserService();
        currentUser.RolesList = ["User"];

        var auditService = new AuditService(auditRepo, currentUser);
        var userService = new UserManagementService(userRepo, auditService, currentUser);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => userService.AnonymizeUserAsync("target", "Motivo"));
    }

    [Fact]
    public async Task UserManagementService_AnonymizeUserAsync_NotFound_ThrowsKeyNotFoundException()
    {
        var userRepo = new FakeUserRepository();
        var auditRepo = new FakeAuditLogRepository();
        var currentUser = new FakeCurrentUserService();

        var auditService = new AuditService(auditRepo, currentUser);
        var userService = new UserManagementService(userRepo, auditService, currentUser);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => userService.AnonymizeUserAsync("non_existent", "Motivo"));
    }

    [Fact]
    public async Task UserManagementService_AnonymizeUserAsync_AlreadyDeleted_ReturnsIdempotentlyWithoutReauditing()
    {
        var userRepo = new FakeUserRepository();
        var auditRepo = new FakeAuditLogRepository();
        var currentUser = new FakeCurrentUserService();

        var user = new AppUser("deleted_user", "Usuario eliminado", "deleted-123@deleted.ludeka.es", status: UserStatus.Deleted);
        await userRepo.AddAsync(user);

        var auditService = new AuditService(auditRepo, currentUser);
        var userService = new UserManagementService(userRepo, auditService, currentUser);

        var result = await userService.AnonymizeUserAsync(user.Id, "Intento repetido");

        Assert.Equal(UserStatus.Deleted, result.Status);
        Assert.Empty(auditRepo.Entries);
    }
}

