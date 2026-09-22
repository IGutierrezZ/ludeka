using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Core.Entities;
using Ludeka.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class SqliteBggCatalogStagingRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<LudekaDbContext> _options;
    private readonly TestDbContextFactory _factory;
    private readonly SqliteBggCatalogStagingRepository _repository;

    public SqliteBggCatalogStagingRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var initContext = new LudekaDbContext(_options);
        initContext.Database.EnsureCreated();

        _factory = new TestDbContextFactory(_options);
        _repository = new SqliteBggCatalogStagingRepository(_factory);
    }

    [Fact]
    public async Task UpsertBatchAsync_WithDuplicatesInSameBatch_ShouldDeduplicateAndNotThrowTrackingException()
    {
        // Arrange: un lote que contiene múltiples elementos con la misma clave BggId
        var items = new List<BggCatalogStagingItem>
        {
            new(174430, "Gloomhaven", 2017, 1, 60000, 8.4, 8.6),
            new(174430, "Gloomhaven (Updated)", 2017, 1, 62000, 8.42, 8.61),
            new(224517, "Brass: Birmingham", 2018, 2, 48000, 8.41, 8.60)
        };

        // Act: debe ejecutarse sin InvalidOperationException de EF Core
        await _repository.UpsertBatchAsync(items);

        // Assert
        int count = await _repository.GetTotalCountAsync();
        Assert.Equal(2, count);

        var item = await _repository.GetByBggIdAsync(174430);
        Assert.NotNull(item);
        Assert.Equal(62000, item.UsersRated);
        Assert.Equal(8.42, item.BayesAverage);
    }

    [Fact]
    public async Task UpsertBatchAsync_MultipleConsecutiveBatches_WithOverlappingKeys_ShouldUpdateWithoutTrackingException()
    {
        // Arrange
        var batch1 = new List<BggCatalogStagingItem>
        {
            new(100, "Juego 100", 2020, 10, 2000, 7.5, 7.8),
            new(200, "Juego 200", 2021, 20, 1500, 7.2, 7.4)
        };

        var batch2 = new List<BggCatalogStagingItem>
        {
            new(200, "Juego 200 Modificado", 2021, 18, 1900, 7.3, 7.5),
            new(300, "Juego 300", 2022, 30, 3000, 8.0, 8.1)
        };

        // Act
        await _repository.UpsertBatchAsync(batch1);
        await _repository.UpsertBatchAsync(batch2);

        // Assert
        int count = await _repository.GetTotalCountAsync();
        Assert.Equal(3, count);

        var item200 = await _repository.GetByBggIdAsync(200);
        Assert.NotNull(item200);
        Assert.Equal(1900, item200.UsersRated);
        Assert.Equal(18, item200.BggRank);
    }

    [Fact]
    public async Task ClearStagingAsync_FollowedBy_UpsertBatchAsync_ShouldInsertCleanlyWithoutTrackingException()
    {
        // Arrange: Sembrar registros iniciales
        var initialItems = new List<BggCatalogStagingItem>
        {
            new(501, "Catan", 1995, 50, 120000, 7.1, 7.2),
            new(502, "Carcassonne", 2000, 60, 115000, 7.3, 7.4)
        };
        await _repository.UpsertBatchAsync(initialItems);

        // Act 1: Vaciar staging
        await _repository.ClearStagingAsync();
        int countAfterClear = await _repository.GetTotalCountAsync();
        Assert.Equal(0, countAfterClear);

        // Act 2: Re-insertar inmediatamente los mismos BggIds (escenario reproducido en pantalla de admin)
        var newItems = new List<BggCatalogStagingItem>
        {
            new(501, "Catan Nuevo", 1995, 48, 121000, 7.12, 7.22),
            new(502, "Carcassonne Nuevo", 2000, 58, 116000, 7.31, 7.41)
        };
        await _repository.UpsertBatchAsync(newItems);

        // Assert
        int finalCount = await _repository.GetTotalCountAsync();
        Assert.Equal(2, finalCount);

        var item501 = await _repository.GetByBggIdAsync(501);
        Assert.NotNull(item501);
        Assert.Equal(121000, item501.UsersRated);
    }

    [Fact]
    public async Task GetMetricsAsync_ShouldReturnAccurateCounts()
    {
        // Arrange
        var items = new List<BggCatalogStagingItem>
        {
            new(1, "Game 1", 2020, 1, 5000, 8.0, 8.0),
            new(2, "Game 2", 2021, 2, 4000, 7.9, 7.9)
        };
        await _repository.UpsertBatchAsync(items);

        // Act
        var metrics = await _repository.GetMetricsAsync();

        // Assert
        Assert.Equal(2, metrics.TotalInStaging);
        Assert.Equal(2, metrics.PendingFetchCount);
        Assert.Equal(0, metrics.FetchedCount);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private sealed class TestDbContextFactory : IDbContextFactory<LudekaDbContext>
    {
        private readonly DbContextOptions<LudekaDbContext> _options;

        public TestDbContextFactory(DbContextOptions<LudekaDbContext> options)
        {
            _options = options;
        }

        public LudekaDbContext CreateDbContext() => new(_options);

        public Task<LudekaDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new LudekaDbContext(_options));
    }
}
