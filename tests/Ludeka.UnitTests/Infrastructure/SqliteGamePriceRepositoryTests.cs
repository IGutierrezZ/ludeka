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

public class SqliteGamePriceRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LudekaDbContext _context;
    private readonly SqliteGamePriceRepository _repository;

    public SqliteGamePriceRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LudekaDbContext(options);
        _context.Database.EnsureCreated();

        _repository = new SqliteGamePriceRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private async Task<Game> CreateTestGameAsync(string title = "Test Game", decimal defaultPrice = 40.00m)
    {
        var game = new Game(
            bggId: Random.Shared.Next(1000, 999999),
            originalTitle: title,
            spanishTitle: title,
            designer: "Autor Test",
            publisher: "Editorial Test",
            yearPublished: 2024,
            coverImageUrl: "https://ludeka.test/cover.jpg",
            thumbnailUrl: null,
            description: "Descripción de prueba",
            bggRating: 8.0,
            bggRank: 10,
            ludistRating: 8.5,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: true,
            age: new AgeRating(10, 10),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(45, 60, 20),
            purchaseLinks: new List<GamePurchaseLink>
            {
                new("Zacatrus", "https://zacatrus.es/test", defaultPrice, inStock: true)
            }
        );

        _context.Games.Add(game);
        await _context.SaveChangesAsync();
        return game;
    }

    [Fact]
    public async Task RecordSnapshotAsync_PersistsNewSnapshot()
    {
        var game = await CreateTestGameAsync();
        var snapshot = new GamePriceSnapshot(game.Id, "Zacatrus", "https://zacatrus.es/test", 35.50m, inStock: true);

        await _repository.RecordSnapshotAsync(snapshot);

        var history = await _repository.GetHistoryAsync(game.Id);
        Assert.Single(history);
        Assert.Equal(35.50m, history[0].Price);
        Assert.Equal("Zacatrus", history[0].StoreName);
    }

    [Fact]
    public async Task RecordSnapshotAsync_IgnoresRedundantSnapshotWithinThreshold()
    {
        var game = await CreateTestGameAsync();
        var snap1 = new GamePriceSnapshot(game.Id, "Jugamos Otra", "https://jugamosotra.com/test", 29.90m, inStock: true);
        var snap2 = new GamePriceSnapshot(game.Id, "Jugamos Otra", "https://jugamosotra.com/test", 29.90m, inStock: true);

        await _repository.RecordSnapshotAsync(snap1);
        await _repository.RecordSnapshotAsync(snap2); // Redundante

        var history = await _repository.GetHistoryAsync(game.Id);
        Assert.Single(history);
    }

    [Fact]
    public async Task RecordSnapshotsBatchAsync_PersistsNonRedundantItems()
    {
        var game = await CreateTestGameAsync();
        var batch = new List<GamePriceSnapshot>
        {
            new(game.Id, "Tienda A", "https://a.com", 20m),
            new(game.Id, "Tienda B", "https://b.com", 22m),
            new(game.Id, "Tienda A", "https://a.com", 20m) // duplicado en mismo lote
        };

        await _repository.RecordSnapshotsBatchAsync(batch);

        var history = await _repository.GetHistoryAsync(game.Id);
        Assert.Equal(2, history.Count);
    }

    [Fact]
    public async Task GetMetricsAsync_ComputesMetricsAccuratelyWithSnapshotsAndOffers()
    {
        var game = await CreateTestGameAsync(defaultPrice: 45.00m);
        var pastDate = DateTimeOffset.UtcNow.AddDays(-10);

        // Instantáneas históricas pasadas
        await _context.GamePriceSnapshots.AddRangeAsync(
            new GamePriceSnapshot(game.Id, "Zacatrus", "https://zacatrus.es/test", 50.00m, recordedAtUtc: pastDate),
            new GamePriceSnapshot(game.Id, "Cuarto de Juegos", "https://cuartodejuegos.com/test", 39.00m, recordedAtUtc: pastDate.AddDays(2))
        );
        await _context.SaveChangesAsync();

        var metrics = await _repository.GetMetricsAsync(game.Id);

        Assert.Equal(game.Id, metrics.GameId);
        Assert.Equal(39.00m, metrics.AllTimeLowPrice);
        Assert.Equal("Cuarto de Juegos", metrics.AllTimeLowStore);
        Assert.Equal(45.00m, metrics.CurrentLowestPrice);
        Assert.Equal("Zacatrus", metrics.CurrentLowestStore);
        Assert.False(metrics.IsAllTimeLow); // 45 > 39
    }

    [Fact]
    public async Task GetMetricsBatchAsync_ReturnsMetricsForMultipleGames()
    {
        var game1 = await CreateTestGameAsync("Juego 1", 30m);
        var game2 = await CreateTestGameAsync("Juego 2", 50m);

        var result = await _repository.GetMetricsBatchAsync(new[] { game1.Id, game2.Id });

        Assert.Equal(2, result.Count);
        Assert.Equal(30m, result[game1.Id].CurrentLowestPrice);
        Assert.Equal(50m, result[game2.Id].CurrentLowestPrice);
    }
}
