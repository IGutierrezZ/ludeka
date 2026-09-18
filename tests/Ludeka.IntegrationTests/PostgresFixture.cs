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
