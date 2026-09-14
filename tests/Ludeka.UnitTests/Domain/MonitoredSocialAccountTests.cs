using System;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Xunit;

namespace Ludeka.UnitTests.Domain;

public class MonitoredSocialAccountTests
{
    [Fact]
    public void Constructor_ValidArguments_CreatesEnabledAccount()
    {
        // Act
        var account = new MonitoredSocialAccount(
            name: "Devir Iberia",
            platform: SocialPlatform.Instagram,
            handleOrChannelId: "@deviriberia",
            accountType: MonitoredAccountType.Publisher,
            profileUrl: "https://instagram.com/deviriberia",
            notes: "Editorial de referencia");

        // Assert
        Assert.NotEqual(Guid.Empty, account.Id);
        Assert.Equal("Devir Iberia", account.Name);
        Assert.Equal(SocialPlatform.Instagram, account.Platform);
        Assert.Equal("@deviriberia", account.HandleOrChannelId);
        Assert.Equal(MonitoredAccountType.Publisher, account.AccountType);
        Assert.Equal("https://instagram.com/deviriberia", account.ProfileUrl);
        Assert.True(account.IsEnabled);
        Assert.Equal("Editorial de referencia", account.Notes);
        Assert.Null(account.LastCheckedAt);
    }

    [Theory]
    [InlineData("", "@handle", "https://instagram.com")]
    [InlineData("Devir", "", "https://instagram.com")]
    [InlineData("Devir", "@handle", "")]
    public void Constructor_InvalidArguments_ThrowsArgumentException(string name, string handle, string url)
    {
        Assert.Throws<ArgumentException>(() => new MonitoredSocialAccount(
            name: name,
            platform: SocialPlatform.Instagram,
            handleOrChannelId: handle,
            accountType: MonitoredAccountType.Publisher,
            profileUrl: url));
    }

    [Fact]
    public void ToggleStatus_ChangesIsEnabledAndSetsUpdatedAt()
    {
        // Arrange
        var account = new MonitoredSocialAccount(
            name: "Zacatrus",
            platform: SocialPlatform.YouTube,
            handleOrChannelId: "UC12345",
            accountType: MonitoredAccountType.Store,
            profileUrl: "https://youtube.com/zacatrus");

        // Act
        account.ToggleStatus(false);

        // Assert
        Assert.False(account.IsEnabled);
        Assert.NotNull(account.UpdatedAt);
    }

    [Fact]
    public void MarkChecked_SetsLastCheckedAt()
    {
        // Arrange
        var account = new MonitoredSocialAccount(
            name: "Mesa 21",
            platform: SocialPlatform.Instagram,
            handleOrChannelId: "@mesa21",
            accountType: MonitoredAccountType.Creator,
            profileUrl: "https://instagram.com/mesa21");

        // Act
        account.MarkChecked();

        // Assert
        Assert.NotNull(account.LastCheckedAt);
    }
}
