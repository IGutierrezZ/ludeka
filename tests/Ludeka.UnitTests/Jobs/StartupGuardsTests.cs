using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Options;
using Ludeka.Jobs;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ludeka.UnitTests.Jobs;

/// <summary>
/// RED de las tareas 10.3/10.4 (INC-47, R6, diseño §8.6): las dos guardas de arranque de
/// <see cref="StartupGuards"/> — coherencia de proveedor y esquema al día — antes de que
/// <c>Program.cs</c> ejecute cualquier unidad de trabajo.
/// </summary>
public class StartupGuardsTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public StartupGuardsTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private ServiceProvider BuildProvider(string? databaseProvider)
    {
        var services = new ServiceCollection();
        services.AddDbContext<LudekaDbContext>(options => options.UseSqlite(_connection));
        services.Configure<DatabaseOptions>(o => o.Provider = databaseProvider);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task EvaluateAsync_EnProductionConRequirePostgreSqlYCadenaSqlite_DevuelveFalloNombrandoLaVariable()
    {
        using var provider = BuildProvider(databaseProvider: "Sqlite");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Workers:RequirePostgreSqlInProduction"] = "true"
            })
            .Build();

        var failure = await StartupGuards.EvaluateAsync(provider, configuration, environmentName: "Production");

        Assert.NotNull(failure);
        Assert.Contains("SUPABASE_DB_CONNECTION", failure);
    }

    [Fact]
    public async Task EvaluateAsync_FueraDeProduction_NoExigePostgreSql()
    {
        using var provider = BuildProvider(databaseProvider: "Sqlite");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Workers:RequirePostgreSqlInProduction"] = "true"
            })
            .Build();

        var failure = await StartupGuards.EvaluateAsync(provider, configuration, environmentName: "Development");

        Assert.Null(failure);
    }

    [Fact]
    public async Task EvaluateAsync_ConProveedorPostgreSqlYMigracionesPendientes_DevuelveFallo()
    {
        // Provider forzado a "PostgreSql" por configuración (DatabaseOptions.IsPostgreSql decide
        // primero por el Provider configurado, antes de mirar la cadena de conexión real). El
        // motor detrás sigue siendo SQLite en memoria sin ninguna migración aplicada, así que
        // GetPendingMigrationsAsync() nunca devuelve vacío: es justo el caso que esta guarda
        // debe atrapar antes de que el trabajo intente nada.
        using var provider = BuildProvider(databaseProvider: "PostgreSql");
        var configuration = new ConfigurationBuilder().Build();

        var failure = await StartupGuards.EvaluateAsync(provider, configuration, environmentName: "Development");

        Assert.NotNull(failure);
        Assert.Contains("pendiente", failure);
    }
}
