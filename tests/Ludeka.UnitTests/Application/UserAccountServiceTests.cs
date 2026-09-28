using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Account;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class UserAccountServiceTests
{
    private class TestUserRepository : IUserRepository
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
            return Task.FromResult<IReadOnlyList<AppUser>>(Users);
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

    private class TestExternalLoginRepository : IExternalLoginRepository
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

    private class TestCurrentUserService : ICurrentUserService
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public List<string> RolesList { get; set; } = [];
        public ModeratorPermission Permissions { get; set; } = ModeratorPermission.None;

        public bool IsAuthenticated => !string.IsNullOrWhiteSpace(UserId);
        public IReadOnlyList<string> Roles => RolesList;
        public bool IsFoundingTeam => RolesList.Contains("FoundingTeam", StringComparer.OrdinalIgnoreCase);
        public bool IsInRole(string role) => RolesList.Any(r => r.Equals(role, StringComparison.OrdinalIgnoreCase));
        public bool HasPermission(ModeratorPermission permission) => (Permissions & permission) == permission;
    }

    private class TestAuditService : IAuditService
    {
        public List<RecordAuditCommand> Commands = [];

        public Task RecordChangeAsync(RecordAuditCommand command, CancellationToken ct = default)
        {
            Commands.Add(command);
            return Task.CompletedTask;
        }

        public Task<AuditLogPageDto> GetAuditLogsAsync(AuditLogFilterDto filter, CancellationToken ct = default)
            => Task.FromResult(new AuditLogPageDto([], 0, 1, 10, 1));
    }

    private class TestSessionInvalidator : IUserSessionInvalidator
    {
        public List<string> InvalidatedUserIds = [];

        public long GetVersion(string userId) => 1;
        public void Invalidate(string userId) => InvalidatedUserIds.Add(userId);
        public event EventHandler<UserSessionInvalidatedEventArgs>? Invalidated;
    }

    [Fact]
    public async Task CloseOwnAccountAsync_WhenUnauthenticated_ReturnsFailure()
    {
        var userRepo = new TestUserRepository();
        var externalLoginRepo = new TestExternalLoginRepository();
        var currentUser = new TestCurrentUserService { UserId = "" };

        var service = new UserAccountService(userRepo, externalLoginRepo, currentUser);

        var result = await service.CloseOwnAccountAsync();

        Assert.False(result.Success);
        Assert.Contains("No hay una sesión", result.Message);
    }

    [Fact]
    public async Task CloseOwnAccountAsync_WhenUserNotFound_ReturnsFailure()
    {
        var userRepo = new TestUserRepository();
        var externalLoginRepo = new TestExternalLoginRepository();
        var currentUser = new TestCurrentUserService { UserId = "ghost_user", UserName = "Fantasma" };

        var service = new UserAccountService(userRepo, externalLoginRepo, currentUser);

        var result = await service.CloseOwnAccountAsync();

        Assert.False(result.Success);
        Assert.Contains("No se encontró el registro", result.Message);
    }

    [Fact]
    public async Task CloseOwnAccountAsync_WhenUserIsFoundingTeam_ReturnsFailure()
    {
        var userRepo = new TestUserRepository();
        var externalLoginRepo = new TestExternalLoginRepository();
        var founder = new AppUser("founder", "Fundador", "founder@ludeka.es", role: UserRole.FoundingTeam);
        await userRepo.AddAsync(founder);

        var currentUser = new TestCurrentUserService { UserId = founder.Id, UserName = founder.UserName, RolesList = ["FoundingTeam"] };
        var service = new UserAccountService(userRepo, externalLoginRepo, currentUser);

        var result = await service.CloseOwnAccountAsync();

        Assert.False(result.Success);
        Assert.Contains("Mesa Fundadora", result.Message);
        Assert.Equal(UserStatus.Active, founder.Status);
    }

    [Fact]
    public async Task CloseOwnAccountAsync_WhenAlreadyDeleted_ReturnsSuccessIdempotently()
    {
        var userRepo = new TestUserRepository();
        var externalLoginRepo = new TestExternalLoginRepository();
        var deletedUser = new AppUser("del_user", "Usuario eliminado", "deleted-123@deleted.ludeka.es", status: UserStatus.Deleted);
        await userRepo.AddAsync(deletedUser);

        var currentUser = new TestCurrentUserService { UserId = deletedUser.Id, UserName = deletedUser.UserName };
        var service = new UserAccountService(userRepo, externalLoginRepo, currentUser);

        var result = await service.CloseOwnAccountAsync();

        Assert.True(result.Success);
        Assert.Contains("ya se encuentra dada de baja", result.Message);
    }

    [Fact]
    public async Task CloseOwnAccountAsync_WhenValidUser_Anonymizes_PurgesLogins_Audits_InvalidatesSession_ReturnsSuccess()
    {
        var userRepo = new TestUserRepository();
        var externalLoginRepo = new TestExternalLoginRepository();
        var audit = new TestAuditService();
        var invalidator = new TestSessionInvalidator();

        var activeUser = new AppUser("user_abc", "Ana Jugadora", "ana@ejemplo.com", country: "ES");
        await userRepo.AddAsync(activeUser);

        var currentUser = new TestCurrentUserService { UserId = activeUser.Id, UserName = activeUser.UserName };
        var service = new UserAccountService(userRepo, externalLoginRepo, currentUser, invalidator, audit);

        var result = await service.CloseOwnAccountAsync();

        Assert.True(result.Success);
        Assert.Contains("dada de baja", result.Message);

        // Validar entidad en repositorio
        var updated = await userRepo.GetByIdAsync(activeUser.Id);
        Assert.NotNull(updated);
        Assert.Equal(UserStatus.Deleted, updated.Status);
        Assert.Equal("Usuario eliminado", updated.UserName);
        Assert.StartsWith("deleted-", updated.Email);
        Assert.EndsWith("@deleted.ludeka.es", updated.Email);
        Assert.Null(updated.Country);

        // Validar purga de logins
        Assert.Contains(activeUser.Id, externalLoginRepo.DeletedUserIds);

        // Validar invalidación de sesión
        Assert.Contains(activeUser.Id, invalidator.InvalidatedUserIds);

        // Validar auditoría
        var auditCmd = Assert.Single(audit.Commands);
        Assert.Equal(AuditAction.Deleted, auditCmd.Action);
        Assert.Equal(AuditEntityType.User, auditCmd.EntityType);
        Assert.Equal(activeUser.Id, auditCmd.EntityId);
        Assert.Contains("Ana Jugadora", auditCmd.Summary);
    }
}
