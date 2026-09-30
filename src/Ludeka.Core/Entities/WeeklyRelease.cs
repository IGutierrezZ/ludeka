using System;

namespace Ludeka.Core.Entities;

public class WeeklyRelease
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Title { get; private set; } = string.Empty;
    public string Publisher { get; private set; } = string.Empty;
    public DateOnly ReleaseDate { get; private set; }
    public Guid? GameId { get; private set; }
    public string? CoverImageUrl { get; private set; }
    public decimal? EstimatedPvp { get; private set; }
    public bool IsReprint { get; private set; }
    public string? Notes { get; private set; }
    public string? SourceUrl { get; private set; }
    public string? InstagramMediaId { get; private set; }
    public string? InstagramPermalink { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; private set; }

    public virtual Game? Game { get; private set; }

    public bool IsPublishedOnInstagram => !string.IsNullOrWhiteSpace(InstagramPermalink);

    private WeeklyRelease() { }

    public WeeklyRelease(
        string title,
        string publisher,
        DateOnly releaseDate,
        Guid? gameId = null,
        string? coverImageUrl = null,
        decimal? estimatedPvp = null,
        bool isReprint = false,
        string? notes = null,
        string? sourceUrl = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("El título del lanzamiento no puede estar vacío.", nameof(title));

        if (string.IsNullOrWhiteSpace(publisher))
            throw new ArgumentException("La editorial del lanzamiento no puede estar vacía.", nameof(publisher));

        Title = title.Trim();
        Publisher = publisher.Trim();
        ReleaseDate = releaseDate;
        GameId = gameId;
        CoverImageUrl = string.IsNullOrWhiteSpace(coverImageUrl) ? null : coverImageUrl.Trim();
        EstimatedPvp = estimatedPvp;
        IsReprint = isReprint;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        SourceUrl = string.IsNullOrWhiteSpace(sourceUrl) ? null : sourceUrl.Trim();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void Update(
        string title,
        string publisher,
        DateOnly releaseDate,
        Guid? gameId = null,
        string? coverImageUrl = null,
        decimal? estimatedPvp = null,
        bool isReprint = false,
        string? notes = null,
        string? sourceUrl = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("El título del lanzamiento no puede estar vacío.", nameof(title));

        if (string.IsNullOrWhiteSpace(publisher))
            throw new ArgumentException("La editorial del lanzamiento no puede estar vacía.", nameof(publisher));

        Title = title.Trim();
        Publisher = publisher.Trim();
        ReleaseDate = releaseDate;
        GameId = gameId;
        CoverImageUrl = string.IsNullOrWhiteSpace(coverImageUrl) ? null : coverImageUrl.Trim();
        EstimatedPvp = estimatedPvp;
        IsReprint = isReprint;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        SourceUrl = string.IsNullOrWhiteSpace(sourceUrl) ? null : sourceUrl.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void LinkGame(Guid gameId)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException("El identificador del juego vinculado no puede ser vacío.", nameof(gameId));

        GameId = gameId;
    }

    public void MarkPublishedOnInstagram(string mediaId, string permalink)
    {
        if (string.IsNullOrWhiteSpace(mediaId))
            throw new ArgumentException("El identificador de medio de Instagram no puede estar vacío.", nameof(mediaId));

        if (string.IsNullOrWhiteSpace(permalink))
            throw new ArgumentException("El enlace permanente de Instagram no puede estar vacío.", nameof(permalink));

        InstagramMediaId = mediaId.Trim();
        InstagramPermalink = permalink.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
