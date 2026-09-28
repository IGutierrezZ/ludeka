using System;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;

namespace Ludeka.Core.Entities;

public class Giveaway
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Title { get; private set; } = string.Empty;
    public string Organizer { get; private set; } = string.Empty;
    public string? Collaborator { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public GiveawayPlatform Platform { get; private set; } = GiveawayPlatform.Instagram;
    public string Country { get; private set; } = "España";
    public DateTimeOffset DeadlineAt { get; private set; }
    public Guid? GameId { get; private set; }
    public string? GameTitle { get; private set; }
    public string? ThumbnailUrl { get; private set; }
    public bool IsCommunityExclusive { get; private set; }
    public bool IsPromoted { get; private set; }
    public string? InstagramMediaId { get; private set; }
    public string? InstagramPermalink { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; private set; }

    // Relación de navegación opcional con Game
    public virtual Game? Game { get; private set; }

    public bool IsExpired => DeadlineAt < DateTimeOffset.UtcNow;
    public bool IsInternational => CountryCatalog.IsInternational(Country);
    public bool IsPublishedOnInstagram => !string.IsNullOrWhiteSpace(InstagramPermalink);

    public string FormattedOrganizer => string.IsNullOrWhiteSpace(Collaborator)
        ? Organizer
        : $"{Organizer} en colaboración con {Collaborator}";

    // Constructor para EF Core
    private Giveaway() { }

    public Giveaway(
        string title,
        string organizer,
        string url,
        GiveawayPlatform platform,
        DateTimeOffset deadlineAt,
        string country = "España",
        Guid? gameId = null,
        string? gameTitle = null,
        string? collaborator = null,
        string? thumbnailUrl = null,
        bool isCommunityExclusive = false,
        bool isPromoted = false)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("El título del sorteo no puede estar vacío.", nameof(title));

        if (string.IsNullOrWhiteSpace(organizer))
            throw new ArgumentException("El organizador del sorteo no puede estar vacío.", nameof(organizer));

        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("La URL del sorteo no puede estar vacía.", nameof(url));

        Title = title.Trim();
        Organizer = organizer.Trim();
        Url = url.Trim();
        Platform = platform;
        Country = string.IsNullOrWhiteSpace(country) ? "España" : CountryCatalog.Normalize(country);
        DeadlineAt = deadlineAt;
        GameId = gameId;
        GameTitle = string.IsNullOrWhiteSpace(gameTitle) ? null : gameTitle.Trim();
        Collaborator = string.IsNullOrWhiteSpace(collaborator) ? null : collaborator.Trim();
        ThumbnailUrl = string.IsNullOrWhiteSpace(thumbnailUrl) ? null : thumbnailUrl.Trim();
        IsCommunityExclusive = isCommunityExclusive;
        IsPromoted = isPromoted;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateCountry(string country)
    {
        Country = string.IsNullOrWhiteSpace(country) ? "España" : CountryCatalog.Normalize(country);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool IsAvailableInCountry(string? targetCountry)
    {
        if (string.IsNullOrWhiteSpace(targetCountry))
            return true;

        if (IsInternational)
            return true;

        return string.Equals(CountryCatalog.Normalize(Country), CountryCatalog.Normalize(targetCountry), StringComparison.OrdinalIgnoreCase);
    }

    public void SetPromoted(bool isPromoted)
    {
        IsPromoted = isPromoted;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MergeCollaborator(string newCollaborator)
    {
        if (string.IsNullOrWhiteSpace(newCollaborator))
            return;

        var cleanCollaborator = newCollaborator.Trim();

        if (string.IsNullOrWhiteSpace(Collaborator))
        {
            Collaborator = cleanCollaborator;
        }
        else if (!Collaborator.Contains(cleanCollaborator, StringComparison.OrdinalIgnoreCase))
        {
            Collaborator = $"{Collaborator} y {cleanCollaborator}";
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ExtendDeadline(DateTimeOffset newDeadline)
    {
        if (newDeadline <= DeadlineAt)
            throw new InvalidOperationException("La nueva fecha límite debe ser posterior a la fecha límite actual.");

        DeadlineAt = newDeadline;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AssociateGame(Guid gameId, string? gameTitle = null)
    {
        GameId = gameId;
        if (!string.IsNullOrWhiteSpace(gameTitle))
            GameTitle = gameTitle.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
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

    public void Update(
        string title,
        string organizer,
        string? collaborator,
        string url,
        GiveawayPlatform platform,
        DateTimeOffset deadlineAt,
        string country = "España",
        Guid? gameId = null,
        string? gameTitle = null,
        string? thumbnailUrl = null,
        bool isCommunityExclusive = false,
        bool isPromoted = false)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("El título del sorteo no puede estar vacío.", nameof(title));

        if (string.IsNullOrWhiteSpace(organizer))
            throw new ArgumentException("El organizador del sorteo no puede estar vacío.", nameof(organizer));

        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("La URL del sorteo no puede estar vacía.", nameof(url));

        Title = title.Trim();
        Organizer = organizer.Trim();
        Collaborator = string.IsNullOrWhiteSpace(collaborator) ? null : collaborator.Trim();
        Url = url.Trim();
        Platform = platform;
        DeadlineAt = deadlineAt;
        Country = string.IsNullOrWhiteSpace(country) ? "España" : CountryCatalog.Normalize(country);
        GameId = gameId;
        GameTitle = string.IsNullOrWhiteSpace(gameTitle) ? null : gameTitle.Trim();
        ThumbnailUrl = string.IsNullOrWhiteSpace(thumbnailUrl) ? null : thumbnailUrl.Trim();
        IsCommunityExclusive = isCommunityExclusive;
        IsPromoted = isPromoted;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
