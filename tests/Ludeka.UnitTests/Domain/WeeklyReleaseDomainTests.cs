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

    [Fact]
    public void Status_DefaultsToPublished_CanBeSetToPendingModerationAndApproved()
    {
        var release = new WeeklyRelease("Crucero Galáctico", "Maldito Games");
        Assert.Equal(Ludeka.Core.Enums.WeeklyReleaseStatus.Published, release.Status);
        Assert.Null(release.AiSuggestedBggId);

        // Pasa a moderación con propuesta de IA
        release.SetPendingModeration(390111, "Galactic Cruise", "Edición en español de Galactic Cruise por Maldito Games.");
        Assert.Equal(Ludeka.Core.Enums.WeeklyReleaseStatus.PendingModeration, release.Status);
        Assert.Equal(390111, release.AiSuggestedBggId);
        Assert.Equal("Galactic Cruise", release.AiSuggestedTitle);
        Assert.Contains("Galactic Cruise", release.AiMatchReasoning);

        // Moderador aprueba vinculando juego
        var gameId = Guid.NewGuid();
        release.Approve(gameId);
        Assert.Equal(Ludeka.Core.Enums.WeeklyReleaseStatus.Published, release.Status);
        Assert.Equal(gameId, release.GameId);
    }

    [Fact]
    public void Reject_SetsStatusToRejected()
    {
        var release = new WeeklyRelease("Accesorio No Juego", "Maldito Games");
        release.SetPendingModeration();
        Assert.Equal(Ludeka.Core.Enums.WeeklyReleaseStatus.PendingModeration, release.Status);

        release.Reject();
        Assert.Equal(Ludeka.Core.Enums.WeeklyReleaseStatus.Rejected, release.Status);
    }
}

