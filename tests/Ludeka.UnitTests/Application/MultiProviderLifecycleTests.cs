using System;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.Features.Identity;
using Ludeka.Core.Entities;
using Ludeka.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Application;

/// <summary>
/// Pruebas de ciclo de vida completo multi-proveedor y prevención de cuentas duplicadas (INC-63, ODD-3).
/// Valida el requerimiento del mantenedor:
/// "si te has logueado con Google sincronizar tu cuenta de Discord y poder loguearte con cualquiera de
/// las dos y te lleve a tu cuenta y no duplique cuenta".
/// </summary>
public class MultiProviderLifecycleTests : IAsyncLifetime
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

    [Fact]
    public async Task Lifecycle_GoogleSignup_LinkDiscord_LoginWithDiscord_ShouldResolveSameAccountWithoutDuplicates()
    {
        // 1. Alta inicial con Google
        var googleEmail = "ana.jugadora@gmail.com";
        var googleSub = "google-sub-ana-100";
        var userFromGoogle = await _service.ResolveAsync(
            "Google", googleSub, googleEmail, emailVerified: true, displayName: "Ana Jugadora");

        Assert.NotNull(userFromGoogle);
        Assert.Equal("ana.jugadora@gmail.com", userFromGoogle.Email);
        Assert.Equal(1, await _context.AppUsers.CountAsync());
        Assert.Equal(1, await _context.ExternalLogins.CountAsync());

        // 2. Vinculación de Discord desde la sesión activa de Ana
        var discordSub = "discord-sub-ana-200";
        var linkResult = await _service.LinkAsync(
            userFromGoogle.Id, "Discord", discordSub, "ana.discord@gmail.com", emailVerified: true);

        Assert.Equal(ExternalLoginLinkOutcome.Linked, linkResult.Outcome);
        Assert.Equal(userFromGoogle.Id, linkResult.User.Id);

        // La base de datos debe tener exactamente 1 usuario y 2 conexiones vinculadas a su UserId
        Assert.Equal(1, await _context.AppUsers.CountAsync());
        Assert.Equal(2, await _context.ExternalLogins.CountAsync());

        var userLinks = await _externalLoginRepository.ListByUserIdAsync(userFromGoogle.Id);
        Assert.Equal(2, userLinks.Count);
        Assert.Contains(userLinks, l => l.Provider == "Google" && l.ProviderKey == googleSub);
        Assert.Contains(userLinks, l => l.Provider == "Discord" && l.ProviderKey == discordSub);

        // 3. Salir y volver a entrar usando Discord
        var resolvedViaDiscord = await _service.ResolveAsync(
            "Discord", discordSub, "ana.discord@gmail.com", emailVerified: true, displayName: "Ana");

        // Assert: Resuelve a la misma cuenta y no duplica usuarios ni enlaces
        Assert.Equal(userFromGoogle.Id, resolvedViaDiscord.Id);
        Assert.Equal(userFromGoogle.Email, resolvedViaDiscord.Email);
        Assert.Equal(1, await _context.AppUsers.CountAsync());
        Assert.Equal(2, await _context.ExternalLogins.CountAsync());

        // 4. Volver a entrar usando Google
        var resolvedViaGoogleAgain = await _service.ResolveAsync(
            "Google", googleSub, googleEmail, emailVerified: true, displayName: "Ana Jugadora");

        Assert.Equal(userFromGoogle.Id, resolvedViaGoogleAgain.Id);
        Assert.Equal(1, await _context.AppUsers.CountAsync());
        Assert.Equal(2, await _context.ExternalLogins.CountAsync());
    }

    [Fact]
    public async Task Lifecycle_DiscordSignup_LinkGoogle_UnlinkDiscord_LoginGoogle_ShouldRetainAccess()
    {
        // 1. Alta inicial con Discord
        var discordSub = "discord-sub-miguel-1";
        var user = await _service.ResolveAsync(
            "Discord", discordSub, "miguel@ludeka.es", emailVerified: true, displayName: "Miguel");

        // 2. Vincular Google
        var googleSub = "google-sub-miguel-2";
        var linkResult = await _service.LinkAsync(
            user.Id, "Google", googleSub, "miguel@gmail.com", emailVerified: true);
        Assert.Equal(ExternalLoginLinkOutcome.Linked, linkResult.Outcome);
        Assert.Equal(2, await _context.ExternalLogins.CountAsync());

        // 3. Desvincular Discord
        await _service.UnlinkAsync(user.Id, "Discord");

        var remainingLinks = await _externalLoginRepository.ListByUserIdAsync(user.Id);
        var singleLink = Assert.Single(remainingLinks);
        Assert.Equal("Google", singleLink.Provider);
        Assert.Equal(googleSub, singleLink.ProviderKey);

        // 4. Iniciar sesión con Google mantiene el acceso a la cuenta existente
        var resolved = await _service.ResolveAsync(
            "Google", googleSub, "miguel@gmail.com", emailVerified: true, displayName: "Miguel Google");
        Assert.Equal(user.Id, resolved.Id);
        Assert.Equal(1, await _context.AppUsers.CountAsync());
    }

    [Fact]
    public async Task DuplicatePrevention_WhenProviderAlreadyBelongsToAnotherAccount_ShouldRejectLinkAndNotDuplicateOrSteal()
    {
        // Arrange: Usuario A y Usuario B
        var userA = await _service.ResolveAsync("Google", "google-sub-userA", "a@ludeka.es", true, "Usuario A");
        var userB = await _service.ResolveAsync("Discord", "discord-sub-userB", "b@ludeka.es", true, "Usuario B");

        Assert.Equal(2, await _context.AppUsers.CountAsync());
        Assert.Equal(2, await _context.ExternalLogins.CountAsync());

        // Act: Usuario A intenta vincular el Discord que ya pertenece a Usuario B
        var linkResult = await _service.LinkAsync(
            userA.Id, "Discord", "discord-sub-userB", "b@ludeka.es", emailVerified: true);

        // Assert: Rechazado, no se crea ninguna fila nueva y Usuario B sigue siendo el dueño
        Assert.Equal(ExternalLoginLinkOutcome.RejectedOwnedByAnotherAccount, linkResult.Outcome);
        Assert.Equal(2, await _context.AppUsers.CountAsync());
        Assert.Equal(2, await _context.ExternalLogins.CountAsync());

        var linkB = await _externalLoginRepository.GetByProviderKeyAsync("Discord", "discord-sub-userB");
        Assert.NotNull(linkB);
        Assert.Equal(userB.Id, linkB!.UserId);
    }

    [Fact]
    public async Task DuplicatePrevention_WhenLoginWithVerifiedEmailCollidesWithAccountHavingLinkedProvider_ShouldThrowCollision()
    {
        // Arrange: Usuario A se dio de alta con Google y correo compartido@ludeka.es
        var userA = await _service.ResolveAsync(
            "Google", "google-sub-shared", "compartido@ludeka.es", emailVerified: true, displayName: "Usuario Titular");

        // Act & Assert: Otra persona intenta entrar con Discord con ese mismo correo verificado pero clave no vinculada.
        // Debe rechazar con ExternalLoginCollisionException y cero mutaciones.
        await Assert.ThrowsAsync<ExternalLoginCollisionException>(() =>
            _service.ResolveAsync("Discord", "discord-sub-intruder", "compartido@ludeka.es", emailVerified: true, displayName: "Intruso"));

        Assert.Equal(1, await _context.AppUsers.CountAsync());
        Assert.Equal(1, await _context.ExternalLogins.CountAsync());
    }

    [Fact]
    public async Task Idempotency_RepeatLinkToSameAccount_ShouldReturnAlreadyLinkedWithoutDuplicating()
    {
        // Arrange
        var user = await _service.ResolveAsync("Google", "google-sub-idem", "idem@ludeka.es", true, "Jugador");

        // Act: Intentar vincular de nuevo el mismo proveedor ya vinculado
        var result = await _service.LinkAsync(user.Id, "Google", "google-sub-idem", "idem@ludeka.es", true);

        // Assert
        Assert.Equal(ExternalLoginLinkOutcome.AlreadyLinkedToThisAccount, result.Outcome);
        Assert.Equal(1, await _context.ExternalLogins.CountAsync());
    }
}
