using System;
using Ludeka.Core.Entities;
using Xunit;

namespace Ludeka.UnitTests.Domain;

public class DailyTrendingGameTests
{
    [Fact]
    public void Constructor_WithValidData_ShouldCreateEntity()
    {
        var date = new DateOnly(2026, 10, 1);
        var gameId = Guid.NewGuid();

        var item = new DailyTrendingGame(
            dateUtc: date,
            rank: 1,
            bggId: 174430,
            title: "Gloomhaven",
            yearPublished: 2017,
            thumbnailUrl: "https://example.com/thumb.jpg",
            gameId: gameId);

        Assert.NotEqual(Guid.Empty, item.Id);
        Assert.Equal(date, item.DateUtc);
        Assert.Equal(1, item.Rank);
        Assert.Equal(174430, item.BggId);
        Assert.Equal("Gloomhaven", item.Title);
        Assert.Equal(2017, item.YearPublished);
        Assert.Equal("https://example.com/thumb.jpg", item.ThumbnailUrl);
        Assert.Equal(gameId, item.GameId);
        Assert.True(item.CreatedAtUtc <= DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    [InlineData(-5)]
    public void Constructor_WithInvalidRank_ShouldThrowArgumentOutOfRangeException(int rank)
    {
        var date = new DateOnly(2026, 10, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DailyTrendingGame(date, rank, 100, "Ark Nova"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithInvalidBggId_ShouldThrowArgumentOutOfRangeException(int bggId)
    {
        var date = new DateOnly(2026, 10, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DailyTrendingGame(date, 5, bggId, "Ark Nova"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyTitle_ShouldThrowArgumentException(string title)
    {
        var date = new DateOnly(2026, 10, 1);
        Assert.Throws<ArgumentException>(() =>
            new DailyTrendingGame(date, 5, 100, title));
    }

    [Fact]
    public void LinkToGame_WithValidGuid_ShouldSetGameId()
    {
        var date = new DateOnly(2026, 10, 1);
        var item = new DailyTrendingGame(date, 3, 224517, "Brass: Birmingham");
        Assert.Null(item.GameId);

        var gameId = Guid.NewGuid();
        item.LinkToGame(gameId);

        Assert.Equal(gameId, item.GameId);
    }

    [Fact]
    public void LinkToGame_WithEmptyGuid_ShouldThrowArgumentException()
    {
        var date = new DateOnly(2026, 10, 1);
        var item = new DailyTrendingGame(date, 3, 224517, "Brass: Birmingham");

        Assert.Throws<ArgumentException>(() => item.LinkToGame(Guid.Empty));
    }
}
