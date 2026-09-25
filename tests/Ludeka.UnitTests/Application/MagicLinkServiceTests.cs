using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Ludeka.Application.Features.Identity;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class MagicLinkServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private LudekaDbContext _context = null!;
    private MagicLinkTokenRepository _tokenRepository = null!;
    private SqliteUserRepository _userRepository = null!;
    private DevelopmentEmailSender _emailSender = null!;
    private MagicLinkService _service = null!;
    private readonly MagicLinkOptions _options = new()
    {
        TokenLifetimeMinutes = 15,
        BaseUrl = "https://ludeka.es"
    };

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        await _connection.OpenAsync();

        var dbOptions = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LudekaDbContext(dbOptions);
        await _context.Database.EnsureCreatedAsync();

        _tokenRepository = new MagicLinkTokenRepository(_context);
        _userRepository = new SqliteUserRepository(_context);
        _emailSender = new DevelopmentEmailSender(NullLogger<DevelopmentEmailSender>.Instance);

        _service = new MagicLinkService(
            _tokenRepository,
            _userRepository,
            _emailSender,
            Options.Create(_options),
            NullLogger<MagicLinkService>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("correo-sin-arroba")]
    [InlineData("@sinusuario.com")]
    [InlineData("sindominio@")]
    public async Task RequestMagicLinkAsync_ShouldReturnError_WhenEmailIsInvalid(string? invalidEmail)
    {
        var result = await _service.RequestMagicLinkAsync(invalidEmail!);

        Assert.False(result.Success);
        Assert.Contains("correo válida", result.Message);
        Assert.Empty(_emailSender.SentEmails);
    }

    [Fact]
    public async Task RequestMagicLinkAsync_ShouldPersistTokenAndSendEmail_WhenEmailIsValid()
    {
        var result = await _service.RequestMagicLinkAsync("Jugador@Ludeka.es", returnUrl: "/cuenta");

        Assert.True(result.Success);
        Assert.NotNull(result.DevTokenLink);
        Assert.Contains("https://ludeka.es/login/magic-link?token=", result.DevTokenLink);
        Assert.Contains("&returnUrl=%2Fcuenta", result.DevTokenLink);

        // Verifica que se guardó el token hasheado en la base de datos
        var tokenInDb = await _context.MagicLinkTokens.FirstOrDefaultAsync();
        Assert.NotNull(tokenInDb);
        Assert.Equal("jugador@ludeka.es", tokenInDb.Email);
        Assert.Null(tokenInDb.ConsumedAt);
        Assert.True(tokenInDb.ExpiresAt > tokenInDb.CreatedAt);

        // Verifica envío de correo simulado
        var sentEmail = Assert.Single(_emailSender.SentEmails);
        Assert.Equal("jugador@ludeka.es", sentEmail.ToEmail);
        Assert.Contains(result.DevTokenLink, sentEmail.HtmlBody);
    }

    [Fact]
    public async Task VerifyAndConsumeAsync_ShouldReturnSuccessAndResolveExistingUser_WhenEmailMatches()
    {
        // Arrange: Creamos usuario existente
        var existingUser = new AppUser("user-existente", "carlos", "carlos@ludeka.es", UserRole.CommunityUser);
        await _userRepository.AddAsync(existingUser);

        // Solicitamos magic link
        var requestResult = await _service.RequestMagicLinkAsync("carlos@ludeka.es");
        Assert.True(requestResult.Success);

        // Extraer raw token de la URL
        var rawToken = ExtractTokenFromLink(requestResult.DevTokenLink!);

        // Act
        var verifyResult = await _service.VerifyAndConsumeAsync(rawToken);

        // Assert
        Assert.True(verifyResult.Success);
        Assert.NotNull(verifyResult.User);
        Assert.Equal("user-existente", verifyResult.User.Id);
        Assert.Equal("carlos@ludeka.es", verifyResult.User.Email);

        // Token consumido en base de datos
        var tokenInDb = await _context.MagicLinkTokens.FirstAsync();
        Assert.NotNull(tokenInDb.ConsumedAt);
    }

    [Fact]
    public async Task VerifyAndConsumeAsync_ShouldCreateNewUser_WhenEmailDoesNotExist()
    {
        // Solicitamos magic link para un usuario nuevo
        var requestResult = await _service.RequestMagicLinkAsync("nuevo.jugador@ludeka.es");
        var rawToken = ExtractTokenFromLink(requestResult.DevTokenLink!);

        // Act
        var verifyResult = await _service.VerifyAndConsumeAsync(rawToken);

        // Assert
        Assert.True(verifyResult.Success);
        Assert.NotNull(verifyResult.User);
        Assert.Equal("nuevo.jugador@ludeka.es", verifyResult.User.Email);
        Assert.Equal("nuevo.jugador", verifyResult.User.UserName);
        Assert.Equal(UserRole.CommunityUser, verifyResult.User.Role);
        Assert.Equal(UserStatus.Active, verifyResult.User.Status);

        // Confirmamos que el usuario está persistido en la base de datos
        var persistedUser = await _userRepository.GetByEmailAsync("nuevo.jugador@ludeka.es");
        Assert.NotNull(persistedUser);
        Assert.Equal(verifyResult.User.Id, persistedUser.Id);
    }

    [Fact]
    public async Task VerifyAndConsumeAsync_ShouldFailOnSecondAttempt_BecauseTokenIsConsumedAtomically()
    {
        var requestResult = await _service.RequestMagicLinkAsync("repetido@ludeka.es");
        var rawToken = ExtractTokenFromLink(requestResult.DevTokenLink!);

        var firstAttempt = await _service.VerifyAndConsumeAsync(rawToken);
        Assert.True(firstAttempt.Success);

        var secondAttempt = await _service.VerifyAndConsumeAsync(rawToken);
        Assert.False(secondAttempt.Success);
        Assert.Contains("inválido o ha caducado", secondAttempt.ErrorMessage);
    }

    [Fact]
    public async Task VerifyAndConsumeAsync_ShouldFail_WhenTokenIsExpired()
    {
        var now = DateTimeOffset.UtcNow.AddMinutes(-30); // Creado hace 30 minutos
        var rawToken = "tokendepruebaexpirado12345678901234567890";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        var tokenHash = Convert.ToHexString(bytes).ToLowerInvariant();

        var expiredToken = new MagicLinkToken("expirado@ludeka.es", tokenHash, now, now.AddMinutes(15));
        await _tokenRepository.AddAsync(expiredToken);

        // Act
        var result = await _service.VerifyAndConsumeAsync(rawToken);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("inválido o ha caducado", result.ErrorMessage);
    }

    [Fact]
    public async Task VerifyAndConsumeAsync_ShouldFail_WhenTokenDoesNotExistOrInvalid()
    {
        var result = await _service.VerifyAndConsumeAsync("token-inexistente-totalmente-inventado");

        Assert.False(result.Success);
        Assert.Contains("inválido o ha caducado", result.ErrorMessage);
    }

    private static string ExtractTokenFromLink(string link)
    {
        var uri = new Uri(link);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        return query["token"]!;
    }
}
