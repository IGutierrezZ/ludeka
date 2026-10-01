using System;
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

public class SqliteGameRepositorySlugDisambiguationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LudekaDbContext _context;
    private readonly SqliteGameRepository _repository;

    public SqliteGameRepositorySlugDisambiguationTests()
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

    private static Game CreateTestGame(int bggId, string title, int year)
    {
        return new Game(
            bggId: bggId,
            originalTitle: title,
            spanishTitle: title,
            designer: "Autor de Prueba",
            publisher: "Editorial de Prueba",
            yearPublished: year,
            coverImageUrl: "https://example.com/cover.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            description: "Descripción de prueba para homónimos",
            bggRating: 7.5,
            bggRank: 500,
            ludistRating: 7.5,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: false,
            age: new AgeRating(10, 10),
            language: LanguageDependence.None,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(30, 60, 20)
        );
    }

    [Fact]
    public async Task AddRangeAsync_WhenNoSlugCollision_PreservesOriginalSlug()
    {
        var game = CreateTestGame(1234, "Everdell", 2018);

        await _repository.AddRangeAsync([game]);

        var saved = await _repository.GetByBggIdAsync(1234);
        Assert.NotNull(saved);
        Assert.Equal("everdell", saved.Slug);
    }

    [Fact]
    public async Task AddRangeAsync_WhenSlugCollidesWithExistingGame_DisambiguatesWithYearPublished()
    {
        var game2008 = CreateTestGame(34707, "The Hanging Gardens", 2008);
        await _repository.AddRangeAsync([game2008]);

        var game2025 = CreateTestGame(437334, "The Hanging Gardens", 2025);
        await _repository.AddRangeAsync([game2025]);

        var saved2008 = await _repository.GetByBggIdAsync(34707);
        var saved2025 = await _repository.GetByBggIdAsync(437334);

        Assert.NotNull(saved2008);
        Assert.NotNull(saved2025);
        Assert.Equal("the-hanging-gardens", saved2008.Slug);
        Assert.Equal("the-hanging-gardens-2025", saved2025.Slug);
    }

    [Fact]
    public async Task AddRangeAsync_WhenSlugAndYearCollide_DisambiguatesWithBggIdAndCounter()
    {
        var first = CreateTestGame(100, "Duplicate Game", 2024);
        await _repository.AddRangeAsync([first]);

        // Segundo juego: mismo título y mismo año -> obtiene slug con año
        var second = CreateTestGame(200, "Duplicate Game", 2024);
        await _repository.AddRangeAsync([second]);

        // Tercer juego: colisiona con el base y con el año -> obtiene slug con BggId
        var third = CreateTestGame(300, "Duplicate Game", 2024);
        await _repository.AddRangeAsync([third]);

        // Cuarto juego: colisiona base y año -> obtiene slug con su BggId único
        var fourth = CreateTestGame(400, "Duplicate Game", 2024);
        await _repository.AddRangeAsync([fourth]);

        var savedFirst = await _repository.GetByIdAsync(first.Id);
        var savedSecond = await _repository.GetByIdAsync(second.Id);
        var savedThird = await _repository.GetByIdAsync(third.Id);
        var savedFourth = await _repository.GetByIdAsync(fourth.Id);

        Assert.NotNull(savedFirst);
        Assert.NotNull(savedSecond);
        Assert.NotNull(savedThird);
        Assert.NotNull(savedFourth);

        Assert.Equal("duplicate-game", savedFirst.Slug);
        Assert.Equal("duplicate-game-2024", savedSecond.Slug);
        Assert.Equal("duplicate-game-300", savedThird.Slug);
        Assert.Equal("duplicate-game-400", savedFourth.Slug);
    }

    [Fact]
    public async Task AddRangeAsync_WhenBatchContainsMultipleHomonyms_DisambiguatesAllInBatch()
    {
        var g1 = CreateTestGame(34707, "The Hanging Gardens", 2008);
        var g2 = CreateTestGame(437334, "The Hanging Gardens", 2025);

        // Se añaden juntos en la misma llamada batch
        await _repository.AddRangeAsync([g1, g2]);

        var saved1 = await _repository.GetByBggIdAsync(34707);
        var saved2 = await _repository.GetByBggIdAsync(437334);

        Assert.NotNull(saved1);
        Assert.NotNull(saved2);
        Assert.Equal("the-hanging-gardens", saved1.Slug);
        Assert.Equal("the-hanging-gardens-2025", saved2.Slug);
    }

    [Fact]
    public async Task GetBySlugAsync_CanRetrieveBothHomonymsSeparately()
    {
        var g1 = CreateTestGame(34707, "The Hanging Gardens", 2008);
        var g2 = CreateTestGame(437334, "The Hanging Gardens", 2025);
        await _repository.AddRangeAsync([g1, g2]);

        var retrieved1 = await _repository.GetBySlugAsync("the-hanging-gardens");
        var retrieved2 = await _repository.GetBySlugAsync("the-hanging-gardens-2025");

        Assert.NotNull(retrieved1);
        Assert.NotNull(retrieved2);
        Assert.Equal(34707, retrieved1.BggId);
        Assert.Equal(437334, retrieved2.BggId);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
