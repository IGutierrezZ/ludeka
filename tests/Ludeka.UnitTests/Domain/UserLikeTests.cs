using System;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Xunit;

namespace Ludeka.UnitTests.Domain;

public class UserLikeTests
{
    private static readonly Guid ValidUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ValidTargetId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Theory]
    [InlineData(LikeTargetType.Publisher)]
    [InlineData(LikeTargetType.Store)]
    [InlineData(LikeTargetType.Creator)]
    [InlineData(LikeTargetType.MediaItem)]
    public void Constructor_ShouldInitializeCorrectly_WhenParametersAreValid(LikeTargetType targetType)
    {
        // Arrange
        var now = new DateTimeOffset(2026, 9, 25, 20, 0, 0, TimeSpan.Zero);
        var customId = Guid.NewGuid();

        // Act
        var like = new UserLike(ValidUserId, targetType, ValidTargetId, customId, now);

        // Assert
        Assert.Equal(customId, like.Id);
        Assert.Equal(ValidUserId, like.UserId);
        Assert.Equal(targetType, like.TargetType);
        Assert.Equal(ValidTargetId, like.TargetId);
        Assert.Equal(now, like.CreatedAt);
    }

    [Fact]
    public void Constructor_ShouldGenerateGuidAndUtcNow_WhenOmitted()
    {
        // Act
        var before = DateTimeOffset.UtcNow;
        var like = new UserLike(ValidUserId, LikeTargetType.Publisher, ValidTargetId);
        var after = DateTimeOffset.UtcNow;

        // Assert
        Assert.NotEqual(Guid.Empty, like.Id);
        Assert.InRange(like.CreatedAt, before, after);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenUserIdIsEmpty()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            new UserLike(Guid.Empty, LikeTargetType.Publisher, ValidTargetId));

        Assert.Contains("usuario", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenTargetIdIsEmpty()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            new UserLike(ValidUserId, LikeTargetType.Publisher, Guid.Empty));

        Assert.Contains("destino", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
