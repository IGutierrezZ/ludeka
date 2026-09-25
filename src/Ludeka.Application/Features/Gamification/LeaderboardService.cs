using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Helpers;

namespace Ludeka.Application.Features.Gamification;

public class LeaderboardService : ILeaderboardService
{
    private readonly ILeaderboardRepository _leaderboardRepo;
    private readonly IUserPreferenceService _preferenceService;
    private readonly IUserMilestoneRepository _milestoneRepo;
    private readonly IUserRepository _userRepo;
    private readonly ICurrentUserService _currentUserService;

    public LeaderboardService(
        ILeaderboardRepository leaderboardRepo,
        IUserPreferenceService preferenceService,
        IUserMilestoneRepository milestoneRepo,
        IUserRepository userRepo,
        ICurrentUserService currentUserService)
    {
        _leaderboardRepo = leaderboardRepo;
        _preferenceService = preferenceService;
        _milestoneRepo = milestoneRepo;
        _userRepo = userRepo;
        _currentUserService = currentUserService;
    }

    public async Task<MonthlyLeaderboardDto> GetMonthlyLeaderboardAsync(int? year = null, int? month = null, CancellationToken ct = default)
    {
        DateTime now = DateTime.UtcNow;
        int targetYear = year.GetValueOrDefault(now.Year);
        int targetMonth = month.GetValueOrDefault(now.Month);

        if (targetMonth < 1 || targetMonth > 12)
        {
            targetMonth = now.Month;
        }

        if (targetYear < 2020 || targetYear > now.Year + 5)
        {
            targetYear = now.Year;
        }

        var periodStart = new DateTimeOffset(targetYear, targetMonth, 1, 0, 0, 0, TimeSpan.Zero);
        var periodEnd = periodStart.AddMonths(1);

        string monthName = new DateTime(targetYear, targetMonth, 1)
            .ToString("MMMM yyyy", CultureInfo.GetCultureInfo("es-ES"));
        monthName = char.ToUpperInvariant(monthName[0]) + monthName[1..];

        var rawAggregates = await _leaderboardRepo.GetMonthlyPlaysAsync(periodStart, periodEnd, ct);

        // Deduplicación defensiva por usuario
        var distinctAggregates = rawAggregates
            .GroupBy(a => a.UserId, StringComparer.OrdinalIgnoreCase)
            .Select(g => new UserMonthlyPlaysAggregate(
                g.Key,
                g.Sum(x => x.PlayCount),
                g.Min(x => x.FirstPlayDate)))
            .ToList();

        // 1. Filtro estricto por opt-in (Criterio 1: test de exclusión)
        var optedInEntries = new List<(UserMonthlyPlaysAggregate Agg, UserPreferenceDto Pref)>();

        foreach (var agg in distinctAggregates)
        {
            var pref = await _preferenceService.GetUserPreferenceAsync(agg.UserId, ct);
            if (pref.LeaderboardOptIn)
            {
                optedInEntries.Add((agg, pref));
            }
        }

        // 2. Ordenación determinista: más partidas jugadas -> desempate por fecha más temprana -> desempate por UserId
        var ordered = optedInEntries
            .OrderByDescending(x => x.Agg.PlayCount)
            .ThenBy(x => x.Agg.FirstPlayDate)
            .ThenBy(x => x.Agg.UserId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var entries = new List<LeaderboardEntryDto>();
        int rank = 1;

        foreach (var (agg, pref) in ordered)
        {
            string displayName;
            string? publicProfileUrl = null;
            string? country = null;

            if (pref.LeaderboardAnonymous)
            {
                // Criterio 2: jamás revelar o derivar email o datos OAuth
                displayName = PseudonymGenerator.Generate(agg.UserId, pref.LeaderboardPseudonym, isAnonymous: true);
                publicProfileUrl = null; // Enlace privado/oculto
                country = null; // Geolocalización protegida
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(pref.LeaderboardPseudonym))
                {
                    displayName = pref.LeaderboardPseudonym.Trim();
                }
                else
                {
                    var user = await _userRepo.GetByIdAsync(agg.UserId, ct);
                    displayName = user?.UserName ?? agg.UserId;
                }

                publicProfileUrl = pref.HidePublicProfile ? null : $"/u/{agg.UserId}";
                country = pref.Country;
            }

            // Enriquecer con hitos de INC-67
            var userMilestones = await _milestoneRepo.GetByUserIdAsync(agg.UserId, ct);
            int milestonesCount = userMilestones.Count;
            string badgeName = ResolvePlayerBadge(milestonesCount, agg.PlayCount);

            entries.Add(new LeaderboardEntryDto(
                Rank: rank++,
                DisplayName: displayName,
                IsAnonymous: pref.LeaderboardAnonymous,
                PublicProfileUrl: publicProfileUrl,
                TotalPlaysThisMonth: agg.PlayCount,
                UnlockedMilestonesCount: milestonesCount,
                BadgeName: badgeName,
                Country: country
            ));
        }

        bool currentUserOptedIn = false;
        int? currentUserRank = null;

        if (!string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            var myPref = await _preferenceService.GetUserPreferenceAsync(_currentUserService.UserId, ct);
            currentUserOptedIn = myPref.LeaderboardOptIn;

            if (currentUserOptedIn)
            {
                var myIndex = ordered.FindIndex(x => x.Agg.UserId.Equals(_currentUserService.UserId, StringComparison.OrdinalIgnoreCase));
                if (myIndex >= 0)
                {
                    currentUserRank = myIndex + 1;
                }
            }
        }

        int totalPlays = entries.Sum(e => e.TotalPlaysThisMonth);

        return new MonthlyLeaderboardDto(
            Year: targetYear,
            Month: targetMonth,
            MonthLabel: monthName,
            PeriodStart: periodStart,
            PeriodEnd: periodEnd,
            TotalParticipants: entries.Count,
            TotalPlaysLogged: totalPlays,
            Entries: entries,
            CurrentUserHasOptedIn: currentUserOptedIn,
            CurrentUserRank: currentUserRank
        );
    }

    public async Task<LeaderboardParticipationDto> GetUserParticipationAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new LeaderboardParticipationDto(string.Empty, false, false, null, "Mesa Anónima");
        }

        var pref = await _preferenceService.GetUserPreferenceAsync(userId.Trim(), ct);
        string effectivePseudonym = PseudonymGenerator.Generate(userId, pref.LeaderboardPseudonym, pref.LeaderboardAnonymous);

        return new LeaderboardParticipationDto(
            pref.UserId,
            pref.LeaderboardOptIn,
            pref.LeaderboardAnonymous,
            pref.LeaderboardPseudonym,
            effectivePseudonym
        );
    }

    public async Task SetUserParticipationAsync(string userId, bool optIn, bool anonymous, string? customPseudonym = null, CancellationToken ct = default)
    {
        string cleanUserId = SessionIdentity.Require(userId);
        string? normalized = PseudonymGenerator.NormalizeCustomPseudonym(customPseudonym);

        await _preferenceService.SetLeaderboardPreferencesAsync(cleanUserId, optIn, anonymous, normalized, ct);
    }

    private static string ResolvePlayerBadge(int milestonesCount, int monthlyPlays)
    {
        if (milestonesCount >= 8 || monthlyPlays >= 20)
            return "Maestro Ludotecario";
        if (milestonesCount >= 5 || monthlyPlays >= 10)
            return "Veterano de Tablero";
        if (milestonesCount >= 2 || monthlyPlays >= 4)
            return "Jugador Activo";

        return "Iniciado en Mesa";
    }
}
