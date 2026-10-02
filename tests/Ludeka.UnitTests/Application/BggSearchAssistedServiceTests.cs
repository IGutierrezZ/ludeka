using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Bgg;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class BggSearchAssistedServiceTests
{
    private class FakeCurrentUserService : ICurrentUserService
    {
        public string UserId => "user-123";
        public string UserName => "Tester";
        public string Email => "test@ludeka.es";
        public bool IsAuthenticated => true;
        public bool IsFoundingTeam => false;
        public bool IsModerator => false;
        public IReadOnlyList<string> Roles => ["User"];
        public bool IsInRole(string role) => false;
        public bool HasPermission(ModeratorPermission permission) => false;
    }

    private class FakeBggClient : IBggClient
    {
        public List<BggSearchResultDto> SearchResults = [];
        public Dictionary<int, Game?> Games = [];

        public Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default) =>
            Task.FromResult(Games.GetValueOrDefault(bggId));

        public Task<IReadOnlyList<BggCollectionItemDto>> FetchUserCollectionAsync(string username, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BggCollectionItemDto>>([]);

        public Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BggSearchResultDto>>(SearchResults);

        public Task<IReadOnlyList<BggTopGameDto>> FetchTopGamesAsync(int limit = 50, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BggTopGameDto>>([]);
    }

    private class FakeGameRepo : IGameRepository
    {
        public List<Game> Games = [];

        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Games.FirstOrDefault(g => g.Id == id));

        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult(Games.FirstOrDefault(g => g.Slug == slug));

        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default) =>
            Task.FromResult(Games.FirstOrDefault(g => g.BggId == bggId));

        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
            Task.FromResult(((IReadOnlyList<Game>)Games, Games.Count));

        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default)
        {
            Games.AddRange(games);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Game game, CancellationToken ct = default) => Task.CompletedTask;

        public Task<bool> HasAnyAsync(CancellationToken ct = default) => Task.FromResult(Games.Count > 0);

        public Task<IReadOnlyList<Game>> GetByBggIdsAsync(IEnumerable<int> bggIds, CancellationToken ct = default)
        {
            var set = bggIds.ToHashSet();
            return Task.FromResult<IReadOnlyList<Game>>(Games.Where(g => set.Contains(g.BggId)).ToList());
        }
    }

    private class FakeCollectionRepo : IUserCollectionRepository
    {
        public List<UserCollectionItem> Items = [];

        public Task<UserCollectionItem?> GetByUserAndGameAsync(string userId, Guid gameId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(i => i.UserId == userId && i.GameId == gameId));

        public Task<UserCollectionItem?> GetByUserAndBggIdAsync(string userId, int bggId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(i => i.UserId == userId && (i.BggId == bggId || (i.Game != null && i.Game.BggId == bggId))));

        public Task<List<UserCollectionItem>> GetByUserIdAsync(string userId, CollectionStatus? status = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(status.HasValue ? Items.Where(i => i.UserId == userId && i.Status == status.Value).ToList() : Items.Where(i => i.UserId == userId).ToList());

        public Task<List<UserCollectionItem>> GetPendingItemsByBggIdAsync(int bggId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Where(i => i.BggId == bggId && i.GameId == null).ToList());

        public Task PromotePendingItemsAsync(int bggId, Guid gameId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<List<UserCollectionItem>> GetPlayedByUserIdAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Where(i => i.UserId == userId && i.IsPlayed).ToList());

        public Task<int> GetPlayedCountAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Count(i => i.UserId == userId && i.IsPlayed));

        public Task<Dictionary<CollectionStatus, int>> GetCountsByStatusAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Where(i => i.UserId == userId && i.Status.HasValue).GroupBy(i => i.Status!.Value).ToDictionary(g => g.Key, g => g.Count()));

        public Task AddAsync(UserCollectionItem item, CancellationToken cancellationToken = default)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(UserCollectionItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveAsync(UserCollectionItem item, CancellationToken cancellationToken = default)
        {
            Items.Remove(item);
            return Task.CompletedTask;
        }
    }

    private class FakePendingRepo : IPendingBggImportRepository
    {
        public List<PendingBggImport> Items = [];

        public Task<PendingBggImport?> GetByBggIdAsync(int bggId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(i => i.BggId == bggId));

        public Task<IReadOnlyList<PendingBggImport>> GetTopPendingAsync(int limit = 50, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<PendingBggImport>)Items);

        public Task<IReadOnlyList<PendingBggImport>> GetAllAsync(CatalogQueueStatus? status = null, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<PendingBggImport>)Items);

        public Task<int> GetTotalPendingCountAsync(CancellationToken ct = default) =>
            Task.FromResult(Items.Count);

        public Task AddAsync(PendingBggImport item, CancellationToken ct = default)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(PendingBggImport item, CancellationToken ct = default) => Task.CompletedTask;

        public Task ResetFailedToPendingAsync(CancellationToken ct = default)
        {
            foreach (var item in Items.Where(i => i.Status == CatalogQueueStatus.Failed))
            {
                item.ResetToPending();
            }
            return Task.CompletedTask;
        }
    }

    private static Game CreateGame(int bggId, string title, GameType type = GameType.BaseGame)
    {
        var game = new Game(
            bggId: bggId,
            originalTitle: title,
            spanishTitle: title,
            designer: "Autor",
            publisher: "Editorial",
            yearPublished: 2022,
            coverImageUrl: null,
            thumbnailUrl: null,
            description: null,
            bggRating: 8.2,
            bggRank: 10,
            ludistRating: 8.2,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: true,
            age: new AgeRating(10, 10),
            language: LanguageDependence.None,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(45, 60, 20)
        );
        game.SetGameType(type);
        return game;
    }

    [Fact]
    public async Task SearchGamesAsync_WithShortQuery_ReturnsEmptyListWithoutCallingBgg()
    {
        // Arrange
        var fakeBgg = new FakeBggClient();
        fakeBgg.SearchResults = [new(13, "Catan", 1995)];
        var service = new BggSearchAssistedService(fakeBgg, new FakeGameRepo(), new FakeCollectionRepo(), new FakePendingRepo(), new FakeCurrentUserService());

        // Act
        var result = await service.SearchGamesAsync("a");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchGamesAsync_EnrichesResultsWithCatalogState()
    {
        // Arrange
        var fakeBgg = new FakeBggClient();
        fakeBgg.SearchResults =
        [
            new(13, "Catan", 1995),
            new(342942, "Ark Nova", 2021)
        ];

        var fakeGameRepo = new FakeGameRepo();
        var catan = CreateGame(13, "Catan");
        fakeGameRepo.Games.Add(catan);

        var service = new BggSearchAssistedService(fakeBgg, fakeGameRepo, new FakeCollectionRepo(), new FakePendingRepo(), new FakeCurrentUserService());

        // Act
        var results = await service.SearchGamesAsync("cat");

        // Assert
        Assert.Equal(2, results.Count);

        var catanResult = results[0];
        Assert.True(catanResult.IsAlreadyCataloged);
        Assert.Equal(catan.Id, catanResult.ExistingGameId);
        Assert.Equal(catan.Slug, catanResult.ExistingGameSlug);

        var arkNovaResult = results[1];
        Assert.False(arkNovaResult.IsAlreadyCataloged);
        Assert.Null(arkNovaResult.ExistingGameId);
    }

    [Fact]
    public async Task AddGameToCollectionAsync_WhenGameNotCataloged_CatalogsOnTheFlyAndAddsToCollection()
    {
        // Arrange
        var fakeBgg = new FakeBggClient();
        var arkNova = CreateGame(342942, "Ark Nova");
        fakeBgg.Games[342942] = arkNova;

        var fakeGameRepo = new FakeGameRepo();
        var fakeCollectionRepo = new FakeCollectionRepo();
        var fakePendingRepo = new FakePendingRepo();
        var service = new BggSearchAssistedService(fakeBgg, fakeGameRepo, fakeCollectionRepo, fakePendingRepo, new FakeCurrentUserService());

        // Act
        var gameId = await service.AddGameToCollectionAsync(342942, CollectionStatus.InCollection);

        // Assert
        Assert.Equal(arkNova.Id, gameId);
        Assert.Single(fakeGameRepo.Games);
        Assert.Single(fakeCollectionRepo.Items);
        Assert.Equal(gameId, fakeCollectionRepo.Items[0].GameId);
        Assert.Equal(CollectionStatus.InCollection, fakeCollectionRepo.Items[0].Status);
    }

    [Fact]
    public async Task AddGameToCollectionAsync_WhenGameNotCataloged_AttachesAiSummaryIfServiceAvailable()
    {
        // Arrange
        var fakeBgg = new FakeBggClient();
        var arkNova = CreateGame(342942, "Ark Nova");
        fakeBgg.Games[342942] = arkNova;

        var fakeGameRepo = new FakeGameRepo();
        var fakeCollectionRepo = new FakeCollectionRepo();
        var fakePendingRepo = new FakePendingRepo();
        var fakeAi = new FakeAiSummaryService();

        var service = new BggSearchAssistedService(
            fakeBgg, fakeGameRepo, fakeCollectionRepo, fakePendingRepo, new FakeCurrentUserService(), fakeAi);

        // Act
        var gameId = await service.AddGameToCollectionAsync(342942, CollectionStatus.InCollection);

        // Assert
        Assert.Single(fakeGameRepo.Games);
        var cataloged = fakeGameRepo.Games[0];
        Assert.NotNull(cataloged.AiSummary);
        Assert.Equal("Fake AI Summary for Ark Nova", cataloged.AiSummary.GeneralVerdict);
    }

    [Fact]
    public async Task AddGameToCollectionAsync_WhenExpansionImportedAndBaseGameExists_AutoLinksToBaseGame()
    {
        // Arrange
        var fakeBgg = new FakeBggClient();
        var baseGame = CreateGame(199792, "Everdell", GameType.BaseGame);
        var expansion = CreateGame(328001, "Everdell: New Leaf", GameType.Expansion);
        fakeBgg.Games[328001] = expansion;

        var fakeGameRepo = new FakeGameRepo();
        fakeGameRepo.Games.Add(baseGame);

        var fakeCollectionRepo = new FakeCollectionRepo();
        var fakePendingRepo = new FakePendingRepo();
        var fakeSnapshotRepo = new FakeBggRawSnapshotRepository();

        string rawJson = """
        {
          "item": {
            "@type": "boardgameexpansion",
            "@id": "328001",
            "link": [
              {
                "@type": "boardgameexpansion",
                "@id": "199792",
                "@value": "Everdell",
                "@inbound": "true"
              }
            ]
          }
        }
        """;
        await fakeSnapshotRepo.UpsertAsync(new BggRawSnapshot(328001, rawJson));

        var service = new BggSearchAssistedService(
            fakeBgg, fakeGameRepo, fakeCollectionRepo, fakePendingRepo, new FakeCurrentUserService(),
            snapshotRepo: fakeSnapshotRepo);

        // Act
        var resultId = await service.AddGameToCollectionAsync(328001, CollectionStatus.InCollection);

        // Assert
        var catalogedExpansion = fakeGameRepo.Games.FirstOrDefault(g => g.BggId == 328001);
        Assert.NotNull(catalogedExpansion);
        Assert.Equal(baseGame.Id, catalogedExpansion.BaseGameId);
    }

    [Fact]
    public async Task AddGameToCollectionAsync_WhenBaseGameImportedAndOrphanExpansionsExist_AutoLinksExpansions()
    {
        // Arrange
        var fakeBgg = new FakeBggClient();
        var baseGame = CreateGame(199792, "Everdell", GameType.BaseGame);
        var orphanExpansion = CreateGame(328001, "Everdell: New Leaf", GameType.Expansion);

        fakeBgg.Games[199792] = baseGame;

        var fakeGameRepo = new FakeGameRepo();
        fakeGameRepo.Games.Add(orphanExpansion);

        var fakeCollectionRepo = new FakeCollectionRepo();
        var fakePendingRepo = new FakePendingRepo();
        var fakeSnapshotRepo = new FakeBggRawSnapshotRepository();

        string baseGameRawJson = """
        {
          "item": {
            "@type": "boardgame",
            "@id": "199792",
            "link": [
              {
                "@type": "boardgameexpansion",
                "@id": "328001",
                "@value": "Everdell: New Leaf"
              }
            ]
          }
        }
        """;
        await fakeSnapshotRepo.UpsertAsync(new BggRawSnapshot(199792, baseGameRawJson));

        var service = new BggSearchAssistedService(
            fakeBgg, fakeGameRepo, fakeCollectionRepo, fakePendingRepo, new FakeCurrentUserService(),
            snapshotRepo: fakeSnapshotRepo);

        // Act
        var resultId = await service.AddGameToCollectionAsync(199792, CollectionStatus.InCollection);

        // Assert
        var catalogedBaseGame = fakeGameRepo.Games.FirstOrDefault(g => g.BggId == 199792);
        Assert.NotNull(catalogedBaseGame);
        Assert.Equal(catalogedBaseGame.Id, orphanExpansion.BaseGameId);
    }

    [Fact]
    public async Task AddGameToCollectionAsync_WhenExpansionImported_GeneratesExpansionAporte()
    {
        // Arrange
        var fakeBgg = new FakeBggClient();
        var baseGame = CreateGame(199792, "Everdell", GameType.BaseGame);
        var expansion = CreateGame(328001, "Everdell: New Leaf", GameType.Expansion);
        fakeBgg.Games[328001] = expansion;

        var fakeGameRepo = new FakeGameRepo();
        fakeGameRepo.Games.Add(baseGame);

        var fakeCollectionRepo = new FakeCollectionRepo();
        var fakePendingRepo = new FakePendingRepo();
        var fakeSnapshotRepo = new FakeBggRawSnapshotRepository();
        var fakeAi = new FakeAiSummaryService();

        string rawJson = """
        {
          "item": {
            "@type": "boardgameexpansion",
            "@id": "328001",
            "link": [
              {
                "@type": "boardgameexpansion",
                "@id": "199792",
                "@value": "Everdell",
                "@inbound": "true"
              }
            ]
          }
        }
        """;
        await fakeSnapshotRepo.UpsertAsync(new BggRawSnapshot(328001, rawJson));

        var service = new BggSearchAssistedService(
            fakeBgg, fakeGameRepo, fakeCollectionRepo, fakePendingRepo, new FakeCurrentUserService(),
            aiSummaryService: fakeAi,
            snapshotRepo: fakeSnapshotRepo);

        // Act
        var resultId = await service.AddGameToCollectionAsync(328001, CollectionStatus.InCollection);

        // Assert
        var catalogedExpansion = fakeGameRepo.Games.FirstOrDefault(g => g.BggId == 328001);
        Assert.NotNull(catalogedExpansion);
        Assert.Equal("Fake Aporte for Everdell: New Leaf", catalogedExpansion.WhatItBringsSummary);
        Assert.Equal(ExpansionNecessity.HighlyRecommended, catalogedExpansion.ExpansionNecessity);
        Assert.Contains(ExpansionImpactTag.AddsPlayers, catalogedExpansion.ImpactTags);
        Assert.Equal(1, catalogedExpansion.ExtraPlayerCount);
        Assert.Equal(15, catalogedExpansion.ExtraDurationMinutes);
    }

    private class FakeBggRawSnapshotRepository : IBggRawSnapshotRepository
    {
        public Dictionary<int, BggRawSnapshot> Snapshots = [];

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
            => Task.FromResult<IReadOnlyList<BggRawSnapshot>>(Snapshots.Values.Where(s => s.BggId > lastBggId).OrderBy(s => s.BggId).Take(limit).ToList());
    }

    private class FakeAiSummaryService : IAiGameSummaryService
    {
        public Task<AiGameSummaryDto> GenerateSummaryAsync(Game game, CancellationToken ct = default)
        {
            return Task.FromResult(new AiGameSummaryDto(
                game.Id,
                game.SpanishTitle,
                "Ideal 2",
                "14+",
                "Monstruo",
                $"Fake AI Summary for {game.SpanishTitle}",
                "FakeModel",
                DateTime.UtcNow
            ));
        }

        public Task<ExpansionAporteAiDto> GenerateExpansionAporteAsync(Game expansion, Game? baseGame = null, CancellationToken ct = default)
        {
            return Task.FromResult(new ExpansionAporteAiDto(
                ExpansionId: expansion.Id,
                WhatItBringsSummary: $"Fake Aporte for {expansion.SpanishTitle}",
                Necessity: ExpansionNecessity.HighlyRecommended,
                ImpactTags: new List<ExpansionImpactTag> { ExpansionImpactTag.ModularContent, ExpansionImpactTag.AddsPlayers }.AsReadOnly(),
                ExtraPlayerCount: 1,
                ExtraDurationMinutes: 15,
                Model: "FakeAiModel",
                GeneratedAt: DateTime.UtcNow
            ));
        }

        public Task<AiGameSummaryDto> EnsureSummaryForGameAsync(Guid gameId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<AiBatchProcessingResultDto> ProcessPendingSummariesBatchAsync(int batchSize = 20, CancellationToken ct = default)
            => Task.FromResult(new AiBatchProcessingResultDto(0, 0, 0, []));

        public Task<AiBatchResultDto> GenerateBatchSummariesAsync(IReadOnlyList<AiGameBatchInputDto> games, CancellationToken ct = default)
        {
            var dict = new Dictionary<int, AiGameSummaryDto>();
            foreach (var g in games)
            {
                dict[g.BggId] = new AiGameSummaryDto(Guid.Empty, g.SpanishTitle, "Ideal 2", "14+", "Monstruo", $"Fake AI Summary for {g.SpanishTitle}", "FakeModel", DateTime.UtcNow);
            }
            return Task.FromResult(new AiBatchResultDto(true, false, dict, null));
        }
    }
}
