using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.Features.Trending;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class TrendingServiceTests
{
    private class FakeTrendingRepository : IDailyTrendingGameRepository
    {
        public DateOnly? LatestDate { get; set; }
        public DateOnly? PreviousDate { get; set; }
        public Dictionary<DateOnly, List<DailyTrendingGame>> TrendsByDate { get; } = new();

        public Task<DateOnly?> GetLatestDateAsync(CancellationToken ct = default) =>
            Task.FromResult(LatestDate);

        public Task<DateOnly?> GetPreviousDateAsync(DateOnly dateUtc, CancellationToken ct = default) =>
            Task.FromResult(PreviousDate);

        public Task<IReadOnlyList<DailyTrendingGame>> GetTrendingByDateAsync(DateOnly dateUtc, CancellationToken ct = default)
        {
            if (TrendsByDate.TryGetValue(dateUtc, out var list))
            {
                return Task.FromResult<IReadOnlyList<DailyTrendingGame>>(list.OrderBy(x => x.Rank).ToList());
            }
            return Task.FromResult<IReadOnlyList<DailyTrendingGame>>(Array.Empty<DailyTrendingGame>());
        }

        public Task<IReadOnlyList<DailyTrendingGame>> GetLatestTrendingAsync(int limit = 50, CancellationToken ct = default)
        {
            if (LatestDate.HasValue && TrendsByDate.TryGetValue(LatestDate.Value, out var list))
            {
                return Task.FromResult<IReadOnlyList<DailyTrendingGame>>(list.OrderBy(x => x.Rank).Take(limit).ToList());
            }
            return Task.FromResult<IReadOnlyList<DailyTrendingGame>>(Array.Empty<DailyTrendingGame>());
        }

        public Task UpsertDailyTrendingBatchAsync(IEnumerable<DailyTrendingGame> items, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task LinkGameAsync(int bggId, Guid gameId, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    [Fact]
    public async Task GetTrendingComparisonAsync_WhenNoDataExists_ShouldReturnEmptyDto()
    {
        var repo = new FakeTrendingRepository();
        var service = new TrendingService(repo);

        var result = await service.GetTrendingComparisonAsync();

        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Null(result.PreviousDateUtc);
    }

    [Fact]
    public async Task GetTrendingComparisonAsync_WhenOnlyOneDateExists_AllItemsShouldBeMarkedAsNew()
    {
        var repo = new FakeTrendingRepository();
        var date = new DateOnly(2026, 10, 1);
        repo.LatestDate = date;
        repo.PreviousDate = null;

        var game = new DailyTrendingGame(date, rank: 1, bggId: 174430, title: "Gloomhaven");
        repo.TrendsByDate[date] = [game];

        var service = new TrendingService(repo);
        var result = await service.GetTrendingComparisonAsync();

        Assert.NotNull(result);
        Assert.Equal(date, result.DateUtc);
        Assert.Null(result.PreviousDateUtc);
        Assert.Single(result.Items);

        var item = result.Items[0];
        Assert.Equal(1, item.Rank);
        Assert.Null(item.PreviousRank);
        Assert.Equal(RankMovement.New, item.Movement);
        Assert.Equal(0, item.PositionsChanged);
        Assert.Equal("Gloomhaven", item.Title);
    }

    [Fact]
    public async Task GetTrendingComparisonAsync_WhenPreviousDateExists_ShouldCalculateMovementsAccurately()
    {
        var repo = new FakeTrendingRepository();
        var today = new DateOnly(2026, 10, 1);
        var yesterday = new DateOnly(2026, 9, 30);
        repo.LatestDate = today;
        repo.PreviousDate = yesterday;

        // Ayer:
        // #1 Ark Nova (2001)
        // #2 Dune Imperium (2002)
        // #5 Brass Birmingham (2003)
        // #10 Terraforming Mars (2004)
        repo.TrendsByDate[yesterday] =
        [
            new DailyTrendingGame(yesterday, rank: 1, bggId: 2001, title: "Ark Nova"),
            new DailyTrendingGame(yesterday, rank: 2, bggId: 2002, title: "Dune: Imperium"),
            new DailyTrendingGame(yesterday, rank: 5, bggId: 2003, title: "Brass: Birmingham"),
            new DailyTrendingGame(yesterday, rank: 10, bggId: 2004, title: "Terraforming Mars")
        ];

        // Hoy:
        // #1 Dune Imperium (era #2 -> SUBE 1 puesto)
        // #2 Ark Nova (era #1 -> BAJA 1 puesto)
        // #5 Brass Birmingham (era #5 -> MANTIENE)
        // #7 SETI (no estaba ayer -> ENTRA nuevo)
        // #8 Terraforming Mars (era #10 -> SUBE 2 puestos)
        repo.TrendsByDate[today] =
        [
            new DailyTrendingGame(today, rank: 1, bggId: 2002, title: "Dune: Imperium"),
            new DailyTrendingGame(today, rank: 2, bggId: 2001, title: "Ark Nova"),
            new DailyTrendingGame(today, rank: 5, bggId: 2003, title: "Brass: Birmingham"),
            new DailyTrendingGame(today, rank: 7, bggId: 3001, title: "SETI"),
            new DailyTrendingGame(today, rank: 8, bggId: 2004, title: "Terraforming Mars")
        ];

        var service = new TrendingService(repo);
        var result = await service.GetTrendingComparisonAsync();

        Assert.NotNull(result);
        Assert.Equal(today, result.DateUtc);
        Assert.Equal(yesterday, result.PreviousDateUtc);
        Assert.Equal(5, result.Items.Count);

        // Dune Imperium (#2 -> #1)
        var dune = result.Items[0];
        Assert.Equal(1, dune.Rank);
        Assert.Equal(2, dune.PreviousRank);
        Assert.Equal(RankMovement.Up, dune.Movement);
        Assert.Equal(1, dune.PositionsChanged);

        // Ark Nova (#1 -> #2)
        var ark = result.Items[1];
        Assert.Equal(2, ark.Rank);
        Assert.Equal(1, ark.PreviousRank);
        Assert.Equal(RankMovement.Down, ark.Movement);
        Assert.Equal(1, ark.PositionsChanged);

        // Brass Birmingham (#5 -> #5)
        var brass = result.Items[2];
        Assert.Equal(5, brass.Rank);
        Assert.Equal(5, brass.PreviousRank);
        Assert.Equal(RankMovement.Same, brass.Movement);
        Assert.Equal(0, brass.PositionsChanged);

        // SETI (nuevo)
        var seti = result.Items[3];
        Assert.Equal(7, seti.Rank);
        Assert.Null(seti.PreviousRank);
        Assert.Equal(RankMovement.New, seti.Movement);
        Assert.Equal(0, seti.PositionsChanged);

        // Terraforming Mars (#10 -> #8)
        var terra = result.Items[4];
        Assert.Equal(8, terra.Rank);
        Assert.Equal(10, terra.PreviousRank);
        Assert.Equal(RankMovement.Up, terra.Movement);
        Assert.Equal(2, terra.PositionsChanged);
    }
}
