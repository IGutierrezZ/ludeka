using System;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Xunit;

namespace Ludeka.UnitTests.Domain;

public class SocialInboxItemTests
{
    [Fact]
    public void Constructor_ValidArguments_CreatesPendingItem()
    {
        // Arrange & Act
        var item = new SocialInboxItem(
            sourceUrl: "https://www.instagram.com/p/test123/",
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Sorteo de Ark Nova",
            organizerOrAuthor: "Maldito Games",
            collaborator: "Ludeka",
            gameTitle: "Ark Nova",
            eventOrReleaseDate: DateTimeOffset.UtcNow.AddDays(7));

        // Assert
        Assert.NotEqual(Guid.Empty, item.Id);
        Assert.Equal("https://www.instagram.com/p/test123/", item.SourceUrl);
        Assert.Equal(SocialPlatform.Instagram, item.Platform);
        Assert.Equal(SocialSubmissionType.Giveaway, item.DetectedType);
        Assert.Equal(SocialInboxStatus.PendingReview, item.Status);
        Assert.Equal("Sorteo de Ark Nova", item.Title);
        Assert.Equal("Maldito Games", item.OrganizerOrAuthor);
        Assert.Equal("Ludeka", item.Collaborator);
        Assert.Equal("Ark Nova", item.GameTitle);
        Assert.Null(item.CreatedEntityId);
        Assert.Null(item.ReviewedAt);
    }

    [Theory]
    [InlineData("", "Título", "Organizador")]
    [InlineData("   ", "Título", "Organizador")]
    [InlineData("https://instagram.com", "", "Organizador")]
    [InlineData("https://instagram.com", "Título", "")]
    public void Constructor_InvalidArguments_ThrowsArgumentException(string url, string title, string organizer)
    {
        Assert.Throws<ArgumentException>(() => new SocialInboxItem(
            sourceUrl: url,
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: title,
            organizerOrAuthor: organizer));
    }

    [Fact]
    public void UpdateDetails_WhenPending_UpdatesFieldsSuccessfully()
    {
        // Arrange
        var item = new SocialInboxItem(
            sourceUrl: "https://www.youtube.com/watch?v=123",
            platform: SocialPlatform.YouTube,
            detectedType: SocialSubmissionType.MediaItem,
            title: "Partida errónea",
            organizerOrAuthor: "Canal A");

        var newGameId = Guid.NewGuid();
        var date = DateTimeOffset.UtcNow.AddDays(2);

        // Act
        item.UpdateDetails(
            title: "Tutorial Oficial Ark Nova",
            organizerOrAuthor: "Maldito Games",
            collaborator: null,
            detectedType: SocialSubmissionType.MediaItem,
            gameId: newGameId,
            gameTitle: "Ark Nova",
            eventOrReleaseDate: date,
            eventEndDate: null,
            location: null,
            estimatedPvp: null,
            mediaCategory: MediaCategory.Tutorial,
            playerCountBadge: "1-4 Jugadores",
            thumbnailUrl: "https://cdn.ludeka.com/thumb.webp",
            moderatorNotes: "Corregido por moderador");

        // Assert
        Assert.Equal("Tutorial Oficial Ark Nova", item.Title);
        Assert.Equal("Maldito Games", item.OrganizerOrAuthor);
        Assert.Equal(newGameId, item.GameId);
        Assert.Equal("Ark Nova", item.GameTitle);
        Assert.Equal(MediaCategory.Tutorial, item.MediaCategory);
        Assert.Equal("1-4 Jugadores", item.PlayerCountBadge);
        Assert.Equal("https://cdn.ludeka.com/thumb.webp", item.ThumbnailUrl);
        Assert.Equal("Corregido por moderador", item.ModeratorNotes);
        Assert.Equal(SocialInboxStatus.PendingReview, item.Status);
    }

    [Fact]
    public void Approve_ValidIdAndReviewer_SetsStatusApproved()
    {
        // Arrange
        var item = new SocialInboxItem(
            sourceUrl: "https://www.instagram.com/p/123",
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Sorteo Cascadia",
            organizerOrAuthor: "Editorial");

        var createdId = Guid.NewGuid();

        // Act
        item.Approve(createdId, "moderador_1");

        // Assert
        Assert.Equal(SocialInboxStatus.Approved, item.Status);
        Assert.Equal(createdId, item.CreatedEntityId);
        Assert.Equal("moderador_1", item.ReviewedByUserId);
        Assert.NotNull(item.ReviewedAt);
    }

    [Fact]
    public void Approve_WhenAlreadyApproved_ThrowsInvalidOperationException()
    {
        // Arrange
        var item = new SocialInboxItem(
            sourceUrl: "https://www.instagram.com/p/123",
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Sorteo Cascadia",
            organizerOrAuthor: "Editorial");

        item.Approve(Guid.NewGuid(), "moderador_1");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => item.Approve(Guid.NewGuid(), "moderador_2"));
    }

    [Fact]
    public void Reject_ValidReasonAndReviewer_SetsStatusRejected()
    {
        // Arrange
        var item = new SocialInboxItem(
            sourceUrl: "https://www.instagram.com/p/123",
            platform: SocialPlatform.Instagram,
            detectedType: SocialSubmissionType.Giveaway,
            title: "Contenido Spam",
            organizerOrAuthor: "Desconocido");

        // Act
        item.Reject("No es un juego de mesa", "moderador_1");

        // Assert
        Assert.Equal(SocialInboxStatus.Rejected, item.Status);
        Assert.Equal("No es un juego de mesa", item.ModeratorNotes);
        Assert.Equal("moderador_1", item.ReviewedByUserId);
        Assert.NotNull(item.ReviewedAt);
    }
}
