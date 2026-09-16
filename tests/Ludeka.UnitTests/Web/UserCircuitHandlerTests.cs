using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Data;
using Ludeka.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Resolución de la identidad al abrir el circuito (INC-46 F3): el snapshot se puebla con el
/// <c>AppUser</c> de la sesión, una sesión anónima o sin fila se degrada sin privilegios y un
/// fallo de lectura nunca rompe el circuito.
/// </summary>
public class UserCircuitHandlerTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private LudekaDbContext _context = null!;
    private SqliteUserRepository _users = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LudekaDbContext(options);
        await _context.Database.EnsureCreatedAsync();
        _users = new SqliteUserRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private sealed class FakeAuthenticationStateProvider(ClaimsPrincipal principal) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => Task.FromResult(new AuthenticationState(principal));
    }

    private sealed class ThrowingUserRepository : IUserRepository
    {
        public Task<AppUser?> GetByIdAsync(string id, CancellationToken ct = default)
            => throw new InvalidOperationException("Fallo de base de datos simulado.");

        public Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default)
            => throw new InvalidOperationException("Fallo de base de datos simulado.");

        public Task<IReadOnlyList<AppUser>> GetAllAsync(string? search = null, UserRole? role = null, UserStatus? status = null, CancellationToken ct = default)
            => throw new InvalidOperationException("Fallo de base de datos simulado.");

        public Task AddAsync(AppUser user, CancellationToken ct = default)
            => throw new InvalidOperationException("Fallo de base de datos simulado.");

        public Task UpdateAsync(AppUser user, CancellationToken ct = default)
            => throw new InvalidOperationException("Fallo de base de datos simulado.");

        public Task DeleteAsync(string id, CancellationToken ct = default)
            => throw new InvalidOperationException("Fallo de base de datos simulado.");
    }

    private static ClaimsPrincipal PrincipalFor(string userId)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Cookies"));

    private static ClaimsPrincipal AnonymousPrincipal() => new(new ClaimsIdentity());

    private UserCircuitHandler CreateHandler(ClaimsPrincipal principal, IUserRepository? repository = null)
        => new(
            new UserIdentitySnapshot(),
            new FakeAuthenticationStateProvider(principal),
            repository ?? _users);

    private async Task SeedUserAsync(AppUser user)
    {
        _context.AppUsers.Add(user);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task ResolveIdentityAsync_WithKnownSession_ShouldPopulateTheSnapshotWithTheAppUser()
    {
        await SeedUserAsync(new AppUser(
            "laura_mod", "Laura Moderadora", "laura@ludeka.es", UserRole.Moderator, ModeratorPermission.CanEditGames));

        var snapshot = new UserIdentitySnapshot();
        var handler = new UserCircuitHandler(snapshot, new FakeAuthenticationStateProvider(PrincipalFor("laura_mod")), _users);

        await handler.ResolveIdentityAsync();

        Assert.True(snapshot.IsResolved);
        Assert.NotNull(snapshot.User);
        Assert.Equal("laura_mod", snapshot.User!.Id);
        Assert.True(snapshot.User.HasPermission(ModeratorPermission.CanEditGames));
    }

    [Fact]
    public async Task ResolveIdentityAsync_WithSuspendedSession_ShouldPopulateTheUserWithoutPrivileges()
    {
        await SeedUserAsync(new AppUser(
            "susana_suspendida", "Susana", "susana@ludeka.es",
            UserRole.Moderator, ModeratorPermission.All, UserStatus.Suspended));

        var snapshot = new UserIdentitySnapshot();
        var handler = new UserCircuitHandler(snapshot, new FakeAuthenticationStateProvider(PrincipalFor("susana_suspendida")), _users);

        await handler.ResolveIdentityAsync();

        var service = new AuthenticatedCurrentUserService(snapshot);
        Assert.Equal("susana_suspendida", service.UserId);
        Assert.Empty(service.Roles);
        Assert.False(service.HasPermission(ModeratorPermission.All));
    }

    [Fact]
    public async Task ResolveIdentityAsync_WithoutSession_ShouldLeaveAnAnonymousSnapshot()
    {
        var snapshot = new UserIdentitySnapshot();
        var handler = new UserCircuitHandler(snapshot, new FakeAuthenticationStateProvider(AnonymousPrincipal()), _users);

        await handler.ResolveIdentityAsync();

        Assert.True(snapshot.IsResolved);
        Assert.Null(snapshot.User);

        var service = new AuthenticatedCurrentUserService(snapshot);
        Assert.Equal(string.Empty, service.UserId);
        Assert.False(service.IsFoundingTeam);
        Assert.False(service.HasPermission(ModeratorPermission.All));
    }

    [Fact]
    public async Task ResolveIdentityAsync_WithUnknownSession_ShouldLeaveAnAnonymousSnapshot()
    {
        var snapshot = new UserIdentitySnapshot();
        var handler = new UserCircuitHandler(snapshot, new FakeAuthenticationStateProvider(PrincipalFor("cuenta-inexistente")), _users);

        await handler.ResolveIdentityAsync();

        Assert.True(snapshot.IsResolved);
        Assert.Null(snapshot.User);

        var service = new AuthenticatedCurrentUserService(snapshot);
        Assert.Equal(string.Empty, service.UserId);
        Assert.False(service.HasPermission(ModeratorPermission.All));
    }

    [Fact]
    public async Task ResolveIdentityAsync_WhenTheRepositoryFails_ShouldDegradeInsteadOfThrowing()
    {
        var snapshot = new UserIdentitySnapshot();
        var handler = new UserCircuitHandler(
            snapshot,
            new FakeAuthenticationStateProvider(PrincipalFor("laura_mod")),
            new ThrowingUserRepository());

        await handler.ResolveIdentityAsync();

        Assert.True(snapshot.IsResolved);
        Assert.Null(snapshot.User);

        var service = new AuthenticatedCurrentUserService(snapshot);
        Assert.Equal(string.Empty, service.UserId);
        Assert.False(service.HasPermission(ModeratorPermission.All));
    }
}
