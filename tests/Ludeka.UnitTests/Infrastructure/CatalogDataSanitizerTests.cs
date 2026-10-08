using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Bgg;
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

    private sealed class FakeBggClient : IBggClient
    {
        private readonly Func<int, bool, Task<string?>> _xmlProvider;

        public FakeBggClient(Func<int, bool, Task<string?>> xmlProvider)
        {
            _xmlProvider = xmlProvider;
        }

        public Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default) => Task.FromResult<Game?>(null);
        public Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BggSearchResultDto>>(Array.Empty<BggSearchResultDto>());
        public Task<IReadOnlyList<BggTopGameDto>> FetchTopGamesAsync(int limit = 50, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BggTopGameDto>>(Array.Empty<BggTopGameDto>());
        public Task<string?> FetchRawThingXmlAsync(int bggId, bool includeVersions, CancellationToken ct = default) => _xmlProvider(bggId, includeVersions);
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
    public async Task SanitizeCorruptedSpanishTitlesAsync_ShouldRestoreQueenAliceOriginalTitle_WhenSpanishTitleIsGenericMultilingualEdition()
    {
        // Arrange: Queen Alice con título corrupto "ENG/GER/FRE/SPA edition" y snapshot de BGG
        var game = CreateTestGame(
            bggId: 456236,
            originalTitle: "Queen Alice",
            spanishTitle: "ENG/GER/FRE/SPA edition",
            spanishPublisher: "Combo Games (II)"
        );
        _context.Games.Add(game);

        const string snapshotJson = @"{
          ""item"": {
            ""@id"": ""456236"",
            ""name"": [
              { ""@type"": ""primary"", ""@value"": ""Queen Alice"" }
            ],
            ""versions"": {
              ""item"": {
                ""name"": { ""@value"": ""ENG/GER/FRE/SPA edition"" },
                ""link"": [
                  { ""@type"": ""language"", ""@id"": ""2184"", ""@value"": ""English"" },
                  { ""@type"": ""language"", ""@id"": ""2194"", ""@value"": ""Spanish"" },
                  { ""@type"": ""boardgamepublisher"", ""@value"": ""Combo Games (II)"" }
                ]
              }
            }
          }
        }";
        var snapshot = new BggRawSnapshot(456236, snapshotJson, 2);
        _context.BggRawSnapshots.Add(snapshot);
        await _context.SaveChangesAsync();

        // Act
        await CatalogDataSanitizer.SanitizeCorruptedSpanishTitlesAsync(_context, NullLogger.Instance);

        // Assert
        var refreshed = await _context.Games.FirstAsync(g => g.BggId == 456236);
        Assert.Equal("Queen Alice", refreshed.SpanishTitle);
        Assert.Equal("Combo Games (II)", refreshed.SpanishPublisher);
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

        // Assert: 342942 se limpia al título original al no haber versión en español;
        // 368966 se asegura de forma prioritaria como Ark Nova: Mundo Marino (Maldito Games)
        var refreshedBase = await _context.Games.FirstAsync(g => g.BggId == 342942);
        var refreshedExp = await _context.Games.FirstAsync(g => g.BggId == 368966);

        Assert.Equal("Ark Nova", refreshedBase.SpanishTitle);
        Assert.Null(refreshedBase.SpanishPublisher);
        Assert.Null(refreshedBase.Ean);

        Assert.Equal("Ark Nova: Mundo Marino", refreshedExp.SpanishTitle);
        Assert.Equal("Maldito Games", refreshedExp.SpanishPublisher);
    }

    [Fact]
    public async Task SanitizeCorruptedSpanishTitlesAsync_ShouldContinueAcrossBatches_EvenWhenIntermediateCandidatesAreUnmodified()
    {
        // Arrange: juego candidato con BggId menor que no requiere modificación porque ya está limpio
        var cleanCandidate = CreateTestGame(
            bggId: 50,
            originalTitle: "Game 50",
            spanishTitle: "Game 50",
            spanishPublisher: null,
            ean: null
        );
        // Forzar un juego que coincide con el filtro de editorial pero ya tiene el valor correcto en BD
        var intermediate = CreateTestGame(
            bggId: 100,
            originalTitle: "Intermediate",
            spanishTitle: "Spanish edition",
            spanishPublisher: "Devir",
            ean: "8435407620001"
        );
        // Juego posterior con BggId alto que SÍ requiere saneamiento
        var corruptHighId = CreateTestGame(
            bggId: 400000,
            originalTitle: "Game 400000",
            spanishTitle: "Korean edition",
            spanishPublisher: "Angry Lion Games",
            ean: "8809641480507"
        );

        _context.Games.AddRange(cleanCandidate, intermediate, corruptHighId);
        await _context.SaveChangesAsync();

        // Act
        await CatalogDataSanitizer.SanitizeCorruptedSpanishTitlesAsync(_context, NullLogger.Instance);

        // Assert: El juego con BggId alto debe ser reparado gracias a la paginación por BggId
        var refreshedCorrupt = await _context.Games.FirstAsync(g => g.BggId == 400000);
        Assert.Equal("Game 400000", refreshedCorrupt.SpanishTitle);
        Assert.Null(refreshedCorrupt.SpanishPublisher);
        Assert.Null(refreshedCorrupt.Ean);
    }

    [Fact]
    public async Task SanitizeCorruptedSpanishTitlesAsync_ShouldUpdateToValidSpanishTitle_WhenTitleMatchesOriginalTitleAndSpanishVersionExists()
    {
        // Arrange: juego donde SpanishTitle == OriginalTitle ("Ark Nova: Marine Worlds") pero el snapshot tiene versión en español con título traducido
        var game = CreateTestGame(
            bggId: 368966,
            originalTitle: "Ark Nova: Marine Worlds",
            spanishTitle: "Ark Nova: Marine Worlds",
            spanishPublisher: "Maldito Games"
        );
        _context.Games.Add(game);

        const string snapshotJson = @"{
          ""item"": {
            ""@id"": ""368966"",
            ""versions"": {
              ""item"": {
                ""name"": { ""@value"": ""Ark Nova: Mundo Marino - Spanish edition (2024)"" },
                ""link"": [
                  { ""@type"": ""language"", ""@value"": ""Spanish"" },
                  { ""@type"": ""boardgamepublisher"", ""@value"": ""Maldito Games"" }
                ]
              }
            }
          }
        }";
        var snapshot = new BggRawSnapshot(368966, snapshotJson, 2);
        _context.BggRawSnapshots.Add(snapshot);
        await _context.SaveChangesAsync();

        // Act
        await CatalogDataSanitizer.SanitizeCorruptedSpanishTitlesAsync(_context, NullLogger.Instance);

        // Assert
        var refreshed = await _context.Games.FirstAsync(g => g.BggId == 368966);
        Assert.Equal("Ark Nova: Mundo Marino", refreshed.SpanishTitle);
        Assert.Equal("Maldito Games", refreshed.SpanishPublisher);
    }

    [Fact]
    public async Task SanitizeCorruptedSpanishTitlesAsync_ShouldEnsureArkNovaMundoMarino_WhenSnapshotMissingAndNoBggClient()
    {
        // Arrange: 368966 existe en BD con título original pero SIN snapshot alguno en BggRawSnapshots
        var game = CreateTestGame(
            bggId: 368966,
            originalTitle: "Ark Nova: Marine Worlds",
            spanishTitle: "Ark Nova: Marine Worlds",
            spanishPublisher: null
        );
        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        // Act
        await CatalogDataSanitizer.SanitizeCorruptedSpanishTitlesAsync(_context, NullLogger.Instance);

        // Assert: Se debe reparar inmediatamente a "Ark Nova: Mundo Marino" y "Maldito Games",
        // y generar el snapshot de respaldo con versiones para consistencia total en BD.
        var refreshed = await _context.Games.FirstAsync(g => g.BggId == 368966);
        Assert.Equal("Ark Nova: Mundo Marino", refreshed.SpanishTitle);
        Assert.Equal("Maldito Games", refreshed.SpanishPublisher);

        var snapshot = await _context.BggRawSnapshots.FirstOrDefaultAsync(s => s.BggId == 368966);
        Assert.NotNull(snapshot);
        Assert.True(BggRawSnapshotParser.HasVersionsFromJson(snapshot.RawJson));
        var vInfo = BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson(snapshot.RawJson);
        Assert.NotNull(vInfo);
        Assert.Equal("Ark Nova: Mundo Marino", vInfo.Title);
        Assert.Equal("Maldito Games", vInfo.Publisher);
    }

    [Fact]
    public async Task SanitizeCorruptedSpanishTitlesAsync_ShouldFetchFromBggClient_WhenSnapshotMissingVersions()
    {
        // Arrange: juego candidato con título genérico "Spanish edition" y snapshot inexistente,
        // pero disponemos de IBggClient que devuelve XML con versión en español
        var game = CreateTestGame(
            bggId: 77777,
            originalTitle: "Fantasy Realm",
            spanishTitle: "Spanish edition"
        );
        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        const string sampleXml = @"<items>
          <item type=""boardgame"" id=""77777"">
            <name type=""primary"" value=""Fantasy Realm"" />
            <versions>
              <item type=""boardgameversion"" id=""88888"">
                <name type=""primary"" value=""Reinos Fantásticos"" />
                <link type=""language"" value=""Spanish"" />
                <link type=""boardgamepublisher"" value=""Editorial Fantástica"" />
              </item>
            </versions>
          </item>
        </items>";

        var fakeBgg = new FakeBggClient((id, includeVersions) => Task.FromResult<string?>(id == 77777 ? sampleXml : null));

        // Act
        await CatalogDataSanitizer.SanitizeCorruptedSpanishTitlesAsync(_context, NullLogger.Instance, fakeBgg);

        // Assert
        var refreshed = await _context.Games.FirstAsync(g => g.BggId == 77777);
        Assert.Equal("Reinos Fantásticos", refreshed.SpanishTitle);
        Assert.Equal("Editorial Fantástica", refreshed.SpanishPublisher);

        var snap = await _context.BggRawSnapshots.FirstOrDefaultAsync(s => s.BggId == 77777);
        Assert.NotNull(snap);
        Assert.True(BggRawSnapshotParser.HasVersionsFromJson(snap.RawJson));
    }
}
