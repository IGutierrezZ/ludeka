using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class SqliteUserMilestoneRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LudekaDbContext _context;
    private readonly SqliteUserMilestoneRepository _repository;

    private const string User1 = "user-1111";
    private const string User2 = "user-2222";

    public SqliteUserMilestoneRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LudekaDbContext(options);
        _context.Database.EnsureCreated();

        _repository = new SqliteUserMilestoneRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetByUserIdAsync_ShouldReturnEmptyList_WhenNoMilestonesForUser()
    {
        // Act
        var result = await _repository.GetByUserIdAsync(User1);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task UnlockMilestoneAsync_ShouldPersistMilestone_WhenNotAlreadyUnlocked()
    {
        // Arrange
        var milestone = new UserMilestone(User1, MilestoneType.FirstGameInCollection);

        // Act
        var unlocked = await _repository.UnlockMilestoneAsync(milestone);
        var list = await _repository.GetByUserIdAsync(User1);
        var hasIt = await _repository.HasMilestoneAsync(User1, MilestoneType.FirstGameInCollection);

        // Assert
        Assert.True(unlocked);
        Assert.True(hasIt);
        Assert.Single(list);
        Assert.Equal(MilestoneType.FirstGameInCollection, list[0].Type);
    }

    [Fact]
    public async Task UnlockMilestoneAsync_ShouldBeIdempotent_WhenMilestoneAlreadyUnlocked()
    {
        // Arrange
        var initialDate = new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero);
        var first = new UserMilestone(User1, MilestoneType.FirstGameInCollection, initialDate);
        await _repository.UnlockMilestoneAsync(first);

        var laterDate = new DateTimeOffset(2026, 9, 25, 20, 0, 0, TimeSpan.Zero);
        var second = new UserMilestone(User1, MilestoneType.FirstGameInCollection, laterDate);

        // Act
        var unlockedAgain = await _repository.UnlockMilestoneAsync(second);
        var list = await _repository.GetByUserIdAsync(User1);

        // Assert
        Assert.False(unlockedAgain);
        Assert.Single(list);
        Assert.Equal(initialDate, list[0].UnlockedAt);
    }

    [Fact]
    public async Task HasMilestoneAsync_ShouldReturnFalse_WhenNotUnlocked()
    {
        // Act
        var hasIt = await _repository.HasMilestoneAsync(User1, MilestoneType.TenGamesInCollection);

        // Assert
        Assert.False(hasIt);
    }

    [Fact]
    public async Task UnlockMilestonesAsync_ShouldPersistMultipleMilestones_AndReturnCountOfNewUnlocks()
    {
        // Arrange
        var existing = new UserMilestone(User1, MilestoneType.FirstGameInCollection);
        await _repository.UnlockMilestoneAsync(existing);

        var batch = new List<UserMilestone>
        {
            new(User1, MilestoneType.FirstGameInCollection), // Ya existente -> ignorar
            new(User1, MilestoneType.FirstPlayLogged),        // Nuevo
            new(User1, MilestoneType.FirstLikeGiven)         // Nuevo
        };

        // Act
        var addedCount = await _repository.UnlockMilestonesAsync(batch);
        var list = await _repository.GetByUserIdAsync(User1);

        // Assert
        Assert.Equal(2, addedCount);
        Assert.Equal(3, list.Count);
        Assert.Contains(list, m => m.Type == MilestoneType.FirstGameInCollection);
        Assert.Contains(list, m => m.Type == MilestoneType.FirstPlayLogged);
        Assert.Contains(list, m => m.Type == MilestoneType.FirstLikeGiven);
    }

    [Fact]
    public async Task GetByUserIdAsync_ShouldOnlyReturnMilestonesForSpecificUser()
    {
        // Arrange
        await _repository.UnlockMilestoneAsync(new UserMilestone(User1, MilestoneType.FirstGameInCollection));
        await _repository.UnlockMilestoneAsync(new UserMilestone(User2, MilestoneType.FirstGamePlayed));

        // Act
        var list1 = await _repository.GetByUserIdAsync(User1);
        var list2 = await _repository.GetByUserIdAsync(User2);

        // Assert
        Assert.Single(list1);
        Assert.Equal(MilestoneType.FirstGameInCollection, list1[0].Type);

        Assert.Single(list2);
        Assert.Equal(MilestoneType.FirstGamePlayed, list2[0].Type);
    }
}
