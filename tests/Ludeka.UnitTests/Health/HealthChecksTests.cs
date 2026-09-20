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
    public async Task DatabaseHealthCheck_ConBaseDeDatosOperativa_DebeRetornarHealthy()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(connection)
            .Options;

        using var dbContext = new LudekaDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var healthCheck = new DatabaseHealthCheck(dbContext);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("operativa", result.Description, StringComparison.OrdinalIgnoreCase);
        Assert.True((bool)result.Data["can_connect"]);
        // INC-48 (PR3), diseño D4: el proveedor DEBE leerse dinámicamente de ProviderName, nunca
        // un literal fijo — de lo contrario en Production con PostgreSQL seguiría mintiendo "Sqlite".
        Assert.Equal(dbContext.Database.ProviderName, result.Data["provider"]);
    }

    /// <summary>
    /// INC-48 (PR3), escenario «El componente `database` reporta el proveedor real» de la
    /// especificación `health-checks`, que exige verificarlo «sin PostgreSQL real».
    /// <para>
    /// Esta es la prueba que impide la regresión de verdad. La de SQLite de arriba compara
    /// <c>ProviderName</c> consigo mismo: si alguien volviera a clavar el literal
    /// <c>"Microsoft.EntityFrameworkCore.Sqlite"</c>, seguiría pasando, porque bajo SQLite ese
    /// literal coincide por casualidad con el valor real. Forzando Npgsql, un literal fijo falla.
    /// </para>
    /// <para>
    /// No hace falta servidor: <c>ProviderName</c> no abre conexión. El estado es
    /// <see cref="HealthStatus.Unhealthy"/> porque no hay PostgreSQL al otro lado, y eso es
    /// justamente lo que se quiere — el proveedor se reporta también cuando la base no responde.
    /// </para>
    /// </summary>
    [Fact]
    public async Task DatabaseHealthCheck_ConProveedorPostgreSql_ReportaNpgsqlYNuncaSqlite()
    {
        // Arrange: proveedor PostgreSQL efectivo, contra un host que no existe.
        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseNpgsql("Host=localhost;Port=1;Database=ludeka_inexistente;Username=nadie;Password=nada;Timeout=1")
            .Options;

        using var dbContext = new LudekaDbContext(options);
        var healthCheck = new DatabaseHealthCheck(dbContext);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        var provider = Assert.IsType<string>(result.Data["provider"]);
        Assert.Contains("Npgsql", provider, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sqlite", provider, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DatabaseHealthCheck_ConConexionCerrada_DebeRetornarUnhealthy()
    {
        // Arrange
        var connection = new SqliteConnection("Data Source=:memory:");
        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new LudekaDbContext(options);
        // Desechar explícitamente para forzar fallo de conexión
        dbContext.Dispose();

        var healthCheck = new DatabaseHealthCheck(dbContext);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    // INC-48 (PR3), diseño D4: StorageHealthCheck deja de parsear "Data Source=" de la cadena de
    // conexión de la base de datos (sin relación con el almacenamiento de medios) y pasa a sondear
    // la vía de medios realmente seleccionada, con la misma precedencia de tres vías que D1
    // (LudekaServiceCollectionExtensions): R2 con credenciales válidas, disco local configurado,
    // memoria. Sustituye al único escenario "StorageHealthCheck_ConDirectorioAccesible" anterior.
    [Fact]
    public async Task StorageHealthCheck_ConR2ConfiguradoConCredencialesValidas_DebeRetornarHealthyConModoR2()
    {
        // Arrange
        var r2Options = Options.Create(new CloudflareR2Options
        {
            Simulate = false,
            AccountId = "cuenta-test",
            AccessKeyId = "clave-test",
            SecretAccessKey = "secreto-test",
            BucketName = "ludeka-media"
        });
        var mediaOptions = Options.Create(new MediaOptions());

        var storageCheck = new StorageHealthCheck(r2Options, mediaOptions);
        var context = new HealthCheckContext();

        // Act
        var result = await storageCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("r2", result.Data["mode"]);
        Assert.Equal("ludeka-media", result.Data["bucket"]);
    }

    [Fact]
    public async Task StorageHealthCheck_SinR2PeroConRutaLocalConfigurada_DebeRetornarHealthyTrasEscrituraReal()
    {
        // Arrange
        var tempMediaPath = Path.Combine(Path.GetTempPath(), $"ludeka_test_storage_{Guid.NewGuid():N}");
        var r2Options = Options.Create(new CloudflareR2Options());
        var mediaOptions = Options.Create(new MediaOptions { LocalStoragePath = tempMediaPath });

        var storageCheck = new StorageHealthCheck(r2Options, mediaOptions);
        var context = new HealthCheckContext();

        try
        {
            // Act
            var result = await storageCheck.CheckHealthAsync(context);

            // Assert: la escritura es real, no simulada — mismo fichero de sonda que la versión anterior.
            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Equal("disk", result.Data["mode"]);
            Assert.True((bool)result.Data["writable"]);
        }
        finally
        {
            if (Directory.Exists(tempMediaPath))
            {
                Directory.Delete(tempMediaPath, true);
            }
        }
    }

    [Fact]
    public async Task StorageHealthCheck_SinR2NiRutaLocalConfigurada_DebeRetornarDegradedConModoMemoria()
    {
        // Arrange
        var r2Options = Options.Create(new CloudflareR2Options());
        var mediaOptions = Options.Create(new MediaOptions());

        var storageCheck = new StorageHealthCheck(r2Options, mediaOptions);
        var context = new HealthCheckContext();

        // Act
        var result = await storageCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal("memory", result.Data["mode"]);
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
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
            .AddCheck<StorageHealthCheck>("storage", tags: ["ready"])
            .AddCheck<NotificationQueueHealthCheck>("notification_queue", tags: ["ready"]);

        var provider = services.BuildServiceProvider();

        // Act
        var healthCheckService = provider.GetService<HealthCheckService>();

        // Assert
        Assert.NotNull(healthCheckService);
    }
}
