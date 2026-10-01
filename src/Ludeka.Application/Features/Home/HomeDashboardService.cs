using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Catalog;
using Ludeka.Core.Entities;

namespace Ludeka.Application.Features.Home;

public class HomeDashboardService : IHomeDashboardService
{
    private readonly ICatalogService _catalogService;
    private readonly IGiveawayService _giveawayService;
    private readonly IWeeklyReleaseService _weeklyReleaseService;
    private readonly IBoardGameEventRepository _eventRepository;
    private readonly IDailyTrendingGameRepository? _trendingRepository;

    public HomeDashboardService(
        ICatalogService catalogService,
        IGiveawayService giveawayService,
        IWeeklyReleaseService weeklyReleaseService,
        IBoardGameEventRepository eventRepository,
        IDailyTrendingGameRepository? trendingRepository = null)
    {
        _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        _giveawayService = giveawayService ?? throw new ArgumentNullException(nameof(giveawayService));
        _weeklyReleaseService = weeklyReleaseService ?? throw new ArgumentNullException(nameof(weeklyReleaseService));
        _eventRepository = eventRepository ?? throw new ArgumentNullException(nameof(eventRepository));
        _trendingRepository = trendingRepository;
    }

    public async Task<HomeDashboardDto> GetDashboardDataAsync(CancellationToken ct = default)
    {
        // 1. Carril 1: Top 20 Juegos ordenados por ranking / valoración
        var catalogResult = await _catalogService.GetCatalogAsync(new GameFilterCriteria(), page: 1, pageSize: 20, ct: ct);
        var topGames = catalogResult.Games;

        // 1b. Carril 1 (Tendencias): Top 20 en tendencia del día (BGG Hotness)
        var trendingGames = new List<GameSummaryDto>();
        if (_trendingRepository != null)
        {
            var latestTrending = await _trendingRepository.GetLatestTrendingAsync(20, ct);
            foreach (var item in latestTrending)
            {
                if (item.Game != null)
                {
                    trendingGames.Add(GameSummaryDto.FromEntity(item.Game) with { TrendingRank = item.Rank });
                }
            }
        }

        // 2. Carril 2: Sorteos Activos y Destacados (priorizando IsPromoted == true y fecha límite inminente)
        var allGiveaways = await _giveawayService.GetGiveawaysAsync(includeExpired: false, ct: ct);
        var giveaways = allGiveaways
            .OrderByDescending(g => g.IsPromoted)
            .ThenBy(g => g.DeadlineAt)
            .Take(20)
            .ToList();

        // 3. Carril 3: Novedades del Sector (orden cronológico descendente)
        var allReleases = await _weeklyReleaseService.GetReleasesAsync(fromDate: null, ct: ct);
        var recentReleases = allReleases
            .OrderByDescending(r => r.ReleaseDate)
            .Take(20)
            .ToList();

        // 4. Carril 4: Próximos Eventos y Ferias Lúdicas (orden cronológico ascendente)
        var events = await _eventRepository.GetUpcomingEventsAsync(20, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var upcomingEvents = events.Select(e => MapEventToDto(e, today)).ToList();

        return new HomeDashboardDto(topGames, giveaways, recentReleases, upcomingEvents, trendingGames);
    }

    private static BoardGameEventDto MapEventToDto(BoardGameEvent e, DateOnly today)
    {
        string remainingDaysText;
        if (e.IsOngoing(today))
        {
            remainingDaysText = "¡En curso!";
        }
        else
        {
            int days = e.DaysUntilStart(today);
            remainingDaysText = days switch
            {
                0 => "Empieza hoy",
                1 => "Empieza mañana",
                > 1 => $"En {days} días",
                _ => "Finalizado"
            };
        }

        return new BoardGameEventDto(
            e.Id,
            e.Title,
            e.Description,
            e.ImageUrl,
            e.StartDate,
            e.EndDate,
            e.Location,
            e.WebsiteUrl,
            e.Organizer,
            e.IsOfficial,
            e.GetFormattedDates(),
            remainingDaysText,
            e.Country,
            Ludeka.Core.ValueObjects.CountryCatalog.GetFlag(e.Country),
            e.IsInternational);
    }
}
