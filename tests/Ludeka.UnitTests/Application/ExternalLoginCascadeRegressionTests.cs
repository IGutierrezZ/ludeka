using System;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.Features.Identity;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Application;

/// <summary>
/// Regresión explícita de las 3 ramas de <see cref="ExternalLoginService.ResolveAsync"/> antes y
/// después de partir la rama 2 en 2a/2b (INC-49, diseño §2.2/§7, mitigación de la propuesta §8:
/// "tocar ResolveAsync ... 1345 pruebas en verde"). Ninguna de estas 6 pruebas sustituye a las 6 de
/// <see cref="ExternalLoginServiceTests"/> (INC-46): ese fichero NO se toca — es el criterio de no
/// regresión innegociable de este PR.
/// </summary>
public class ExternalLoginCascadeRegressionTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private LudekaDbContext _context = null!;
    private IExternalLoginRepository _externalLoginRepository = null!;
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

        _externalLoginRepository = new ExternalLoginRepository(_context);
        _service = new ExternalLoginService(_externalLoginRepository, new SqliteUserRepository(_context));
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
    public async Task RepeatPair_ShouldStillResolveWithoutCreatingRows()
    {
        // Rama 1 intacta: el par (Provider, ProviderKey) ya vinculado resuelve sin escribir nada más.
        var first = await _service.ResolveAsync("Google", "google-sub-1", "jugador@ludeka.es", true, "Jugador Uno");
        var second = await _service.ResolveAsync("Google", "google-sub-1", "jugador@ludeka.es", true, "Jugador Uno");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await _context.AppUsers.CountAsync());
        Assert.Equal(1, await _context.ExternalLogins.CountAsync());
    }

    [Fact]
    public async Task VerifiedEmailMatchingAccountWithoutLinks_ShouldStillLinkAutomatically()
    {
        // Rama 2a: la cuenta encontrada por correo verificado NO tiene ningún ExternalLogin previo.
        // Es la excepción segura de INC-49 §2.2 y debe comportarse EXACTAMENTE como la rama 2 original
        // de INC-46 (mismo resultado), más el invariante nuevo: ProviderEmailVerifiedAt establecido.
        var existing = await SeedUserAsync(new AppUser(
            "laura_mod", "Laura Moderadora", "laura@ludeka.es",
            UserRole.Moderator, ModeratorPermission.CanApproveMedia));

        var resolved = await _service.ResolveAsync("Google", "google-sub-2", "Laura@Ludeka.es", true, "Laura");

        Assert.Equal(existing.Id, resolved.Id);
        Assert.Equal(UserRole.Moderator, resolved.Role);
        Assert.Equal(1, await _context.AppUsers.CountAsync());

        var link = await _context.ExternalLogins.SingleAsync();
        Assert.Equal(existing.Id, link.UserId);
        Assert.NotNull(link.ProviderEmailVerifiedAt);
    }

    [Fact]
    public async Task VerifiedEmailMatchingAccountWithExistingLinks_ShouldThrowCollisionWithoutCreatingRows()
    {
        // Rama 2b: la cuenta encontrada por correo verificado YA tiene al menos un ExternalLogin.
        // INC-49 §2.2 exige informar del conflicto sin fusionar nunca en silencio: cero filas nuevas.
        var existing = await SeedUserAsync(new AppUser("mario_user", "Mario Jugador", "mario@ludeka.es"));
        await _externalLoginRepository.AddAsync(new ExternalLogin(
            existing.Id, "Discord", "discord-mario", "mario@ludeka.es", providerEmailVerified: true));

        var usersBefore = await _context.AppUsers.CountAsync();
        var linksBefore = await _context.ExternalLogins.CountAsync();

        await Assert.ThrowsAsync<ExternalLoginCollisionException>(() =>
            _service.ResolveAsync("Google", "google-sub-collision", "Mario@Ludeka.es", true, "Mario"));

        Assert.Equal(usersBefore, await _context.AppUsers.CountAsync());
        Assert.Equal(linksBefore, await _context.ExternalLogins.CountAsync());
    }

    [Fact]
    public async Task Collision_ShouldNeverChangeTheExistingLinkOwner()
    {
        // Intentos de colisión repetidos con proveedores distintos: la fila original nunca cambia
        // de UserId ni de proveedor, venga la colisión de donde venga.
        var existing = await SeedUserAsync(new AppUser("ana_user", "Ana Jugadora", "ana@ludeka.es"));
        await _externalLoginRepository.AddAsync(new ExternalLogin(
            existing.Id, "Discord", "discord-ana", "ana@ludeka.es", providerEmailVerified: true));

        await Assert.ThrowsAsync<ExternalLoginCollisionException>(() =>
            _service.ResolveAsync("Google", "google-sub-ana-1", "Ana@Ludeka.es", true, "Ana"));
        await Assert.ThrowsAsync<ExternalLoginCollisionException>(() =>
            _service.ResolveAsync("Facebook", "fb-ana-1", "Ana@Ludeka.es", true, "Ana"));

        var link = await _context.ExternalLogins.SingleAsync();
        Assert.Equal(existing.Id, link.UserId);
        Assert.Equal("Discord", link.Provider);
    }

    [Fact]
    public async Task UnverifiedEmail_ShouldStillProvisionANewAccount()
    {
        // Rama 3 intacta: el correo sin verificar nunca fusiona con la cuenta fundadora coincidente.
        var founder = await SeedUserAsync(new AppUser(
            "carlos_fundador", "Carlos Fundador", "carlos@ludeka.es", UserRole.FoundingTeam));

        var user = await _service.ResolveAsync("Facebook", "fb-99", "carlos@ludeka.es", false, "Carlos");

        Assert.NotEqual(founder.Id, user.Id);
        Assert.Equal(UserRole.CommunityUser, user.Role);
        Assert.Equal(2, await _context.AppUsers.CountAsync());
    }

    [Fact]
    public async Task NoEmail_ShouldStillUseThePlaceholderDomain()
    {
        // Rama 3 sin correo: sigue usando el dominio reservado ludeka.invalid, nunca null ni vacío.
        var user = await _service.ResolveAsync("Google", "google-sub-noemail", null, false, null);

        Assert.Equal(UserRole.CommunityUser, user.Role);
        Assert.Contains("ludeka.invalid", user.Email, StringComparison.Ordinal);
    }
}
