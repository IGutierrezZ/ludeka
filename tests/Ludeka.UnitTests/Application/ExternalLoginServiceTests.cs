using System;
using System.Threading.Tasks;
using Ludeka.Application.Features.Identity;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Application;

/// <summary>
/// Vinculación y aprovisionamiento de identidad externa (INC-46 F2): par reincidente, correo
/// verificado, alta comunitaria, correo sin verificar y prohibición de auto-conceder FoundingTeam.
/// </summary>
public class ExternalLoginServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private LudekaDbContext _context = null!;
    private IExternalLoginService _service = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LudekaDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        _service = new ExternalLoginService(new ExternalLoginRepository(_context), new SqliteUserRepository(_context));
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task<AppUser> SeedUserAsync(AppUser user)
    {
        _context.AppUsers.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task RepeatPair_ShouldResolveToTheSameAccountWithoutCreatingRows()
    {
        // Act
        var first = await _service.ResolveAsync("Google", "google-sub-1", "jugador@ludeka.es", true, "Jugador Uno");
        var second = await _service.ResolveAsync("Google", "google-sub-1", "jugador@ludeka.es", true, "Jugador Uno");

        // Assert
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await _context.AppUsers.CountAsync());
        Assert.Equal(1, await _context.ExternalLogins.CountAsync());
    }

    [Fact]
    public async Task VerifiedEmailMatchingExistingAccount_ShouldLinkAndPreserveRolesAndPermissions()
    {
        // Arrange
        var existing = await SeedUserAsync(new AppUser(
            "laura_mod", "Laura Moderadora", "laura@ludeka.es",
            UserRole.Moderator, ModeratorPermission.CanApproveMedia));

        // Act: el correo del proveedor llega con otra caja para probar la normalización
        var resolved = await _service.ResolveAsync("Google", "google-sub-2", "Laura@Ludeka.es", true, "Laura");

        // Assert
        Assert.Equal(existing.Id, resolved.Id);
        Assert.Equal(UserRole.Moderator, resolved.Role);
        Assert.Equal(ModeratorPermission.CanApproveMedia, resolved.Permissions);
        Assert.Equal(1, await _context.AppUsers.CountAsync());

        var link = await _context.ExternalLogins.SingleAsync();
        Assert.Equal(existing.Id, link.UserId);
        Assert.Equal("Google", link.Provider);
        Assert.Equal("google-sub-2", link.ProviderKey);
        Assert.Equal("laura@ludeka.es", link.ProviderEmail);
    }

    [Fact]
    public async Task FirstLoginWithoutMatch_ShouldProvisionCommunityUserWithNoPermissions()
    {
        // Act
        var user = await _service.ResolveAsync("Discord", "discord-123", "nuevo@ludeka.es", true, "Nuevo Jugador");

        // Assert
        Assert.Equal(UserRole.CommunityUser, user.Role);
        Assert.Equal(ModeratorPermission.None, user.Permissions);
        Assert.Equal("Nuevo Jugador", user.UserName);
        Assert.False(string.IsNullOrWhiteSpace(user.Id));

        var link = await _context.ExternalLogins.SingleAsync();
        Assert.Equal(user.Id, link.UserId);
        Assert.Equal("Discord", link.Provider);
        Assert.Equal("discord-123", link.ProviderKey);
    }

    [Fact]
    public async Task UnverifiedEmail_ShouldNotFuseAccountsAndShouldProvisionANewOne()
    {
        // Arrange: el correo coincide con una cuenta fundadora, pero el proveedor no lo verificó
        var founder = await SeedUserAsync(new AppUser(
            "carlos_fundador", "Carlos Fundador", "carlos@ludeka.es", UserRole.FoundingTeam));

        // Act
        var user = await _service.ResolveAsync("Facebook", "fb-99", "carlos@ludeka.es", false, "Carlos");

        // Assert
        Assert.NotEqual(founder.Id, user.Id);
        Assert.Equal(UserRole.CommunityUser, user.Role);
        Assert.Equal(2, await _context.AppUsers.CountAsync());

        var link = await _context.ExternalLogins.SingleAsync();
        Assert.Equal(user.Id, link.UserId);
    }

    [Fact]
    public async Task UnverifiedEmailOrNoEmail_ShouldNeverGrantFoundingTeam()
    {
        // Act: sin correo y sin verificación no se puede fusionar con la cuenta fundadora
        var user = await _service.ResolveAsync("Google", "google-sub-3", null, false, null);

        // Assert
        Assert.Equal(UserRole.CommunityUser, user.Role);
        Assert.NotEqual(UserRole.FoundingTeam, user.Role);
        Assert.False(string.IsNullOrWhiteSpace(user.Email));
        Assert.Contains("ludeka.invalid", user.Email, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingProviderKey_ShouldThrow()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.ResolveAsync("Google", "   ", "jugador@ludeka.es", true, "Jugador"));
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.ResolveAsync("", "google-sub-4", "jugador@ludeka.es", true, "Jugador"));
    }
}
