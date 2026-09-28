using System;
using System.Collections.Generic;
using Ludeka.Core.Enums;

namespace Ludeka.Application.DTOs;

public record GiveawayDto(
    Guid Id,
    string Title,
    string Organizer,
    string? Collaborator,
    string FormattedOrganizer,
    string Url,
    GiveawayPlatform Platform,
    DateTimeOffset DeadlineAt,
    string RemainingTimeText,
    bool IsExpired,
    Guid? GameId,
    string? GameTitle,
    string? ThumbnailUrl,
    bool IsCommunityExclusive,
    DateTimeOffset CreatedAt,
    bool IsPromoted = false,
    string Country = "España",
    string CountryFlag = "🇪🇸",
    bool IsInternational = false,
    string? InstagramPermalink = null,
    bool IsPublishedOnInstagram = false);

public record CreateGiveawayRequest(
    string Title,
    string Organizer,
    string? Collaborator,
    string Url,
    GiveawayPlatform Platform,
    DateTimeOffset DeadlineAt,
    Guid? GameId = null,
    string? GameTitle = null,
    string? ThumbnailUrl = null,
    bool IsCommunityExclusive = false,
    bool IsPromoted = false,
    string Country = "España");

public record UpdateGiveawayRequest(
    Guid Id,
    string Title,
    string Organizer,
    string? Collaborator,
    string Url,
    GiveawayPlatform Platform,
    DateTimeOffset DeadlineAt,
    string Country = "España",
    Guid? GameId = null,
    string? GameTitle = null,
    string? ThumbnailUrl = null,
    bool IsCommunityExclusive = false,
    bool IsPromoted = false);

public record WeeklyReleaseDto(
    Guid Id,
    string Title,
    string Publisher,
    DateOnly ReleaseDate,
    Guid? GameId,
    string? CoverImageUrl,
    decimal? EstimatedPvp,
    bool IsReprint,
    string? Notes,
    string? InstagramPermalink = null,
    bool IsPublishedOnInstagram = false);

public record CreateWeeklyReleaseRequest(
    string Title,
    string Publisher,
    DateOnly ReleaseDate,
    Guid? GameId = null,
    string? CoverImageUrl = null,
    decimal? EstimatedPvp = null,
    bool IsReprint = false,
    string? Notes = null);

public record RuleAnswerDto(
    Guid Id,
    Guid QuestionId,
    string UserId,
    string UserName,
    string Body,
    string? OfficialRuleReference,
    int VotesCount,
    bool IsAccepted,
    bool HasUserVoted,
    DateTimeOffset CreatedAt);

public record RuleQuestionDto(
    Guid Id,
    Guid GameId,
    string UserId,
    string UserName,
    string Title,
    string Body,
    int VotesCount,
    Guid? AcceptedAnswerId,
    bool HasUserVoted,
    int AnswersCount,
    DateTimeOffset CreatedAt,
    IReadOnlyList<RuleAnswerDto> Answers);

public record CreateRuleQuestionRequest(
    Guid GameId,
    string Title,
    string Body);

public record CreateRuleAnswerRequest(
    Guid QuestionId,
    string Body,
    string? OfficialRuleReference = null);

public record SocialCardDataDto(
    string GameTitle,
    string OriginalTitle,
    int YearPublished,
    string Designer,
    string Publisher,
    string? CoverImageUrl,
    double Rating,
    string StyleText,
    string ConfrontationText,
    string IdealPlayersText,
    int? EstimatedDurationPerPlayer,
    string? FoundingVerdictBadge = null);

public record GeneratedSocialCardDto(
    string SvgContent,
    string InstagramCaption,
    string FileName);
