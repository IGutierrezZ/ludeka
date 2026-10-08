using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Expansions;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class ExpansionServiceTests
{
    private class MockExpansionRepository : IExpansionRepository
    {
        public List<Game> Expansions { get; } = [];
        public List<ExpansionSynergy> Synergies { get; } = [];
        public List<ExpansionRecipe> Recipes { get; } = [];
        public Game? ExpansionWithBase { get; set; }

        public Task<IReadOnlyList<Game>> GetExpansionsByBaseGameIdAsync(Guid baseGameId, CancellationToken ct = default)
        {
            var result = Expansions.Where(e => e.BaseGameId == baseGameId).ToList();
            return Task.FromResult<IReadOnlyList<Game>>(result);
        }

        public Task<Game?> GetExpansionWithBaseGameAsync(Guid expansionId, CancellationToken ct = default)
        {
            return Task.FromResult(ExpansionWithBase);
        }

        public Task<IReadOnlyList<ExpansionSynergy>> GetSynergiesByBaseGameIdAsync(Guid baseGameId, CancellationToken ct = default)
        {
            var result = Synergies.Where(s => s.BaseGameId == baseGameId).ToList();
            return Task.FromResult<IReadOnlyList<ExpansionSynergy>>(result);
        }

        public Task<IReadOnlyList<ExpansionRecipe>> GetRecipesByBaseGameIdAsync(Guid baseGameId, CancellationToken ct = default)
        {
            var result = Recipes.Where(r => r.BaseGameId == baseGameId).ToList();
            return Task.FromResult<IReadOnlyList<ExpansionRecipe>>(result);
        }

        public Task AddSynergyAsync(ExpansionSynergy synergy, CancellationToken ct = default)
        {
            Synergies.Add(synergy);
            return Task.CompletedTask;
        }

        public Task AddRecipeAsync(ExpansionRecipe recipe, CancellationToken ct = default)
        {
            Recipes.Add(recipe);
            return Task.CompletedTask;
        }
    }

    private class MockGameRepository : IGameRepository
    {
        public List<Game> Games { get; } = [];

        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Games.FirstOrDefault(g => g.Id == id));

        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult(Games.FirstOrDefault(g => g.Slug == slug));

        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default) =>
            Task.FromResult(Games.FirstOrDefault(g => g.BggId == bggId));

        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
            Task.FromResult(((IReadOnlyList<Game>)Games, Games.Count));

        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default)
        {
            Games.AddRange(games);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Game game, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> HasAnyAsync(CancellationToken ct = default) => Task.FromResult(Games.Count > 0);
    }

    private static Game CreateBaseGame() => new(
        bggId: 100,
        originalTitle: "Wingspan",
        spanishTitle: "Wingspan",
        designer: "Elizabeth Hargrave",
        publisher: "Maldito Games",
        yearPublished: 2019,
        coverImageUrl: null,
        thumbnailUrl: null,
        description: "Base",
        bggRating: 8.1,
        bggRank: 20,
        ludistRating: 8.2,
        confrontation: ConfrontationType.Competitive,
        style: GameStyle.Eurogame,
        isOfficialSolo: true,
        age: new AgeRating(10, 10),
        language: LanguageDependence.Low,
        footprint: TableFootprint.StandardTable,
        duration: new GameDuration(40, 70, 25),
        scalability: [new ScalabilityEntry(1, "1J", ScalabilityStatus.Recommended, 10, 50, 5), new ScalabilityEntry(4, "4J", ScalabilityStatus.MustPlay, 80, 20, 0)]
    );

    [Fact]
    public async Task GetExpansionsForBaseGameAsync_ReturnsMappedExpansions()
    {
        var baseGame = CreateBaseGame();
        var expRepo = new MockExpansionRepository();
        var gameRepo = new MockGameRepository();

        var exp1 = new Game(
            bggId: 201, originalTitle: "Exp 1", spanishTitle: "Expansión 1",
            designer: "Autor", publisher: "Ed", yearPublished: 2020,
            coverImageUrl: null, thumbnailUrl: null, description: "Desc",
            bggRating: 8.0, bggRank: 50, ludistRating: 8.5,
            confrontation: ConfrontationType.Competitive, style: GameStyle.Eurogame,
            isOfficialSolo: true, age: new AgeRating(10, 10), language: LanguageDependence.None,
            footprint: TableFootprint.StandardTable, duration: new GameDuration(40, 70, 25),
            type: GameType.Expansion, baseGameId: baseGame.Id,
            expansionNecessity: ExpansionNecessity.MustHave,
            impactTags: [ExpansionImpactTag.AddsPlayers],
            extraPlayerCount: 1
        );

        expRepo.Expansions.Add(exp1);

        var service = new ExpansionService(expRepo, gameRepo);
        var result = await service.GetExpansionsForBaseGameAsync(baseGame.Id);

        Assert.Single(result);
        Assert.Equal("Expansión 1", result[0].SpanishTitle);
        Assert.Equal(ExpansionNecessity.MustHave, result[0].Necessity);
        Assert.Contains("🟢 Imprescindible", result[0].NecessityBadgeText);
        Assert.Equal(1, result[0].ExtraPlayerCount);
    }

    [Fact]
    public async Task GetExpansionsForBaseGameAsync_OrdersByRatingDescending()
    {
        var baseGame = CreateBaseGame();
        var expRepo = new MockExpansionRepository();
        var gameRepo = new MockGameRepository();

        var expBaja = new Game(
            bggId: 201, originalTitle: "Exp Baja", spanishTitle: "Expansión Baja",
            designer: "Autor", publisher: "Ed", yearPublished: 2020,
            coverImageUrl: null, thumbnailUrl: null, description: "Desc",
            bggRating: 6.5, bggRank: 50, ludistRating: 6.5,
            confrontation: ConfrontationType.Competitive, style: GameStyle.Eurogame,
            isOfficialSolo: true, age: new AgeRating(10, 10), language: LanguageDependence.None,
            footprint: TableFootprint.StandardTable, duration: new GameDuration(40, 70, 25),
            type: GameType.Expansion, baseGameId: baseGame.Id
        );

        var expAlta = new Game(
            bggId: 202, originalTitle: "Exp Alta", spanishTitle: "Expansión Alta",
            designer: "Autor", publisher: "Ed", yearPublished: 2022,
            coverImageUrl: null, thumbnailUrl: null, description: "Desc",
            bggRating: 8.8, bggRank: 10, ludistRating: 9.0,
            confrontation: ConfrontationType.Competitive, style: GameStyle.Eurogame,
            isOfficialSolo: true, age: new AgeRating(10, 10), language: LanguageDependence.None,
            footprint: TableFootprint.StandardTable, duration: new GameDuration(40, 70, 25),
            type: GameType.Expansion, baseGameId: baseGame.Id
        );

        var expMedia = new Game(
            bggId: 203, originalTitle: "Exp Media", spanishTitle: "Expansión Media",
            designer: "Autor", publisher: "Ed", yearPublished: 2021,
            coverImageUrl: null, thumbnailUrl: null, description: "Desc",
            bggRating: 7.8, bggRank: 30, ludistRating: 7.9,
            confrontation: ConfrontationType.Competitive, style: GameStyle.Eurogame,
            isOfficialSolo: true, age: new AgeRating(10, 10), language: LanguageDependence.None,
            footprint: TableFootprint.StandardTable, duration: new GameDuration(40, 70, 25),
            type: GameType.Expansion, baseGameId: baseGame.Id
        );

        expRepo.Expansions.AddRange([expBaja, expAlta, expMedia]);

        var service = new ExpansionService(expRepo, gameRepo);
        var result = await service.GetExpansionsForBaseGameAsync(baseGame.Id);

        Assert.Equal(3, result.Count);
        Assert.Equal("Expansión Alta", result[0].SpanishTitle);
        Assert.Equal(9.0, result[0].LudistRating);
        Assert.Equal("Expansión Media", result[1].SpanishTitle);
        Assert.Equal(7.9, result[1].LudistRating);
        Assert.Equal("Expansión Baja", result[2].SpanishTitle);
        Assert.Equal(6.5, result[2].LudistRating);
    }

    [Fact]
    public async Task EvaluateMixerCombinationAsync_DetectsConflict()
    {
        var baseGame = CreateBaseGame();
        var expRepo = new MockExpansionRepository();
        var gameRepo = new MockGameRepository();
        gameRepo.Games.Add(baseGame);

        var expA = new Game(
            bggId: 201, originalTitle: "Exp A", spanishTitle: "Expansión A",
            designer: "Autor", publisher: "Ed", yearPublished: 2020,
            coverImageUrl: null, thumbnailUrl: null, description: "A",
            bggRating: 8.0, bggRank: 50, ludistRating: 8.0,
            confrontation: ConfrontationType.Competitive, style: GameStyle.Eurogame,
            isOfficialSolo: true, age: new AgeRating(10, 10), language: LanguageDependence.None,
            footprint: TableFootprint.StandardTable, duration: new GameDuration(40, 70, 25),
            type: GameType.Expansion, baseGameId: baseGame.Id
        );

        var expB = new Game(
            bggId: 202, originalTitle: "Exp B", spanishTitle: "Expansión B",
            designer: "Autor", publisher: "Ed", yearPublished: 2021,
            coverImageUrl: null, thumbnailUrl: null, description: "B",
            bggRating: 8.0, bggRank: 50, ludistRating: 8.0,
            confrontation: ConfrontationType.Competitive, style: GameStyle.Eurogame,
            isOfficialSolo: true, age: new AgeRating(10, 10), language: LanguageDependence.None,
            footprint: TableFootprint.StandardTable, duration: new GameDuration(40, 70, 25),
            type: GameType.Expansion, baseGameId: baseGame.Id
        );

        expRepo.Expansions.Add(expA);
        expRepo.Expansions.Add(expB);

        // Conflicto registrado
        expRepo.Synergies.Add(new ExpansionSynergy(
            baseGame.Id, expA.Id, expB.Id, ExpansionSynergyLevel.Incompatible, "Sustituyen el mismo tablero."
        ));

        var service = new ExpansionService(expRepo, gameRepo);
        var evaluation = await service.EvaluateMixerCombinationAsync(baseGame.Id, [expA.Id, expB.Id]);

        Assert.Equal("Conflict", evaluation.GlobalStatus);
        Assert.Single(evaluation.ConflictWarnings);
        Assert.Contains("Sustituyen el mismo tablero.", evaluation.ConflictWarnings[0]);
    }

    [Fact]
    public async Task EvaluateMixerCombinationAsync_PerfectCombo_ReturnsBalancedAndIncreasesPlayers()
    {
        var baseGame = CreateBaseGame();
        var expRepo = new MockExpansionRepository();
        var gameRepo = new MockGameRepository();
        gameRepo.Games.Add(baseGame);

        var expA = new Game(
            bggId: 201, originalTitle: "Exp A", spanishTitle: "Expansión A",
            designer: "Autor", publisher: "Ed", yearPublished: 2020,
            coverImageUrl: null, thumbnailUrl: null, description: "A",
            bggRating: 8.0, bggRank: 50, ludistRating: 8.0,
            confrontation: ConfrontationType.Competitive, style: GameStyle.Eurogame,
            isOfficialSolo: true, age: new AgeRating(10, 10), language: LanguageDependence.None,
            footprint: TableFootprint.StandardTable, duration: new GameDuration(40, 70, 25),
            type: GameType.Expansion, baseGameId: baseGame.Id,
            extraPlayerCount: 2, extraDurationMinutes: 15
        );

        expRepo.Expansions.Add(expA);

        var service = new ExpansionService(expRepo, gameRepo);
        var evaluation = await service.EvaluateMixerCombinationAsync(baseGame.Id, [expA.Id]);

        Assert.Equal("Balanced", evaluation.GlobalStatus);
        Assert.Equal(4 + 2, evaluation.ResultingMaxPlayers);
        Assert.Equal(70 + 15, evaluation.ResultingEstimatedMinutes);
        Assert.Empty(evaluation.ConflictWarnings);
    }
}
