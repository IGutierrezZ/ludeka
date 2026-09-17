using System;
using System.Collections.Generic;
using System.Threading;
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
/// Vinculación y desvinculación de proveedores de identidad desde sesión activa (INC-49, PR #2):
/// <see cref="IExternalLoginService.LinkAsync"/>, <see cref="IExternalLoginService.UnlinkAsync"/>,
/// la guarda del último método y la petición que evita la interfaz. Sigue el armazón de
/// <see cref="ExternalLoginServiceTests"/> (SQLite en memoria).
/// </summary>
public class ExternalLoginLinkingTests : IAsyncLifetime
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
    public async Task LinkAsync_WhenNoExistingLink_ShouldCreateRowWithoutProvisioningNewUser()
    {
        // Arrange: sesión activa sin ninguna fila ExternalLogin para el proveedor elegido.
        var user = await SeedUserAsync(new AppUser("user-1", "Jugador Uno", "jugador1@ludeka.es"));

        // Act
        var result = await _service.LinkAsync(user.Id, "Google", "google-sub-10", "jugador1@ludeka.es", true);

        // Assert: se crea la fila para el UserId de la sesión y no se aprovisiona ningún AppUser nuevo.
        Assert.Equal(ExternalLoginLinkOutcome.Linked, result.Outcome);
        Assert.Equal(1, await _context.AppUsers.CountAsync());
        var link = await _context.ExternalLogins.SingleAsync();
        Assert.Equal(user.Id, link.UserId);
        Assert.Equal("Google", link.Provider);
        Assert.Equal("google-sub-10", link.ProviderKey);
    }

    [Fact]
    public async Task LinkAsync_WhenSamePairAlreadyLinkedToThisAccount_ShouldNotCreateAnotherRow()
    {
        // Arrange: la propia cuenta ya tiene ese mismo par vinculado.
        var user = await SeedUserAsync(new AppUser("user-2", "Jugador Dos", "jugador2@ludeka.es"));
        await _externalLoginRepository.AddAsync(new ExternalLogin(user.Id, "Discord", "discord-20"));

        // Act
        var result = await _service.LinkAsync(user.Id, "Discord", "discord-20", null, false);

        // Assert
        Assert.Equal(ExternalLoginLinkOutcome.AlreadyLinkedToThisAccount, result.Outcome);
        Assert.Equal(1, await _context.ExternalLogins.CountAsync());
    }

    [Fact]
    public async Task LinkAsync_WhenPairOwnedByAnotherAccount_ShouldRejectWithoutMovingTheExistingRow()
    {
        // Arrange: el par ya pertenece a otra cuenta (UserId B).
        var owner = await SeedUserAsync(new AppUser("user-owner", "Propietaria", "propietaria@ludeka.es"));
        var requester = await SeedUserAsync(new AppUser("user-requester", "Solicitante", "solicitante@ludeka.es"));
        await _externalLoginRepository.AddAsync(new ExternalLogin(owner.Id, "Facebook", "fb-30"));

        // Act: la sesión de "requester", distinta de "owner", intenta vincular el mismo par.
        var result = await _service.LinkAsync(requester.Id, "Facebook", "fb-30", null, false);

        // Assert: se rechaza, no se crea ninguna fila nueva y la fila existente conserva su dueño.
        Assert.Equal(ExternalLoginLinkOutcome.RejectedOwnedByAnotherAccount, result.Outcome);
        Assert.Equal(1, await _context.ExternalLogins.CountAsync());
        var link = await _context.ExternalLogins.SingleAsync();
        Assert.Equal(owner.Id, link.UserId);
    }

    [Fact]
    public async Task LinkAsync_WhenAddAsyncRacesAgainstAConcurrentInsert_ShouldRejectAndKeepOriginalOwner()
    {
        // Arrange: la fila competidora ya pertenece a "owner", pero el repositorio bajo prueba
        // simula la ventana de carrera en la que la comprobación previa todavía no la ve (el
        // índice único de LudekaDbContext.cs:485 es quien detiene realmente la escritura).
        var owner = await SeedUserAsync(new AppUser("user-race-owner", "Propietaria Carrera", "carrera-o@ludeka.es"));
        var requester = await SeedUserAsync(new AppUser("user-race-requester", "Solicitante Carrera", "carrera-r@ludeka.es"));
        await _externalLoginRepository.AddAsync(new ExternalLogin(owner.Id, "Google", "race-key"));

        var racingService = new ExternalLoginService(
            new RaceSimulatingRepository(_externalLoginRepository), new SqliteUserRepository(_context));

        // Act
        var result = await racingService.LinkAsync(requester.Id, "Google", "race-key", null, false);

        // Assert: mismo resultado de rechazo que el chequeo previo, y la fila original no cambia.
        Assert.Equal(ExternalLoginLinkOutcome.RejectedOwnedByAnotherAccount, result.Outcome);
        Assert.Equal(1, await _context.ExternalLogins.CountAsync());
        var link = await _context.ExternalLogins.AsNoTracking()
            .SingleAsync(l => l.Provider == "Google" && l.ProviderKey == "race-key");
        Assert.Equal(owner.Id, link.UserId);
    }

    [Fact]
    public async Task UnlinkAsync_WhenAccountHasTwoOrMoreLinks_ShouldRemoveOnlyTheIndicatedOne()
    {
        // Arrange: la cuenta tiene dos vínculos, así que desvincular uno deja el otro intacto.
        var user = await SeedUserAsync(new AppUser("user-unlink-1", "Jugadora Unlink", "unlink1@ludeka.es"));
        await _externalLoginRepository.AddAsync(new ExternalLogin(user.Id, "Google", "google-unlink"));
        await _externalLoginRepository.AddAsync(new ExternalLogin(user.Id, "Discord", "discord-unlink"));

        // Act
        await _service.UnlinkAsync(user.Id, "Google");

        // Assert: la fila de Google se eliminó y la cuenta conserva la de Discord.
        var remaining = await _externalLoginRepository.ListByUserIdAsync(user.Id);
        Assert.Single(remaining);
        Assert.Equal("Discord", remaining[0].Provider);
    }

    [Fact]
    public async Task UnlinkAsync_WhenAccountHasOnlyOneLink_ShouldDenyEvenWhenCalledDirectlyBypassingTheInterface()
    {
        // Arrange: una cuenta con exactamente un único vínculo.
        var user = await SeedUserAsync(new AppUser("user-guard-1", "Jugadora Guarda", "guarda1@ludeka.es"));
        await _externalLoginRepository.AddAsync(new ExternalLogin(user.Id, "Google", "google-guarda"));

        // Act + Assert: se llama DIRECTAMENTE a UnlinkAsync desde la prueba, sin pasar por ningún
        // botón — es, por construcción, "la petición que evita la interfaz" del escenario.
        await Assert.ThrowsAsync<LastAccessMethodException>(
            () => _service.UnlinkAsync(user.Id, "Google"));

        // Assert: la fila permanece vinculada.
        var remaining = await _externalLoginRepository.ListByUserIdAsync(user.Id);
        Assert.Single(remaining);
    }

    [Fact]
    public async Task UnlinkAsync_WhenProviderNotLinked_ShouldBeIdempotentAndNotThrow()
    {
        // Arrange: la cuenta tiene un vínculo, pero no con el proveedor que se intenta desvincular.
        var user = await SeedUserAsync(new AppUser("user-unlink-2", "Jugador Idempotente", "unlink2@ludeka.es"));
        await _externalLoginRepository.AddAsync(new ExternalLogin(user.Id, "Discord", "discord-idempotente"));

        // Act + Assert: no lanza, y la fila existente permanece intacta.
        await _service.UnlinkAsync(user.Id, "Google");
        var remaining = await _externalLoginRepository.ListByUserIdAsync(user.Id);
        Assert.Single(remaining);
        Assert.Equal("Discord", remaining[0].Provider);
    }

    /// <summary>
    /// Doble de prueba que fuerza la ventana de carrera del escenario "La fila en conflicto nunca
    /// cambia de propietario": la comprobación previa nunca ve la fila competidora (como ocurriría
    /// si dos intentos llegan casi a la vez), pero el <see cref="AddAsync"/> real sigue chocando
    /// contra el índice único de <c>(Provider, ProviderKey)</c>.
    /// </summary>
    private sealed class RaceSimulatingRepository : IExternalLoginRepository
    {
        private readonly IExternalLoginRepository _inner;

        public RaceSimulatingRepository(IExternalLoginRepository inner) => _inner = inner;

        public Task<ExternalLogin?> GetByProviderKeyAsync(string provider, string providerKey, CancellationToken cancellationToken = default)
            => Task.FromResult<ExternalLogin?>(null);

        public Task AddAsync(ExternalLogin externalLogin, CancellationToken cancellationToken = default)
            => _inner.AddAsync(externalLogin, cancellationToken);

        public Task<IReadOnlyList<ExternalLogin>> ListByUserIdAsync(string userId, CancellationToken cancellationToken = default)
            => _inner.ListByUserIdAsync(userId, cancellationToken);

        public Task RemoveAsync(ExternalLogin externalLogin, CancellationToken cancellationToken = default)
            => _inner.RemoveAsync(externalLogin, cancellationToken);
    }
}
