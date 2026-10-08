using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Catalog;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Ludeka.Infrastructure.Bgg;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class GameEditorServiceTests
{
    private class FakeGameRepository : IGameRepository
    {
        public readonly Dictionary<Guid, Game> Store = new();

        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Store.TryGetValue(id, out var g) ? g : null);

        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default)
            => Task.FromResult(Store.Values.FirstOrDefault(g => g.Slug == slug));

        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default)
            => Task.FromResult(Store.Values.FirstOrDefault(g => g.BggId == bggId));

        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default)
            => Task.FromResult(((IReadOnlyList<Game>)Store.Values.ToList(), Store.Count));

        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default)
        {
            foreach (var g in games) Store[g.Id] = g;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Game game, CancellationToken ct = default)
        {
            Store[game.Id] = game;
            return Task.CompletedTask;
        }

        public Task<bool> HasAnyAsync(CancellationToken ct = default) => Task.FromResult(Store.Count > 0);
    }

    private class FakeCurrentUserService : ICurrentUserService
    {
        public string UserId { get; set; } = "mod-user";
        public string UserName { get; set; } = "Moderador Principal";
        public IReadOnlyList<string> Roles { get; set; } = ["Moderator"];
        public bool IsFoundingTeam { get; set; } = false;
        public ModeratorPermission Permissions { get; set; } = ModeratorPermission.All;

        public bool IsInRole(string role) => Roles.Contains(role);

        public bool HasPermission(ModeratorPermission permission)
            => IsFoundingTeam || (IsInRole("Moderator") && (Permissions & permission) == permission);
    }

    private class FakeEditLogRepository : IGameEditLogRepository
    {
        public readonly List<GameEditLog> Logs = [];

        public Task AddAsync(GameEditLog log, CancellationToken ct = default)
        {
            Logs.Add(log);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<GameEditLog>> GetByGameIdAsync(Guid gameId, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<GameEditLog>)Logs.Where(l => l.GameId == gameId).ToList());
    }

    private class FakeCatalogService : ICatalogService
    {
        public Task<CatalogResult> GetCatalogAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default)
            => Task.FromResult(new CatalogResult([], 0, page, pageSize));
        public Task<GameDetailDto?> GetGameBySlugAsync(string slug, CancellationToken ct = default)
            => Task.FromResult<GameDetailDto?>(null);
        public Task<IReadOnlyList<GameSummaryDto>> GetQuickSearchAsync(string term, int limit = 5, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<GameSummaryDto>>([]);
    }

    private class FakeGameIssueReportService : IGameIssueReportService
    {
        public readonly List<(Guid ReportId, UpdateGameReportStatusCommand Command)> StatusUpdates = [];

        public ValueTask<GameIssueReportDto> CreateReportAsync(CreateGameReportCommand command, CancellationToken ct = default)
            => throw new NotImplementedException();
        public ValueTask<IReadOnlyList<GameIssueReportDto>> GetReportsAsync(GameReportFilter filter, CancellationToken ct = default)
            => throw new NotImplementedException();
        public ValueTask<GameIssueReportSummaryDto> GetSummaryAsync(CancellationToken ct = default)
            => throw new NotImplementedException();
        public ValueTask<GameIssueReportDto?> ChangeStatusAsync(Guid id, UpdateGameReportStatusCommand command, CancellationToken ct = default)
        {
            StatusUpdates.Add((id, command));
            return ValueTask.FromResult<GameIssueReportDto?>(null);
        }
    }

    private static Game CreateGame() => new(
        bggId: 100,
        originalTitle: "Wingspan",
        spanishTitle: "Wingspan",
        designer: "Elizabeth Hargrave",
        publisher: "Maldito Games",
        yearPublished: 2019,
        coverImageUrl: "/images/games/wingspan.png",
        thumbnailUrl: "/images/games/wingspan.png",
        description: "Juego de aves competitivo.",
        bggRating: 8.1,
        bggRank: 25,
        ludistRating: 8.5,
        confrontation: ConfrontationType.Competitive,
        style: GameStyle.Eurogame,
        isOfficialSolo: true,
        age: new AgeRating(10, 10),
        language: LanguageDependence.Low,
        footprint: TableFootprint.StandardTable,
        duration: new GameDuration(40, 70, 20),
        scalability: [new ScalabilityEntry(1, "1", ScalabilityStatus.Recommended)]
    );

    [Fact]
    public async Task UpdateGameAsync_WhenUserNotAuthorized_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var gameRepo = new FakeGameRepository();
        var user = new FakeCurrentUserService { Roles = ["Player"], IsFoundingTeam = false };
        var logRepo = new FakeEditLogRepository();
        var catalog = new FakeCatalogService();
        var service = new GameEditorService(gameRepo, user, logRepo, catalog);

        var cmd = new UpdateGameDetailsCommand(
            GameId: Guid.NewGuid(),
            SpanishTitle: "Test",
            OriginalTitle: "Test",
            Designer: "Autor",
            Publisher: "Editorial",
            YearPublished: 2020,
            Description: "Desc",
            MinPlayers: 1,
            MaxPlayers: 4,
            MinDurationMinutes: 30,
            MaxDurationMinutes: 60,
            EstimatedPerPlayerMinutes: 15,
            BoxAge: 10,
            CommunityAge: 10,
            Confrontation: ConfrontationType.Competitive,
            Style: GameStyle.Eurogame,
            IsOfficialSolo: false,
            Language: LanguageDependence.None,
            Footprint: TableFootprint.StandardTable,
            CoverImageUrl: null
        );

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdateGameAsync(cmd));
    }

    [Fact]
    public async Task UpdateGameAsync_ValidModerator_UpdatesGameAndCreatesAuditLog()
    {
        // Arrange
        var gameRepo = new FakeGameRepository();
        var game = CreateGame();
        await gameRepo.UpdateAsync(game);

        var user = new FakeCurrentUserService { Roles = ["Moderator"] };
        var logRepo = new FakeEditLogRepository();
        var catalog = new FakeCatalogService();
        var service = new GameEditorService(gameRepo, user, logRepo, catalog);

        var cmd = new UpdateGameDetailsCommand(
            GameId: game.Id,
            SpanishTitle: "Wingspan (Edición Revisada)",
            OriginalTitle: "Wingspan",
            Designer: "Elizabeth Hargrave",
            Publisher: "Maldito Games / Stonemaier",
            YearPublished: 2019,
            Description: "Nueva descripción detallada.",
            MinPlayers: 1,
            MaxPlayers: 5,
            MinDurationMinutes: 45,
            MaxDurationMinutes: 90,
            EstimatedPerPlayerMinutes: 20,
            BoxAge: 10,
            CommunityAge: 10,
            Confrontation: ConfrontationType.Competitive,
            Style: GameStyle.Eurogame,
            IsOfficialSolo: true,
            Language: LanguageDependence.Low,
            Footprint: TableFootprint.TableMonster,
            CoverImageUrl: "/images/games/wingspan-hd.jpg"
        );

        // Act
        var result = await service.UpdateGameAsync(cmd);

        // Assert
        Assert.Equal("Wingspan (Edición Revisada)", result.SpanishTitle);
        Assert.Equal("/images/games/wingspan-hd.jpg", result.CoverImageUrl);
        Assert.Equal(5, result.Scalability.Count);

        // Verify audit log
        Assert.Single(logRepo.Logs);
        var log = logRepo.Logs[0];
        Assert.Equal(game.Id, log.GameId);
        Assert.Equal("mod-user", log.EditorUserId);
        Assert.Contains("Wingspan (Edición Revisada)", log.SummaryOfChanges);
    }

    [Fact]
    public async Task UpdateGameAsync_WithAssociatedReport_ResolvesReportAutomatically()
    {
        // Arrange
        var gameRepo = new FakeGameRepository();
        var game = CreateGame();
        await gameRepo.UpdateAsync(game);

        var user = new FakeCurrentUserService { Roles = ["Moderator"] };
        var logRepo = new FakeEditLogRepository();
        var catalog = new FakeCatalogService();
        var issueReportService = new FakeGameIssueReportService();
        var service = new GameEditorService(gameRepo, user, logRepo, catalog, issueReportService);

        var reportId = Guid.NewGuid();
        var cmd = new UpdateGameDetailsCommand(
            GameId: game.Id,
            SpanishTitle: "Wingspan",
            OriginalTitle: "Wingspan",
            Designer: "Elizabeth Hargrave",
            Publisher: "Maldito Games",
            YearPublished: 2019,
            Description: "Corregida autora del juego.",
            MinPlayers: 1,
            MaxPlayers: 5,
            MinDurationMinutes: 40,
            MaxDurationMinutes: 70,
            EstimatedPerPlayerMinutes: 20,
            BoxAge: 10,
            CommunityAge: 10,
            Confrontation: ConfrontationType.Competitive,
            Style: GameStyle.Eurogame,
            IsOfficialSolo: true,
            Language: LanguageDependence.Low,
            Footprint: TableFootprint.StandardTable,
            CoverImageUrl: null,
            AssociatedReportId: reportId,
            ResolutionNotes: "Añadida Elizabeth Hargrave como diseñadora principal."
        );

        // Act
        await service.UpdateGameAsync(cmd);

        // Assert
        Assert.Single(issueReportService.StatusUpdates);
        var update = issueReportService.StatusUpdates[0];
        Assert.Equal(reportId, update.ReportId);
        Assert.Equal(GameReportStatus.Resolved, update.Command.NewStatus);
        Assert.Equal("Añadida Elizabeth Hargrave como diseñadora principal.", update.Command.Notes);
    }

    [Fact]
    public async Task UpdateGameAsync_UpdatesSleeves_AndLogsAuditSummary()
    {
        // Arrange
        var game = CreateGame();
        var gameRepo = new FakeGameRepository();
        await gameRepo.UpdateAsync(game);

        var user = new FakeCurrentUserService { Roles = ["Moderator"] };
        var logRepo = new FakeEditLogRepository();
        var catalog = new FakeCatalogService();
        var service = new GameEditorService(gameRepo, user, logRepo, catalog);

        var newSleeves = new List<SleeveItem>
        {
            new("Mini Euro", 44, 68, 73, "https://zacatrus.es/mini-euro"),
            new("Tarot", 65, 100, 12, null)
        };

        var cmd = new UpdateGameDetailsCommand(
            GameId: game.Id,
            SpanishTitle: game.SpanishTitle,
            OriginalTitle: game.OriginalTitle,
            Designer: game.Designer,
            Publisher: game.Publisher,
            YearPublished: game.YearPublished,
            Description: game.Description,
            MinPlayers: 1,
            MaxPlayers: 5,
            MinDurationMinutes: game.Duration.MinMinutes,
            MaxDurationMinutes: game.Duration.MaxMinutes,
            EstimatedPerPlayerMinutes: game.Duration.EstimatedPerPlayerMinutes,
            BoxAge: game.Age.BoxAge,
            CommunityAge: game.Age.CommunityAge,
            Confrontation: game.Confrontation,
            Style: game.Style,
            IsOfficialSolo: game.IsOfficialSolo,
            Language: game.Language,
            Footprint: game.Footprint,
            CoverImageUrl: game.CoverImageUrl,
            Sleeves: newSleeves
        );

        // Act
        var result = await service.UpdateGameAsync(cmd);

        // Assert
        var savedGame = await gameRepo.GetByIdAsync(game.Id);
        Assert.NotNull(savedGame);
        Assert.Equal(2, savedGame.Sleeves.Count);
        Assert.Equal("Mini Euro", savedGame.Sleeves[0].FormatName);
        Assert.Equal(73, savedGame.Sleeves[0].CardCount);

        Assert.Single(logRepo.Logs);
        Assert.Contains("Fundas de cartas actualizadas (2 formatos)", logRepo.Logs[0].SummaryOfChanges);
    }

    [Fact]
    public async Task UpdateGameAsync_ShouldUpdateEan_AndRecordAuditLog()
    {
        // Arrange
        var game = CreateGame();
        var gameRepo = new FakeGameRepository();
        await gameRepo.UpdateAsync(game);

        var user = new FakeCurrentUserService { Roles = ["Moderator"] };
        var logRepo = new FakeEditLogRepository();
        var catalog = new FakeCatalogService();
        var service = new GameEditorService(gameRepo, user, logRepo, catalog);

        var cmd = new UpdateGameDetailsCommand(
            GameId: game.Id,
            SpanishTitle: game.SpanishTitle,
            OriginalTitle: game.OriginalTitle,
            Designer: game.Designer,
            Publisher: game.Publisher,
            YearPublished: game.YearPublished,
            Description: game.Description,
            MinPlayers: 1,
            MaxPlayers: 4,
            MinDurationMinutes: game.Duration.MinMinutes,
            MaxDurationMinutes: game.Duration.MaxMinutes,
            EstimatedPerPlayerMinutes: game.Duration.EstimatedPerPlayerMinutes,
            BoxAge: game.Age.BoxAge,
            CommunityAge: game.Age.CommunityAge,
            Confrontation: game.Confrontation,
            Style: game.Style,
            IsOfficialSolo: game.IsOfficialSolo,
            Language: game.Language,
            Footprint: game.Footprint,
            CoverImageUrl: game.CoverImageUrl,
            Ean: "8436017220100"
        );

        // Act
        var result = await service.UpdateGameAsync(cmd);

        // Assert
        var savedGame = await gameRepo.GetByIdAsync(game.Id);
        Assert.NotNull(savedGame);
        Assert.Equal("8436017220100", savedGame.Ean);
        Assert.Equal("8436017220100", result.Ean);

        Assert.Single(logRepo.Logs);
        Assert.Contains("Código de barras (EAN-13)", logRepo.Logs[0].SummaryOfChanges);
    }

    [Fact]
    public async Task AssociateBggIdAsync_WhenAuthorized_UpdatesBggIdAndRecordsAudit()
    {
        // Arrange
        var gameRepo = new FakeGameRepository();
        var game = CreateGame();
        await gameRepo.UpdateAsync(game);

        var logRepo = new FakeEditLogRepository();
        var userSvc = new FakeCurrentUserService
        {
            Roles = ["Moderator"],
            Permissions = ModeratorPermission.CanEditGames
        };
        var catalogSvc = new FakeCatalogService();
        var service = new GameEditorService(gameRepo, userSvc, logRepo, catalogSvc);

        // Act
        var result = await service.AssociateBggIdAsync(game.Id, 99999);

        // Assert
        Assert.Equal(99999, result.BggId);
        var updated = await gameRepo.GetByIdAsync(game.Id);
        Assert.NotNull(updated);
        Assert.Equal(99999, updated.BggId);

        Assert.Single(logRepo.Logs);
        Assert.Contains("99999", logRepo.Logs[0].SummaryOfChanges);
    }

    [Fact]
    public async Task AssociateBggIdAsync_WhenUnauthorized_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var gameRepo = new FakeGameRepository();
        var game = CreateGame();
        await gameRepo.UpdateAsync(game);

        var logRepo = new FakeEditLogRepository();
        var userSvc = new FakeCurrentUserService
        {
            Roles = ["User"],
            Permissions = ModeratorPermission.None
        };
        var catalogSvc = new FakeCatalogService();
        var service = new GameEditorService(gameRepo, userSvc, logRepo, catalogSvc);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.AssociateBggIdAsync(game.Id, 12345));
    }

    private class FakeBggClient : IBggClient
    {
        public string? XmlResponse { get; set; }
        public string? JsonResponse { get; set; }
        public int LastRequestedBggId { get; set; }
        public bool LastRequestedIncludeVersions { get; set; }

        public Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default) => Task.FromResult<Game?>(null);
        public Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BggSearchResultDto>>([]);
        public Task<IReadOnlyList<BggTopGameDto>> FetchTopGamesAsync(int limit = 50, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BggTopGameDto>>([]);
        public Task<string?> FetchRawThingXmlAsync(int bggId, bool includeVersions, CancellationToken ct = default)
        {
            LastRequestedBggId = bggId;
            LastRequestedIncludeVersions = includeVersions;
            return Task.FromResult(XmlResponse);
        }

        public Task<string?> FetchRawThingJsonAsync(int bggId, bool includeVersions = true, CancellationToken ct = default)
        {
            LastRequestedBggId = bggId;
            LastRequestedIncludeVersions = includeVersions;
            if (JsonResponse != null) return Task.FromResult<string?>(JsonResponse);
            if (XmlResponse != null) return Task.FromResult<string?>(BggXmlToJsonConverter.ConvertXmlStringToJson(XmlResponse));
            return Task.FromResult<string?>(null);
        }
    }


    private class FakeBggRawSnapshotRepository : IBggRawSnapshotRepository
    {
        public readonly Dictionary<int, BggRawSnapshot> Snapshots = new();

        public Task<BggRawSnapshot?> GetByBggIdAsync(int bggId, CancellationToken ct = default)
            => Task.FromResult(Snapshots.GetValueOrDefault(bggId));

        public Task UpsertAsync(BggRawSnapshot snapshot, CancellationToken ct = default)
        {
            Snapshots[snapshot.BggId] = snapshot;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<int>> GetMissingBggIdsAsync(int limit = 50, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<int>>([]);
        public Task<int> GetCountAsync(CancellationToken ct = default)
            => Task.FromResult(Snapshots.Count);
        public Task<int> GetTotalGamesWithBggIdCountAsync(CancellationToken ct = default)
            => Task.FromResult(0);
        public Task<IReadOnlyList<BggRawSnapshot>> GetAllSnapshotsAsync(int limit = 500, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BggRawSnapshot>>(Snapshots.Values.ToList());
        public Task<IReadOnlyList<BggRawSnapshot>> GetSnapshotsAfterBggIdAsync(int lastBggId, int limit = 200, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BggRawSnapshot>>([]);
        public Task<IReadOnlyList<int>> GetBggIdsMissingVersionsAsync(int limit = 50, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<int>>([]);
        public Task<IReadOnlyList<int>> GetBggIdsNeedingVersionRefreshAsync(int minYear = 2025, int limit = 50, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<int>>([]);
    }

    [Fact]
    public async Task ForceSyncFromBggAsync_WhenUnauthorized_ThrowsUnauthorizedAccessException()
    {
        var gameRepo = new FakeGameRepository();
        var game = CreateGame();
        await gameRepo.UpdateAsync(game);

        var userSvc = new FakeCurrentUserService { Roles = ["User"], Permissions = ModeratorPermission.None };
        var service = new GameEditorService(gameRepo, userSvc, new FakeEditLogRepository(), new FakeCatalogService());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ForceSyncFromBggAsync(game.Id));
    }

    [Fact]
    public async Task ForceSyncFromBggAsync_WhenGameNotFound_ThrowsKeyNotFoundException()
    {
        var gameRepo = new FakeGameRepository();
        var userSvc = new FakeCurrentUserService { Roles = ["Moderator"], Permissions = ModeratorPermission.CanEditGames };
        var service = new GameEditorService(gameRepo, userSvc, new FakeEditLogRepository(), new FakeCatalogService());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ForceSyncFromBggAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ForceSyncFromBggAsync_WhenBggIdZeroOrNegative_ThrowsInvalidOperationException()
    {
        var gameRepo = new FakeGameRepository();
        var game = CreateGame();
        typeof(Game).GetProperty(nameof(Game.BggId))!.SetValue(game, 0);
        await gameRepo.UpdateAsync(game);

        var userSvc = new FakeCurrentUserService { Roles = ["Moderator"], Permissions = ModeratorPermission.CanEditGames };
        var service = new GameEditorService(gameRepo, userSvc, new FakeEditLogRepository(), new FakeCatalogService());


        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ForceSyncFromBggAsync(game.Id));
    }

    [Fact]
    public async Task ForceSyncFromBggAsync_WhenBggReturnsValidXmlWithSpanishVersion_UpdatesGameAndSnapshotAndAudit()
    {
        // Arrange
        var gameRepo = new FakeGameRepository();
        var game = CreateGame();
        game.UpdateBggId(453526);
        game.UpdateSpanishTitle("Got Five!");
        game.UpdateSpanishPublisher("Asmodee Ibérica");
        await gameRepo.UpdateAsync(game);

        var logRepo = new FakeEditLogRepository();
        var userSvc = new FakeCurrentUserService { Roles = ["Moderator"], Permissions = ModeratorPermission.CanEditGames };
        var catalogSvc = new FakeCatalogService();

        var bggXml = """
            <items termsofuse="https://boardgamegeek.com/xmlapi/termsofuse">
              <item type="boardgame" id="453526">
                <thumbnail>https://cf.geekdo-images.com/thumb/pic_root.jpg</thumbnail>
                <image>https://cf.geekdo-images.com/original/pic_root.jpg</image>
                <name type="primary" sortindex="1" value="Got Five!" />
                <description>Got Five! is a game of logic...</description>
                <yearpublished value="2026" />
                <versions>
                  <item type="boardgameversion" id="750000">
                    <thumbnail>https://cf.geekdo-images.com/thumb/pic_es.jpg</thumbnail>
                    <image>https://cf.geekdo-images.com/original/pic_es.jpg</image>
                    <name type="primary" sortindex="1" value="Código 5 - Spanish edition (2026)" />
                    <yearpublished value="2026" />
                    <link type="boardgamepublisher" id="9999" value="Lúdilo" />
                    <link type="language" id="1000" value="Spanish" />
                    <barcode value="8436017220100" />
                  </item>
                </versions>
              </item>
            </items>
            """;

        var bggClient = new FakeBggClient { XmlResponse = bggXml };
        var snapshotRepo = new FakeBggRawSnapshotRepository();

        var service = new GameEditorService(
            gameRepo,
            userSvc,
            logRepo,
            catalogSvc,
            issueReportService: null,
            auditService: null,
            bggClient: bggClient,
            snapshotRepo: snapshotRepo);

        // Act
        var result = await service.ForceSyncFromBggAsync(game.Id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(453526, result.BggId);
        Assert.Equal("Got Five!", result.OldSpanishTitle);
        Assert.Equal("Código 5", result.NewSpanishTitle);
        Assert.Equal("Asmodee Ibérica", result.OldSpanishPublisher);
        Assert.Equal("Lúdilo", result.NewSpanishPublisher);
        Assert.Equal("8436017220100", result.NewEan);
        Assert.Contains("Título en español", result.UpdatedFields);
        Assert.Contains("Editorial española", result.UpdatedFields);
        Assert.Contains("Código EAN", result.UpdatedFields);

        // Entity checks
        var updatedGame = await gameRepo.GetByIdAsync(game.Id);
        Assert.NotNull(updatedGame);
        Assert.Equal("Código 5", updatedGame.SpanishTitle);
        Assert.Equal("Lúdilo", updatedGame.SpanishPublisher);
        Assert.Equal("8436017220100", updatedGame.Ean);

        // Snapshot repo check
        var savedSnapshot = await snapshotRepo.GetByBggIdAsync(453526);
        Assert.NotNull(savedSnapshot);
        Assert.Equal(453526, savedSnapshot.BggId);

        // Audit log check
        Assert.Single(logRepo.Logs);
        Assert.Contains("Sincronización forzada desde BGG #453526", logRepo.Logs[0].SummaryOfChanges);
    }

    [Fact]
    public async Task ForceSyncFromBggAsync_WhenAlreadyUpToDate_ReturnsSuccessWithoutRedundantEntityChanges()
    {
        // Arrange
        var gameRepo = new FakeGameRepository();
        var game = CreateGame();
        game.UpdateBggId(453526);
        game.UpdateSpanishTitle("Código 5");
        game.UpdateSpanishPublisher("Lúdilo");
        game.UpdateEan("8436017220100");
        await gameRepo.UpdateAsync(game);

        var logRepo = new FakeEditLogRepository();
        var userSvc = new FakeCurrentUserService { Roles = ["Moderator"], Permissions = ModeratorPermission.CanEditGames };
        var catalogSvc = new FakeCatalogService();

        var bggXml = """
            <items termsofuse="https://boardgamegeek.com/xmlapi/termsofuse">
              <item type="boardgame" id="453526">
                <name type="primary" sortindex="1" value="Got Five!" />
                <versions>
                  <item type="boardgameversion" id="750000">
                    <name type="primary" sortindex="1" value="Código 5 - Spanish edition (2026)" />
                    <link type="boardgamepublisher" id="9999" value="Lúdilo" />
                    <link type="language" id="1000" value="Spanish" />
                    <barcode value="8436017220100" />
                  </item>
                </versions>
              </item>
            </items>
            """;

        var bggClient = new FakeBggClient { XmlResponse = bggXml };
        var snapshotRepo = new FakeBggRawSnapshotRepository();

        var service = new GameEditorService(
            gameRepo,
            userSvc,
            logRepo,
            catalogSvc,
            bggClient: bggClient,
            snapshotRepo: snapshotRepo);

        // Act
        var result = await service.ForceSyncFromBggAsync(game.Id);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.UpdatedFields);
        Assert.Contains("ya estaban al día", result.Message);
        Assert.Empty(logRepo.Logs); // No audit log created when no changes occurred

        var snapshot = await snapshotRepo.GetByBggIdAsync(453526);
        Assert.NotNull(snapshot);
    }
}


