using System;
using System.Linq;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
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

    [Fact]
    public async Task UpdateAsync_TransfersScalabilitySleevesAndPublishers_WithoutDegradingCommunityVotes()
    {
        // Arrange
        var initialGame = new Game(
            bggId: 28720,
            originalTitle: "Brass: Lancashire",
            spanishTitle: "Brass: Lancashire",
            designer: "Martin Wallace",
            publisher: "Roxley",
            yearPublished: 2007,
            coverImageUrl: "https://cdn.ludeka.com/brass.jpg",
            thumbnailUrl: "https://cdn.ludeka.com/brass_thumb.jpg",
            description: "Juego de economía en la revolución industrial.",
            bggRating: 8.2,
            bggRank: 20,
            ludistRating: 8.5,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: false,
            age: new AgeRating(14, 14),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(60, 120, 30),
            scalability: [],
            sleeves: []
        );

        await _repository.AddRangeAsync([initialGame]);

        // Entidad desacoplada con escalabilidad comunitaria rica, fundas y editorial española
        var enrichedDetached = new Game(
            bggId: 28720,
            originalTitle: "Brass: Lancashire",
            spanishTitle: "Brass: Lancashire",
            designer: "Martin Wallace",
            publisher: "Roxley",
            yearPublished: 2007,
            coverImageUrl: "https://cdn.ludeka.com/brass.jpg",
            thumbnailUrl: "https://cdn.ludeka.com/brass_thumb.jpg",
            description: "Juego de economía en la revolución industrial.",
            bggRating: 8.2,
            bggRank: 20,
            ludistRating: 8.5,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: false,
            age: new AgeRating(14, 14),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(60, 120, 30),
            scalability: [
                new ScalabilityEntry(3, "3J", ScalabilityStatus.MustPlay, 450, 50, 5),
                new ScalabilityEntry(4, "4J", ScalabilityStatus.MustPlay, 850, 40, 2)
            ],
            sleeves: [
                new SleeveItem("Mini Euro", 44, 68, 77, null)
            ],
            spanishPublisher: "Maldito Games",
            regionalPublishers: [
                new RegionalPublisherEntry("ES", "Maldito Games", "maldito-games")
            ]
        );

        // Act
        await _repository.UpdateAsync(enrichedDetached);

        // Assert
        var persisted = await _repository.GetByIdAsync(initialGame.Id);
        Assert.NotNull(persisted);
        Assert.Equal(2, persisted.Scalability.Count);
        Assert.Equal("Maldito Games", persisted.SpanishPublisher);
        Assert.Single(persisted.Sleeves);
        Assert.Equal(77, persisted.Sleeves[0].CardCount);
        Assert.Contains(persisted.Scalability, s => s.PlayerCount == 4 && s.Status == ScalabilityStatus.MustPlay && s.BestVotes == 850);
        Assert.Equal("Ideal: 3-4 jugadores", persisted.IdealPlayerCountText);
    }

    [Fact]
    public async Task GetGamesPendingQualityBackfillAsync_IncludesGamesWithZeroCommunityVotes()
    {
        // Arrange
        var gameWithZeroVotes = new Game(
            bggId: 99999,
            originalTitle: "Zero Votes Game",
            spanishTitle: "Juego Votos Cero",
            designer: "Autor",
            publisher: "Editorial",
            yearPublished: 2020,
            coverImageUrl: null,
            thumbnailUrl: null,
            description: "Desc",
            bggRating: 7.0,
            bggRank: 50,
            ludistRating: 7.0,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: false,
            age: new AgeRating(10, 10),
            language: LanguageDependence.None,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(30, 60, 15),
            scalability: [
                new ScalabilityEntry(2, "2J", ScalabilityStatus.Recommended, 0, 0, 0),
                new ScalabilityEntry(3, "3J", ScalabilityStatus.Recommended, 0, 0, 0)
            ],
            sleeves: []
        );

        await _repository.AddRangeAsync([gameWithZeroVotes]);

        // Act
        int pendingCount = await _repository.GetGamesPendingQualityBackfillCountAsync();
        var pendingList = await _repository.GetGamesPendingQualityBackfillAsync(limit: 10);

        // Assert
        Assert.True(pendingCount >= 1);
        Assert.Contains(pendingList, g => g.BggId == 99999);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
