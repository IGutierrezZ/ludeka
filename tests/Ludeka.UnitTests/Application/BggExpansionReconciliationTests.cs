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
using Ludeka.Infrastructure.Bgg;
using Ludeka.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class BggExpansionReconciliationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LudekaDbContext _context;
    private readonly TestDbContextFactory _factory;
    private readonly SqliteBggRawSnapshotRepository _snapshotRepo;
    private readonly SqliteGameRepository _gameRepo;
    private readonly SqlitePendingBggImportRepository _pendingRepo;
    private readonly FakeBggClient _bggClient;
    private readonly BggRawSnapshotSyncService _service;

    public BggExpansionReconciliationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LudekaDbContext(options);
        _context.Database.EnsureCreated();

        _factory = new TestDbContextFactory(options);
        _snapshotRepo = new SqliteBggRawSnapshotRepository(_factory);
        _gameRepo = new SqliteGameRepository(_factory);
        _pendingRepo = new SqlitePendingBggImportRepository(_factory);
        _bggClient = new FakeBggClient();

        _service = new BggRawSnapshotSyncService(
            _snapshotRepo,
            _bggClient,
            _gameRepo,
            _pendingRepo,
            _factory,
            NullLogger<BggRawSnapshotSyncService>.Instance
        );
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void Parser_IsExpansionTypeFromJson_ReturnsTrue_ForBoardgameExpansion()
    {
        var expansionJson = """
        {
          "item": {
            "@type": "boardgameexpansion",
            "@id": "2807",
            "name": { "@value": "Catan: 5-6 Player Extension" }
          }
        }
        """;

        Assert.True(BggRawSnapshotParser.IsExpansionTypeFromJson(expansionJson));
    }

    [Fact]
    public void Parser_IsExpansionTypeFromJson_ReturnsFalse_ForBaseBoardgameOrInvalid()
    {
        var baseGameJson = """
        {
          "item": {
            "@type": "boardgame",
            "@id": "13",
            "name": { "@value": "Catan" }
          }
        }
        """;

        Assert.False(BggRawSnapshotParser.IsExpansionTypeFromJson(baseGameJson));
        Assert.False(BggRawSnapshotParser.IsExpansionTypeFromJson(""));
        Assert.False(BggRawSnapshotParser.IsExpansionTypeFromJson("not json"));
    }

    [Fact]
    public void Parser_ExtractLinks_IdentifiesInboundAndOutboundCorrectly()
    {
        var expansionJson = """
        {
          "item": {
            "@type": "boardgameexpansion",
            "@id": "2807",
            "link": [
              {
                "@type": "boardgameexpansion",
                "@id": "13",
                "@value": "Catan",
                "@inbound": "true"
              },
              {
                "@type": "boardgamedesigner",
                "@id": "1",
                "@value": "Klaus Teuber"
              }
            ]
          }
        }
        """;

        var inbound = BggRawSnapshotParser.ExtractInboundBaseGameLinks(expansionJson);
        Assert.Single(inbound);
        Assert.Equal(13, inbound[0].BggId);
        Assert.Equal("Catan", inbound[0].Title);

        var baseGameJson = """
        {
          "item": {
            "@type": "boardgame",
            "@id": "13",
            "link": [
              {
                "@type": "boardgameexpansion",
                "@id": "2807",
                "@value": "Catan: 5-6 Player Extension"
              }
            ]
          }
        }
        """;

        var outbound = BggRawSnapshotParser.ExtractOutboundExpansionLinks(baseGameJson);
        Assert.Single(outbound);
        Assert.Equal(2807, outbound[0].BggId);
        Assert.Equal("Catan: 5-6 Player Extension", outbound[0].Title);
    }

    [Fact]
    public async Task ReconcileAndLinkExpansions_ReclassifiesFalselyClassifiedBaseGame_AndLinksToParent()
    {
        // Juego base legítimo
        var baseGame = CreateGame(13, "Catan", GameType.BaseGame);
        // Expansión que nació incorrectamente como BaseGame sin BaseGameId
        var expansion = CreateGame(2807, "Catan 5-6 Jugadores", GameType.BaseGame);

        _context.Games.AddRange(baseGame, expansion);
        await _context.SaveChangesAsync();

        // Snapshot satélite de la expansión con @type="boardgameexpansion" y link inbound hacia Catan (13)
        var snapshotJson = """
        {
          "item": {
            "@type": "boardgameexpansion",
            "@id": "2807",
            "link": [
              {
                "@type": "boardgameexpansion",
                "@id": "13",
                "@value": "Catan",
                "@inbound": "true"
              }
            ]
          }
        }
        """;
        await _snapshotRepo.UpsertAsync(new BggRawSnapshot(2807, snapshotJson));

        var result = await _service.ReconcileAndLinkExpansionsFromSnapshotsAsync(batchSize: 50);

        Assert.Equal(1, result.TotalEvaluated);
        Assert.Equal(1, result.ReclassifiedExpansionsCount);
        Assert.Equal(1, result.LinkedExpansionsCount);
        Assert.Contains("Catan 5-6 Jugadores", result.ReclassifiedTitles);

        // Verificar persistencia en base de datos
        var reloaded = await _gameRepo.GetByBggIdAsync(2807);
        Assert.NotNull(reloaded);
        Assert.Equal(GameType.Expansion, reloaded.Type);
        Assert.Equal(baseGame.Id, reloaded.BaseGameId);
    }

    [Fact]
    public async Task ReconcileAndLinkExpansions_LinksOutboundExpansion_FromBaseGameSnapshot()
    {
        var baseGame = CreateGame(13, "Catan", GameType.BaseGame);
        var expansion = CreateGame(3000, "Catan Ciudades y Caballeros", GameType.BaseGame);

        _context.Games.AddRange(baseGame, expansion);
        await _context.SaveChangesAsync();

        // Snapshot del juego base con link outbound hacia la expansión 3000
        var baseSnapshotJson = """
        {
          "item": {
            "@type": "boardgame",
            "@id": "13",
            "link": [
              {
                "@type": "boardgameexpansion",
                "@id": "3000",
                "@value": "Catan Ciudades y Caballeros"
              }
            ]
          }
        }
        """;
        await _snapshotRepo.UpsertAsync(new BggRawSnapshot(13, baseSnapshotJson));

        var result = await _service.ReconcileAndLinkExpansionsFromSnapshotsAsync(batchSize: 50);

        Assert.Equal(1, result.TotalEvaluated);
        Assert.Equal(1, result.ReclassifiedExpansionsCount);
        Assert.Equal(1, result.LinkedExpansionsCount);

        var reloaded = await _gameRepo.GetByBggIdAsync(3000);
        Assert.NotNull(reloaded);
        Assert.Equal(GameType.Expansion, reloaded.Type);
        Assert.Equal(baseGame.Id, reloaded.BaseGameId);
    }

    [Fact]
    public async Task ReconcileAndLinkExpansions_IsIdempotent_WhenRunMultipleTimes()
    {
        var baseGame = CreateGame(13, "Catan", GameType.BaseGame);
        var expansion = CreateGame(2807, "Catan 5-6 Jugadores", GameType.BaseGame);

        _context.Games.AddRange(baseGame, expansion);
        await _context.SaveChangesAsync();

        var snapshotJson = """
        {
          "item": {
            "@type": "boardgameexpansion",
            "@id": "2807",
            "link": [
              {
                "@type": "boardgameexpansion",
                "@id": "13",
                "@value": "Catan",
                "@inbound": "true"
              }
            ]
          }
        }
        """;
        await _snapshotRepo.UpsertAsync(new BggRawSnapshot(2807, snapshotJson));

        // Primera ejecución: reclasifica y vincula
        var result1 = await _service.ReconcileAndLinkExpansionsFromSnapshotsAsync(batchSize: 50);
        Assert.Equal(1, result1.ReclassifiedExpansionsCount);
        Assert.Equal(1, result1.LinkedExpansionsCount);

        // Segunda ejecución: debe detectar que ya está reclasificada y vinculada sin mutaciones adicionales
        var result2 = await _service.ReconcileAndLinkExpansionsFromSnapshotsAsync(batchSize: 50);
        Assert.Equal(1, result2.TotalEvaluated);
        Assert.Equal(0, result2.ReclassifiedExpansionsCount);
        Assert.Equal(0, result2.LinkedExpansionsCount);
    }

    [Fact]
    public async Task ReconcileAndLinkExpansions_CursorPagination_IteratesMultipleBatches()
    {
        var base1 = CreateGame(10, "Base 1", GameType.BaseGame);
        var exp1 = CreateGame(11, "Exp 1", GameType.BaseGame);
        var base2 = CreateGame(20, "Base 2", GameType.BaseGame);
        var exp2 = CreateGame(21, "Exp 2", GameType.BaseGame);

        _context.Games.AddRange(base1, exp1, base2, exp2);
        await _context.SaveChangesAsync();

        var snap1 = """{"item":{"@type":"boardgameexpansion","@id":"11","link":[{"@type":"boardgameexpansion","@id":"10","@value":"Base 1","@inbound":"true"}]}}""";
        var snap2 = """{"item":{"@type":"boardgameexpansion","@id":"21","link":[{"@type":"boardgameexpansion","@id":"20","@value":"Base 2","@inbound":"true"}]}}""";

        await _snapshotRepo.UpsertAsync(new BggRawSnapshot(11, snap1));
        await _snapshotRepo.UpsertAsync(new BggRawSnapshot(21, snap2));

        // Forzar lotes de tamaño 1 para ejercitar la paginación con cursor
        var result = await _service.ReconcileAndLinkExpansionsFromSnapshotsAsync(batchSize: 1);

        Assert.Equal(2, result.TotalEvaluated);
        Assert.Equal(2, result.ReclassifiedExpansionsCount);
        Assert.Equal(2, result.LinkedExpansionsCount);
    }

    [Fact]
    public async Task Reconcile_GameWithZeroPlayerCountInScalability_DoesNotThrowAndSanitizesScalability()
    {
        var baseGame = CreateGame(500, "Base Catan", GameType.BaseGame);
        // Creamos un juego con Scalability que incluye PlayerCount = 0
        var scalabilityWithZero = new List<ScalabilityEntry>
        {
            new(0, "0J", ScalabilityStatus.Recommended, 5, 2, 0),
            new(2, "2J", ScalabilityStatus.MustPlay, 10, 1, 0)
        };
        var expGame = new Game(
            bggId: 501,
            originalTitle: "Catan Exp",
            spanishTitle: "Catan Exp",
            designer: "Teuber",
            publisher: "Devir",
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
            duration: new GameDuration(30, 60, 20),
            scalability: scalabilityWithZero,
            type: GameType.BaseGame
        );

        _context.Games.AddRange(baseGame, expGame);
        await _context.SaveChangesAsync();

        var snap = """{"item":{"@type":"boardgameexpansion","@id":"501","link":[{"@type":"boardgameexpansion","@id":"500","@value":"Base Catan","@inbound":"true"}]}}""";
        await _snapshotRepo.UpsertAsync(new BggRawSnapshot(501, snap));

        var result = await _service.ReconcileAndLinkExpansionsFromSnapshotsAsync(batchSize: 50);

        Assert.Equal(1, result.TotalEvaluated);
        Assert.Equal(1, result.ReclassifiedExpansionsCount);
        Assert.Equal(1, result.LinkedExpansionsCount);

        var updated = await _gameRepo.GetByBggIdAsync(501);
        Assert.NotNull(updated);
        Assert.Equal(GameType.Expansion, updated.Type);
        Assert.Equal(baseGame.Id, updated.BaseGameId);
        // Debe haber purgado el 0
        Assert.DoesNotContain(updated.Scalability, s => s.PlayerCount <= 0);
    }

    private static Game CreateGame(int bggId, string title, GameType type, Guid? baseGameId = null)
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
            bggRank: 100,
            ludistRating: 7.5,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: false,
            age: new AgeRating(10, 10),
            language: LanguageDependence.None,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(30, 60, 20),
            type: type,
            baseGameId: baseGameId
        );
    }

    private sealed class FakeBggClient : IBggClient
    {
        public Task<string?> FetchRawThingXmlAsync(int bggId, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task<string?> FetchRawThingsXmlAsync(IEnumerable<int> bggIds, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default) => Task.FromResult<Game?>(null);
        public Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BggSearchResultDto>>([]);
        public Task<IReadOnlyList<BggCollectionItemDto>> FetchUserCollectionAsync(string username, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BggCollectionItemDto>>([]);
        public Task<IReadOnlyList<BggTopGameDto>> FetchTopGamesAsync(int limit = 50, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BggTopGameDto>>([]);
    }

    private sealed class TestDbContextFactory : IDbContextFactory<LudekaDbContext>
    {
        private readonly DbContextOptions<LudekaDbContext> _options;

        public TestDbContextFactory(DbContextOptions<LudekaDbContext> options)
        {
            _options = options;
        }

        public LudekaDbContext CreateDbContext() => new(_options);
        public Task<LudekaDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new LudekaDbContext(_options));
    }
}
