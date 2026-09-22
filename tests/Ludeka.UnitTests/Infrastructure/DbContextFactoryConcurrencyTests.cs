using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Seeding;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class DbContextFactoryConcurrencyTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<LudekaDbContext> _options;

    public DbContextFactoryConcurrencyTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var initContext = new LudekaDbContext(_options);
        initContext.Database.EnsureCreated();
        CatalogSeeder.SeedAsync(initContext).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task ConcurrentRepositoryQueries_WithDbContextFactory_ShouldSucceedWithoutConcurrencyException()
    {
        // Arrange
        var factory = new TestDbContextFactory(_options);
        var repository = new SqliteGameRepository(factory);

        // Act: Ejecución simultánea de 25 consultas asíncronas concurrentes (Task.WhenAll)
        var tasks = Enumerable.Range(0, 25).Select(async i =>
        {
            if (i % 3 == 0)
            {
                var (items, count) = await repository.SearchAsync(new GameFilterCriteria(), page: 1, pageSize: 10);
                Assert.NotEmpty(items);
            }
            else if (i % 3 == 1)
            {
                var game = await repository.GetBySlugAsync("wingspan");
                Assert.NotNull(game);
            }
            else
            {
                var hasAny = await repository.HasAnyAsync();
                Assert.True(hasAny);
            }
        });

        // Con una sola instancia de DbContext Scoped esto arrojaría InvalidOperationException de inmediato.
        // Con IDbContextFactory y ámbitos efímeros aislados, todas las operaciones completan limpiamente.
        await Task.WhenAll(tasks);
    }

    [Fact]
    public void DependencyInjection_ResolvingAllRepositories_ShouldSelectFactoryConstructorWithoutAmbiguity()
    {
        var services = new ServiceCollection();
        services.AddScoped<LudekaDbContext>(_ => new LudekaDbContext(_options));
        services.AddScoped<IDbContextFactory<LudekaDbContext>>(_ => new TestDbContextFactory(_options));

        services.AddScoped<IGameRepository, SqliteGameRepository>();
        services.AddScoped<IUserRepository, SqliteUserRepository>();
        services.AddScoped<IUserCollectionRepository, SqliteUserCollectionRepository>();
        services.AddScoped<IGameLoanRepository, SqliteGameLoanRepository>();
        services.AddScoped<IUserReviewRepository, SqliteUserReviewRepository>();
        services.AddScoped<IGamePlayLogRepository, SqliteGamePlayLogRepository>();
        services.AddScoped<IFoundingVerdictRepository, SqliteFoundingVerdictRepository>();
        services.AddScoped<IMediaRepository, SqliteMediaRepository>();
        services.AddScoped<IGiveawayRepository, SqliteGiveawayRepository>();
        services.AddScoped<IWeeklyReleaseRepository, SqliteWeeklyReleaseRepository>();
        services.AddScoped<IBoardGameEventRepository, SqliteBoardGameEventRepository>();
        services.AddScoped<IExpansionRepository, Ludeka.Infrastructure.Repositories.SqliteExpansionRepository>();
        services.AddScoped<IGamePriceRepository, SqliteGamePriceRepository>();
        services.AddScoped<IBggCatalogStagingRepository, SqliteBggCatalogStagingRepository>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IGameRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUserRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUserCollectionRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IGameLoanRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUserReviewRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IGamePlayLogRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IFoundingVerdictRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IMediaRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IGiveawayRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IWeeklyReleaseRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IBoardGameEventRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IExpansionRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IGamePriceRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IBggCatalogStagingRepository>());
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
