using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
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

public class BggRawSnapshotSyncServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LudekaDbContext _context;
    private readonly TestDbContextFactory _factory;
    private readonly SqliteBggRawSnapshotRepository _snapshotRepo;
    private readonly SqliteGameRepository _gameRepo;
    private readonly SqlitePendingBggImportRepository _pendingRepo;
    private readonly FakeBggClient _bggClient;
    private readonly BggRawSnapshotSyncService _service;

    public BggRawSnapshotSyncServiceTests()
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
    public async Task GetStatusAsync_ShouldReturnAccurateMetrics()
    {
        var baseGame = CreateGame(13, "Catan", GameType.BaseGame);
        var exp1 = CreateGame(2807, "Catan 5-6", GameType.Expansion, baseGame.Id);
        var exp2 = CreateGame(3000, "Catan Ciudades", GameType.Expansion); // huérfana

        _context.Games.AddRange(baseGame, exp1, exp2);
        await _context.SaveChangesAsync();

        // 1 snapshot guardado para Catan base
        await _snapshotRepo.UpsertAsync(new BggRawSnapshot(13, "{}"));

        var status = await _service.GetStatusAsync();

        Assert.Equal(3, status.TotalGamesWithBggId);
        Assert.Equal(1, status.TotalSnapshots);
        Assert.Equal(2, status.PendingSnapshots);
        Assert.Equal(2, status.TotalExpansions);
        Assert.Equal(1, status.LinkedExpansions);
        Assert.Equal(1, status.UnlinkedExpansions);
    }

    [Fact]
    public async Task SyncBatchAsync_ShouldFetchXml_SaveSnapshot_AndAutoLinkExpansion()
    {
        var baseGame = CreateGame(316554, "Dune: Imperium", GameType.BaseGame);
        var expGame = CreateGame(342035, "Dune: Imperium – Rise of Ix", GameType.Expansion);

        _context.Games.AddRange(baseGame, expGame);
        await _context.SaveChangesAsync();

        // Configurar XML de BGG para la expansión con enlace inbound al juego base
        _bggClient.SetupXml(342035, @"<items><item type=""boardgameexpansion"" id=""342035"">
            <name type=""primary"" value=""Dune: Imperium – Rise of Ix"" />
            <link type=""boardgameexpansion"" id=""316554"" value=""Dune: Imperium"" inbound=""true"" />
        </item></items>");

        var result = await _service.SyncBatchAsync(batchSize: 10, delayMs: 0);

        Assert.True(result.SuccessCount >= 1);

        // Verificar que el snapshot existe
        var snapshot = await _snapshotRepo.GetByBggIdAsync(342035);
        Assert.NotNull(snapshot);
        Assert.Contains("342035", snapshot.RawJson);

        // Verificar que la expansión fue auto-vinculada al juego base
        var updatedExp = await _gameRepo.GetByBggIdAsync(342035);
        Assert.NotNull(updatedExp);
        Assert.Equal(baseGame.Id, updatedExp.BaseGameId);
    }

    [Fact]
    public async Task DiscoverAndEnqueueMissingExpansionsAsync_ShouldFindUncatalogedExpansions()
    {
        // Snapshot con juego base que contiene 2 expansiones salientes
        string rawJson = @"{
            ""@id"": ""13"",
            ""@type"": ""boardgame"",
            ""link"": [
                { ""@type"": ""boardgameexpansion"", ""@id"": ""2807"", ""@value"": ""Catan 5-6 Jugadores"" },
                { ""@type"": ""boardgameexpansion"", ""@id"": ""3000"", ""@value"": ""Catan Ciudades y Caballeros"" }
            ]
        }";

        await _snapshotRepo.UpsertAsync(new BggRawSnapshot(13, rawJson));

        // En catálogo ya existe una de las expansiones (2807)
        _context.Games.Add(CreateGame(2807, "Catan 5-6"));
        await _context.SaveChangesAsync();

        var discovery = await _service.DiscoverAndEnqueueMissingExpansionsAsync(maxToEnqueue: 10);

        // Solo 3000 debe ser descubierta y encolada
        Assert.Equal(1, discovery.DiscoveredCount);
        Assert.Equal(1, discovery.EnqueuedCount);
        Assert.Contains("Catan Ciudades y Caballeros", discovery.EnqueuedTitles);

        var pending = await _pendingRepo.GetByBggIdAsync(3000);
        Assert.NotNull(pending);
        Assert.Equal(CatalogQueueOrigin.BggExpansionDiscovery, pending.Origin);
    }

    [Fact]
    public async Task AutoLinkExistingExpansionsAsync_ShouldLinkUnlinkedExpansions_UsingExistingSnapshots()
    {
        var baseGame = CreateGame(822, "Carcassonne", GameType.BaseGame);
        var expGame = CreateGame(2993, "Carcassonne: Inns & Cathedrals", GameType.Expansion); // huérfana

        _context.Games.AddRange(baseGame, expGame);
        await _context.SaveChangesAsync();

        // Snapshot existente para la expansión
        string expRawJson = @"{
            ""@id"": ""2993"",
            ""@type"": ""boardgameexpansion"",
            ""link"": { ""@type"": ""boardgameexpansion"", ""@id"": ""822"", ""@inbound"": ""true"" }
        }";
        await _snapshotRepo.UpsertAsync(new BggRawSnapshot(2993, expRawJson));

        int linked = await _service.AutoLinkExistingExpansionsAsync();

        Assert.Equal(1, linked);

        var refreshedExp = await _gameRepo.GetByBggIdAsync(2993);
        Assert.NotNull(refreshedExp);
        Assert.Equal(baseGame.Id, refreshedExp.BaseGameId);
    }

    [Fact]
    public async Task SyncBatchAsync_ShouldProcessInChunksOf20_AndSaveAllSnapshots()
    {
        // Crear 25 juegos sin snapshot
        for (int i = 1; i <= 25; i++)
        {
            _context.Games.Add(CreateGame(100 + i, $"Juego #{100 + i}"));
        }
        await _context.SaveChangesAsync();

        var result = await _service.SyncBatchAsync(batchSize: 25, delayMs: 0);

        Assert.Equal(25, result.ProcessedCount);
        Assert.Equal(25, result.SuccessCount);
        Assert.Equal(0, result.FailedCount);

        int count = await _snapshotRepo.GetCountAsync();
        Assert.Equal(25, count);

        var snapshot1 = await _snapshotRepo.GetByBggIdAsync(101);
        Assert.NotNull(snapshot1);
        var snapshot25 = await _snapshotRepo.GetByBggIdAsync(125);
        Assert.NotNull(snapshot25);
    }

    private static Game CreateGame(int bggId, string title, GameType type = GameType.BaseGame, Guid? baseGameId = null)
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
        private readonly Dictionary<int, string> _xmlPayloads = new();

        public void SetupXml(int bggId, string xml)
        {
            _xmlPayloads[bggId] = xml;
        }

        public Task<string?> FetchRawThingXmlAsync(int bggId, CancellationToken ct = default)
        {
            return Task.FromResult<string?>(_xmlPayloads.GetValueOrDefault(bggId, $"<items><item type=\"boardgame\" id=\"{bggId}\"><name type=\"primary\" value=\"Juego {bggId}\" /></item></items>"));
        }

        public Task<string?> FetchRawThingsXmlAsync(IEnumerable<int> bggIds, CancellationToken ct = default)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<items>");
            foreach (var id in bggIds)
            {
                if (_xmlPayloads.TryGetValue(id, out var xml))
                {
                    var doc = System.Xml.Linq.XDocument.Parse(xml);
                    var item = doc.Root?.Element("item");
                    if (item != null)
                    {
                        sb.AppendLine(item.ToString());
                    }
                }
                else
                {
                    sb.AppendLine($"<item type=\"boardgame\" id=\"{id}\"><name type=\"primary\" value=\"Juego {id}\" /></item>");
                }
            }
            sb.AppendLine("</items>");
            return Task.FromResult<string?>(sb.ToString());
        }

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
