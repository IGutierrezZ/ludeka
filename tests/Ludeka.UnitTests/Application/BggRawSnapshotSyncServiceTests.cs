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
    public async Task DiscoverAndEnqueueMissingExpansionsAsync_FiltersOutPromosAndLowTractionExpansions()
    {
        // Snapshot de juego base con 5 enlaces a expansiones
        string baseGameJson = @"{
            ""@id"": ""167791"",
            ""@type"": ""boardgame"",
            ""link"": [
                { ""@type"": ""boardgameexpansion"", ""@id"": ""4001"", ""@value"": ""Terraforming Mars: Promo Cards"" },
                { ""@type"": ""boardgameexpansion"", ""@id"": ""4002"", ""@value"": ""Terraforming Mars: Metal Coins"" },
                { ""@type"": ""boardgameexpansion"", ""@id"": ""4003"", ""@value"": ""Terraforming Mars: Low Votes Minor"" },
                { ""@type"": ""boardgameexpansion"", ""@id"": ""4004"", ""@value"": ""Terraforming Mars: Prelude"" },
                { ""@type"": ""boardgameexpansion"", ""@id"": ""4005"", ""@value"": ""Terraforming Mars: Hellas & Elysium"" }
            ]
        }";

        await _snapshotRepo.UpsertAsync(new BggRawSnapshot(167791, baseGameJson));

        // 4003 tiene estadísticas muy bajas y sin versión en español
        _bggClient.SetupXml(4003, @"<items><item type=""boardgameexpansion"" id=""4003"">
            <name type=""primary"" value=""Terraforming Mars: Low Votes Minor"" />
            <statistics><ratings><usersrated value=""8"" /><owned value=""15"" /></ratings></statistics>
        </item></items>");

        // 4004 tiene alta tracción comunitaria (usersrated > 30 y owned > 100)
        _bggClient.SetupXml(4004, @"<items><item type=""boardgameexpansion"" id=""4004"">
            <name type=""primary"" value=""Terraforming Mars: Prelude"" />
            <statistics><ratings><usersrated value=""450"" /><owned value=""1200"" /></ratings></statistics>
        </item></items>");

        // 4005 tiene bajas valoraciones pero cuenta con edición comercial en español
        _bggClient.SetupXml(4005, @"<items><item type=""boardgameexpansion"" id=""4005"">
            <name type=""primary"" value=""Terraforming Mars: Hellas &amp; Elysium"" />
            <statistics><ratings><usersrated value=""5"" /><owned value=""10"" /></ratings></statistics>
            <versions>
                <item type=""boardgameversion"" id=""8888"">
                    <name type=""primary"" value=""Terraforming Mars: Hellas y Elysium"" />
                    <link type=""language"" value=""Spanish"" />
                    <link type=""boardgamepublisher"" value=""Maldito Games"" />
                </item>
            </versions>
        </item></items>");

        var discovery = await _service.DiscoverAndEnqueueMissingExpansionsAsync(maxToEnqueue: 10);

        // 4001 y 4002 se descartan en el pre-filtro léxico, restan 3 descubiertas
        Assert.Equal(3, discovery.DiscoveredCount);
        // De las 3, 4003 se descarta por umbral comunitario; se encolan 4004 y 4005
        Assert.Equal(2, discovery.EnqueuedCount);
        Assert.Contains("Terraforming Mars: Prelude", discovery.EnqueuedTitles);
        Assert.Contains("Terraforming Mars: Hellas & Elysium", discovery.EnqueuedTitles);

        Assert.NotNull(await _pendingRepo.GetByBggIdAsync(4004));
        Assert.NotNull(await _pendingRepo.GetByBggIdAsync(4005));
        Assert.Null(await _pendingRepo.GetByBggIdAsync(4001));
        Assert.Null(await _pendingRepo.GetByBggIdAsync(4002));
        Assert.Null(await _pendingRepo.GetByBggIdAsync(4003));
    }

    [Fact]
    public async Task DiscoverAndEnqueueMissingExpansionsAsync_PrioritizesTopRankedBaseGames_OverOldSnapshots()
    {
        // Juego 1: Die Macher (BggId 1, BggRank 500)
        var oldGame = CreateGame(1, "Die Macher", GameType.BaseGame, bggRank: 500);
        // Juego 2: Terraforming Mars (BggId 167791, BggRank 4 - Top de Ludeka)
        var topGame = CreateGame(167791, "Terraforming Mars", GameType.BaseGame, bggRank: 4);

        _context.Games.AddRange(oldGame, topGame);
        await _context.SaveChangesAsync();

        // Snapshot de Die Macher con expansión 1001 (BggId menor en tabla de snapshots)
        string oldGameJson = @"{
            ""@id"": ""1"",
            ""@type"": ""boardgame"",
            ""link"": [
                { ""@type"": ""boardgameexpansion"", ""@id"": ""1001"", ""@value"": ""Die Macher Expansion"" }
            ]
        }";

        // Snapshot de Terraforming Mars con expansión 4004 (Prelude)
        string topGameJson = @"{
            ""@id"": ""167791"",
            ""@type"": ""boardgame"",
            ""link"": [
                { ""@type"": ""boardgameexpansion"", ""@id"": ""4004"", ""@value"": ""Terraforming Mars: Prelude"" }
            ]
        }";

        await _snapshotRepo.UpsertAsync(new BggRawSnapshot(1, oldGameJson));
        await _snapshotRepo.UpsertAsync(new BggRawSnapshot(167791, topGameJson));

        // Ambas cumplen umbral comunitario
        _bggClient.SetupXml(1001, @"<items><item type=""boardgameexpansion"" id=""1001"">
            <name type=""primary"" value=""Die Macher Expansion"" />
            <statistics><ratings><usersrated value=""200"" /><owned value=""500"" /></ratings></statistics>
        </item></items>");

        _bggClient.SetupXml(4004, @"<items><item type=""boardgameexpansion"" id=""4004"">
            <name type=""primary"" value=""Terraforming Mars: Prelude"" />
            <statistics><ratings><usersrated value=""1500"" /><owned value=""3000"" /></ratings></statistics>
        </item></items>");

        // Solicitamos encolar solo 1 expansión (maxToEnqueue = 1)
        var discovery = await _service.DiscoverAndEnqueueMissingExpansionsAsync(maxToEnqueue: 1);

        // Debe priorizar la expansión del juego Top (Prelude), ignorando la de Die Macher a pesar de tener BggId = 1
        Assert.Equal(2, discovery.DiscoveredCount);
        Assert.Equal(1, discovery.EnqueuedCount);
        Assert.Contains("Terraforming Mars: Prelude", discovery.EnqueuedTitles);
        Assert.DoesNotContain("Die Macher Expansion", discovery.EnqueuedTitles);

        var pendingPrelude = await _pendingRepo.GetByBggIdAsync(4004);
        Assert.NotNull(pendingPrelude);
        var pendingDieMacher = await _pendingRepo.GetByBggIdAsync(1001);
        Assert.Null(pendingDieMacher);
    }

    [Fact]
    public async Task GetTopRankedBaseGameBggIdsAsync_ShouldReturnBaseGamesOrderedByRank()
    {
        var gameRank10 = CreateGame(10, "Rank 10", GameType.BaseGame, bggRank: 10);
        var gameRank2 = CreateGame(2, "Rank 2", GameType.BaseGame, bggRank: 2);
        var gameRank5 = CreateGame(5, "Rank 5", GameType.BaseGame, bggRank: 5);
        var expansionRank1 = CreateGame(99, "Exp Rank 1", GameType.Expansion, bggRank: 1); // ignorado por ser Expansion
        var gameNoRank = CreateGame(100, "No Rank", GameType.BaseGame, bggRank: null); // ignorado por null rank

        _context.Games.AddRange(gameRank10, gameRank2, gameRank5, expansionRank1, gameNoRank);
        await _context.SaveChangesAsync();

        var topIds = await _gameRepo.GetTopRankedBaseGameBggIdsAsync(limit: 10);

        Assert.Equal(new[] { 2, 5, 10 }, topIds);
    }

    [Fact]
    public async Task GetSnapshotsByBggIdsAsync_ShouldReturnRequestedSnapshots()
    {
        await _snapshotRepo.UpsertAsync(new BggRawSnapshot(1, "{\"item\":1}"));
        await _snapshotRepo.UpsertAsync(new BggRawSnapshot(2, "{\"item\":2}"));
        await _snapshotRepo.UpsertAsync(new BggRawSnapshot(3, "{\"item\":3}"));

        var snapshots = await _snapshotRepo.GetSnapshotsByBggIdsAsync(new[] { 1, 3 });

        Assert.Equal(2, snapshots.Count);
        Assert.Contains(snapshots, s => s.BggId == 1);
        Assert.Contains(snapshots, s => s.BggId == 3);
        Assert.DoesNotContain(snapshots, s => s.BggId == 2);
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

    [Fact]
    public async Task GetStatusAsync_ShouldIncludeVersionMetrics()
    {
        var game1 = CreateGame(1, "Game 1");
        var game2 = CreateGame(2, "Game 2");
        _context.Games.AddRange(game1, game2);
        await _context.SaveChangesAsync();

        // 1 snapshot con versiones, 1 snapshot sin versiones
        await _snapshotRepo.UpsertAsync(new BggRawSnapshot(1, "{\"item\":{\"@id\":\"1\",\"versions\":{\"item\":{}}}}"));
        await _snapshotRepo.UpsertAsync(new BggRawSnapshot(2, "{\"item\":{\"@id\":\"2\"}}"));

        var status = await _service.GetStatusAsync();

        Assert.Equal(2, status.TotalGamesWithBggId);
        Assert.Equal(2, status.TotalSnapshots);
        Assert.Equal(1, status.SnapshotsWithVersions);
        Assert.Equal(1, status.SnapshotsPendingVersions);
    }

    [Fact]
    public async Task SyncVersionsBatchAsync_ShouldFetchVersionsAndHotUpdateGameTitleAndEan()
    {
        // Arrange
        var game = CreateGame(2651, "Power Grid");
        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        // Snapshot inicial sin versiones
        await _snapshotRepo.UpsertAsync(new BggRawSnapshot(2651, "{\"item\":{\"@id\":\"2651\"}}"));

        const string xmlWithVersions = @"
<items>
  <item type=""boardgame"" id=""2651"">
    <name type=""primary"" value=""Power Grid"" />
    <versions>
      <item type=""boardgameversion"" id=""21950"">
        <name type=""primary"" value=""Alta Tensión"" />
        <link type=""language"" id=""2195"" value=""Spanish"" />
        <link type=""boardgamepublisher"" id=""2222"" value=""Edge Entertainment"" />
        <barcode value=""8435407626515"" />
      </item>
    </versions>
  </item>
</items>";
        _bggClient.SetupXml(2651, xmlWithVersions);

        // Act
        var result = await _service.SyncVersionsBatchAsync(batchSize: 10, delayMs: 0);

        // Assert
        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(1, result.SuccessCount);

        // Verificar juego en caliente
        var updatedGame = await _gameRepo.GetByBggIdAsync(2651);
        Assert.NotNull(updatedGame);
        Assert.Equal("Alta Tensión", updatedGame.SpanishTitle);
        Assert.Equal("Edge Entertainment", updatedGame.SpanishPublisher);
        Assert.Equal("8435407626515", updatedGame.Ean);
        Assert.Equal("Alta Tensión", updatedGame.LocalizedTitles[0].Title);
    }

    [Fact]
    public async Task SweepCatalogFromVersionsAsync_ShouldReadLocalSnapshotsAndHotUpdateCatalog_WithoutNetwork()
    {
        // Arrange: Juego en base de datos con título en inglés y sin EAN
        var game = CreateGame(2651, "Power Grid");
        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        // Snapshot ya guardado localmente con versiones
        const string xmlWithVersions = @"
<items>
  <item type=""boardgame"" id=""2651"">
    <name type=""primary"" value=""Power Grid"" />
    <versions>
      <item type=""boardgameversion"" id=""21950"">
        <name type=""primary"" value=""Alta Tensión"" />
        <link type=""language"" id=""2195"" value=""Spanish"" />
        <link type=""boardgamepublisher"" id=""2222"" value=""Edge Entertainment"" />
        <barcode value=""8435407626515"" />
      </item>
    </versions>
  </item>
</items>";
        string json = BggXmlToJsonConverter.ConvertXmlStringToJson(xmlWithVersions);
        await _snapshotRepo.UpsertAsync(new BggRawSnapshot(2651, json));

        // Act: Barrido 100% offline
        var result = await _service.SweepCatalogFromVersionsAsync(batchSize: 50, lastBggId: 0);

        // Assert
        Assert.Equal(1, result.EvaluatedCount);
        Assert.Equal(1, result.UpdatedTitlesCount);
        Assert.Equal(1, result.UpdatedEansCount);
        Assert.Equal(0, result.SkippedCount);

        var updatedGame = await _gameRepo.GetByBggIdAsync(2651);
        Assert.NotNull(updatedGame);
        Assert.Equal("Alta Tensión", updatedGame.SpanishTitle);
        Assert.Equal("Edge Entertainment", updatedGame.SpanishPublisher);
        Assert.Equal("8435407626515", updatedGame.Ean);
    }

    [Fact]
    public async Task SweepCatalogFromVersionsAsync_ShouldRestoreOriginalTitle_WhenGameWasCorruptedWithKoreanEdition()
    {
        // Arrange: Juego en base de datos cuyo SpanishTitle fue contaminado con "Korean edition"
        var game = CreateGame(342942, "Ark Nova: Marine Worlds");
        game.UpdateSpanishTitle("Korean edition");
        game.UpdateSpanishPublisher("Angry Lion Games");
        game.UpdateEan("8809641480507");
        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        // Snapshot local que solo tiene versiones en coreano (sin español)
        const string xml = @"
<items>
  <item type=""boardgame"" id=""342942"">
    <name type=""primary"" value=""Ark Nova: Marine Worlds"" />
    <versions>
      <item type=""boardgameversion"" id=""9999"">
        <name type=""primary"" value=""Angry Lion Korean edition"" />
        <link type=""language"" id=""2195"" value=""Korean"" />
        <link type=""boardgamepublisher"" value=""Angry Lion Games"" />
        <barcode value=""8809641480507"" />
      </item>
    </versions>
  </item>
</items>";
        string json = BggXmlToJsonConverter.ConvertXmlStringToJson(xml);
        await _snapshotRepo.UpsertAsync(new BggRawSnapshot(342942, json));

        // Act: Barrido local
        var result = await _service.SweepCatalogFromVersionsAsync(batchSize: 50, lastBggId: 0);

        // Assert
        Assert.Equal(1, result.EvaluatedCount);
        Assert.Equal(1, result.UpdatedTitlesCount);

        var restoredGame = await _gameRepo.GetByBggIdAsync(342942);
        Assert.NotNull(restoredGame);
        Assert.Equal("Ark Nova: Marine Worlds", restoredGame.SpanishTitle);
        Assert.Null(restoredGame.SpanishPublisher);
        Assert.Null(restoredGame.Ean);
    }

    [Fact]
    public async Task SweepCatalogFromVersionsAsync_ShouldPromoteSpanishCover_WhenSpanishVersionHasCoverImage()
    {
        // Arrange
        var game = CreateGame(13, "The Settlers of Catan");
        game.UpdateImages("https://cf.geekdo-images.com/original-english.jpg", "https://cf.geekdo-images.com/original-thumb.jpg");
        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        const string xml = @"
<items>
  <item type=""boardgame"" id=""13"">
    <name type=""primary"" value=""The Settlers of Catan"" />
    <image>https://cf.geekdo-images.com/original-english.jpg</image>
    <thumbnail>https://cf.geekdo-images.com/original-thumb.jpg</thumbnail>
    <versions>
      <item type=""boardgameversion"" id=""5001"">
        <name type=""primary"" value=""Catán"" />
        <image>https://cf.geekdo-images.com/catan-spanish-cover.jpg</image>
        <thumbnail>https://cf.geekdo-images.com/catan-spanish-thumb.jpg</thumbnail>
        <link type=""language"" id=""2190"" value=""Spanish"" />
        <link type=""boardgamepublisher"" value=""Devir"" />
        <barcode value=""8436574340556"" />
      </item>
    </versions>
  </item>
</items>";
        string json = BggXmlToJsonConverter.ConvertXmlStringToJson(xml);
        await _snapshotRepo.UpsertAsync(new BggRawSnapshot(13, json));

        // Act
        var result = await _service.SweepCatalogFromVersionsAsync(batchSize: 10, lastBggId: 0);

        // Assert
        Assert.Equal(1, result.EvaluatedCount);
        Assert.Equal(1, result.UpdatedTitlesCount);

        var updated = await _gameRepo.GetByBggIdAsync(13);
        Assert.NotNull(updated);
        Assert.Equal("https://cf.geekdo-images.com/catan-spanish-cover.jpg", updated.CoverImageUrl);
        Assert.Equal("https://cf.geekdo-images.com/catan-spanish-thumb.jpg", updated.ThumbnailUrl);
        Assert.Equal("Catán", updated.SpanishTitle);
        Assert.Equal("Devir", updated.SpanishPublisher);
    }

    [Fact]
    public async Task SweepCatalogFromVersionsAsync_ShouldRecoverBrokenOrSimulatedCover_FromRootSnapshot_WhenNoSpanishCover()
    {
        // Arrange: Juego con URL de portada simulada/rota apuntando a R2 efímero
        var game = CreateGame(200, "Lost Ruins");
        game.UpdateImages("https://pub-lost-mock.r2.dev/games/200/cover.webp", "https://pub-lost-mock.r2.dev/games/200/thumb.webp");
        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        // Snapshot con imagen raíz canónica de BGG pero versión sin portada propia
        const string xml = @"
<items>
  <item type=""boardgame"" id=""200"">
    <name type=""primary"" value=""Lost Ruins"" />
    <image>https://cf.geekdo-images.com/lost-ruins-root-cover.jpg</image>
    <thumbnail>https://cf.geekdo-images.com/lost-ruins-root-thumb.jpg</thumbnail>
    <versions>
      <item type=""boardgameversion"" id=""6001"">
        <name type=""primary"" value=""Las Ruinas Perdidas"" />
        <link type=""language"" id=""2190"" value=""Spanish"" />
        <link type=""boardgamepublisher"" value=""Devir"" />
      </item>
    </versions>
  </item>
</items>";
        string json = BggXmlToJsonConverter.ConvertXmlStringToJson(xml);
        await _snapshotRepo.UpsertAsync(new BggRawSnapshot(200, json));

        // Act
        var result = await _service.SweepCatalogFromVersionsAsync(batchSize: 10, lastBggId: 0);

        // Assert
        Assert.Equal(1, result.EvaluatedCount);
        Assert.Equal(1, result.UpdatedTitlesCount);

        var updated = await _gameRepo.GetByBggIdAsync(200);
        Assert.NotNull(updated);
        // Recuperada la carátula raíz de BGG al ser la anterior una URL rota de R2
        Assert.Equal("https://cf.geekdo-images.com/lost-ruins-root-cover.jpg", updated.CoverImageUrl);
        Assert.Equal("https://cf.geekdo-images.com/lost-ruins-root-thumb.jpg", updated.ThumbnailUrl);
        Assert.Equal("Las Ruinas Perdidas", updated.SpanishTitle);
    }

    private static Game CreateGame(int bggId, string title, GameType type = GameType.BaseGame, Guid? baseGameId = null, int? bggRank = 100)
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
            bggRank: bggRank,
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
            => FetchRawThingXmlAsync(bggId, false, ct);

        public Task<string?> FetchRawThingXmlAsync(int bggId, bool includeVersions, CancellationToken ct = default)
        {
            return Task.FromResult<string?>(_xmlPayloads.GetValueOrDefault(bggId, $"<items><item type=\"boardgame\" id=\"{bggId}\"><name type=\"primary\" value=\"Juego {bggId}\" /></item></items>"));
        }

        public Task<string?> FetchRawThingsXmlAsync(IEnumerable<int> bggIds, CancellationToken ct = default)
            => FetchRawThingsXmlAsync(bggIds, false, ct);

        public Task<string?> FetchRawThingsXmlAsync(IEnumerable<int> bggIds, bool includeVersions, CancellationToken ct = default)
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
