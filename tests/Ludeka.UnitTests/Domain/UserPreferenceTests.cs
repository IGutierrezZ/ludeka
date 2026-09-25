using System;
using Ludeka.Core.Entities;
using Xunit;

namespace Ludeka.UnitTests.Domain;

public class UserPreferenceTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithNormalizedTheme_WhenValid()
    {
        // Arrange & Act
        var pref = new UserPreference("usuario-1", "wood");

        // Assert
        Assert.Equal("usuario-1", pref.UserId);
        Assert.Equal("wood", pref.PreferredTheme);
        Assert.False(pref.HidePublicProfile);
        Assert.True((DateTime.UtcNow - pref.UpdatedAt).TotalSeconds < 5);
    }

    [Fact]
    public void Constructor_ShouldInitializeWithHidePublicProfile_WhenProvided()
    {
        // Arrange & Act
        var pref = new UserPreference("usuario-1", "wood", "España", hidePublicProfile: true);

        // Assert
        Assert.Equal("usuario-1", pref.UserId);
        Assert.Equal("España", pref.Country);
        Assert.True(pref.HidePublicProfile);
        Assert.False(pref.LeaderboardOptIn);
        Assert.False(pref.LeaderboardAnonymous);
        Assert.Null(pref.LeaderboardPseudonym);
    }

    [Fact]
    public void SetLeaderboardPreferences_ShouldUpdateFlagsAndTimestamp()
    {
        // Arrange
        var pref = new UserPreference("usuario-1", "charcoal");
        var originalTime = pref.UpdatedAt;

        // Act
        pref.SetLeaderboardPreferences(optIn: true, anonymous: true, pseudonym: "Estratega");

        // Assert
        Assert.True(pref.LeaderboardOptIn);
        Assert.True(pref.LeaderboardAnonymous);
        Assert.Equal("Estratega", pref.LeaderboardPseudonym);
        Assert.True(pref.UpdatedAt >= originalTime);
    }

    [Fact]
    public void SetProfileVisibility_ShouldUpdateFlagAndTimestamp()
    {
        // Arrange
        var pref = new UserPreference("usuario-1", "charcoal");
        var originalTime = pref.UpdatedAt;

        // Act
        pref.SetProfileVisibility(true);

        // Assert
        Assert.True(pref.HidePublicProfile);
        Assert.True(pref.UpdatedAt >= originalTime);
    }

    [Theory]
    [InlineData("editorial", "editorial")]
    [InlineData("wood", "wood")]
    [InlineData("tabletop", "tabletop")]
    [InlineData("midnight", "midnight")]
    [InlineData("charcoal", "charcoal")]
    [InlineData("WOOD", "wood")]
    [InlineData("  Editorial  ", "editorial")]
    [InlineData("desconocido", "charcoal")]
    [InlineData(null, "charcoal")]
    [InlineData("", "charcoal")]
    public void NormalizeTheme_ShouldReturnExpectedNormalizedTheme(string? input, string expected)
    {
        // Act
        var result = UserPreference.NormalizeTheme(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SetTheme_ShouldUpdateThemeAndTimestamp()
    {
        // Arrange
        var pref = new UserPreference("usuario-1", "charcoal");
        var originalTime = pref.UpdatedAt;

        // Act
        pref.SetTheme("wood");

        // Assert
        Assert.Equal("wood", pref.PreferredTheme);
        Assert.True(pref.UpdatedAt >= originalTime);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrowArgumentException_WhenUserIdIsInvalid(string? invalidUserId)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new UserPreference(invalidUserId!, "editorial"));
    }
}
