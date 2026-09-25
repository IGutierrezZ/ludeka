using System;
using System.Threading.Tasks;
using Ludeka.Core.Entities;
using Ludeka.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class MagicLinkTokenRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LudekaDbContext _context;
    private readonly MagicLinkTokenRepository _repository;

    public MagicLinkTokenRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LudekaDbContext(options);
        _context.Database.EnsureCreated();

        _repository = new MagicLinkTokenRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task AddAsync_ShouldPersistTokenCorrectly()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new MagicLinkToken("test@ludeka.es", "hash123", now, now.AddMinutes(15));

        await _repository.AddAsync(token);

        var retrieved = await _repository.GetByIdAsync(token.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("test@ludeka.es", retrieved.Email);
        Assert.Equal("hash123", retrieved.TokenHash);
        Assert.Null(retrieved.ConsumedAt);
    }

    [Fact]
    public async Task GetValidByTokenHashAsync_ShouldReturnToken_WhenNotConsumedAndNotExpired()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new MagicLinkToken("user@ludeka.es", "validhash456", now, now.AddMinutes(15));
        await _repository.AddAsync(token);

        var valid = await _repository.GetValidByTokenHashAsync("validhash456", now.AddMinutes(5));

        Assert.NotNull(valid);
        Assert.Equal(token.Id, valid.Id);
    }

    [Fact]
    public async Task GetValidByTokenHashAsync_ShouldReturnNull_WhenExpired()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new MagicLinkToken("user@ludeka.es", "expiredhash", now, now.AddMinutes(15));
        await _repository.AddAsync(token);

        var valid = await _repository.GetValidByTokenHashAsync("expiredhash", now.AddMinutes(16));

        Assert.Null(valid);
    }

    [Fact]
    public async Task GetValidByTokenHashAsync_ShouldReturnNull_WhenConsumed()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new MagicLinkToken("user@ludeka.es", "consumedhash", now, now.AddMinutes(15));
        token.Consume(now.AddMinutes(2));
        await _repository.AddAsync(token);

        var valid = await _repository.GetValidByTokenHashAsync("consumedhash", now.AddMinutes(5));

        Assert.Null(valid);
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistConsumption()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new MagicLinkToken("user@ludeka.es", "consumeupdate", now, now.AddMinutes(15));
        await _repository.AddAsync(token);

        var retrieved = await _repository.GetByIdAsync(token.Id);
        Assert.NotNull(retrieved);

        var consumedTime = now.AddMinutes(4);
        retrieved.Consume(consumedTime);
        await _repository.UpdateAsync(retrieved);

        var fresh = await _repository.GetByIdAsync(token.Id);
        Assert.NotNull(fresh);
        Assert.Equal(consumedTime, fresh.ConsumedAt);
        Assert.False(fresh.IsValid(now.AddMinutes(5)));
    }
}
