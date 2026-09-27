using System;
using System.Linq;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Seeding;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class SqliteGameRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LudekaDbContext _context;
    private readonly SqliteGameRepository _repository;

    public SqliteGameRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LudekaDbContext(options);
        _context.Database.EnsureCreated();

        _repository = new SqliteGameRepository(_context);
    }

    [Fact]
    public async Task CatalogSeeder_ShouldSeedCuratedGamesIdempotently()
    {
        // Act
        int firstSeed = await CatalogSeeder.SeedAsync(_context);
        int secondSeed = await CatalogSeeder.SeedAsync(_context);

        // Assert
        Assert.True(firstSeed > 0);
        Assert.Equal(0, secondSeed); // Idempotente
        Assert.True(await _repository.HasAnyAsync());
    }

    [Fact]
    public async Task GetBySlugAsync_ShouldRetrieveGameWithScalabilityAndSleeves()
    {
        // Arrange
        await CatalogSeeder.SeedAsync(_context);

        // Act
        var wingspan = await _repository.GetBySlugAsync("wingspan");

        // Assert
        Assert.NotNull(wingspan);
        Assert.Equal("Wingspan", wingspan.SpanishTitle);
        Assert.NotEmpty(wingspan.Scalability);
        Assert.NotEmpty(wingspan.Sleeves);
        Assert.Equal("Ideal: 2-3 jugadores", wingspan.IdealPlayerCountText);
    }

    [Fact]
    public async Task SearchAsync_EspecialParejas_ShouldReturnOnlyGamesMustPlayAtTwoPlayers()
    {
        // Arrange
        await CatalogSeeder.SeedAsync(_context);

        // Act
        var criteria = new GameFilterCriteria(EspecialParejas: true);
        var (items, total) = await _repository.SearchAsync(criteria, page: 1, pageSize: 50);

        // Assert
        Assert.True(total > 0);
        foreach (var game in items)
        {
            var twoPlayers = game.Scalability.FirstOrDefault(s => s.PlayerCount == 2);
            Assert.NotNull(twoPlayers);
            Assert.Equal(ScalabilityStatus.MustPlay, twoPlayers.Status);
        }
    }

    [Fact]
    public async Task SearchAsync_SearchTerm_ShouldFindGameBySpanishOrOriginalTitle()
    {
        // Arrange
        await CatalogSeeder.SeedAsync(_context);

        // Act
        var (items, total) = await _repository.SearchAsync(new GameFilterCriteria(SearchTerm: "Castillos de Borgoña"));

        // Assert
        Assert.True(total >= 1);
        Assert.Contains(items, g => g.Slug == "los-castillos-de-borgona");
    }

    [Fact]
    public async Task SearchAsync_Footprint_SmallTable_ShouldReturnOnlySmallTableGames()
    {
        // Arrange
        await CatalogSeeder.SeedAsync(_context);

        // Act
        var criteria = new GameFilterCriteria(Footprint: TableFootprint.SmallTable);
        var (items, total) = await _repository.SearchAsync(criteria, page: 1, pageSize: 50);

        // Assert
        Assert.True(total > 0);
        Assert.All(items, g => Assert.Equal(TableFootprint.SmallTable, g.Footprint));
    }

    [Fact]
    public async Task SearchAsync_Footprint_TableMonster_ShouldReturnOnlyTableMonsterGames()
    {
        // Arrange
        await CatalogSeeder.SeedAsync(_context);

        // Act
        var criteria = new GameFilterCriteria(Footprint: TableFootprint.TableMonster);
        var (items, total) = await _repository.SearchAsync(criteria, page: 1, pageSize: 50);

        // Assert
        Assert.True(total > 0);
        Assert.All(items, g => Assert.Equal(TableFootprint.TableMonster, g.Footprint));
    }

    [Fact]
    public async Task SearchAsync_PlayerCount_ShouldReturnGamesSupportingSpecifiedCount()
    {
        // Arrange
        await CatalogSeeder.SeedAsync(_context);

        // Act (Filtrar a 4 jugadores)
        var criteria = new GameFilterCriteria(PlayerCount: 4);
        var (items, total) = await _repository.SearchAsync(criteria, page: 1, pageSize: 50);

        // Assert
        Assert.True(total > 0);
        foreach (var game in items)
        {
            var match = game.Scalability.FirstOrDefault(s => s.PlayerCount == 4);
            Assert.NotNull(match);
            Assert.NotEqual(ScalabilityStatus.NotRecommended, match.Status);
        }
    }

    [Fact]
    public async Task SearchAsync_MaxDurationMinutes_ShouldReturnGamesWithinDurationLimit()
    {
        // Arrange
        await CatalogSeeder.SeedAsync(_context);

        // Act (<= 45 minutos)
        var criteria = new GameFilterCriteria(MaxDurationMinutes: 45);
        var (items, total) = await _repository.SearchAsync(criteria, page: 1, pageSize: 50);

        // Assert
        Assert.True(total > 0);
        Assert.All(items, g => Assert.True(g.Duration.MaxMinutes <= 45));
    }

    [Fact]
    public async Task SearchAsync_MultiSelect_PlayerCounts_ShouldReturnGamesSupportingAllSelectedCounts()
    {
        // Arrange
        await CatalogSeeder.SeedAsync(_context);

        // Act (Filtrar a 2 y 3 jugadores simultáneamente)
        var criteria = new GameFilterCriteria(PlayerCounts: new[] { 2, 3 }, PlayerCountsMatchAll: true);
        var (items, total) = await _repository.SearchAsync(criteria, page: 1, pageSize: 50);

        // Assert
        Assert.True(total > 0);
        foreach (var game in items)
        {
            var p2 = game.Scalability.FirstOrDefault(s => s.PlayerCount == 2);
            var p3 = game.Scalability.FirstOrDefault(s => s.PlayerCount == 3);
            Assert.NotNull(p2);
            Assert.NotNull(p3);
            Assert.NotEqual(ScalabilityStatus.NotRecommended, p2.Status);
            Assert.NotEqual(ScalabilityStatus.NotRecommended, p3.Status);
        }
    }

    [Fact]
    public async Task SearchAsync_MultiSelect_Styles_ShouldReturnGamesMatchingAnySelectedStyle()
    {
        // Arrange
        await CatalogSeeder.SeedAsync(_context);

        // Act (Eurogame o PartyGame)
        var selectedStyles = new[] { GameStyle.Eurogame, GameStyle.PartyGame };
        var criteria = new GameFilterCriteria(Styles: selectedStyles);
        var (items, total) = await _repository.SearchAsync(criteria, page: 1, pageSize: 50);

        // Assert
        Assert.True(total > 0);
        Assert.All(items, g => Assert.Contains(g.Style, selectedStyles));
    }

    [Fact]
    public async Task SearchAsync_MultiSelect_Footprints_ShouldReturnGamesMatchingAnySelectedFootprint()
    {
        // Arrange
        await CatalogSeeder.SeedAsync(_context);

        // Act (Mesa pequeña o Monstruo de mesa)
        var selectedFp = new[] { TableFootprint.SmallTable, TableFootprint.TableMonster };
        var criteria = new GameFilterCriteria(Footprints: selectedFp);
        var (items, total) = await _repository.SearchAsync(criteria, page: 1, pageSize: 50);

        // Assert
        Assert.True(total > 0);
        Assert.All(items, g => Assert.Contains(g.Footprint, selectedFp));
    }

    [Fact]
    public async Task SearchAsync_MultiSelect_Complexities_ShouldFilterByCognitiveLoad()
    {
        // Arrange
        await CatalogSeeder.SeedAsync(_context);

        // Act (Juegos ligeros)
        var criteria = new GameFilterCriteria(Complexities: new[] { GameComplexity.Light });
        var (items, total) = await _repository.SearchAsync(criteria, page: 1, pageSize: 50);

        // Assert
        Assert.True(total > 0);
        Assert.All(items, g => Assert.Equal(GameComplexity.Light, SqliteGameRepository.CalculateComplexity(g)));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
