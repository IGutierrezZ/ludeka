using System;
using Ludeka.Core.Enums;

namespace Ludeka.Application.DTOs;

public record UserNotificationDto(
    Guid Id,
    string UserId,
    UserNotificationType Type,
    string Title,
    string Message,
    string TargetUrl,
    Guid? GameId,
    string? ImageUrl,
    bool IsRead,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc);

public record UserGameAlertSubscriptionDto(
    Guid Id,
    string UserId,
    Guid GameId,
    string GameTitle,
    string GameSlug,
    string? GameCoverUrl,
    GameAlertType AlertType,
    decimal? ReferenceBasePvp,
    decimal? LastNotifiedPrice,
    DateTime? LastNotifiedAtUtc,
    DateTime CreatedAtUtc);

public record WebPushSubscriptionRequestDto(
    string Endpoint,
    string P256dh,
    string Auth,
    string? UserAgent = null);

public record AdminPendingSummaryDto(
    int PendingSocialInboxCount,
    int PendingIssueReportsCount,
    int PendingWeeklyReleasesCount,
    int TotalPendingCount);
