using System;
using System.Linq;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Xunit;

namespace Ludeka.UnitTests.Domain;

public class UserMilestoneTests
{
    private const string ValidUserId = "user-12345";
    private const MilestoneType ValidType = MilestoneType.FirstGameInCollection;

    [Fact]
    public void Constructor_ShouldInitializeCorrectly_WhenParametersAreValid()
    {
        // Arrange
        var customId = Guid.NewGuid();
        var customDate = new DateTimeOffset(2026, 9, 25, 21, 0, 0, TimeSpan.Zero);

        // Act
        var milestone = new UserMilestone(ValidUserId, ValidType, customDate, customId);

        // Assert
        Assert.Equal(customId, milestone.Id);
        Assert.Equal(ValidUserId, milestone.UserId);
        Assert.Equal(ValidType, milestone.Type);
        Assert.Equal(customDate, milestone.UnlockedAt);
    }

    [Fact]
    public void Constructor_ShouldGenerateGuidAndUtcNow_WhenOmitted()
    {
        // Act
        var before = DateTimeOffset.UtcNow;
        var milestone = new UserMilestone(ValidUserId, ValidType);
        var after = DateTimeOffset.UtcNow;

        // Assert
        Assert.NotEqual(Guid.Empty, milestone.Id);
        Assert.InRange(milestone.UnlockedAt, before, after);
    }

    [Fact]
    public void Constructor_ShouldTrimUserId()
    {
        // Act
        var milestone = new UserMilestone("   user-padded   ", ValidType);

        // Assert
        Assert.Equal("user-padded", milestone.UserId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrowArgumentException_WhenUserIdIsNullOrWhitespace(string? invalidUserId)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => new UserMilestone(invalidUserId!, ValidType));
        Assert.Contains("usuario", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentOutOfRangeException_WhenMilestoneTypeIsInvalid()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new UserMilestone(ValidUserId, (MilestoneType)9999));
    }

    [Fact]
    public void MilestoneCatalog_ShouldContainDefinitionsForAllMilestoneTypes()
    {
        // Arrange
        var definedTypes = Enum.GetValues<MilestoneType>();

        // Act
        var allDefinitions = MilestoneCatalog.All;

        // Assert
        Assert.Equal(definedTypes.Length, allDefinitions.Count);
        foreach (var type in definedTypes)
        {
            var def = MilestoneCatalog.Get(type);
            Assert.NotNull(def);
            Assert.Equal(type, def.Type);
            Assert.False(string.IsNullOrWhiteSpace(def.Title));
            Assert.False(string.IsNullOrWhiteSpace(def.Description));
            Assert.False(string.IsNullOrWhiteSpace(def.IconEmoji));
            Assert.True(Enum.IsDefined(typeof(MilestoneCategory), def.Category));
        }
    }

    [Fact]
    public void MilestoneCatalog_TryGet_ShouldReturnCorrectly()
    {
        // Act & Assert
        Assert.True(MilestoneCatalog.TryGet(MilestoneType.FirstGameInCollection, out var validDef));
        Assert.NotNull(validDef);
        Assert.Equal("Primera Piedra", validDef.Title);

        Assert.False(MilestoneCatalog.TryGet((MilestoneType)9999, out var invalidDef));
        Assert.Null(invalidDef);
    }

    [Fact]
    public void MilestoneCatalog_All_ShouldHaveUniqueTitlesAndTypes()
    {
        // Arrange
        var all = MilestoneCatalog.All;

        // Act & Assert
        var uniqueTypes = all.Select(m => m.Type).Distinct().Count();
        var uniqueTitles = all.Select(m => m.Title).Distinct().Count();

        Assert.Equal(all.Count, uniqueTypes);
        Assert.Equal(all.Count, uniqueTitles);
    }
}
