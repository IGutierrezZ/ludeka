using System;
using Ludeka.Core.Entities;
using Xunit;

namespace Ludeka.UnitTests.Domain;

public class WeeklyReleaseDomainTests
{
    [Fact]
    public void Constructor_WithNullReleaseDate_InitializesSuccessfully()
    {
        // Act
        var release = new WeeklyRelease("Novedad Sin Fecha", "Devir", releaseDate: null);

        // Assert
        Assert.Null(release.ReleaseDate);
        Assert.Equal("Novedad Sin Fecha", release.Title);
        Assert.Equal("Devir", release.Publisher);
        Assert.True(release.CreatedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Update_WithNullReleaseDate_ClearsReleaseDate()
    {
        // Arrange
        var release = new WeeklyRelease("Novedad Con Fecha", "Devir", new DateOnly(2026, 11, 20));
        Assert.NotNull(release.ReleaseDate);

        // Act
        release.Update("Novedad Con Fecha Pospuesta", "Devir", releaseDate: null);

        // Assert
        Assert.Null(release.ReleaseDate);
        Assert.Equal("Novedad Con Fecha Pospuesta", release.Title);
        Assert.NotNull(release.UpdatedAt);
    }

    [Fact]
    public void Update_WithValidReleaseDate_UpdatesReleaseDate()
    {
        // Arrange
        var release = new WeeklyRelease("Novedad TBD", "Maldito", releaseDate: null);
        var targetDate = new DateOnly(2026, 12, 1);

        // Act
        release.Update("Novedad TBD", "Maldito", releaseDate: targetDate);

        // Assert
        Assert.Equal(targetDate, release.ReleaseDate);
        Assert.NotNull(release.UpdatedAt);
    }
}
