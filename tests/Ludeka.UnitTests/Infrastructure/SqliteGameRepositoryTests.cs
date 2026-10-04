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

    [Fact]
    public async Task GetGamesCursorPagedAsync_ReturnsGamesAscendingAndFiltersByCursor()
    {
        // Arrange: limpiar y sembrar 3 juegos con BggIds específicos
        var g1 = new Game(100, "Game A", "Juego A", "D1", "P1", 2020, "", "", "", 7.0, 10, 0.0,
            ConfrontationType.Competitive, GameStyle.Eurogame, false, new AgeRating(10, 10),
            LanguageDependence.Low, TableFootprint.SmallTable, new GameDuration(30, 60, 20), [], []);
        var g2 = new Game(200, "Game B", "Juego B", "D2", "P2", 2021, "", "", "", 8.0, 5, 0.0,
            ConfrontationType.Competitive, GameStyle.Ameritrash, false, new AgeRating(12, 12),
            LanguageDependence.Low, TableFootprint.StandardTable, new GameDuration(60, 120, 30), [], []);
        var g3 = new Game(300, "Game C", "Juego C", "D3", "P3", 2022, "", "", "", 8.5, 1, 0.0,
            ConfrontationType.Cooperative, GameStyle.Eurogame, false, new AgeRating(14, 14),
            LanguageDependence.Low, TableFootprint.TableMonster, new GameDuration(90, 180, 45), [], []);

        await _repository.AddRangeAsync([g3, g1, g2]); // Añadidos desordenados

        // Act 1: Primer lote desde afterBggId = 0
        var batch1 = await _repository.GetGamesCursorPagedAsync(afterBggId: 0, limit: 2);

        // Assert 1: Deben venir 100 y 200 en orden ascendente
        Assert.Equal(2, batch1.Count);
        Assert.Equal(100, batch1[0].BggId);
        Assert.Equal(200, batch1[1].BggId);

        // Act 2: Segundo lote desde afterBggId = 200
        var batch2 = await _repository.GetGamesCursorPagedAsync(afterBggId: 200, limit: 2);

        // Assert 2: Debe venir 300
        Assert.Single(batch2);
        Assert.Equal(300, batch2[0].BggId);

        // Act 3: Tercer lote desde afterBggId = 300
        var batch3 = await _repository.GetGamesCursorPagedAsync(afterBggId: 300, limit: 2);
        Assert.Empty(batch3);

        // Act 4: Conteo total
        int total = await _repository.GetTotalCatalogCountAsync();
        Assert.True(total >= 3);
    }

    [Fact]
    public async Task QuickSearchAsync_WithMatchingTitle_ReturnsTopGamesLimitedByLimit()
    {
        // Arrange
        await CatalogSeeder.SeedAsync(_context);

        // Act
        var results = await _repository.QuickSearchAsync("Catan", limit: 3);

        // Assert
        Assert.NotEmpty(results);
        Assert.True(results.Count <= 3);
        Assert.All(results, g =>
            Assert.True(g.SpanishTitle.Contains("Catan", StringComparison.OrdinalIgnoreCase) ||
                        g.OriginalTitle.Contains("Catan", StringComparison.OrdinalIgnoreCase)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task QuickSearchAsync_WithEmptyOrWhitespaceTerm_ReturnsEmpty(string? term)
    {
        // Arrange
        await CatalogSeeder.SeedAsync(_context);

        // Act
        var results = await _repository.QuickSearchAsync(term!, limit: 5);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_WithoutInMemoryFilters_ExecutesNativeSqlPagingCorrectly()
    {
        // Arrange
        await CatalogSeeder.SeedAsync(_context);

        // Act: Página 1 con 5 elementos
        var criteria = new GameFilterCriteria(Style: GameStyle.Eurogame);
        var (p1, total) = await _repository.SearchAsync(criteria, page: 1, pageSize: 5);

        // Act: Página 2 con 5 elementos
        var (p2, total2) = await _repository.SearchAsync(criteria, page: 2, pageSize: 5);

        // Assert
        Assert.Equal(total, total2);
        Assert.True(total > 5);
        Assert.Equal(5, p1.Count);
        Assert.True(p2.Count > 0);
        // Los elementos de la p1 y p2 no deben solaparse
        Assert.DoesNotContain(p1, item1 => p2.Any(item2 => item2.Id == item1.Id));
    }

    [Fact]
    public async Task SearchAsync_SortByTrending_OrdersByDailyTrendingRankFirst()
    {
        // Arrange
        await CatalogSeeder.SeedAsync(_context);
        var games = await _context.Games.Take(3).ToListAsync();
        Assert.True(games.Count >= 3);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // Marcamos el tercer juego con rank 1 y el primer juego con rank 2
        var trend1 = new DailyTrendingGame(today, 1, games[2].BggId, games[2].SpanishTitle, gameId: games[2].Id);
        var trend2 = new DailyTrendingGame(today, 2, games[0].BggId, games[0].SpanishTitle, gameId: games[0].Id);

        await _context.DailyTrendingGames.AddRangeAsync(trend1, trend2);
        await _context.SaveChangesAsync();

        // Act
        var criteria = new GameFilterCriteria(SortBy: GameSortOrder.Trending);
        var (results, total) = await _repository.SearchAsync(criteria, page: 1, pageSize: 10);

        // Assert
        Assert.True(total >= 3);
        Assert.Equal(games[2].Id, results[0].Id);
        Assert.Equal(games[0].Id, results[1].Id);
    }

    [Fact]
    public async Task GetTopRankedGamesWithoutVideosAsync_FiltersOutGamesWithVideos_AndRespectsMaxRank()
    {
        // Arrange
        var g1 = new Game(1001, "Top Game 1", "Top Game 1", "Autor", "Editorial", 2020, "", "", "", 8.0, 10, 8.0, ConfrontationType.Competitive, GameStyle.Eurogame, false, new AgeRating(10, 10), LanguageDependence.None, TableFootprint.StandardTable, new GameDuration(30, 60, 20), []);
        var g2WithVideo = new Game(1002, "Top Game 2", "Top Game 2", "Autor", "Editorial", 2020, "", "", "", 8.0, 20, 8.0, ConfrontationType.Competitive, GameStyle.Eurogame, false, new AgeRating(10, 10), LanguageDependence.None, TableFootprint.StandardTable, new GameDuration(30, 60, 20), []);
        var g3 = new Game(1003, "Top Game 3", "Top Game 3", "Autor", "Editorial", 2020, "", "", "", 8.0, 30, 8.0, ConfrontationType.Competitive, GameStyle.Eurogame, false, new AgeRating(10, 10), LanguageDependence.None, TableFootprint.StandardTable, new GameDuration(30, 60, 20), []);
        var g4TooLow = new Game(1004, "Low Rank Game", "Low Rank Game", "Autor", "Editorial", 2020, "", "", "", 8.0, 5000, 8.0, ConfrontationType.Competitive, GameStyle.Eurogame, false, new AgeRating(10, 10), LanguageDependence.None, TableFootprint.StandardTable, new GameDuration(30, 60, 20), []);

        await _context.Games.AddRangeAsync(g1, g2WithVideo, g3, g4TooLow);
        await _context.SaveChangesAsync();

        var video = new MediaItem(
            MediaType.Tutorial,
            MediaPlatform.YouTube,
            "Tutorial G2",
            "https://youtube.com/watch?v=g2vid",
            "https://thumb.jpg",
            "Canal",
            gameId: g2WithVideo.Id,
            status: ModerationStatus.Approved
        );
        await _context.MediaItems.AddAsync(video);
        await _context.SaveChangesAsync();

        // Act
        var results = await _repository.GetTopRankedGamesWithoutVideosAsync(maxRank: 4000, limit: 10);
        var count = await _repository.GetTopRankedGamesWithoutVideosCountAsync(maxRank: 4000);

        // Assert
        Assert.Equal(2, count);
        Assert.Equal(2, results.Count);
        Assert.Equal(g1.Id, results[0].Id);
        Assert.Equal(g3.Id, results[1].Id);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
