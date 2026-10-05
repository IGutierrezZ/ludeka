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

public class BggImportServiceTests
{
    private class FakeCurrentUserService : ICurrentUserService
    {
        public string UserId => "test-user-1";
        public string UserName => "Usuario Test";
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
        public List<BggCollectionItemDto> ItemsToReturn = [];

        public Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default) => Task.FromResult<Game?>(null);

        public Task<IReadOnlyList<BggCollectionItemDto>> FetchUserCollectionAsync(string username, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<BggCollectionItemDto>>(ItemsToReturn);
        }

        public Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<BggSearchResultDto>>([]);
        }

        public Task<IReadOnlyList<BggTopGameDto>> FetchTopGamesAsync(int limit = 50, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<BggTopGameDto>>([]);
        }
    }

    private class FakeGameRepository : IGameRepository
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
    }

    private class FakeUserCollectionRepository : IUserCollectionRepository
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

        public Task PromotePendingItemsAsync(int bggId, Guid gameId, CancellationToken cancellationToken = default)
        {
            foreach (var item in Items.Where(i => i.BggId == bggId && i.GameId == null))
            {
                item.PromoteToCataloged(gameId);
            }
            return Task.CompletedTask;
        }

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

    private class FakePendingBggImportRepository : IPendingBggImportRepository
    {
        public List<PendingBggImport> Queue = [];

        public Task<PendingBggImport?> GetByBggIdAsync(int bggId, CancellationToken ct = default) =>
            Task.FromResult(Queue.FirstOrDefault(p => p.BggId == bggId));

        public Task<IReadOnlyList<PendingBggImport>> GetTopPendingAsync(int limit = 50, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<PendingBggImport>)Queue.Where(p => p.Status == CatalogQueueStatus.Pending).OrderByDescending(p => p.RequestedCount).Take(limit).ToList());

        public Task<IReadOnlyList<PendingBggImport>> GetAllAsync(CatalogQueueStatus? status = null, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<PendingBggImport>)(status.HasValue ? Queue.Where(p => p.Status == status.Value).ToList() : Queue.ToList()));

        public Task<int> GetTotalPendingCountAsync(CancellationToken ct = default) =>
            Task.FromResult(Queue.Count(p => p.Status == CatalogQueueStatus.Pending));

        public Task AddAsync(PendingBggImport item, CancellationToken ct = default)
        {
            Queue.Add(item);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(PendingBggImport item, CancellationToken ct = default) => Task.CompletedTask;

        public Task ResetFailedToPendingAsync(CancellationToken ct = default)
        {
            foreach (var item in Queue.Where(p => p.Status == CatalogQueueStatus.Failed))
            {
                item.ResetToPending();
            }
            return Task.CompletedTask;
        }

        public Task<int> RecoverStaleProcessingToPendingAsync(CancellationToken ct = default)
        {
            var stale = Queue.Where(i => i.Status == CatalogQueueStatus.Processing).ToList();
            foreach (var item in stale)
            {
                item.ResetToPending();
            }
            return Task.FromResult(stale.Count);
        }
    }

    private static Game CreateGame(int bggId, string title)
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
            description: null,
            bggRating: 8.0,
            bggRank: 1,
            ludistRating: 8.0,
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
    public async Task ImportUserCollectionAsync_WithExistingGame_LinksDirectlyToCollection()
    {
        // Arrange
        var fakeBgg = new FakeBggClient();
        fakeBgg.ItemsToReturn =
        [
            new(13, "Catan", 1995, null, null, IsOwned: true, IsWishlist: false, IsWantToBuy: false, NumPlays: 5)
        ];

        var fakeGameRepo = new FakeGameRepository();
        var existingCatan = CreateGame(13, "Catan");
        fakeGameRepo.Games.Add(existingCatan);

        var fakeCollectionRepo = new FakeUserCollectionRepository();
        var fakePendingRepo = new FakePendingBggImportRepository();
        var service = new BggImportService(fakeBgg, fakeGameRepo, fakeCollectionRepo, fakePendingRepo, new FakeCurrentUserService());

        // Act
        var result = await service.ImportUserCollectionAsync(new BggImportRequest("userbgg"));

        // Assert
        Assert.Equal(1, result.TotalProcessed);
        Assert.Equal(1, result.ImportedToCollection);
        Assert.Equal(0, result.EnqueuedForCataloging);
        Assert.Single(fakeCollectionRepo.Items);
        Assert.Equal(existingCatan.Id, fakeCollectionRepo.Items[0].GameId);
        Assert.False(fakeCollectionRepo.Items[0].IsPendingCataloging);
        Assert.Empty(fakePendingRepo.Queue);
    }

    [Fact]
    public async Task ImportUserCollectionAsync_WithUncatalogedGame_EnqueuesAndCreatesPendingCollectionItem()
    {
        // Arrange
        var fakeBgg = new FakeBggClient();
        fakeBgg.ItemsToReturn =
        [
            new(342942, "Ark Nova", 2021, "https://example.com/thumb.jpg", null, IsOwned: true, IsWishlist: false, IsWantToBuy: false, NumPlays: 0)
        ];

        var fakeGameRepo = new FakeGameRepository();
        var fakeCollectionRepo = new FakeUserCollectionRepository();
        var fakePendingRepo = new FakePendingBggImportRepository();
        var service = new BggImportService(fakeBgg, fakeGameRepo, fakeCollectionRepo, fakePendingRepo, new FakeCurrentUserService());

        // Act
        var result = await service.ImportUserCollectionAsync(new BggImportRequest("userbgg"));

        // Assert
        Assert.Equal(1, result.TotalProcessed);
        Assert.Equal(0, result.ImportedToCollection);
        Assert.Equal(1, result.EnqueuedForCataloging);

        Assert.Single(fakeCollectionRepo.Items);
        var pendingItem = fakeCollectionRepo.Items[0];
        Assert.True(pendingItem.IsPendingCataloging);
        Assert.Equal(342942, pendingItem.BggId);
        Assert.Equal("Ark Nova", pendingItem.PendingTitle);

        Assert.Single(fakePendingRepo.Queue);
        Assert.Equal(342942, fakePendingRepo.Queue[0].BggId);
        Assert.Equal(1, fakePendingRepo.Queue[0].RequestedCount);
    }
}
