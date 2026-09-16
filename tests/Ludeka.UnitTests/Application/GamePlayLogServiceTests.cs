using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Plays;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Xunit;

namespace Ludeka.UnitTests.Application;

public class GamePlayLogServiceTests
{
    private class FakeGamePlayLogRepository : IGamePlayLogRepository
    {
        public List<GamePlayLog> Plays { get; } = [];

        public Task AddAsync(GamePlayLog play, CancellationToken cancellationToken = default)
        {
            Plays.Add(play);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Plays.RemoveAll(p => p.Id == id);
            return Task.CompletedTask;
        }

        public Task<GamePlayLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Plays.FirstOrDefault(p => p.Id == id));
        }

        public Task<List<GamePlayLog>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Plays.Where(p => p.UserId == userId).OrderByDescending(p => p.PlayDate).ToList());
        }

        public Task<List<GamePlayLog>> GetByUserAndGameAsync(string userId, Guid gameId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Plays.Where(p => p.UserId == userId && p.GameId == gameId).OrderByDescending(p => p.PlayDate).ToList());
        }

        public Task<int> GetCountByUserAsync(string userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Plays.Count(p => p.UserId == userId));
        }

        public Task<int> GetCountByUserAndGameAsync(string userId, Guid gameId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Plays.Count(p => p.UserId == userId && p.GameId == gameId));
        }
    }

    private class FakeGameRepository : IGameRepository
    {
        public List<Game> Games { get; } = [];

        public Task<Game?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Games.FirstOrDefault(g => g.Id == id));

        public Task<Game?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
            => Task.FromResult(Games.FirstOrDefault(g => g.Slug == slug));

        public Task<Game?> GetByBggIdAsync(int bggId, CancellationToken cancellationToken = default)
            => Task.FromResult(Games.FirstOrDefault(g => g.BggId == bggId));

        public Task<List<Game>> SearchAsync(string term, CancellationToken cancellationToken = default)
            => Task.FromResult(Games.Where(g => g.SpanishTitle.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList());

        public Task<List<Game>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Games.ToList());

        public Task<List<Game>> GetCatalogAsync(int page, int pageSize, CancellationToken cancellationToken = default)
            => Task.FromResult(Games.Skip((page - 1) * pageSize).Take(pageSize).ToList());

        public Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Games.Count);

        public Task<(IReadOnlyList<Game> Items, int TotalCount)> SearchAsync(GameFilterCriteria criteria, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
            => Task.FromResult(((IReadOnlyList<Game>)Games, Games.Count));

        public Task AddRangeAsync(IEnumerable<Game> games, CancellationToken cancellationToken = default)
        {
            Games.AddRange(games);
            return Task.CompletedTask;
        }

        public Task<bool> HasAnyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Games.Count > 0);

        public Task AddAsync(Game game, CancellationToken cancellationToken = default)
        {
            Games.Add(game);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Game game, CancellationToken cancellationToken = default)
        {
            var idx = Games.FindIndex(g => g.Id == game.Id);
            if (idx >= 0) Games[idx] = game;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Games.RemoveAll(g => g.Id == id);
            return Task.CompletedTask;
        }
    }

    private class FakeUserCollectionRepository : IUserCollectionRepository
    {
        public List<UserCollectionItem> Items { get; } = [];

        public Task<UserCollectionItem?> GetByUserAndGameAsync(string userId, Guid gameId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(i => i.UserId == userId && i.GameId == gameId));

        public Task<UserCollectionItem?> GetByUserAndBggIdAsync(string userId, int bggId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(i => i.UserId == userId && i.BggId == bggId));

        public Task<List<UserCollectionItem>> GetByUserIdAsync(string userId, CollectionStatus? status = null, CancellationToken cancellationToken = default)
        {
            var res = Items.Where(i => i.UserId == userId);
            if (status.HasValue) res = res.Where(i => i.Status == status.Value);
            return Task.FromResult(res.ToList());
        }

        public Task<List<UserCollectionItem>> GetPlayedByUserIdAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Where(i => i.UserId == userId && i.IsPlayed).ToList());

        public Task<int> GetPlayedCountAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Count(i => i.UserId == userId && i.IsPlayed));

        public Task<List<UserCollectionItem>> GetPendingItemsByBggIdAsync(int bggId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Where(i => i.BggId == bggId && i.GameId == null).ToList());

        public Task PromotePendingItemsAsync(int bggId, Guid gameId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Dictionary<CollectionStatus, int>> GetCountsByStatusAsync(string userId, CancellationToken cancellationToken = default)
        {
            var dict = Items
                .Where(i => i.UserId == userId && i.Status.HasValue)
                .GroupBy(i => i.Status!.Value)
                .ToDictionary(g => g.Key, g => g.Count());
            return Task.FromResult(dict);
        }

        public Task AddAsync(UserCollectionItem item, CancellationToken cancellationToken = default)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(UserCollectionItem item, CancellationToken cancellationToken = default)
        {
            var idx = Items.FindIndex(i => i.Id == item.Id);
            if (idx >= 0) Items[idx] = item;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(UserCollectionItem item, CancellationToken cancellationToken = default)
        {
            Items.RemoveAll(i => i.Id == item.Id);
            return Task.CompletedTask;
        }
    }

    private class FakeCurrentUserService : ICurrentUserService
    {
        public string UserId => "user-play-tester";
        public string UserName => "PlayTester";
        public bool IsAuthenticated => true;
        public IReadOnlyList<string> Roles { get; set; } = ["User"];
        public bool IsFoundingTeam => IsInRole("FoundingTeam");
        public bool IsInRole(string role) => Roles.Contains(role);
        public bool HasPermission(ModeratorPermission permission) => IsFoundingTeam || IsInRole("Moderator");
    }

    private static Game CreateGame(string title = "Catan")
    {
        return new Game(
            bggId: 13,
            originalTitle: title,
            spanishTitle: title,
            designer: "Klaus Teuber",
            publisher: "Devir",
            yearPublished: 1995,
            coverImageUrl: "https://example.com/catan.jpg",
            thumbnailUrl: null,
            description: "Juego de comercio",
            bggRating: 7.2,
            bggRank: 500,
            ludistRating: 0.0,
            confrontation: ConfrontationType.Competitive,
            style: GameStyle.Eurogame,
            isOfficialSolo: false,
            age: new AgeRating(10, 10),
            language: LanguageDependence.Low,
            footprint: TableFootprint.StandardTable,
            duration: new GameDuration(60, 120, 30)
        );
    }

    [Fact]
    public async Task RecordPlayAsync_ShouldCreatePlayLog_AndAutomaticallyMarkGameAsPlayed()
    {
        // Arrange
        var playRepo = new FakeGamePlayLogRepository();
        var gameRepo = new FakeGameRepository();
        var colRepo = new FakeUserCollectionRepository();
        var userSvc = new FakeCurrentUserService();
        var game = CreateGame();
        gameRepo.Games.Add(game);

        var service = new GamePlayLogService(playRepo, gameRepo, colRepo, userSvc);

        // Act
        var req = new RecordPlayRequest(
            game.Id,
            DateTimeOffset.UtcNow,
            "Club El Troquel",
            4,
            60,
            "Victoria ajustada"
        );
        var result = await service.RecordPlayAsync(req);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(game.Id, result.GameId);
        Assert.Equal("Club El Troquel", result.Location);
        Assert.Equal(4, result.PlayerCount);
        Assert.Single(playRepo.Plays);

        // Verifica que se marcó como Jugado en la colección del usuario
        var colItem = colRepo.Items.FirstOrDefault(i => i.GameId == game.Id);
        Assert.NotNull(colItem);
        Assert.True(colItem.IsPlayed);
        Assert.Null(colItem.Status); // No tenía posesión previa
    }

    [Fact]
    public async Task RecordPlayAsync_ShouldPreserveExistingWantToBuyStatus_WhenPlayed()
    {
        // Arrange
        var playRepo = new FakeGamePlayLogRepository();
        var gameRepo = new FakeGameRepository();
        var colRepo = new FakeUserCollectionRepository();
        var userSvc = new FakeCurrentUserService();
        var game = CreateGame("Terraforming Mars");
        gameRepo.Games.Add(game);

        // El usuario ya tenía el juego en su lista de compra
        var existing = new UserCollectionItem(userSvc.UserId, game.Id, CollectionStatus.WantToBuy, isPlayed: false);
        colRepo.Items.Add(existing);

        var service = new GamePlayLogService(playRepo, gameRepo, colRepo, userSvc);

        // Act
        var req = new RecordPlayRequest(game.Id, DateTimeOffset.UtcNow, "En casa", 3);
        await service.RecordPlayAsync(req);

        // Assert
        Assert.Equal(CollectionStatus.WantToBuy, existing.Status);
        Assert.True(existing.IsPlayed);
    }

    [Fact]
    public async Task RecordPlayAsync_ShouldThrowKeyNotFoundException_WhenGameNotFound()
    {
        var service = new GamePlayLogService(new FakeGamePlayLogRepository(), new FakeGameRepository(), new FakeUserCollectionRepository(), new FakeCurrentUserService());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.RecordPlayAsync(new RecordPlayRequest(Guid.NewGuid(), DateTimeOffset.UtcNow, "Casa", 2)));
    }

    [Fact]
    public async Task GetUserPlaysStatsAsync_ShouldCalculateMetricsCorrectly()
    {
        // Arrange
        var playRepo = new FakeGamePlayLogRepository();
        var gameRepo = new FakeGameRepository();
        var colRepo = new FakeUserCollectionRepository();
        var userSvc = new FakeCurrentUserService();

        var gameA = CreateGame("Ark Nova");
        var gameB = CreateGame("Wingspan");
        gameRepo.Games.Add(gameA);
        gameRepo.Games.Add(gameB);

        var now = DateTimeOffset.UtcNow;
        playRepo.Plays.Add(new GamePlayLog(userSvc.UserId, gameA.Id, now, "Club Lúdico", 4));
        playRepo.Plays.Add(new GamePlayLog(userSvc.UserId, gameA.Id, now.AddDays(-1), "Club Lúdico", 4));
        playRepo.Plays.Add(new GamePlayLog(userSvc.UserId, gameB.Id, now.AddDays(-2), "En casa", 2));

        var service = new GamePlayLogService(playRepo, gameRepo, colRepo, userSvc);

        // Act
        var stats = await service.GetUserPlaysStatsAsync();

        // Assert
        Assert.Equal(3, stats.TotalPlays);
        Assert.Equal("Ark Nova", stats.MostPlayedGameTitle);
        Assert.Equal(2, stats.MostPlayedGameCount);
        Assert.Equal("Club Lúdico", stats.FavoriteLocation);
        Assert.Equal(4, stats.MostCommonPlayerCount);
    }

    [Fact]
    public async Task DeletePlayAsync_ShouldRemovePlay_WhenUserMatches()
    {
        // Arrange
        var playRepo = new FakeGamePlayLogRepository();
        var userSvc = new FakeCurrentUserService();
        var play = new GamePlayLog(userSvc.UserId, Guid.NewGuid(), DateTimeOffset.UtcNow, "Casa", 2);
        playRepo.Plays.Add(play);

        var service = new GamePlayLogService(playRepo, new FakeGameRepository(), new FakeUserCollectionRepository(), userSvc);

        // Act
        await service.DeletePlayAsync(play.Id);

        // Assert
        Assert.Empty(playRepo.Plays);
    }
}
