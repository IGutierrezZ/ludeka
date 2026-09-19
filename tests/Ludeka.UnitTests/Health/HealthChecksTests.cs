using Ludeka.Application.Contracts;
using Ludeka.Application.Options;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Repositories;
using Ludeka.Web.Health;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Ludeka.UnitTests.Health;

public class HealthChecksTests
{
    /// <summary>Doble de prueba de <see cref="IOptionsMonitor{TOptions}"/> (INC-47, R4c):
    /// el chequeo de salud del outbox lee los umbrales de <see cref="OutboxOptions"/> a través
    /// de esta interfaz, no de <see cref="IOptions{TOptions}"/>. Mismo patrón que
    /// <c>InstagramFeedCollectorTests.TestOptionsMonitor</c>.</summary>
    private class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public TestOptionsMonitor(T value) => CurrentValue = value;
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private static async Task<(SqliteConnection Connection, LudekaDbContext DbContext, NotificationOutboxRepository Repository)> CreateOutboxAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new LudekaDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        return (connection, dbContext, new NotificationOutboxRepository(dbContext));
    }

    private static NotificationOutboxMessage NewMessage(string title = "Ping de prueba") =>
        new(NotificationEventType.CustomTestPing, title, "Resumen del ping de prueba");

    [Fact]
    public async Task NotificationQueueHealthCheck_ConOutboxPorDebajoDeLosUmbrales_DebeRetornarHealthyConDatosReales()
    {
        // Arrange
        var (connection, dbContext, repository) = await CreateOutboxAsync();
        using var _ = connection;
        using var __ = dbContext;
        await repository.EnqueueAsync(NewMessage());

        var healthCheck = new NotificationQueueHealthCheck(repository, new TestOptionsMonitor<OutboxOptions>(new OutboxOptions()));
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(1, result.Data["pending_count"]);
        Assert.Equal(0, result.Data["dead_count"]);
        Assert.Equal("SQLite", result.Data["provider"]);
        Assert.True((double)result.Data["oldest_pending_age_seconds"] >= 0);
        Assert.False(result.Data.ContainsKey("queue_type"));
        Assert.False(result.Data.ContainsKey("operational"));
    }

    [Fact]
    public async Task NotificationQueueHealthCheck_ConProfundidadPendientePorEncimaDelUmbral_DebeRetornarDegraded()
    {
        // Arrange
        var (connection, dbContext, repository) = await CreateOutboxAsync();
        using var _ = connection;
        using var __ = dbContext;
        await repository.EnqueueAsync(NewMessage("Ping 1"));
        await repository.EnqueueAsync(NewMessage("Ping 2"));

        var lowThreshold = new OutboxOptions { HealthPendingDepthDegraded = 1 };
        var healthCheck = new NotificationQueueHealthCheck(repository, new TestOptionsMonitor<OutboxOptions>(lowThreshold));
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert: nunca Unhealthy por profundidad (diseño §10.1).
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal(2, result.Data["pending_count"]);
    }

    [Fact]
    public async Task NotificationQueueHealthCheck_ConAntiguedadDelPendienteMasAntiguoPorEncimaDelUmbral_DebeRetornarDegraded()
    {
        // Arrange
        var (connection, dbContext, repository) = await CreateOutboxAsync();
        using var _ = connection;
        using var __ = dbContext;
        var message = NewMessage("Ping antiguo");
        await repository.EnqueueAsync(message);

        // El setter de CreatedAt es privado (dominio inmutable): se retrocede vía el rastreador
        // de cambios de EF Core, igual que exige cualquier prueba de antigüedad sobre esta entidad.
        dbContext.Entry(message).Property(nameof(NotificationOutboxMessage.CreatedAt)).CurrentValue =
            DateTimeOffset.UtcNow.AddMinutes(-5);
        await dbContext.SaveChangesAsync();

        var lowThreshold = new OutboxOptions { HealthOldestPendingDegradedMinutes = 1 };
        var healthCheck = new NotificationQueueHealthCheck(repository, new TestOptionsMonitor<OutboxOptions>(lowThreshold));
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert: nunca Unhealthy por antigüedad (diseño §10.1).
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.True((double)result.Data["oldest_pending_age_seconds"] >= 250);
    }

    [Fact]
    public async Task NotificationQueueHealthCheck_ConMensajesAgotados_DebeRetornarDegraded()
    {
        // Arrange
        var (connection, dbContext, repository) = await CreateOutboxAsync();
        using var _ = connection;
        using var __ = dbContext;
        var message = NewMessage("Ping agotado");
        await repository.EnqueueAsync(message);
        await repository.MarkMessageDeadAsync(message.Id, "Se agotó MaxClaimAttempts");

        var healthCheck = new NotificationQueueHealthCheck(repository, new TestOptionsMonitor<OutboxOptions>(new OutboxOptions()));
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert: un solo mensaje muerto ya degrada, sin umbral configurable (diseño §10.1).
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal(1, result.Data["dead_count"]);
        Assert.Equal(0, result.Data["pending_count"]);
    }

    [Fact]
    public async Task SqliteDatabaseHealthCheck_ConBaseDeDatosOperativa_DebeRetornarHealthy()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(connection)
            .Options;

        using var dbContext = new LudekaDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var healthCheck = new SqliteDatabaseHealthCheck(dbContext);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("operativa", result.Description, StringComparison.OrdinalIgnoreCase);
        Assert.True((bool)result.Data["can_connect"]);
    }

    [Fact]
    public async Task SqliteDatabaseHealthCheck_ConConexionCerrada_DebeRetornarUnhealthy()
    {
        // Arrange
        var connection = new SqliteConnection("Data Source=:memory:");
        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new LudekaDbContext(options);
        // Desechar explícitamente para forzar fallo de conexión
        dbContext.Dispose();

        var healthCheck = new SqliteDatabaseHealthCheck(dbContext);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public async Task StorageHealthCheck_ConDirectorioAccesible_DebeRetornarHealthy()
    {
        // Arrange
        var tempDbPath = Path.Combine(Path.GetTempPath(), "ludeka_test_storage", "test.db");
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "ConnectionStrings:DefaultConnection", $"Data Source={tempDbPath}" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var storageCheck = new StorageHealthCheck(configuration);
        var context = new HealthCheckContext();

        // Act
        var result = await storageCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.True((bool)result.Data["writable"]);

        // Cleanup
        var dir = Path.GetDirectoryName(tempDbPath);
        if (dir != null && Directory.Exists(dir))
        {
            Directory.Delete(dir, true);
        }
    }

    // Nota de decisión de sdd-apply (INC-47, R4c, impacto conocido, diseño §10.2): la prueba
    // "ConColaOperativa" original (escenario "Healthy" con el chequeo antiguo) queda consolidada
    // en `NotificationQueueHealthCheck_ConOutboxPorDebajoDeLosUmbrales_DebeRetornarHealthyConDatosReales`
    // de más arriba, que cubre el mismo escenario "Healthy" con datos reales del outbox en vez de
    // `queue_type`/`operational` — mantener las dos habría duplicado el mismo caso.
    [Fact]
    public async Task NotificationQueueHealthCheck_ConRepositorioQueLanzaExcepcion_DebeRetornarUnhealthy()
    {
        // Arrange: el repositorio nulo fuerza NullReferenceException al leer la muestra de
        // salud, simulando un outbox ilegible (diseño §10.1: "la consulta lanza → Unhealthy").
        var healthCheck = new NotificationQueueHealthCheck(null!, new TestOptionsMonitor<OutboxOptions>(new OutboxOptions()));
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public void DependencyInjection_DebeRegistrarHealthChecks_ConEtiquetasReady()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<LudekaDbContext>(options => options.UseSqlite("Data Source=:memory:"));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.Configure<OutboxOptions>(_ => { });
        services.AddScoped<INotificationOutboxRepository, NotificationOutboxRepository>();

        services.AddHealthChecks()
            .AddCheck<SqliteDatabaseHealthCheck>("sqlite_db", tags: ["ready"])
            .AddCheck<StorageHealthCheck>("storage", tags: ["ready"])
            .AddCheck<NotificationQueueHealthCheck>("notification_queue", tags: ["ready"]);

        var provider = services.BuildServiceProvider();

        // Act
        var healthCheckService = provider.GetService<HealthCheckService>();

        // Assert
        Assert.NotNull(healthCheckService);
    }
}
