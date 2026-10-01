using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Ludeka.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class SqliteDailyTrendingGameRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LudekaDbContext _context;
    private readonly SqliteDailyTrendingGameRepository _repository;

    public SqliteDailyTrendingGameRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LudekaDbContext(options);
        _context.Database.EnsureCreated();

        _repository = new SqliteDailyTrendingGameRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static Game CreateTestGame(int bggId, string title, string slug)
    {
        return new Game(
            bggId: bggId,
            originalTitle: title,
            spanishTitle: title,
            designer: "Autor Test",
            publisher: "Editorial Test",
            yearPublished: 2022,
            coverImageUrl: "https://example.com/cover.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            description: "Descripción de prueba",
            bggRating: 8.5,
            bggRank: 1,
            ludistRating: 8.5,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: false,
            age: new AgeRating(14, 14),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(60, 120, 45),
            customSlug: slug);
    }

    [Fact]
    public async Task UpsertDailyTrendingBatchAsync_ShouldInsertItems_WhenNoneExist()
    {
        var date = new DateOnly(2026, 10, 1);
        var items = new List<DailyTrendingGame>
        {
            new(date, rank: 1, bggId: 100, title: "Game 1"),
            new(date, rank: 2, bggId: 200, title: "Game 2")
        };

        await _repository.UpsertDailyTrendingBatchAsync(items);

        var retrieved = await _repository.GetTrendingByDateAsync(date);
        Assert.Equal(2, retrieved.Count);
        Assert.Equal(1, retrieved[0].Rank);
        Assert.Equal(100, retrieved[0].BggId);
        Assert.Equal(2, retrieved[1].Rank);
        Assert.Equal(200, retrieved[1].BggId);
    }

    [Fact]
    public async Task GetLatestTrendingAsync_ShouldReturnItemsFromMostRecentDate()
    {
        var day1 = new DateOnly(2026, 9, 30);
        var day2 = new DateOnly(2026, 10, 1);

        await _repository.UpsertDailyTrendingBatchAsync(new List<DailyTrendingGame>
        {
            new(day1, 1, 101, "Old Game 1"),
            new(day1, 2, 102, "Old Game 2"),
        });

        await _repository.UpsertDailyTrendingBatchAsync(new List<DailyTrendingGame>
        {
            new(day2, 1, 201, "New Game 1"),
            new(day2, 2, 202, "New Game 2"),
            new(day2, 3, 203, "New Game 3"),
        });

        var latest = await _repository.GetLatestTrendingAsync(limit: 2);
        Assert.Equal(2, latest.Count);
        Assert.Equal(201, latest[0].BggId);
        Assert.Equal(day2, latest[0].DateUtc);
        Assert.Equal(202, latest[1].BggId);
    }

    [Fact]
    public async Task LinkGameAsync_ShouldUpdateGameId_ForMatchingBggId()
    {
        var date = new DateOnly(2026, 10, 1);
        var trending = new DailyTrendingGame(date, 1, 300, "Unlinked Game");
        await _repository.UpsertDailyTrendingBatchAsync([trending]);

        var game = CreateTestGame(300, "Unlinked Game", "unlinked-game");
        await _context.Games.AddAsync(game);
        await _context.SaveChangesAsync();

        await _repository.LinkGameAsync(300, game.Id);

        var retrieved = await _repository.GetTrendingByDateAsync(date);
        Assert.Single(retrieved);
        Assert.Equal(game.Id, retrieved[0].GameId);
        Assert.NotNull(retrieved[0].Game);
        Assert.Equal("unlinked-game", retrieved[0].Game!.Slug);
    }
}
