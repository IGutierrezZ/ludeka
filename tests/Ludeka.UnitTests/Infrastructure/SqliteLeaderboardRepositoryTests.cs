using System;
using System.Linq;
using System.Threading.Tasks;
using Ludeka.Core.Entities;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Seeding;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class SqliteLeaderboardRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LudekaDbContext _context;
    private readonly SqliteLeaderboardRepository _repository;

    public SqliteLeaderboardRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LudekaDbContext(options);
        _context.Database.EnsureCreated();

        _repository = new SqliteLeaderboardRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetMonthlyPlaysAsync_FiltersByDateRangeAndAggregatesByUser()
    {
        // Arrange
        await CatalogSeeder.SeedAsync(_context);
        var game = await _context.Games.FirstAsync();

        var septemberStart = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var septemberEnd = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

        // Partidas de Usuario 1 en septiembre (2 partidas)
        _context.GamePlayLogs.Add(new GamePlayLog("user-1", game.Id, new DateTimeOffset(2026, 9, 5, 18, 0, 0, TimeSpan.Zero), "Club", 4));
        _context.GamePlayLogs.Add(new GamePlayLog("user-1", game.Id, new DateTimeOffset(2026, 9, 20, 20, 0, 0, TimeSpan.Zero), "Casa", 2));

        // Partida de Usuario 2 en septiembre (1 partida)
        _context.GamePlayLogs.Add(new GamePlayLog("user-2", game.Id, new DateTimeOffset(2026, 9, 12, 17, 0, 0, TimeSpan.Zero), "Casa", 3));

        // Partida de Usuario 1 en agosto (fuera de rango)
        _context.GamePlayLogs.Add(new GamePlayLog("user-1", game.Id, new DateTimeOffset(2026, 8, 30, 22, 0, 0, TimeSpan.Zero), "Casa", 4));

        // Partida de Usuario 3 en octubre (fuera de rango)
        _context.GamePlayLogs.Add(new GamePlayLog("user-3", game.Id, new DateTimeOffset(2026, 10, 2, 19, 0, 0, TimeSpan.Zero), "Casa", 5));

        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetMonthlyPlaysAsync(septemberStart, septemberEnd);

        // Assert
        Assert.Equal(2, result.Count);

        var user1 = result.Single(r => r.UserId == "user-1");
        Assert.Equal(2, user1.PlayCount);
        Assert.Equal(new DateTimeOffset(2026, 9, 5, 18, 0, 0, TimeSpan.Zero), user1.FirstPlayDate);

        var user2 = result.Single(r => r.UserId == "user-2");
        Assert.Equal(1, user2.PlayCount);
        Assert.Equal(new DateTimeOffset(2026, 9, 12, 17, 0, 0, TimeSpan.Zero), user2.FirstPlayDate);

        Assert.DoesNotContain(result, r => r.UserId == "user-3");
    }

    [Fact]
    public async Task GetMonthlyPlaysAsync_WhenEmpty_ReturnsEmptyList()
    {
        var result = await _repository.GetMonthlyPlaysAsync(
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.Empty(result);
    }
}
