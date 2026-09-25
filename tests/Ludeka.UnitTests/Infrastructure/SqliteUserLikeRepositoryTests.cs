using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class SqliteUserLikeRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LudekaDbContext _context;
    private readonly SqliteUserLikeRepository _repository;

    private readonly Guid _user1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _user2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _publisherId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _storeId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public SqliteUserLikeRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LudekaDbContext(options);
        _context.Database.EnsureCreated();

        _repository = new SqliteUserLikeRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ToggleLikeAsync_ShouldAddLike_WhenNotPresent()
    {
        // Act
        var isLiked = await _repository.ToggleLikeAsync(_user1, LikeTargetType.Publisher, _publisherId);

        // Assert
        Assert.True(isLiked);
        var hasLiked = await _repository.HasUserLikedAsync(_user1, LikeTargetType.Publisher, _publisherId);
        Assert.True(hasLiked);
        var count = await _repository.GetLikesCountAsync(LikeTargetType.Publisher, _publisherId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ToggleLikeAsync_ShouldRemoveLike_WhenAlreadyPresent()
    {
        // Arrange
        await _repository.ToggleLikeAsync(_user1, LikeTargetType.Publisher, _publisherId);

        // Act
        var isLiked = await _repository.ToggleLikeAsync(_user1, LikeTargetType.Publisher, _publisherId);

        // Assert
        Assert.False(isLiked);
        var hasLiked = await _repository.HasUserLikedAsync(_user1, LikeTargetType.Publisher, _publisherId);
        Assert.False(hasLiked);
        var count = await _repository.GetLikesCountAsync(LikeTargetType.Publisher, _publisherId);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ToggleLikeAsync_ShouldThrow_WhenUserIdOrTargetIdIsEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _repository.ToggleLikeAsync(Guid.Empty, LikeTargetType.Publisher, _publisherId));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _repository.ToggleLikeAsync(_user1, LikeTargetType.Publisher, Guid.Empty));
    }

    [Fact]
    public async Task GetLikesCountAsync_ShouldAggregateAcrossMultipleUsers()
    {
        // Arrange
        await _repository.ToggleLikeAsync(_user1, LikeTargetType.Publisher, _publisherId);
        await _repository.ToggleLikeAsync(_user2, LikeTargetType.Publisher, _publisherId);

        // Act
        var count = await _repository.GetLikesCountAsync(LikeTargetType.Publisher, _publisherId);

        // Assert
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetLikesCountsAsync_ShouldReturnCountsForMultipleTargets()
    {
        // Arrange
        await _repository.ToggleLikeAsync(_user1, LikeTargetType.Publisher, _publisherId);
        await _repository.ToggleLikeAsync(_user2, LikeTargetType.Publisher, _publisherId);
        await _repository.ToggleLikeAsync(_user1, LikeTargetType.Store, _storeId);

        var otherId = Guid.NewGuid();

        // Act
        var counts = await _repository.GetLikesCountsAsync(
            LikeTargetType.Publisher,
            [_publisherId, otherId]);

        // Assert
        Assert.Equal(2, counts[_publisherId]);
        Assert.Equal(0, counts[otherId]);
    }

    [Fact]
    public async Task GetUserLikedTargetIdsAsync_ShouldReturnOnlyTargetsLikedBySpecificUser()
    {
        // Arrange
        await _repository.ToggleLikeAsync(_user1, LikeTargetType.Publisher, _publisherId);
        var otherPublisherId = Guid.NewGuid();
        await _repository.ToggleLikeAsync(_user2, LikeTargetType.Publisher, otherPublisherId);

        // Act
        var user1Liked = await _repository.GetUserLikedTargetIdsAsync(
            _user1,
            LikeTargetType.Publisher,
            [_publisherId, otherPublisherId]);

        // Assert
        Assert.Contains(_publisherId, user1Liked);
        Assert.DoesNotContain(otherPublisherId, user1Liked);
    }
}
