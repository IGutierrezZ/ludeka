using System;
using Ludeka.Core.Enums;

namespace Ludeka.Core.Entities;

/// <summary>
/// Registro de control e idempotencia para cada Reel de la cuenta oficial de Instagram de Ludeka
/// sincronizado hacia el canal oficial de YouTube como YouTube Short (INC-150).
/// </summary>
public class InstagramReelSyncItem
{
    public Guid Id { get; private set; }
    public string InstagramMediaId { get; private set; } = string.Empty;
    public string InstagramPermalink { get; private set; } = string.Empty;
    public string? Caption { get; private set; }
    public DateTimeOffset MediaPublishedAtUtc { get; private set; }
    public InstagramReelSyncStatus Status { get; private set; }
    public string? GeneratedYouTubeTitle { get; private set; }
    public string? GeneratedYouTubeDescription { get; private set; }
    public string? YouTubeVideoId { get; private set; }
    public string? YouTubeUrl { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? SyncedAtUtc { get; private set; }

    private InstagramReelSyncItem() { }

    public InstagramReelSyncItem(
        string instagramMediaId,
        string instagramPermalink,
        string? caption,
        DateTimeOffset mediaPublishedAtUtc,
        Guid? id = null,
        DateTimeOffset? createdAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(instagramMediaId))
            throw new ArgumentException("El ID de media de Instagram no puede estar vacío.", nameof(instagramMediaId));

        if (string.IsNullOrWhiteSpace(instagramPermalink))
            throw new ArgumentException("El enlace permanente del Reel no puede estar vacío.", nameof(instagramPermalink));

        Id = id ?? Guid.NewGuid();
        InstagramMediaId = instagramMediaId.Trim();
        InstagramPermalink = instagramPermalink.Trim();
        Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
        MediaPublishedAtUtc = mediaPublishedAtUtc;
        Status = InstagramReelSyncStatus.Pending;
        AttemptCount = 0;
        CreatedAtUtc = createdAtUtc ?? DateTimeOffset.UtcNow;
    }

    public void UpdateAdaptedMetadata(string title, string description)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("El título adaptado para YouTube no puede estar vacío.", nameof(title));

        GeneratedYouTubeTitle = title.Trim().Length > 100
            ? title.Trim()[..100]
            : title.Trim();
        GeneratedYouTubeDescription = description?.Trim() ?? string.Empty;
    }

    public void MarkUploaded(string youTubeVideoId, string? youTubeUrl = null, DateTimeOffset? syncedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(youTubeVideoId))
            throw new ArgumentException("El ID de vídeo de YouTube no puede estar vacío.", nameof(youTubeVideoId));

        YouTubeVideoId = youTubeVideoId.Trim();
        YouTubeUrl = !string.IsNullOrWhiteSpace(youTubeUrl)
            ? youTubeUrl.Trim()
            : $"https://www.youtube.com/shorts/{YouTubeVideoId}";
        Status = InstagramReelSyncStatus.Uploaded;
        AttemptCount++;
        LastError = null;
        SyncedAtUtc = syncedAtUtc ?? DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = InstagramReelSyncStatus.Failed;
        AttemptCount++;
        LastError = string.IsNullOrWhiteSpace(errorMessage)
            ? "Error desconocido al subir el Short a YouTube."
            : (errorMessage.Trim().Length > 1000 ? errorMessage.Trim()[..1000] : errorMessage.Trim());
    }

    public void MarkSkipped(string reason)
    {
        Status = InstagramReelSyncStatus.Skipped;
        LastError = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }
}
