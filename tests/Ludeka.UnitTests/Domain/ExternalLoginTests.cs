using System;
using Ludeka.Core.Entities;
using Xunit;

namespace Ludeka.UnitTests.Domain;

public class ExternalLoginTests
{
    private const string ValidUserId = "user-123";
    private const string ValidProvider = "Google";
    private const string ValidProviderKey = "105234567890123456789";

    [Fact]
    public void Constructor_ShouldInitializeCorrectly_WhenParametersAreValid()
    {
        // Arrange
        var linkedAt = new DateTimeOffset(2026, 9, 15, 12, 30, 0, TimeSpan.Zero);

        // Act
        var login = new ExternalLogin(ValidUserId, ValidProvider, ValidProviderKey, "alguien@ejemplo.com", linkedAt);

        // Assert
        Assert.NotEqual(Guid.Empty, login.Id);
        Assert.Equal(ValidUserId, login.UserId);
        Assert.Equal(ValidProvider, login.Provider);
        Assert.Equal(ValidProviderKey, login.ProviderKey);
        Assert.Equal("alguien@ejemplo.com", login.ProviderEmail);
        Assert.Equal(linkedAt, login.LinkedAt);
    }

    [Fact]
    public void Constructor_ShouldDefaultLinkedAtToUtcNow_WhenNotProvided()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow;

        // Act
        var login = new ExternalLogin(ValidUserId, ValidProvider, ValidProviderKey);

        // Assert
        var after = DateTimeOffset.UtcNow;
        Assert.InRange(login.LinkedAt, before, after);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrowArgumentException_WhenUserIdIsInvalid(string? invalidUserId)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new ExternalLogin(invalidUserId!, ValidProvider, ValidProviderKey));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrowArgumentException_WhenProviderIsInvalid(string? invalidProvider)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new ExternalLogin(ValidUserId, invalidProvider!, ValidProviderKey));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrowArgumentException_WhenProviderKeyIsInvalid(string? invalidProviderKey)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new ExternalLogin(ValidUserId, ValidProvider, invalidProviderKey!));
    }

    [Fact]
    public void Constructor_ShouldAllowMissingProviderEmail()
    {
        // Act
        var login = new ExternalLogin(ValidUserId, ValidProvider, ValidProviderKey);

        // Assert
        Assert.Null(login.ProviderEmail);
    }

    [Fact]
    public void Constructor_ShouldTrimValuesAndNormalizeBlankProviderEmail()
    {
        // Act
        var login = new ExternalLogin("  user-456  ", "  Discord  ", "  987654321098765432  ", "   ");

        // Assert
        Assert.Equal("user-456", login.UserId);
        Assert.Equal("Discord", login.Provider);
        Assert.Equal("987654321098765432", login.ProviderKey);
        Assert.Null(login.ProviderEmail);
    }

    [Fact]
    public void Constructor_ShouldAssignDistinctIds_ForDistinctLogins()
    {
        // Act
        var first = new ExternalLogin(ValidUserId, ValidProvider, "key-1");
        var second = new ExternalLogin(ValidUserId, ValidProvider, "key-2");

        // Assert
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Constructor_ShouldSetProviderEmailVerifiedAtToLinkedAt_WhenProviderEmailVerifiedAndEmailPresent()
    {
        // Arrange
        var linkedAt = new DateTimeOffset(2026, 9, 17, 9, 0, 0, TimeSpan.Zero);

        // Act: el proveedor entrega un correo y lo declara verificado
        var login = new ExternalLogin(
            ValidUserId, ValidProvider, ValidProviderKey, "alguien@ejemplo.com", linkedAt, providerEmailVerified: true);

        // Assert: el invariante de dominio fija la marca al mismo instante de vinculación
        Assert.Equal(linkedAt, login.ProviderEmailVerifiedAt);
    }

    [Fact]
    public void Constructor_ShouldLeaveProviderEmailVerifiedAtNull_WhenProviderEmailVerifiedButEmailMissing()
    {
        // Act: el proveedor declara verificación pero no entrega ningún correo utilizable
        var login = new ExternalLogin(
            ValidUserId, ValidProvider, ValidProviderKey, linkedAt: DateTimeOffset.UtcNow, providerEmailVerified: true);

        // Assert: sin correo no hay nada que verificar, así que la marca permanece nula
        Assert.Null(login.ProviderEmailVerifiedAt);
    }

    [Fact]
    public void Constructor_ShouldLeaveProviderEmailVerifiedAtNull_WhenProviderEmailNotVerified()
    {
        // Act: el correo llega, pero el proveedor no lo declaró verificado (valor por defecto)
        var login = new ExternalLogin(ValidUserId, ValidProvider, ValidProviderKey, "alguien@ejemplo.com");

        // Assert
        Assert.Null(login.ProviderEmailVerifiedAt);
    }
}
