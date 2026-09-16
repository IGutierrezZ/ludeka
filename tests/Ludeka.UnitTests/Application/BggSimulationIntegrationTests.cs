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
using Ludeka.Infrastructure.Bgg;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class BggSimulationIntegrationTests
{
    private class FakeCurrentUserService : ICurrentUserService
    {
        public string UserId => "user-sim-1";
        public string UserName => "Tester";
        public string Email => "tester@ludeka.es";
        public bool IsAuthenticated => true;
        public bool IsFoundingTeam => false;
        public bool IsModerator => false;
        public IReadOnlyList<string> Roles => ["User"];
        public bool IsInRole(string role) => false;
        public bool HasPermission(ModeratorPermission permission) => false;
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

        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default)
        {
            var query = Games.AsQueryable();
            if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
            {
                query = query.Where(g => g.SpanishTitle.Contains(criteria.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                                         g.OriginalTitle.Contains(criteria.SearchTerm, StringComparison.OrdinalIgnoreCase));
            }
            var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(((IReadOnlyList<Game>)items, query.Count()));
        }

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

    private class FakePendingRepo : IPendingBggImportRepository
    {
        public List<PendingBggImport> Items = [];

        public Task<PendingBggImport?> GetByBggIdAsync(int bggId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(i => i.BggId == bggId));

        public Task<IReadOnlyList<PendingBggImport>> GetTopPendingAsync(int limit = 50, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<PendingBggImport>)Items.Where(i => i.Status == CatalogQueueStatus.Pending || i.Status == CatalogQueueStatus.Failed)
                .OrderByDescending(i => i.RequestedCount).Take(limit).ToList());

        public Task<IReadOnlyList<PendingBggImport>> GetAllAsync(CatalogQueueStatus? status = null, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<PendingBggImport>)(status.HasValue ? Items.Where(i => i.Status == status.Value).ToList() : Items.ToList()));

        public Task<int> GetTotalPendingCountAsync(CancellationToken ct = default) =>
            Task.FromResult(Items.Count(i => i.Status == CatalogQueueStatus.Pending || i.Status == CatalogQueueStatus.Failed));

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

    [Fact]
    public async Task SearchAssisted_WithSimulatedClient_ReturnsOfficialMetadataAndDetectsCatalogedGames()
    {
        var client = new SimulatedBggClient();
        var gameRepo = new FakeGameRepository();
        var collectionRepo = new FakeUserCollectionRepository();
        var pendingRepo = new FakePendingRepo();
        var user = new FakeCurrentUserService();

        // Precargar Dune: Imperium en el catálogo local
        var dune = await client.FetchGameByBggIdAsync(316554);
        Assert.NotNull(dune);
        gameRepo.Games.Add(dune);

        var service = new BggSearchAssistedService(client, gameRepo, collectionRepo, pendingRepo, user);

        var results = await service.SearchGamesAsync("Dune");

        Assert.NotEmpty(results);
        var duneResult = results.FirstOrDefault(r => r.BggId == 316554);
        Assert.NotNull(duneResult);
        Assert.True(duneResult.IsAlreadyCataloged);
        Assert.Equal(dune.Id, duneResult.ExistingGameId);
    }

    [Fact]
    public async Task ImportUserCollection_WithSimulatedClient_PartitionsBetweenLocalAndPendingQueue()
    {
        var client = new SimulatedBggClient();
        var gameRepo = new FakeGameRepository();
        var collectionRepo = new FakeUserCollectionRepository();
        var pendingRepo = new FakePendingRepo();
        var user = new FakeCurrentUserService();

        // Solo Wingspan está en el catálogo local
        var wingspan = await client.FetchGameByBggIdAsync(266192);
        Assert.NotNull(wingspan);
        gameRepo.Games.Add(wingspan);

        var service = new BggImportService(client, gameRepo, collectionRepo, pendingRepo, user);

        var result = await service.ImportUserCollectionAsync(new BggImportRequest("ludeka_demo", true, true));

        Assert.Equal(12, result.TotalProcessed);
        Assert.True(result.ImportedToCollection >= 1); // Wingspan importado
        Assert.True(result.EnqueuedForCataloging >= 1); // Otros encolados
        Assert.Contains(collectionRepo.Items, i => i.GameId == wingspan.Id);
        Assert.Contains(pendingRepo.Items, p => p.BggId == 13); // Catán encolado
    }

    [Fact]
    public async Task ProcessPendingQueue_WithSimulatedClient_CatalogsGameWithAllDataAndPromotesCollection()
    {
        var client = new SimulatedBggClient();
        var gameRepo = new FakeGameRepository();
        var collectionRepo = new FakeUserCollectionRepository();
        var pendingRepo = new FakePendingRepo();

        // Encolar Heat: Pedal to the Metal (366013)
        var pendingItem = new PendingBggImport(366013, "Heat: Pedal to the Metal", 2022);
        pendingRepo.Items.Add(pendingItem);

        // Simular que un usuario tenía este juego en espera en su colección
        var pendingUserItem = new UserCollectionItem("user-1", 366013, "Heat: Pedal to the Metal", CollectionStatus.InCollection);
        collectionRepo.Items.Add(pendingUserItem);

        var queueService = new BggCatalogQueueService(pendingRepo, client, gameRepo, collectionRepo);

        var processResult = await queueService.ProcessPendingQueueBatchAsync(batchSize: 10);

        Assert.Equal(1, processResult.ProcessedCount);
        Assert.Equal(1, processResult.SuccessCount);
        Assert.Equal(CatalogQueueStatus.Completed, pendingItem.Status);

        // El juego debe estar en gameRepo con sus ValueObjects completos
        var catalogedGame = gameRepo.Games.FirstOrDefault(g => g.BggId == 366013);
        Assert.NotNull(catalogedGame);
        Assert.Equal("Heat: Pedal to the Metal", catalogedGame.SpanishTitle);
        Assert.NotEmpty(catalogedGame.Scalability);
        Assert.NotEmpty(catalogedGame.Sleeves);

        // La colección del usuario debe haberse promovido al Id real
        Assert.False(pendingUserItem.IsPendingCataloging);
        Assert.Equal(catalogedGame.Id, pendingUserItem.GameId);
    }
}
