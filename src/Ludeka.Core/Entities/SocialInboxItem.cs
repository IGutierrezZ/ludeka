using System;
using Ludeka.Core.Enums;

namespace Ludeka.Core.Entities;

/// <summary>
/// Elemento de la bandeja de entrada de ingesta social y moderación.
/// Permite capturar publicaciones de Instagram, YouTube o la web y editarlas antes de aprobar y publicar.
/// </summary>
public class SocialInboxItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string SourceUrl { get; private set; } = string.Empty;
    public SocialPlatform Platform { get; private set; } = SocialPlatform.Instagram;
    public SocialSubmissionType DetectedType { get; private set; } = SocialSubmissionType.Giveaway;
    public SocialInboxStatus Status { get; private set; } = SocialInboxStatus.PendingReview;

    // Metadatos editables por el moderador
    public string Title { get; private set; } = string.Empty;
    public string OrganizerOrAuthor { get; private set; } = string.Empty;
    public string? Collaborator { get; private set; }
    public Guid? GameId { get; private set; }
    public string? GameTitle { get; private set; }
    public DateTimeOffset? EventOrReleaseDate { get; private set; }
    public DateTimeOffset? EventEndDate { get; private set; }
    public string? Location { get; private set; }
    public decimal? EstimatedPvp { get; private set; }
    public MediaCategory? MediaCategory { get; private set; }
    public string? PlayerCountBadge { get; private set; }

    // Medios y texto original
    public string? OriginalCaption { get; private set; }
    public string? ThumbnailUrl { get; private set; }
    public bool IsVideo { get; private set; }
    public string? AiAnalysisNotes { get; private set; }

    // Auditoría y ciclo de vida
    public Guid? CreatedEntityId { get; private set; }
    public string? ModeratorNotes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? ReviewedByUserId { get; private set; }

    // Relación de navegación opcional con Game
    public virtual Game? Game { get; private set; }

    // Constructor privado para EF Core
    private SocialInboxItem() { }

    public SocialInboxItem(
        string sourceUrl,
        SocialPlatform platform,
        SocialSubmissionType detectedType,
        string title,
        string organizerOrAuthor,
        string? collaborator = null,
        Guid? gameId = null,
        string? gameTitle = null,
        DateTimeOffset? eventOrReleaseDate = null,
        DateTimeOffset? eventEndDate = null,
        string? location = null,
        decimal? estimatedPvp = null,
        MediaCategory? mediaCategory = null,
        string? playerCountBadge = null,
        string? originalCaption = null,
        string? thumbnailUrl = null,
        bool isVideo = false,
        string? aiAnalysisNotes = null,
        Guid? id = null,
        DateTimeOffset? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
            throw new ArgumentException("La URL de origen no puede estar vacía.", nameof(sourceUrl));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("El título del elemento no puede estar vacío.", nameof(title));

        if (string.IsNullOrWhiteSpace(organizerOrAuthor))
            throw new ArgumentException("El organizador o autor no puede estar vacío.", nameof(organizerOrAuthor));

        Id = id ?? Guid.NewGuid();
        SourceUrl = sourceUrl.Trim();
        Platform = platform;
        DetectedType = detectedType;
        Status = SocialInboxStatus.PendingReview;
        Title = title.Trim();
        OrganizerOrAuthor = organizerOrAuthor.Trim();
        Collaborator = string.IsNullOrWhiteSpace(collaborator) ? null : collaborator.Trim();
        GameId = gameId;
        GameTitle = string.IsNullOrWhiteSpace(gameTitle) ? null : gameTitle.Trim();
        EventOrReleaseDate = eventOrReleaseDate;
        EventEndDate = eventEndDate;
        Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        EstimatedPvp = estimatedPvp;
        MediaCategory = mediaCategory;
        PlayerCountBadge = string.IsNullOrWhiteSpace(playerCountBadge) ? null : playerCountBadge.Trim();
        OriginalCaption = string.IsNullOrWhiteSpace(originalCaption) ? null : originalCaption.Trim();
        ThumbnailUrl = string.IsNullOrWhiteSpace(thumbnailUrl) ? null : thumbnailUrl.Trim();
        IsVideo = isVideo;
        AiAnalysisNotes = string.IsNullOrWhiteSpace(aiAnalysisNotes) ? null : aiAnalysisNotes.Trim();
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Permite al moderador actualizar cualquier dato extraído antes de su aprobación o publicación final.
    /// </summary>
    public void UpdateDetails(
        string title,
        string organizerOrAuthor,
        string? collaborator,
        SocialSubmissionType detectedType,
        Guid? gameId,
        string? gameTitle,
        DateTimeOffset? eventOrReleaseDate,
        DateTimeOffset? eventEndDate,
        string? location,
        decimal? estimatedPvp,
        MediaCategory? mediaCategory,
        string? playerCountBadge,
        string? thumbnailUrl,
        string? moderatorNotes)
    {
        if (Status != SocialInboxStatus.PendingReview)
            throw new InvalidOperationException("Solo se pueden editar elementos en estado pendiente de revisión.");

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("El título del elemento no puede estar vacío.", nameof(title));

        if (string.IsNullOrWhiteSpace(organizerOrAuthor))
            throw new ArgumentException("El organizador o autor no puede estar vacío.", nameof(organizerOrAuthor));

        Title = title.Trim();
        OrganizerOrAuthor = organizerOrAuthor.Trim();
        Collaborator = string.IsNullOrWhiteSpace(collaborator) ? null : collaborator.Trim();
        DetectedType = detectedType;
        GameId = gameId;
        GameTitle = string.IsNullOrWhiteSpace(gameTitle) ? null : gameTitle.Trim();
        EventOrReleaseDate = eventOrReleaseDate;
        EventEndDate = eventEndDate;
        Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        EstimatedPvp = estimatedPvp;
        MediaCategory = mediaCategory;
        PlayerCountBadge = string.IsNullOrWhiteSpace(playerCountBadge) ? null : playerCountBadge.Trim();

        if (!string.IsNullOrWhiteSpace(thumbnailUrl))
        {
            ThumbnailUrl = thumbnailUrl.Trim();
        }

        ModeratorNotes = string.IsNullOrWhiteSpace(moderatorNotes) ? null : moderatorNotes.Trim();
    }

    public void UpdateThumbnailUrl(string? thumbnailUrl)
    {
        ThumbnailUrl = string.IsNullOrWhiteSpace(thumbnailUrl) ? null : thumbnailUrl.Trim();
    }

    public void SetAiAnalysisNotes(string? notes)
    {
        AiAnalysisNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    /// <summary>
    /// Marca el ítem como aprobado tras instanciar la entidad definitiva en el catálogo/radar.
    /// </summary>
    public void Approve(Guid createdEntityId, string reviewerUserId)
    {
        if (Status != SocialInboxStatus.PendingReview)
            throw new InvalidOperationException("Solo se pueden aprobar elementos pendientes de revisión.");

        if (createdEntityId == Guid.Empty)
            throw new ArgumentException("El identificador de la entidad creada no puede ser vacío.", nameof(createdEntityId));

        if (string.IsNullOrWhiteSpace(reviewerUserId))
            throw new ArgumentException("El identificador del moderador revisor no puede estar vacío.", nameof(reviewerUserId));

        Status = SocialInboxStatus.Approved;
        CreatedEntityId = createdEntityId;
        ReviewedByUserId = reviewerUserId.Trim();
        ReviewedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Descarta el ítem de la bandeja sin instanciar entidades públicas.
    /// </summary>
    public void Reject(string? reason, string reviewerUserId)
    {
        if (Status != SocialInboxStatus.PendingReview)
            throw new InvalidOperationException("Solo se pueden rechazar elementos pendientes de revisión.");

        if (string.IsNullOrWhiteSpace(reviewerUserId))
            throw new ArgumentException("El identificador del moderador revisor no puede estar vacío.", nameof(reviewerUserId));

        Status = SocialInboxStatus.Rejected;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            ModeratorNotes = reason.Trim();
        }
        ReviewedByUserId = reviewerUserId.Trim();
        ReviewedAt = DateTimeOffset.UtcNow;
    }
}
