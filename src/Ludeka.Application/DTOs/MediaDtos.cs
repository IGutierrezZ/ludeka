using System;
using System.Collections.Generic;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Application.DTOs;

public record MediaItemDto(
    Guid Id,
    Guid? GameId,
    string? GameTitle,
    MediaType Type,
    MediaCategory Category,
    MediaPlatform Platform,
    string Title,
    string Url,
    string? EmbedUrl,
    string ThumbnailUrl,
    string AuthorChannel,
    int? DurationSeconds,
    string FormattedDuration,
    string? PlayerCountBadge,
    int? LikesCount,
    string? Excerpt,
    ModerationStatus Status,
    bool IsBroken,
    bool IsOrphan,
    DateTimeOffset PublishedAt,
    DateTimeOffset CreatedAt,
    int UserLikesCount = 0
)
{
    public string CategoryDisplayName => Category switch
    {
        MediaCategory.QuickOverview => "Cómo Funciona",
        MediaCategory.Tutorial => "Tutorial",
        MediaCategory.Gameplay => "Partida Completa",
        MediaCategory.ReviewOpinion => "Reseña u Opinión",
        _ => Category.ToString()
    };

    public static MediaItemDto FromDomain(MediaItem item, string? gameTitle = null, int userLikesCount = 0)
    {
        return new MediaItemDto(
            item.Id,
            item.GameId,
            gameTitle ?? item.Game?.SpanishTitle,
            item.Type,
            item.Category,
            item.Platform,
            item.Title,
            item.Url,
            item.EmbedUrl,
            item.ThumbnailUrl,
            item.AuthorChannel,
            item.DurationSeconds,
            item.FormattedDuration,
            item.PlayerCountBadge,
            item.LikesCount,
            item.Excerpt,
            item.Status,
            item.IsBroken,
            item.IsOrphan,
            item.PublishedAt,
            item.CreatedAt,
            userLikesCount
        );
    }
}

public record GameMediaHubDto(
    Guid GameId,
    IReadOnlyList<MediaItemDto> Tutorials,
    IReadOnlyList<MediaItemDto> Playthroughs,
    IReadOnlyList<MediaItemDto> InstagramPosts,
    IReadOnlyList<MediaItemDto> ShortReels,
    IReadOnlyList<MediaItemDto>? QuickOverviews = null,
    IReadOnlyList<MediaItemDto>? ReviewsAndOpinions = null
)
{
    public IReadOnlyList<MediaItemDto> QuickOverviews { get; init; } = QuickOverviews ?? [];
    public IReadOnlyList<MediaItemDto> ReviewsAndOpinions { get; init; } = ReviewsAndOpinions ?? [];
    public int TotalCount => Tutorials.Count + Playthroughs.Count + InstagramPosts.Count + ShortReels.Count + QuickOverviews.Count + ReviewsAndOpinions.Count;
    public bool HasMedia => TotalCount > 0;
    public bool HasQuickOverviews => QuickOverviews.Count > 0;
    public bool HasTutorials => Tutorials.Count > 0;
    public bool HasPlaythroughs => Playthroughs.Count > 0;
    public bool HasSocial => InstagramPosts.Count > 0 || ShortReels.Count > 0 || ReviewsAndOpinions.Count > 0;
}

public record ModerateMediaItemRequest(
    Guid MediaId,
    bool Approve,
    MediaCategory? Category = null
);

public record UpdateMediaCategoryRequest(
    Guid MediaId,
    MediaCategory NewCategory
);

public record ReassignMediaGameRequest(
    Guid MediaId,
    Guid NewGameId
);

public record BrokenLinkReportDto(
    int TotalChecked,
    int BrokenCount,
    IReadOnlyList<MediaItemDto> BrokenItems
);
