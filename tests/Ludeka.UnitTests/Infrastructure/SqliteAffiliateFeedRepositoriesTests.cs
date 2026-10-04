using System;
using System.Threading.Tasks;
using Ludeka.Core.Entities;
using Ludeka.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class SqliteAffiliateFeedRepositoriesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LudekaDbContext _context;
    private readonly SqliteAffiliateFeedSourceRepository _feedRepo;
    private readonly SqliteAffiliateEanDiscrepancyRepository _discrepancyRepo;

    public SqliteAffiliateFeedRepositoriesTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LudekaDbContext(options);
        _context.Database.EnsureCreated();

        _feedRepo = new SqliteAffiliateFeedSourceRepository(_context);
        _discrepancyRepo = new SqliteAffiliateEanDiscrepancyRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task AffiliateFeedSourceRepository_CrudOperations_WorkCorrectly()
    {
        var source = new AffiliateFeedSource(
            storeName: "Zacatrus",
            feedUrl: "https://zacatrus.es/feeds/google.xml",
            format: FeedFormat.GoogleShoppingXml,
            affiliateTag: "ludeka");

        await _feedRepo.AddAsync(source);

        var retrieved = await _feedRepo.GetByIdAsync(source.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Zacatrus", retrieved.StoreName);

        var byName = await _feedRepo.GetByStoreNameAsync("Zacatrus");
        Assert.NotNull(byName);
        Assert.Equal(source.Id, byName.Id);

        var activeSources = await _feedRepo.GetActiveSourcesAsync();
        Assert.Single(activeSources);

        source.IsEnabled = false;
        await _feedRepo.UpdateAsync(source);

        var activeAfterDisable = await _feedRepo.GetActiveSourcesAsync();
        Assert.Empty(activeAfterDisable);

        await _feedRepo.DeleteAsync(source.Id);
        var afterDelete = await _feedRepo.GetByIdAsync(source.Id);
        Assert.Null(afterDelete);
    }

    [Fact]
    public async Task AffiliateEanDiscrepancyRepository_CrudAndQuery_WorkCorrectly()
    {
        var gameId = Guid.NewGuid();
        var discrepancy = new AffiliateEanDiscrepancyLog(
            gameId: gameId,
            gameTitle: "Wingspan",
            gameSlug: "wingspan",
            currentEan: "0012345678905",
            feedEan: "8435407629616",
            storeName: "Zacatrus");

        await _discrepancyRepo.AddAsync(discrepancy);

        var pending = await _discrepancyRepo.GetPendingDiscrepanciesAsync();
        Assert.Single(pending);
        Assert.Equal(gameId, pending[0].GameId);

        var existing = await _discrepancyRepo.FindExistingPendingAsync(gameId, "8435407629616", "Zacatrus");
        Assert.NotNull(existing);

        discrepancy.MarkResolved("Promovido a principal por admin");
        await _discrepancyRepo.UpdateAsync(discrepancy);

        var pendingAfterResolved = await _discrepancyRepo.GetPendingDiscrepanciesAsync();
        Assert.Empty(pendingAfterResolved);
    }
}
