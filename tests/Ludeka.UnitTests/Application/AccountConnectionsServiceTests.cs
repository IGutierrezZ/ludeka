using System;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.Features.Identity;
using Ludeka.Core.Entities;
using Ludeka.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;
using AuthenticationOptions = Ludeka.Application.Features.Identity.AuthenticationOptions;

namespace Ludeka.UnitTests.Application;

/// <summary>
/// Lectura de conexiones de la propia cuenta de la sesión (INC-49, diseño §D6):
/// <see cref="IAccountConnectionsService.GetConnectionsAsync"/> y
/// <see cref="IAccountConnectionsService.HasVerifiedProviderEmailAsync"/>. Sigue el armazón de
/// <see cref="ExternalLoginServiceTests"/> (SQLite en memoria).
/// </summary>
public class AccountConnectionsServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private LudekaDbContext _context = null!;
    private ExternalLoginRepository _externalLoginRepository = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LudekaDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        _externalLoginRepository = new ExternalLoginRepository(_context);
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

    /// <summary>Google y Discord habilitados y utilizables; Facebook deshabilitado.</summary>
    private static AuthenticationOptions TwoEnabledProvidersOptions()
    {
        var options = new AuthenticationOptions();
        options.Providers[ExternalProviderNames.Google] = new ExternalProviderOptions
        {
            Enabled = true, ClientId = "google-id", ClientSecret = "google-secret"
        };
        options.Providers[ExternalProviderNames.Discord] = new ExternalProviderOptions
        {
            Enabled = true, ClientId = "discord-id", ClientSecret = "discord-secret"
        };
        options.Providers[ExternalProviderNames.Facebook] = new ExternalProviderOptions { Enabled = false };
        return options;
    }

    private AccountConnectionsService BuildService(ICurrentUserService currentUser, AuthenticationOptions? options = null)
        => new(_externalLoginRepository, currentUser, Options.Create(options ?? TwoEnabledProvidersOptions()));

    [Fact]
    public async Task GetConnectionsAsync_WithoutSession_ShouldReturnAnEmptyView()
    {
        var service = BuildService(StubCurrentUserService.Anonymous());

        var view = await service.GetConnectionsAsync();

        Assert.Empty(view.Connections);
        Assert.False(view.HasVerifiedProviderEmail);
        Assert.False(view.CanUnlink);
    }

    [Fact]
    public async Task GetConnectionsAsync_WithSession_ShouldReturnEveryEnabledProviderWithItsLinkStatus()
    {
        // Arrange: el usuario tiene Google vinculado, pero no Discord; Facebook no está habilitado.
        var user = await SeedUserAsync(new AppUser("user-conn-1", "Jugador Conexión", "conexion1@ludeka.es"));
        await _externalLoginRepository.AddAsync(new ExternalLogin(user.Id, "Google", "google-conn-1"));

        var service = BuildService(StubCurrentUserService.WithSession(user.Id));

        // Act
        var view = await service.GetConnectionsAsync();

        // Assert: exactamente los dos proveedores habilitados, ninguno deshabilitado.
        Assert.Equal(2, view.Connections.Count);

        var google = Assert.Single(view.Connections, c => c.Provider == "Google");
        Assert.True(google.IsLinked);

        var discord = Assert.Single(view.Connections, c => c.Provider == "Discord");
        Assert.False(discord.IsLinked);

        Assert.DoesNotContain(view.Connections, c => c.Provider == "Facebook");
    }

    [Fact]
    public async Task HasVerifiedProviderEmailAsync_WithoutSession_ShouldReturnFalse()
    {
        var service = BuildService(StubCurrentUserService.Anonymous());

        Assert.False(await service.HasVerifiedProviderEmailAsync());
    }

    [Fact]
    public async Task HasVerifiedProviderEmailAsync_WithSessionButNoVerifiedRow_ShouldReturnFalse()
    {
        // Arrange: la fila existe, pero el proveedor no entregó un correo verificado.
        var user = await SeedUserAsync(new AppUser("user-conn-2", "Jugadora Conexión Dos", "conexion2@ludeka.es"));
        await _externalLoginRepository.AddAsync(
            new ExternalLogin(user.Id, "Discord", "discord-conn-2", providerEmailVerified: false));

        var service = BuildService(StubCurrentUserService.WithSession(user.Id));

        Assert.False(await service.HasVerifiedProviderEmailAsync());
    }

    [Fact]
    public async Task HasVerifiedProviderEmailAsync_WithAtLeastOneVerifiedRow_ShouldReturnTrue()
    {
        // Arrange: una fila sin verificar y otra verificada; basta una sola para el aviso.
        var user = await SeedUserAsync(new AppUser("user-conn-3", "Jugador Conexión Tres", "conexion3@ludeka.es"));
        await _externalLoginRepository.AddAsync(
            new ExternalLogin(user.Id, "Discord", "discord-conn-3", providerEmailVerified: false));
        await _externalLoginRepository.AddAsync(new ExternalLogin(
            user.Id, "Google", "google-conn-3", "conexion3@ludeka.es", providerEmailVerified: true));

        var service = BuildService(StubCurrentUserService.WithSession(user.Id));

        Assert.True(await service.HasVerifiedProviderEmailAsync());
    }

    [Fact]
    public async Task GetConnectionsAsync_AfterInvalidateCache_ShouldReturnFreshDataFromTheRepository()
    {
        // Corrección del orquestador (INC-49 PR #6): AccountConnectionsService cachea por ámbito de
        // instancia (diseño §D6), que en Blazor Server equivale a todo el circuito, no a una sola
        // petición (App.razor:39 monta InteractiveServer global). Sin invalidación explícita, una
        // desvinculación dentro del mismo circuito quedaría invisible para cualquier lector hasta
        // una recarga completa. Arrange: una fila vinculada, primera lectura ya cacheada.
        var user = await SeedUserAsync(new AppUser("user-conn-4", "Jugador Conexión Cuatro", "conexion4@ludeka.es"));
        var link = new ExternalLogin(user.Id, "Google", "google-conn-4");
        await _externalLoginRepository.AddAsync(link);

        var service = BuildService(StubCurrentUserService.WithSession(user.Id));
        var beforeUnlink = await service.GetConnectionsAsync();
        Assert.True(Assert.Single(beforeUnlink.Connections, c => c.Provider == "Google").IsLinked);

        // Act: se borra la fila por fuera del servicio (lo que ExternalLoginService.UnlinkAsync hace
        // en producción) y se invalida la caché de ámbito.
        await _externalLoginRepository.RemoveAsync(link);
        service.InvalidateCache();

        // Assert: la siguiente lectura ya no es la vista cacheada anterior y refleja la fila borrada.
        var afterUnlink = await service.GetConnectionsAsync();
        Assert.NotSame(beforeUnlink, afterUnlink);
        Assert.False(Assert.Single(afterUnlink.Connections, c => c.Provider == "Google").IsLinked);
    }

    [Fact]
    public void InvalidateCache_ShouldRaiseInvalidatedEachTimeItIsCalled()
    {
        // Corrección del orquestador (INC-49 PR #6): el aviso de cabecera (AccountEmailNotice, PR
        // #6) se suscribe a este evento para refrescarse sin recarga completa. Debe dispararse en
        // cada invalidación, no solo la primera vez.
        var service = BuildService(StubCurrentUserService.Anonymous());
        var raisedCount = 0;
        service.Invalidated += (_, _) => raisedCount++;

        service.InvalidateCache();
        Assert.Equal(1, raisedCount);

        service.InvalidateCache();
        Assert.Equal(2, raisedCount);
    }
}
