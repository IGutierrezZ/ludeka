using System;
using System.Collections.Generic;

namespace Ludeka.Application.DTOs;

/// <summary>
/// Entrada individual en la clasificación pública de jugadores.
/// </summary>
public record LeaderboardEntryDto(
    int Rank,
    string DisplayName,
    bool IsAnonymous,
    string? PublicProfileUrl,
    int TotalPlaysThisMonth,
    int UnlockedMilestonesCount,
    string? BadgeName,
    string? Country
);

/// <summary>
/// Vista agregada de una clasificación mensual de la comunidad.
/// </summary>
public record MonthlyLeaderboardDto(
    int Year,
    int Month,
    string MonthLabel,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    int TotalParticipants,
    int TotalPlaysLogged,
    List<LeaderboardEntryDto> Entries,
    bool CurrentUserHasOptedIn,
    int? CurrentUserRank
);

/// <summary>
/// Estado de participación y anonimato del usuario en las clasificaciones.
/// </summary>
public record LeaderboardParticipationDto(
    string UserId,
    bool OptIn,
    bool Anonymous,
    string? CustomPseudonym,
    string EffectivePseudonym
);
