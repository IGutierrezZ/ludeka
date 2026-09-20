using System;
using System.Threading.Tasks;
using Testcontainers.PostgreSql;
using Xunit;

namespace Ludeka.IntegrationTests;

/// <summary>
/// Arranca un contenedor PostgreSQL real (Testcontainers) compartido por toda la colección
/// <c>postgres-real</c> (INC-47, diseño §D6). Si el arranque falla, nunca lo traga: captura el
/// diagnóstico en <see cref="StartupFailure"/> y cada prueba afectada lo relanza al llamar a
/// <see cref="EnsureAvailable"/>, para que la suite falle en rojo con un mensaje accionable en
/// lugar de reportar un error de colección difícil de atribuir, y nunca omitir la prueba en
/// silencio (especificación <c>postgres-integration-testing</c>).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    /// <summary>
    /// La excepción capturada al intentar construir o arrancar el contenedor, o
    /// <see langword="null"/> si arrancó correctamente.
    /// </summary>
    public Exception? StartupFailure { get; private set; }

    /// <summary>
    /// Cadena de conexión al contenedor real. Solo válida cuando <see cref="StartupFailure"/> es
    /// <see langword="null"/>; las pruebas deben llamar primero a <see cref="EnsureAvailable"/>.
    /// </summary>
    public string ConnectionString => _container!.GetConnectionString();

    public async Task InitializeAsync()
    {
        try
        {
            // La propia construcción (Build) ya valida la disponibilidad de Docker de forma
            // eager (lanza DockerUnavailableException antes de intentar arrancar nada), así que
            // debe quedar DENTRO de este try junto con StartAsync: si Build() se quedara fuera,
            // el fallo escaparía sin pasar por ContainerCapabilityGuard.
            _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
            await _container.StartAsync();
        }
        catch (Exception failure)
        {
            StartupFailure = failure;
        }
    }

    public async Task DisposeAsync()
    {
        if (StartupFailure is null && _container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// Relanza el fallo de arranque capturado, vía <see cref="ContainerCapabilityGuard.Evaluate"/>,
    /// para que la prueba que la invoca aparezca en rojo con un mensaje accionable. No hace nada
    /// cuando el contenedor arrancó correctamente.
    /// </summary>
    public void EnsureAvailable()
    {
        if (StartupFailure is not null)
        {
            throw ContainerCapabilityGuard.Evaluate(StartupFailure, IsRunningOnCi());
        }
    }

    private static bool IsRunningOnCi() =>
        Environment.GetEnvironmentVariable("CI") is not null ||
        Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is not null;
}

/// <summary>
/// Agrupa las pruebas que ejercitan primitivas reales de PostgreSQL bajo un único contenedor
/// compartido (INC-47, diseño §D6). Vive en un ensamblado propio (<c>Ludeka.IntegrationTests</c>)
/// para que su política de fallo ruidoso no arrastre a las pruebas unitarias sobre SQLite.
/// </summary>
[CollectionDefinition("postgres-real")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
}

/// <summary>
/// Colección independiente para pruebas de migración que, igual que
/// <see cref="NotificationOutboxMigrationTests"/>, aplican <c>MigrateAsync</c> hasta un punto
/// histórico concreto antes de migrar hacia delante (INC-47, R3b). xUnit no garantiza el orden
/// de ejecución entre clases de una misma colección: si esta prueba compartiera "postgres-real"
/// con <see cref="NotificationOutboxMigrationTests"/>, una de las dos podría migrar HACIA ATRÁS
/// el estado que la otra necesita, según qué clase corra primero. Un contenedor propio (misma
/// clase <see cref="PostgresFixture"/>, instancia independiente por colección) elimina el riesgo
/// de raíz sin modificar el fixture existente ni la colección "postgres-real".
/// </summary>
[CollectionDefinition("postgres-real-job-leases")]
public sealed class PostgresJobLeasesCollection : ICollectionFixture<PostgresFixture>
{
}

/// <summary>
/// Colección independiente para <see cref="NotificationOutboxClaimConcurrencyTests"/> (INC-47,
/// R4a, diseño §6.3). El motivo es el mismo que el de <see cref="PostgresJobLeasesCollection"/>:
/// la colección "postgres-real" ya aloja <see cref="NotificationOutboxMigrationTests"/>, que
/// migra hasta un punto histórico concreto antes de migrar hacia delante, y xUnit no garantiza
/// el orden de ejecución entre clases de una misma colección. Compartir contenedor arriesgaría
/// que la migración parcial de esa prueba dejase el esquema del outbox sin crear justo cuando
/// esta prueba necesita reclamar filas reales. Un contenedor propio (misma clase
/// <see cref="PostgresFixture"/>, instancia independiente por colección) elimina el riesgo sin
/// modificar el fixture existente ni ninguna de las otras dos colecciones.
/// </summary>
[CollectionDefinition("postgres-real-outbox-claim")]
public sealed class PostgresOutboxClaimCollection : ICollectionFixture<PostgresFixture>
{
}

/// <summary>
/// Colección independiente para <see cref="JobExecutionCoordinatorConcurrencyTests"/> (INC-47,
/// R5, diseño §7, tasks.md 9.1/9.2). Mismo motivo que <see cref="PostgresJobLeasesCollection"/> y
/// <see cref="PostgresOutboxClaimCollection"/>: aislar en su propio contenedor una prueba que
/// ejercita concurrencia real y aserciones de fila exactas sobre <c>JobExecutionLeases</c>, sin
/// arriesgar interferencia de estado con otras clases de "postgres-real" ni con
/// <see cref="JobExecutionLeaseMigrationTests"/> (que migra hasta un punto histórico concreto).
/// Un contenedor propio elimina el riesgo sin modificar el fixture existente ni ninguna otra
/// colección.
/// </summary>
[CollectionDefinition("postgres-real-job-coordinator")]
public sealed class PostgresJobCoordinatorCollection : ICollectionFixture<PostgresFixture>
{
}

/// <summary>
/// Colección independiente para <c>PostgresSchemaVerificationTests</c> (INC-48, R-esquema, diseño
/// §D5, especificación <c>postgres-schema-verification</c>). Motivo distinto al de las otras tres
/// colecciones adicionales: esa prueba no migra hasta un punto histórico, sino que migra el
/// historial COMPLETO desde una base vacía y asevera recuentos EXACTOS (tablas totales, filas de
/// <c>__EFMigrationsHistory</c>). Compartir contenedor con "postgres-real" — que ya aloja pruebas
/// que migran a un punto histórico concreto o insertan filas propias — invalidaría esos recuentos
/// de forma intermitente, porque xUnit no garantiza el orden de ejecución entre clases de una
/// misma colección. Un contenedor propio (misma clase <see cref="PostgresFixture"/>, instancia
/// independiente) elimina el riesgo sin modificar el fixture existente ni ninguna otra colección.
/// </summary>
[CollectionDefinition("postgres-real-schema")]
public sealed class PostgresSchemaCollection : ICollectionFixture<PostgresFixture>
{
}
