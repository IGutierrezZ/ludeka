using System;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Application.DTOs;

public record SocialMetadataResultDto(
    string Url,
    SocialPlatform Platform,
    string? Title,
    string? AuthorOrChannel,
    string? Description,
    string? ImageUrl,
    bool IsVideo,
    string? VideoId = null
);

public record NormalizedBoundingBoxDto(int YMin, int XMin, int YMax, int XMax)
{
    public bool IsValid => YMin >= 0 && XMin >= 0 && YMax <= 1000 && XMax <= 1000 && YMax > YMin && XMax > XMin;
}

public record SocialAiAnalysisResultDto(
    SocialSubmissionType DetectedType,
    string Title,
    string OrganizerOrAuthor,
    string? Collaborator,
    string? SuggestedGameTitle,
    DateTimeOffset? EventOrReleaseDate,
    DateTimeOffset? EventEndDate,
    string? Location,
    decimal? EstimatedPvp,
    MediaCategory? MediaCategory,
    string? PlayerCountBadge,
    string? Notes,
    NormalizedBoundingBoxDto? CropBoundingBox = null,
    string? TerritorialScope = null
);

public record SocialExpressMultimodalInputDto(
    string SourceUrl,
    string? ManualCaption = null,
    byte[]? CoverImageBytes = null,
    string? CoverImageFileName = null,
    string? CoverImageMimeType = null,
    byte[]? BasesImageBytes = null,
    string? BasesImageFileName = null,
    string? BasesImageMimeType = null
);

public record SocialInboxItemDto(
    Guid Id,
    string SourceUrl,
    SocialPlatform Platform,
    SocialSubmissionType DetectedType,
    SocialInboxStatus Status,
    string Title,
    string OrganizerOrAuthor,
    string? Collaborator,
    Guid? GameId,
    string? GameTitle,
    DateTimeOffset? EventOrReleaseDate,
    DateTimeOffset? EventEndDate,
    string? Location,
    decimal? EstimatedPvp,
    MediaCategory? MediaCategory,
    string? PlayerCountBadge,
    string? OriginalCaption,
    string? ThumbnailUrl,
    bool IsVideo,
    string? AiAnalysisNotes,
    Guid? CreatedEntityId,
    string? ModeratorNotes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt,
    string? ReviewedByUserId)
{
    public static SocialInboxItemDto FromEntity(SocialInboxItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new SocialInboxItemDto(
            item.Id,
            item.SourceUrl,
            item.Platform,
            item.DetectedType,
            item.Status,
            item.Title,
            item.OrganizerOrAuthor,
            item.Collaborator,
            item.GameId,
            item.GameTitle,
            item.EventOrReleaseDate,
            item.EventEndDate,
            item.Location,
            item.EstimatedPvp,
            item.MediaCategory,
            item.PlayerCountBadge,
            item.OriginalCaption,
            item.ThumbnailUrl,
            item.IsVideo,
            item.AiAnalysisNotes,
            item.CreatedEntityId,
            item.ModeratorNotes,
            item.CreatedAt,
            item.ReviewedAt,
            item.ReviewedByUserId);
    }
}

public record SocialInboxManualInputDto(
    string SourceUrl,
    SocialSubmissionType SubmissionType,
    string Title,
    string OrganizerOrAuthor,
    Guid? GameId = null,
    string? GameTitle = null,
    DateTimeOffset? EventOrReleaseDate = null,
    DateTimeOffset? EventEndDate = null,
    string? Location = null,
    decimal? EstimatedPvp = null,
    MediaCategory? MediaCategory = null,
    string? PlayerCountBadge = null,
    string? CustomThumbnailUrl = null,
    string? Notes = null
);

public record SocialInboxUpdateDto(
    Guid Id,
    string Title,
    string OrganizerOrAuthor,
    string? Collaborator,
    SocialSubmissionType DetectedType,
    Guid? GameId,
    string? GameTitle,
    DateTimeOffset? EventOrReleaseDate,
    DateTimeOffset? EventEndDate,
    string? Location,
    decimal? EstimatedPvp,
    MediaCategory? MediaCategory,
    string? PlayerCountBadge,
    string? ThumbnailUrl,
    string? ModeratorNotes
);

public record MonitoredAccountDto(
    Guid Id,
    string Name,
    SocialPlatform Platform,
    string HandleOrChannelId,
    MonitoredAccountType AccountType,
    string ProfileUrl,
    bool IsEnabled,
    DateTimeOffset? LastCheckedAt,
    string? Notes,
    DateTimeOffset CreatedAt)
{
    public static MonitoredAccountDto FromEntity(MonitoredSocialAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        return new MonitoredAccountDto(
            account.Id,
            account.Name,
            account.Platform,
            account.HandleOrChannelId,
            account.AccountType,
            account.ProfileUrl,
            account.IsEnabled,
            account.LastCheckedAt,
            account.Notes,
            account.CreatedAt);
    }
}
