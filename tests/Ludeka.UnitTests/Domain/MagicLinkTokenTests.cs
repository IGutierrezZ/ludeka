using System;
using Ludeka.Core.Entities;
using Xunit;

namespace Ludeka.UnitTests.Domain;

public class MagicLinkTokenTests
{
    private const string ValidEmail = "jugador@ludeka.es";
    private const string ValidTokenHash = "abc123hashsha256demo0000000000000000000000000000000000000000000000";

    [Fact]
    public void Constructor_ShouldInitializeCorrectly_WhenParametersAreValid()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);
        var expiresAt = createdAt.AddMinutes(15);

        // Act
        var token = new MagicLinkToken(ValidEmail, ValidTokenHash, createdAt, expiresAt, "user-42");

        // Assert
        Assert.NotEqual(Guid.Empty, token.Id);
        Assert.Equal(ValidEmail, token.Email);
        Assert.Equal(ValidTokenHash, token.TokenHash);
        Assert.Equal(createdAt, token.CreatedAt);
        Assert.Equal(expiresAt, token.ExpiresAt);
        Assert.Equal("user-42", token.TargetUserId);
        Assert.Null(token.ConsumedAt);
    }

    [Fact]
    public void Constructor_ShouldNormalizeEmail_ToLowerCaseAndTrim()
    {
        // Arrange
        var createdAt = DateTimeOffset.UtcNow;
        var expiresAt = createdAt.AddMinutes(15);

        // Act
        var token = new MagicLinkToken("  JUGADOR@Ludeka.ES  ", ValidTokenHash, createdAt, expiresAt);

        // Assert
        Assert.Equal("jugador@ludeka.es", token.Email);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrowArgumentException_WhenEmailIsInvalid(string? invalidEmail)
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() => new MagicLinkToken(invalidEmail!, ValidTokenHash, now, now.AddMinutes(15)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrowArgumentException_WhenTokenHashIsInvalid(string? invalidHash)
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() => new MagicLinkToken(ValidEmail, invalidHash!, now, now.AddMinutes(15)));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenExpiresAtIsBeforeOrEqualToCreatedAt()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() => new MagicLinkToken(ValidEmail, ValidTokenHash, now, now));
        Assert.Throws<ArgumentException>(() => new MagicLinkToken(ValidEmail, ValidTokenHash, now, now.AddMinutes(-5)));
    }

    [Fact]
    public void IsValid_ShouldReturnTrue_WhenNotConsumedAndNotExpired()
    {
        var now = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        var token = new MagicLinkToken(ValidEmail, ValidTokenHash, now, now.AddMinutes(15));

        Assert.True(token.IsValid(now.AddMinutes(5)));
        Assert.True(token.IsValid(now.AddMinutes(15)));
    }

    [Fact]
    public void IsValid_ShouldReturnFalse_WhenExpired()
    {
        var now = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        var token = new MagicLinkToken(ValidEmail, ValidTokenHash, now, now.AddMinutes(15));

        Assert.False(token.IsValid(now.AddMinutes(15).AddSeconds(1)));
    }

    [Fact]
    public void IsValid_ShouldReturnFalse_WhenConsumed()
    {
        var now = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        var token = new MagicLinkToken(ValidEmail, ValidTokenHash, now, now.AddMinutes(15));

        token.Consume(now.AddMinutes(2));

        Assert.False(token.IsValid(now.AddMinutes(5)));
    }

    [Fact]
    public void Consume_ShouldSetConsumedAt_WhenValid()
    {
        var now = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        var token = new MagicLinkToken(ValidEmail, ValidTokenHash, now, now.AddMinutes(15));
        var consumedTime = now.AddMinutes(3);

        token.Consume(consumedTime);

        Assert.Equal(consumedTime, token.ConsumedAt);
    }

    [Fact]
    public void Consume_ShouldThrowInvalidOperationException_WhenAlreadyConsumed()
    {
        var now = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        var token = new MagicLinkToken(ValidEmail, ValidTokenHash, now, now.AddMinutes(15));

        token.Consume(now.AddMinutes(2));

        Assert.Throws<InvalidOperationException>(() => token.Consume(now.AddMinutes(3)));
    }

    [Fact]
    public void Consume_ShouldThrowArgumentException_WhenConsumedAtIsBeforeCreatedAt()
    {
        var now = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        var token = new MagicLinkToken(ValidEmail, ValidTokenHash, now, now.AddMinutes(15));

        Assert.Throws<ArgumentException>(() => token.Consume(now.AddMinutes(-1)));
    }
}
