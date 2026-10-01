using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Features.Trending;

/// <summary>
/// Servicio de aplicación para la gestión de tendencias de juegos de mesa y cálculo de deltas diarios.
/// </summary>
public class TrendingService : ITrendingService
{
    private readonly IDailyTrendingGameRepository _trendingRepository;

    public TrendingService(IDailyTrendingGameRepository trendingRepository)
    {
        _trendingRepository = trendingRepository ?? throw new ArgumentNullException(nameof(trendingRepository));
    }

    public async Task<TrendingComparisonDto> GetTrendingComparisonAsync(CancellationToken ct = default)
    {
        var latestDate = await _trendingRepository.GetLatestDateAsync(ct);
        if (!latestDate.HasValue)
        {
            return new TrendingComparisonDto(DateOnly.FromDateTime(DateTime.UtcNow), null, Array.Empty<TrendingGameItemDto>());
        }

        var currentEntries = await _trendingRepository.GetTrendingByDateAsync(latestDate.Value, ct);
        var previousDate = await _trendingRepository.GetPreviousDateAsync(latestDate.Value, ct);

        Dictionary<int, int>? previousRanksByBggId = null;
        if (previousDate.HasValue)
        {
            var previousEntries = await _trendingRepository.GetTrendingByDateAsync(previousDate.Value, ct);
            previousRanksByBggId = previousEntries.ToDictionary(x => x.BggId, x => x.Rank);
        }

        var items = new List<TrendingGameItemDto>(currentEntries.Count);
        foreach (var entry in currentEntries)
        {
            int? prevRank = null;
            RankMovement movement = RankMovement.New;
            int deltaPositions = 0;

            if (previousRanksByBggId != null && previousRanksByBggId.TryGetValue(entry.BggId, out var pastRank))
            {
                prevRank = pastRank;
                if (entry.Rank < pastRank)
                {
                    movement = RankMovement.Up;
                    deltaPositions = pastRank - entry.Rank;
                }
                else if (entry.Rank > pastRank)
                {
                    movement = RankMovement.Down;
                    deltaPositions = entry.Rank - pastRank;
                }
                else
                {
                    movement = RankMovement.Same;
                    deltaPositions = 0;
                }
            }

            var game = entry.Game;
            items.Add(new TrendingGameItemDto(
                Rank: entry.Rank,
                PreviousRank: prevRank,
                Movement: movement,
                PositionsChanged: deltaPositions,
                BggId: entry.BggId,
                Title: entry.Title,
                YearPublished: entry.YearPublished,
                ThumbnailUrl: entry.ThumbnailUrl ?? game?.ThumbnailUrl,
                GameId: entry.GameId,
                Slug: game?.Slug,
                SpanishTitle: game?.SpanishTitle,
                BggRating: game?.BggRating,
                BggRank: game?.BggRank
            ));
        }

        return new TrendingComparisonDto(latestDate.Value, previousDate, items);
    }
}
