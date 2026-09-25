using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Community;
using Ludeka.Application.Features.Directory;
using Ludeka.Application.Features.Media;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class UserLikeServiceTests
{
    private class InMemoryUserLikeRepository : IUserLikeRepository
    {
        private readonly HashSet<(Guid UserId, LikeTargetType TargetType, Guid TargetId)> _likes = [];

        public Task<int> GetLikesCountAsync(LikeTargetType targetType, Guid targetId, CancellationToken ct = default)
        {
            var count = _likes.Count(l => l.TargetType == targetType && l.TargetId == targetId);
            return Task.FromResult(count);
        }

        public Task<Dictionary<Guid, int>> GetLikesCountsAsync(LikeTargetType targetType, IEnumerable<Guid> targetIds, CancellationToken ct = default)
        {
            var dict = targetIds.ToDictionary(
                id => id,
                id => _likes.Count(l => l.TargetType == targetType && l.TargetId == id)
            );
            return Task.FromResult(dict);
        }

        public Task<bool> HasUserLikedAsync(Guid userId, LikeTargetType targetType, Guid targetId, CancellationToken ct = default)
        {
            var exists = _likes.Contains((userId, targetType, targetId));
            return Task.FromResult(exists);
        }

        public Task<HashSet<Guid>> GetUserLikedTargetIdsAsync(Guid userId, LikeTargetType targetType, IEnumerable<Guid> targetIds, CancellationToken ct = default)
        {
            var liked = targetIds
                .Where(id => _likes.Contains((userId, targetType, id)))
                .ToHashSet();
            return Task.FromResult(liked);
        }

        public Task<int> GetLikesGivenCountByUserAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(_likes.Count(l => l.UserId == userId));

        public Task<bool> ToggleLikeAsync(Guid userId, LikeTargetType targetType, Guid targetId, CancellationToken ct = default)
        {
            var key = (userId, targetType, targetId);
            if (_likes.Contains(key))
            {
                _likes.Remove(key);
                return Task.FromResult(false);
            }

            _likes.Add(key);
            return Task.FromResult(true);
        }

        public void AddDirectLike(Guid userId, LikeTargetType targetType, Guid targetId)
        {
            _likes.Add((userId, targetType, targetId));
        }
    }

    [Fact]
    public async Task ToggleLikeAsync_WhenEmptyUserId_ThrowsArgumentException()
    {
        var repo = new InMemoryUserLikeRepository();
        var service = new UserLikeService(repo);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ToggleLikeAsync(Guid.Empty, LikeTargetType.Publisher, Guid.NewGuid()));
    }

    [Fact]
    public async Task ToggleLikeAsync_WhenAuthenticated_TogglesAndReturnsUpdatedStatus()
    {
        var repo = new InMemoryUserLikeRepository();
        var service = new UserLikeService(repo);
        var userId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        // 1. Primer toggle: añade like
        var status1 = await service.ToggleLikeAsync(userId, LikeTargetType.Publisher, targetId);
        Assert.True(status1.IsLiked);
        Assert.Equal(1, status1.LikesCount);

        // 2. Segundo toggle: quita like
        var status2 = await service.ToggleLikeAsync(userId, LikeTargetType.Publisher, targetId);
        Assert.False(status2.IsLiked);
        Assert.Equal(0, status2.LikesCount);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsAccurateStateForAuthenticatedAndAnonymous()
    {
        var repo = new InMemoryUserLikeRepository();
        var targetId = Guid.NewGuid();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        repo.AddDirectLike(user1, LikeTargetType.Creator, targetId);
        repo.AddDirectLike(user2, LikeTargetType.Creator, targetId);

        var service = new UserLikeService(repo);

        // Usuario autenticado que votó
        var authedStatus = await service.GetStatusAsync(user1, LikeTargetType.Creator, targetId);
        Assert.True(authedStatus.IsLiked);
        Assert.Equal(2, authedStatus.LikesCount);

        // Usuario anónimo
        var anonStatus = await service.GetStatusAsync(null, LikeTargetType.Creator, targetId);
        Assert.False(anonStatus.IsLiked);
        Assert.Equal(2, anonStatus.LikesCount);
    }

    [Fact]
    public async Task GetStatusesAsync_ReturnsBatchStatusesForMultipleTargets()
    {
        var repo = new InMemoryUserLikeRepository();
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        var user = Guid.NewGuid();

        repo.AddDirectLike(user, LikeTargetType.Store, t1);
        repo.AddDirectLike(Guid.NewGuid(), LikeTargetType.Store, t1);
        repo.AddDirectLike(Guid.NewGuid(), LikeTargetType.Store, t2);

        var service = new UserLikeService(repo);
        var statuses = await service.GetStatusesAsync(user, LikeTargetType.Store, [t1, t2]);

        Assert.Equal(2, statuses.Count);
        Assert.True(statuses[t1].IsLiked);
        Assert.Equal(2, statuses[t1].LikesCount);
        Assert.False(statuses[t2].IsLiked);
        Assert.Equal(1, statuses[t2].LikesCount);
    }

    [Fact]
    public async Task PublisherService_GetAllAsync_OrdersByLikesDescendingThenByName()
    {
        var pub1 = new Publisher("Devir", "devir", "España");
        var pub2 = new Publisher("Asmodee", "asmodee", "Francia");
        var pub3 = new Publisher("Arrakis", "arrakis", "España");

        var pubRepo = new FakePublisherRepository { Items = [pub1, pub2, pub3] };
        var gameRepo = new FakeGameRepository();
        var likeRepo = new InMemoryUserLikeRepository();

        // Devir: 2 likes, Asmodee: 5 likes, Arrakis: 2 likes (Arrakis antes que Devir alfabéticamente)
        for (int i = 0; i < 5; i++) likeRepo.AddDirectLike(Guid.NewGuid(), LikeTargetType.Publisher, pub2.Id);
        likeRepo.AddDirectLike(Guid.NewGuid(), LikeTargetType.Publisher, pub1.Id);
        likeRepo.AddDirectLike(Guid.NewGuid(), LikeTargetType.Publisher, pub1.Id);
        likeRepo.AddDirectLike(Guid.NewGuid(), LikeTargetType.Publisher, pub3.Id);
        likeRepo.AddDirectLike(Guid.NewGuid(), LikeTargetType.Publisher, pub3.Id);

        var service = new PublisherService(pubRepo, gameRepo, userLikeRepository: likeRepo);
        var result = await service.GetAllAsync();

        Assert.Equal(3, result.Count);
        Assert.Equal("Asmodee", result[0].Name);
        Assert.Equal(5, result[0].LikesCount);
        Assert.Equal("Arrakis", result[1].Name);
        Assert.Equal(2, result[1].LikesCount);
        Assert.Equal("Devir", result[2].Name);
        Assert.Equal(2, result[2].LikesCount);
    }

    [Fact]
    public async Task StoreService_GetAllAsync_OrdersByLikesDescendingThenByName()
    {
        var store1 = new Store("Zacatrus", "zacatrus", StoreType.Hybrid, "España");
        var store2 = new Store("Juegos de la Mesa Redonda", "jmr", StoreType.OnlineOnly, "España");

        var storeRepo = new FakeStoreRepository { Items = [store1, store2] };
        var gameRepo = new FakeGameRepository();
        var likeRepo = new InMemoryUserLikeRepository();

        likeRepo.AddDirectLike(Guid.NewGuid(), LikeTargetType.Store, store2.Id);
        likeRepo.AddDirectLike(Guid.NewGuid(), LikeTargetType.Store, store2.Id);
        likeRepo.AddDirectLike(Guid.NewGuid(), LikeTargetType.Store, store1.Id);

        var service = new StoreService(storeRepo, gameRepo, userLikeRepository: likeRepo);
        var result = await service.GetAllAsync();

        Assert.Equal("Juegos de la Mesa Redonda", result[0].Name);
        Assert.Equal(2, result[0].LikesCount);
        Assert.Equal("Zacatrus", result[1].Name);
        Assert.Equal(1, result[1].LikesCount);
    }

    [Fact]
    public async Task CreatorService_GetAllAsync_OrdersByLikesDescendingThenByName()
    {
        var c1 = new Creator("El Agujero Hobbit", "el-agujero-hobbit");
        var c2 = new Creator("Meeple Foundry", "meeple-foundry");

        var creatorRepo = new FakeCreatorRepository { Items = [c1, c2] };
        var likeRepo = new InMemoryUserLikeRepository();

        likeRepo.AddDirectLike(Guid.NewGuid(), LikeTargetType.Creator, c2.Id);

        var service = new CreatorService(creatorRepo, userLikeRepository: likeRepo);
        var result = await service.GetAllAsync();

        Assert.Equal("Meeple Foundry", result[0].Name);
        Assert.Equal(1, result[0].LikesCount);
        Assert.Equal("El Agujero Hobbit", result[1].Name);
        Assert.Equal(0, result[1].LikesCount);
    }

    [Fact]
    public async Task MediaService_GetGameMediaAsync_OrdersVideosByLikesDescendingThenByPublishedAt()
    {
        var gameId = Guid.NewGuid();
        var video1 = new MediaItem(
            MediaType.Tutorial,
            MediaPlatform.YouTube,
            "Tutorial Básico (Menos likes)",
            "https://youtube.com/watch?v=111",
            "https://img.youtube.com/1.jpg",
            "Canal A",
            gameId: gameId,
            status: ModerationStatus.Approved,
            publishedAt: DateTimeOffset.UtcNow.AddDays(-1)
        );

        var video2 = new MediaItem(
            MediaType.Tutorial,
            MediaPlatform.YouTube,
            "Tutorial Completo (Más likes)",
            "https://youtube.com/watch?v=222",
            "https://img.youtube.com/2.jpg",
            "Canal B",
            gameId: gameId,
            status: ModerationStatus.Approved,
            publishedAt: DateTimeOffset.UtcNow.AddDays(-5)
        );

        var mediaRepo = new FakeMediaRepository { Items = [video1, video2] };
        var gameRepo = new FakeGameRepository();
        var brokenChecker = new FakeBrokenLinkCheckerService();
        var likeRepo = new InMemoryUserLikeRepository();

        // video2 tiene 3 likes, video1 tiene 0 likes
        likeRepo.AddDirectLike(Guid.NewGuid(), LikeTargetType.MediaItem, video2.Id);
        likeRepo.AddDirectLike(Guid.NewGuid(), LikeTargetType.MediaItem, video2.Id);
        likeRepo.AddDirectLike(Guid.NewGuid(), LikeTargetType.MediaItem, video2.Id);

        var service = new MediaService(mediaRepo, gameRepo, brokenChecker, userLikeRepository: likeRepo);
        var mediaHub = await service.GetGameMediaAsync(gameId);

        Assert.Equal(2, mediaHub.Tutorials.Count);
        Assert.Equal(video2.Id, mediaHub.Tutorials[0].Id);
        Assert.Equal(3, mediaHub.Tutorials[0].UserLikesCount);
        Assert.Equal(video1.Id, mediaHub.Tutorials[1].Id);
        Assert.Equal(0, mediaHub.Tutorials[1].UserLikesCount);
    }

    private class FakePublisherRepository : IPublisherRepository
    {
        public List<Publisher> Items = [];
        public Task<IReadOnlyList<Publisher>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Publisher>>(Items);
        public Task<Publisher?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<Publisher?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Slug == slug));
        public Task<Publisher?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Name == name));
        public Task AddAsync(Publisher p, CancellationToken ct = default) { Items.Add(p); return Task.CompletedTask; }
        public Task UpdateAsync(Publisher p, CancellationToken ct = default) { return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { Items.RemoveAll(x => x.Id == id); return Task.CompletedTask; }
    }

    private class FakeStoreRepository : IStoreRepository
    {
        public List<Store> Items = [];
        public Task<IReadOnlyList<Store>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Store>>(Items);
        public Task<Store?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<Store?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Slug == slug));
        public Task<Store?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Name == name));
        public Task AddAsync(Store s, CancellationToken ct = default) { Items.Add(s); return Task.CompletedTask; }
        public Task UpdateAsync(Store s, CancellationToken ct = default) { return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { Items.RemoveAll(x => x.Id == id); return Task.CompletedTask; }
    }

    private class FakeCreatorRepository : ICreatorRepository
    {
        public List<Creator> Items = [];
        public Task<IReadOnlyList<Creator>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Creator>>(Items);
        public Task<Creator?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<Creator?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Slug == slug));
        public Task<Creator?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Name == name));
        public Task AddAsync(Creator c, CancellationToken ct = default) { Items.Add(c); return Task.CompletedTask; }
        public Task UpdateAsync(Creator c, CancellationToken ct = default) { return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { Items.RemoveAll(x => x.Id == id); return Task.CompletedTask; }
    }

    private class FakeMediaRepository : IMediaRepository
    {
        public List<MediaItem> Items = [];
        public Task<IReadOnlyList<MediaItem>> GetApprovedByGameIdAsync(Guid gameId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MediaItem>>(Items.Where(x => x.GameId == gameId && x.Status == ModerationStatus.Approved).ToList());
        public Task<IReadOnlyList<MediaItem>> GetPendingModerationAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MediaItem>>([]);
        public Task<IReadOnlyList<MediaItem>> GetOrphansAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MediaItem>>([]);
        public Task<IReadOnlyList<MediaItem>> GetApprovedAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MediaItem>>([]);
        public Task<IReadOnlyList<MediaItem>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MediaItem>>(Items);
        public Task<bool> ExistsByUrlAsync(string url, CancellationToken ct = default) => Task.FromResult(Items.Any(x => x.Url == url));
        public Task<MediaItem?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task AddAsync(MediaItem item, CancellationToken ct = default) { Items.Add(item); return Task.CompletedTask; }
        public Task UpdateAsync(MediaItem item, CancellationToken ct = default) { return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { Items.RemoveAll(x => x.Id == id); return Task.CompletedTask; }
    }

    private class FakeGameRepository : IGameRepository
    {
        public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Game?>(null);
        public Task<Game?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult<Game?>(null);
        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken ct = default) => Task.FromResult<Game?>(null);
        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
            Task.FromResult<(IReadOnlyList<Game> Items, int TotalCount)>(([], 0));
        public Task<bool> HasAnyAsync(CancellationToken ct = default) => Task.FromResult(false);
        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Game game, CancellationToken ct = default) => Task.CompletedTask;
    }

    private class FakeBrokenLinkCheckerService : IBrokenLinkCheckerService
    {
        public Task<bool> IsUrlBrokenAsync(string url, CancellationToken ct = default) => Task.FromResult(false);
        public Task<BrokenLinkReportDto> CheckLinksAsync(CancellationToken ct = default) =>
            Task.FromResult(new BrokenLinkReportDto(0, 0, []));
    }
}
