using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Milestones;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class MilestoneServiceTests
{
    private readonly InMemoryUserMilestoneRepository _milestoneRepo = new();
    private readonly InMemoryUserCollectionRepository _collectionRepo = new();
    private readonly InMemoryGamePlayLogRepository _playLogRepo = new();
    private readonly InMemoryGameLoanRepository _loanRepo = new();
    private readonly InMemoryUserReviewRepository _reviewRepo = new();
    private readonly InMemoryUserLikeRepository _likeRepo = new();

    private readonly MilestoneService _service;
    private const string TestUserId = "user-test-777";

    public MilestoneServiceTests()
    {
        _service = new MilestoneService(
            _milestoneRepo,
            _collectionRepo,
            _playLogRepo,
            _loanRepo,
            _reviewRepo,
            _likeRepo
        );
    }

    [Fact]
    public async Task GetUserMilestonesAsync_ShouldReturnAllMilestones_WithZeroUnlockedForNewUser()
    {
        // Act
        var progress = await _service.GetUserMilestonesAsync(TestUserId);

        // Assert
        Assert.NotNull(progress);
        Assert.Equal(TestUserId, progress.UserId);
        Assert.Equal(0, progress.TotalUnlocked);
        Assert.Equal(MilestoneCatalog.All.Count, progress.TotalMilestones);
        Assert.Equal(0.0, progress.CompletionPercentage);
        Assert.All(progress.Milestones, m => Assert.False(m.IsUnlocked));
    }

    [Fact]
    public async Task EvaluateAndSyncMilestonesAsync_ShouldUnlockCollectionMilestones_WhenThresholdsMet()
    {
        // Arrange: 10 títulos en colección y 10 jugados
        for (int i = 0; i < 10; i++)
        {
            var item = new UserCollectionItem(TestUserId, Guid.NewGuid(), CollectionStatus.InCollection, isPlayed: true);
            await _collectionRepo.AddAsync(item);
        }

        // Act
        var progress = await _service.EvaluateAndSyncMilestonesAsync(TestUserId);

        // Assert
        Assert.True(progress.Milestones.First(m => m.Type == MilestoneType.FirstGameInCollection).IsUnlocked);
        Assert.True(progress.Milestones.First(m => m.Type == MilestoneType.TenGamesInCollection).IsUnlocked);
        Assert.True(progress.Milestones.First(m => m.Type == MilestoneType.FirstGamePlayed).IsUnlocked);
        Assert.True(progress.Milestones.First(m => m.Type == MilestoneType.TenGamesPlayed).IsUnlocked);
    }

    [Fact]
    public async Task EvaluateAndSyncMilestonesAsync_ShouldUnlockLoanMilestone_WhenUserHasLoaned()
    {
        // Arrange
        var loan = new GameLoan(TestUserId, Guid.NewGuid(), "Amigo Lúdico", DateTimeOffset.UtcNow);
        await _loanRepo.AddAsync(loan);

        // Act
        var progress = await _service.EvaluateAndSyncMilestonesAsync(TestUserId);

        // Assert
        var loanMilestone = progress.Milestones.First(m => m.Type == MilestoneType.FirstGameLoaned);
        Assert.True(loanMilestone.IsUnlocked);
    }

    [Fact]
    public async Task EvaluateAndSyncMilestonesAsync_ShouldUnlockPlayMilestones_AndLargeGroup()
    {
        // Arrange: 5 partidas registradas, una de ellas con 6 jugadores
        var gameId = Guid.NewGuid();
        for (int i = 1; i <= 4; i++)
        {
            await _playLogRepo.AddAsync(new GamePlayLog(TestUserId, gameId, DateTimeOffset.UtcNow, "En casa", playerCount: 3));
        }
        await _playLogRepo.AddAsync(new GamePlayLog(TestUserId, gameId, DateTimeOffset.UtcNow, "Club", playerCount: 6));

        // Act
        var progress = await _service.EvaluateAndSyncMilestonesAsync(TestUserId);

        // Assert
        Assert.True(progress.Milestones.First(m => m.Type == MilestoneType.FirstPlayLogged).IsUnlocked);
        Assert.True(progress.Milestones.First(m => m.Type == MilestoneType.FivePlaysLogged).IsUnlocked);
        Assert.True(progress.Milestones.First(m => m.Type == MilestoneType.LargeGroupPlay).IsUnlocked);
    }

    [Fact]
    public async Task EvaluateAndSyncMilestonesAsync_ShouldUnlockReviewAndLikeMilestones()
    {
        // Arrange
        var review = new UserGameReview(TestUserId, Guid.NewGuid(), 9.0, "Juegazo");
        await _reviewRepo.AddAsync(review);

        if (Guid.TryParse(TestUserId, out var guidUser))
        {
            await _likeRepo.AddLikeAsync(new UserLike(guidUser, LikeTargetType.Publisher, Guid.NewGuid()));
        }
        else
        {
            // Para strings libres, usar Guid determinista o Guid para el test
            var deterministicGuid = Guid.Parse("00000000-0000-0000-0000-000000000777");
            await _likeRepo.AddLikeAsync(new UserLike(deterministicGuid, LikeTargetType.Publisher, Guid.NewGuid()));
        }

        // Act (evaluando con guidUser o deterministicGuid)
        var userIdForLike = Guid.Parse("00000000-0000-0000-0000-000000000777").ToString();
        await _reviewRepo.AddAsync(new UserGameReview(userIdForLike, Guid.NewGuid(), 8.0, "Bueno"));
        var progress = await _service.EvaluateAndSyncMilestonesAsync(userIdForLike);

        // Assert
        Assert.True(progress.Milestones.First(m => m.Type == MilestoneType.FirstReviewSubmitted).IsUnlocked);
        Assert.True(progress.Milestones.First(m => m.Type == MilestoneType.FirstLikeGiven).IsUnlocked);
    }

    [Fact]
    public async Task EvaluateAndSyncMilestonesAsync_ShouldPreserveOriginalUnlockedDate_OnMultipleEvaluations()
    {
        // Arrange
        await _collectionRepo.AddAsync(new UserCollectionItem(TestUserId, Guid.NewGuid(), CollectionStatus.InCollection));
        var initialProgress = await _service.EvaluateAndSyncMilestonesAsync(TestUserId);
        var initialUnlockDate = initialProgress.Milestones.First(m => m.Type == MilestoneType.FirstGameInCollection).UnlockedAt;
        Assert.NotNull(initialUnlockDate);

        // Act
        await Task.Delay(10);
        var secondProgress = await _service.EvaluateAndSyncMilestonesAsync(TestUserId);
        var secondUnlockDate = secondProgress.Milestones.First(m => m.Type == MilestoneType.FirstGameInCollection).UnlockedAt;

        // Assert
        Assert.Equal(initialUnlockDate, secondUnlockDate);
    }

    // --- Repositorios InMemory auxiliares para pruebas ---
    private class InMemoryUserMilestoneRepository : IUserMilestoneRepository
    {
        private readonly List<UserMilestone> _items = [];

        public Task<List<UserMilestone>> GetByUserIdAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult(_items.Where(m => m.UserId == userId).ToList());

        public Task<bool> HasMilestoneAsync(string userId, MilestoneType type, CancellationToken ct = default) =>
            Task.FromResult(_items.Any(m => m.UserId == userId && m.Type == type));

        public Task<bool> UnlockMilestoneAsync(UserMilestone milestone, CancellationToken ct = default)
        {
            if (_items.Any(m => m.UserId == milestone.UserId && m.Type == milestone.Type))
                return Task.FromResult(false);

            _items.Add(milestone);
            return Task.FromResult(true);
        }

        public Task<int> UnlockMilestonesAsync(IEnumerable<UserMilestone> milestones, CancellationToken ct = default)
        {
            int added = 0;
            foreach (var m in milestones)
            {
                if (!_items.Any(existing => existing.UserId == m.UserId && existing.Type == m.Type))
                {
                    _items.Add(m);
                    added++;
                }
            }
            return Task.FromResult(added);
        }
    }

    private class InMemoryUserCollectionRepository : IUserCollectionRepository
    {
        private readonly List<UserCollectionItem> _items = [];

        public Task AddAsync(UserCollectionItem item, CancellationToken cancellationToken = default)
        {
            _items.Add(item);
            return Task.CompletedTask;
        }

        public Task<List<UserCollectionItem>> GetByUserIdAsync(string userId, CollectionStatus? status = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.Where(i => i.UserId == userId && (!status.HasValue || i.Status == status.Value)).ToList());

        public Task<Dictionary<CollectionStatus, int>> GetCountsByStatusAsync(string userId, CancellationToken cancellationToken = default)
        {
            var dict = _items.Where(i => i.UserId == userId && i.Status.HasValue)
                .GroupBy(i => i.Status!.Value)
                .ToDictionary(g => g.Key, g => g.Count());
            return Task.FromResult(dict);
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UserCollectionItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UserCollectionItem?> GetByUserAndGameAsync(string userId, Guid gameId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UserCollectionItem?> GetByUserAndBggIdAsync(string userId, int bggId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<UserCollectionItem>> GetPendingCatalogingAsync(int limit = 50, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> GetCountByGameIdAsync(Guid gameId, CollectionStatus? status = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Dictionary<Guid, int>> GetCountsByGameIdsAsync(IEnumerable<Guid> gameIds, CollectionStatus? status = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> GetTotalCountByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Dictionary<Guid, int>> GetTotalCountsByGameIdsAsync(IEnumerable<Guid> gameIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(UserCollectionItem item, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<UserCollectionItem>> GetPendingItemsByBggIdAsync(int bggId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task PromotePendingItemsAsync(int bggId, Guid officialGameId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task RemoveAsync(UserCollectionItem item, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private class InMemoryGamePlayLogRepository : IGamePlayLogRepository
    {
        private readonly List<GamePlayLog> _plays = [];

        public Task AddAsync(GamePlayLog play, CancellationToken cancellationToken = default)
        {
            _plays.Add(play);
            return Task.CompletedTask;
        }

        public Task<List<GamePlayLog>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_plays.Where(p => p.UserId == userId).ToList());

        public Task<int> GetCountByUserAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_plays.Count(p => p.UserId == userId));

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<GamePlayLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<GamePlayLog>> GetByUserAndGameAsync(string userId, Guid gameId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> GetCountByUserAndGameAsync(string userId, Guid gameId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private class InMemoryGameLoanRepository : IGameLoanRepository
    {
        private readonly List<GameLoan> _loans = [];

        public Task AddAsync(GameLoan loan, CancellationToken cancellationToken = default)
        {
            _loans.Add(loan);
            return Task.CompletedTask;
        }

        public Task<int> GetActiveLoansCountAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_loans.Count(l => l.UserId == userId && l.ReturnedDate == null));

        public Task<List<GameLoan>> GetLoanHistoryByUserIdAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_loans.Where(l => l.UserId == userId).ToList());

        public Task<List<GameLoan>> GetActiveLoansByUserIdAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_loans.Where(l => l.UserId == userId && l.ReturnedDate == null).ToList());

        public Task<GameLoan?> GetByIdAsync(Guid loanId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<GameLoan?> GetActiveLoanByUserAndGameAsync(string userId, Guid gameId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(GameLoan loan, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private class InMemoryUserReviewRepository : IUserReviewRepository
    {
        private readonly List<UserGameReview> _reviews = [];

        public Task AddAsync(UserGameReview review, CancellationToken cancellationToken = default)
        {
            _reviews.Add(review);
            return Task.CompletedTask;
        }

        public Task<int> GetCountByUserIdAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_reviews.Count(r => r.UserId == userId));

        public Task<UserGameReview?> GetByUserAndGameAsync(string userId, Guid gameId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_reviews.FirstOrDefault(r => r.UserId == userId && r.GameId == gameId));

        public Task<List<UserGameReview>> GetByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<double?> GetAverageScoreByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(UserGameReview review, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private class InMemoryUserLikeRepository : IUserLikeRepository
    {
        private readonly List<UserLike> _likes = [];

        public Task AddLikeAsync(UserLike like)
        {
            _likes.Add(like);
            return Task.CompletedTask;
        }

        public Task<int> GetLikesGivenCountByUserAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(_likes.Count(l => l.UserId == userId));

        public Task<int> GetLikesCountAsync(LikeTargetType targetType, Guid targetId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Dictionary<Guid, int>> GetLikesCountsAsync(LikeTargetType targetType, IEnumerable<Guid> targetIds, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<HashSet<Guid>> GetUserLikedTargetIdsAsync(Guid userId, LikeTargetType targetType, IEnumerable<Guid> targetIds, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> HasUserLikedAsync(Guid userId, LikeTargetType targetType, Guid targetId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> ToggleLikeAsync(Guid userId, LikeTargetType targetType, Guid targetId, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
