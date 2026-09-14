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

public class BggImportServiceProgressTests
{
    private class FakeCurrentUserService : ICurrentUserService
    {
        public string UserId => "user-progress-test";
        public string UserName => "Tester";
        public string Email => "tester@ludeka.es";
        public bool IsAuthenticated => true;
        public bool IsFoundingTeam => false;
        public bool IsModerator => false;
        public IReadOnlyList<string> Roles => ["User"];
        public bool IsInRole(string role) => false;
        public void SwitchRole(string role) { }
    }

    private class ProgressTrackingBggClient : IBggClient
    {
        public List<BggCollectionItemDto> ItemsToReturn = [];

        public Task<Game?> FetchGameByBggIdAsync(int bggId, CancellationToken ct = default) => Task.FromResult<Game?>(null);

        public Task<IReadOnlyList<BggCollectionItemDto>> FetchUserCollectionAsync(string username, CancellationToken ct = default)
            => FetchUserCollectionAsync(username, null, ct);

        public Task<IReadOnlyList<BggCollectionItemDto>> FetchUserCollectionAsync(
            string username,
            IProgress<BggImportProgressReport>? progress,
            CancellationToken ct = default)
        {
            progress?.Report(new BggImportProgressReport(BggImportPhase.PreparingInBgg, "Simulando preparación en BGG...", CurrentAttempt: 1, MaxAttempts: 3));
            return Task.FromResult<IReadOnlyList<BggCollectionItemDto>>(ItemsToReturn);
        }

        public Task<IReadOnlyList<BggSearchResultDto>> SearchGamesAsync(string query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BggSearchResultDto>>([]);
        public Task<IReadOnlyList<BggTopGameDto>> FetchTopGamesAsync(int limit = 50, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BggTopGameDto>>([]);
    }

    private class InMemoryGameRepository : IGameRepository
    {
        public List<Game> Games = [];
        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Games.FirstOrDefault(g => g.Id == id));
        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(Games.FirstOrDefault(g => g.Slug == slug));
        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default) => Task.FromResult(Games.FirstOrDefault(g => g.BggId == bggId));
        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default) => Task.FromResult(((IReadOnlyList<Game>)Games, Games.Count));
        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default) { Games.AddRange(games); return Task.CompletedTask; }
        public Task UpdateAsync(Game game, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> HasAnyAsync(CancellationToken ct = default) => Task.FromResult(Games.Count > 0);
    }

    private class InMemoryUserCollectionRepository : IUserCollectionRepository
    {
        public List<UserCollectionItem> Items = [];
        public Task<UserCollectionItem?> GetByUserAndGameAsync(string userId, Guid gameId, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(i => i.UserId == userId && i.GameId == gameId));
        public Task<UserCollectionItem?> GetByUserAndBggIdAsync(string userId, int bggId, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(i => i.UserId == userId && (i.BggId == bggId || (i.Game != null && i.Game.BggId == bggId))));
        public Task<List<UserCollectionItem>> GetByUserIdAsync(string userId, CollectionStatus? status = null, CancellationToken cancellationToken = default) => Task.FromResult(status.HasValue ? Items.Where(i => i.UserId == userId && i.Status == status.Value).ToList() : Items.Where(i => i.UserId == userId).ToList());
        public Task<List<UserCollectionItem>> GetPendingItemsByBggIdAsync(int bggId, CancellationToken cancellationToken = default) => Task.FromResult(Items.Where(i => i.BggId == bggId && i.GameId == null).ToList());
        public Task PromotePendingItemsAsync(int bggId, Guid gameId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<UserCollectionItem>> GetPlayedByUserIdAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult(Items.Where(i => i.UserId == userId && i.IsPlayed).ToList());
        public Task<int> GetPlayedCountAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult(Items.Count(i => i.UserId == userId && i.IsPlayed));
        public Task<Dictionary<CollectionStatus, int>> GetCountsByStatusAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult(Items.Where(i => i.UserId == userId && i.Status.HasValue).GroupBy(i => i.Status!.Value).ToDictionary(g => g.Key, g => g.Count()));
        public Task AddAsync(UserCollectionItem item, CancellationToken cancellationToken = default) { Items.Add(item); return Task.CompletedTask; }
        public Task UpdateAsync(UserCollectionItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(UserCollectionItem item, CancellationToken cancellationToken = default) { Items.Remove(item); return Task.CompletedTask; }
    }

    private class InMemoryPendingBggImportRepository : IPendingBggImportRepository
    {
        public List<PendingBggImport> Queue = [];
        public Task<PendingBggImport?> GetByBggIdAsync(int bggId, CancellationToken ct = default) => Task.FromResult(Queue.FirstOrDefault(p => p.BggId == bggId));
        public Task<IReadOnlyList<PendingBggImport>> GetTopPendingAsync(int limit = 50, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<PendingBggImport>)Queue.Take(limit).ToList());
        public Task<IReadOnlyList<PendingBggImport>> GetAllAsync(CatalogQueueStatus? status = null, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<PendingBggImport>)Queue.ToList());
        public Task<int> GetTotalPendingCountAsync(CancellationToken ct = default) => Task.FromResult(Queue.Count);
        public Task AddAsync(PendingBggImport item, CancellationToken ct = default) { Queue.Add(item); return Task.CompletedTask; }
        public Task UpdateAsync(PendingBggImport item, CancellationToken ct = default) => Task.CompletedTask;
        public Task ResetFailedToPendingAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task ImportUserCollectionAsync_WithProgress_ReportsAllLifecyclePhases()
    {
        // Arrange
        var fakeBgg = new ProgressTrackingBggClient();
        fakeBgg.ItemsToReturn =
        [
            new(13, "Catan", 1995, null, null, IsOwned: true, IsWishlist: false, IsWantToBuy: false, NumPlays: 2),
            new(342942, "Ark Nova", 2021, null, null, IsOwned: true, IsWishlist: false, IsWantToBuy: false, NumPlays: 0)
        ];

        var gameRepo = new InMemoryGameRepository();
        var collectionRepo = new InMemoryUserCollectionRepository();
        var pendingRepo = new InMemoryPendingBggImportRepository();
        var service = new BggImportService(fakeBgg, gameRepo, collectionRepo, pendingRepo, new FakeCurrentUserService());

        var reportedProgress = new System.Collections.Concurrent.ConcurrentBag<BggImportProgressReport>();
        var progress = new Progress<BggImportProgressReport>(p => reportedProgress.Add(p));

        // Act
        var result = await service.ImportUserCollectionAsync(new BggImportRequest("ludofan"), progress);

        // Pequeña pausa opcional para asegurar que los callbacks asíncronos de Progress hayan drenado
        await Task.Delay(50);

        // Assert
        Assert.Equal(2, result.TotalProcessed);
        Assert.Contains(reportedProgress, p => p.Phase == BggImportPhase.Initializing);
        Assert.Contains(reportedProgress, p => p.Phase == BggImportPhase.PreparingInBgg);
        Assert.Contains(reportedProgress, p => p.Phase == BggImportPhase.ProcessingItems && p.ItemsFound == 2);
        Assert.Contains(reportedProgress, p => p.Phase == BggImportPhase.Completed);
    }
}
