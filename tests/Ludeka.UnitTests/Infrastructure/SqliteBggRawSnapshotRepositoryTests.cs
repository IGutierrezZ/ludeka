using System;
using System.Threading.Tasks;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Ludeka.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class SqliteBggRawSnapshotRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LudekaDbContext _context;
    private readonly SqliteBggRawSnapshotRepository _repository;

    public SqliteBggRawSnapshotRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LudekaDbContext(options);
        _context.Database.EnsureCreated();

        _repository = new SqliteBggRawSnapshotRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task UpsertAsync_ShouldInsertNewSnapshot_WhenNotExists()
    {
        var snapshot = new BggRawSnapshot(13, "{\"@id\":\"13\",\"name\":\"Catan\"}", apiVersion: 2);

        await _repository.UpsertAsync(snapshot);

        var retrieved = await _repository.GetByBggIdAsync(13);
        Assert.NotNull(retrieved);
        Assert.Equal(13, retrieved.BggId);
        Assert.Equal("{\"@id\":\"13\",\"name\":\"Catan\"}", retrieved.RawJson);
        Assert.Equal(2, retrieved.ApiVersion);
        Assert.Null(retrieved.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpsertAsync_ShouldUpdatePayload_WhenSnapshotAlreadyExists()
    {
        var snapshot = new BggRawSnapshot(13, "{\"initial\":true}", apiVersion: 2);
        await _repository.UpsertAsync(snapshot);

        var updated = new BggRawSnapshot(13, "{\"updated\":true}", apiVersion: 2);
        await _repository.UpsertAsync(updated);

        var retrieved = await _repository.GetByBggIdAsync(13);
        Assert.NotNull(retrieved);
        Assert.Equal(13, retrieved.BggId);
        Assert.Equal("{\"updated\":true}", retrieved.RawJson);
        Assert.NotNull(retrieved.UpdatedAtUtc);
    }

    [Fact]
    public async Task GetMissingBggIdsAsync_ShouldReturnOnlyGamesWithoutSnapshots()
    {
        var game1 = CreateTestGame(101, "Juego 101", 10);
        var game2 = CreateTestGame(102, "Juego 102", 5);
        var game3 = CreateTestGame(103, "Juego 103", 20);

        _context.Games.AddRange(game1, game2, game3);
        await _context.SaveChangesAsync();

        // Guardar snapshot para 102
        await _repository.UpsertAsync(new BggRawSnapshot(102, "{}"));

        var missing = await _repository.GetMissingBggIdsAsync(limit: 10);

        Assert.Equal(2, missing.Count);
        // Debe ordenar por BggRank ascendente: primero 101 (rank 10), luego 103 (rank 20)
        Assert.Equal(101, missing[0]);
        Assert.Equal(103, missing[1]);
    }

    [Fact]
    public async Task GetCountAsync_And_GetTotalGamesWithBggIdCountAsync_ShouldReturnAccurateCounts()
    {
        _context.Games.AddRange(
            CreateTestGame(201, "Juego 201"),
            CreateTestGame(202, "Juego 202")
        );
        await _context.SaveChangesAsync();

        await _repository.UpsertAsync(new BggRawSnapshot(201, "{}"));

        int totalGames = await _repository.GetTotalGamesWithBggIdCountAsync();
        int totalSnapshots = await _repository.GetCountAsync();

        Assert.Equal(2, totalGames);
        Assert.Equal(1, totalSnapshots);
    }

    [Fact]
    public async Task GetAllSnapshotsAsync_ShouldReturnAllPersistedSnapshots()
    {
        await _repository.UpsertAsync(new BggRawSnapshot(301, "{\"a\":1}"));
        await _repository.UpsertAsync(new BggRawSnapshot(302, "{\"b\":2}"));

        var all = await _repository.GetAllSnapshotsAsync(10);

        Assert.Equal(2, all.Count);
    }

    private static Game CreateTestGame(int bggId, string title, int? rank = null)
    {
        return new Game(
            bggId: bggId,
            originalTitle: title,
            spanishTitle: title,
            designer: "Autor",
            publisher: "Editorial",
            yearPublished: 2020,
            coverImageUrl: null,
            thumbnailUrl: null,
            description: "Desc",
            bggRating: 7.5,
            bggRank: rank,
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
}
