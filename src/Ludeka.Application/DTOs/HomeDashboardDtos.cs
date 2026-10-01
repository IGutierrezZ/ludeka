using System;
using System.Collections.Generic;

namespace Ludeka.Application.DTOs;

public record BoardGameEventDto(
    Guid Id,
    string Title,
    string Description,
    string ImageUrl,
    DateOnly StartDate,
    DateOnly EndDate,
    string Location,
    string? WebsiteUrl,
    string Organizer,
    bool IsOfficial,
    string FormattedDates,
    string RemainingDaysText,
    string Country = "España",
    string CountryFlag = "🇪🇸",
    bool IsInternational = false);

public record HomeDashboardDto(
    IReadOnlyList<GameSummaryDto> TopGames,
    IReadOnlyList<GiveawayDto> Giveaways,
    IReadOnlyList<WeeklyReleaseDto> RecentReleases,
    IReadOnlyList<BoardGameEventDto> UpcomingEvents,
    IReadOnlyList<GameSummaryDto>? TrendingGames = null);
