using System;
using System.Threading.Tasks;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Seeding;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public sealed class CatalogDataSanitizerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LudekaDbContext _context;

    public CatalogDataSanitizerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LudekaDbContext(options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static Game CreateTestGame(int bggId, string originalTitle, string spanishTitle, string? spanishPublisher = null, string? ean = null, string? customSlug = null)
    {
        var game = new Game(
            bggId: bggId,
            originalTitle: originalTitle,
            spanishTitle: spanishTitle,
            designer: "Autor",
            publisher: "Editorial",
            yearPublished: 2023,
            coverImageUrl: "https://example.com/cover.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            description: "Descripción",
            bggRating: 8.0,
            bggRank: 10,
            ludistRating: 8.0,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: false,
            age: new AgeRating(10, 10),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(30, 60, 20),
            customSlug: customSlug ?? $"game-{bggId}"
        );

        if (!string.IsNullOrWhiteSpace(spanishPublisher))
            game.UpdateSpanishPublisher(spanishPublisher);

        if (!string.IsNullOrWhiteSpace(ean))
            game.UpdateEan(ean);

        return game;
    }

    [Fact]
    public async Task SanitizeCorruptedSpanishTitlesAsync_ShouldRestoreOriginalTitle_WhenNoSpanishVersionExists()
    {
        // Arrange: juego con título coreano corrupto y snapshot que solo contiene versión en coreano
        var game = CreateTestGame(
            bggId: 342942,
            originalTitle: "Ark Nova: Marine Worlds",
            spanishTitle: "Korean edition",
            spanishPublisher: "Angry Lion Games",
            ean: "8809641480507"
        );
        _context.Games.Add(game);

        const string snapshotJson = @"{
          ""item"": {
            ""@id"": ""342942"",
            ""versions"": {
              ""item"": {
                ""name"": { ""@value"": ""Angry Lion Korean edition"" },
                ""barcode"": { ""@value"": ""8809641480507"" },
                ""link"": [
                  { ""@type"": ""language"", ""@id"": ""2195"", ""@value"": ""Korean"" },
                  { ""@type"": ""boardgamepublisher"", ""@value"": ""Angry Lion Games"" }
                ]
              }
            }
          }
        }";
        var snapshot = new BggRawSnapshot(342942, snapshotJson, 2);
        _context.BggRawSnapshots.Add(snapshot);
        await _context.SaveChangesAsync();

        // Act
        await CatalogDataSanitizer.SanitizeCorruptedSpanishTitlesAsync(_context, NullLogger.Instance);

        // Assert
        var refreshed = await _context.Games.FirstAsync(g => g.BggId == 342942);
        Assert.Equal("Ark Nova: Marine Worlds", refreshed.SpanishTitle);
        Assert.Null(refreshed.SpanishPublisher);
        Assert.Null(refreshed.Ean);
    }

    [Fact]
    public async Task SanitizeCorruptedSpanishTitlesAsync_ShouldRestoreValidSpanishTitle_WhenSpanishVersionInSnapshot()
    {
        // Arrange: juego corrupto con "Korean edition" pero el snapshot sí tiene versión en español
        var game = CreateTestGame(
            bggId: 2651,
            originalTitle: "Power Grid",
            spanishTitle: "Angry Lion Korean edition"
        );
        _context.Games.Add(game);

        const string snapshotJson = @"{
          ""item"": {
            ""@id"": ""2651"",
            ""versions"": {
              ""item"": [
                {
                  ""name"": { ""@value"": ""Angry Lion Korean edition"" },
                  ""link"": [
                    { ""@type"": ""language"", ""@id"": ""2195"", ""@value"": ""Korean"" }
                  ]
                },
                {
                  ""name"": { ""@value"": ""Alta Tensión"" },
                  ""barcode"": { ""@value"": ""8435407626515"" },
                  ""link"": [
                    { ""@type"": ""language"", ""@value"": ""Spanish"" },
                    { ""@type"": ""boardgamepublisher"", ""@value"": ""Edge Entertainment"" }
                  ]
                }
              ]
            }
          }
        }";
        var snapshot = new BggRawSnapshot(2651, snapshotJson, 2);
        _context.BggRawSnapshots.Add(snapshot);
        await _context.SaveChangesAsync();

        // Act
        await CatalogDataSanitizer.SanitizeCorruptedSpanishTitlesAsync(_context, NullLogger.Instance);

        // Assert
        var refreshed = await _context.Games.FirstAsync(g => g.BggId == 2651);
        Assert.Equal("Alta Tensión", refreshed.SpanishTitle);
        Assert.Equal("Edge Entertainment", refreshed.SpanishPublisher);
        Assert.Equal("8435407626515", refreshed.Ean);
    }

    [Fact]
    public async Task SanitizeCorruptedSpanishTitlesAsync_ShouldNotModify_WhenTitleIsClean()
    {
        // Arrange: juego con título legítimo no corrupto
        var game = CreateTestGame(
            bggId: 100,
            originalTitle: "Citadels",
            spanishTitle: "Ciudadelas",
            spanishPublisher: "Edge Entertainment",
            ean: "8435407620001"
        );
        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        // Act
        await CatalogDataSanitizer.SanitizeCorruptedSpanishTitlesAsync(_context, NullLogger.Instance);

        // Assert
        var refreshed = await _context.Games.FirstAsync(g => g.BggId == 100);
        Assert.Equal("Ciudadelas", refreshed.SpanishTitle);
        Assert.Equal("Edge Entertainment", refreshed.SpanishPublisher);
        Assert.Equal("8435407620001", refreshed.Ean);
    }

    [Fact]
    public async Task SanitizeCorruptedSpanishTitlesAsync_ShouldCleanArkNovaMarineWorlds_WhenMultipleCorruptedExist()
    {
        // Arrange: juego base y expansión con título coreano
        var baseGame = CreateTestGame(
            bggId: 342942,
            originalTitle: "Ark Nova",
            spanishTitle: "Korean edition",
            spanishPublisher: "Angry Lion Games",
            ean: "8809641480507"
        );
        var expansion = CreateTestGame(
            bggId: 368966,
            originalTitle: "Ark Nova: Marine Worlds",
            spanishTitle: "Korean edition",
            spanishPublisher: "Angry Lion Games",
            ean: "8809641480507"
        );
        _context.Games.AddRange(baseGame, expansion);
        await _context.SaveChangesAsync();

        // Act
        await CatalogDataSanitizer.SanitizeCorruptedSpanishTitlesAsync(_context, NullLogger.Instance);

        // Assert
        var refreshedBase = await _context.Games.FirstAsync(g => g.BggId == 342942);
        var refreshedExp = await _context.Games.FirstAsync(g => g.BggId == 368966);

        Assert.Equal("Ark Nova", refreshedBase.SpanishTitle);
        Assert.Null(refreshedBase.SpanishPublisher);
        Assert.Null(refreshedBase.Ean);

        Assert.Equal("Ark Nova: Marine Worlds", refreshedExp.SpanishTitle);
        Assert.Null(refreshedExp.SpanishPublisher);
        Assert.Null(refreshedExp.Ean);
    }
}

