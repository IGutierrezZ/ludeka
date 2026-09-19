using System.Collections.Generic;
using Ludeka.Infrastructure.Options;
using Ludeka.Web;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// RED de la tarea 3.1 (INC-48, PR2, diseño D3): guarda de coherencia de proveedor del host web,
/// espejo de la Guarda 1 de <c>Ludeka.Jobs.StartupGuards</c>
/// (<c>tests/Ludeka.UnitTests/Jobs/StartupGuardsTests.cs</c>, solo lectura). A diferencia de esa
/// guarda, <see cref="WebStartupGuards.Evaluate"/> recibe <see cref="DatabaseOptions"/>
/// directamente en vez de un <c>IServiceProvider</c>: es una función pura y no hace falta
/// SQLite en memoria ni composición de servicios para probarla.
/// </summary>
public class WebStartupGuardsTests
{
    private static IConfiguration BuildConfiguration(bool? requirePostgreSqlInProduction = null)
    {
        var values = new Dictionary<string, string?>();
        if (requirePostgreSqlInProduction.HasValue)
        {
            values["Database:RequirePostgreSqlInProduction"] = requirePostgreSqlInProduction.Value ? "true" : "false";
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void Evaluate_EnProductionConPostgreSqlResoluble_NoActivaLaGuarda()
    {
        var configuration = BuildConfiguration(requirePostgreSqlInProduction: true);
        var databaseOptions = new DatabaseOptions { Provider = "PostgreSql" };

        var failure = WebStartupGuards.Evaluate(configuration, databaseOptions, environmentName: "Production");

        Assert.Null(failure);
    }

    [Fact]
    public void Evaluate_EnProductionConSqliteOAusente_DevuelveFalloNombrandoLaConexion()
    {
        var configuration = BuildConfiguration(requirePostgreSqlInProduction: true);
        var databaseOptions = new DatabaseOptions { Provider = "Sqlite" };

        var failure = WebStartupGuards.Evaluate(configuration, databaseOptions, environmentName: "Production");

        Assert.NotNull(failure);
        Assert.Contains("SUPABASE_DB_CONNECTION", failure);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    public void Evaluate_FueraDeProduction_NoActivaLaGuardaAunqueSeaSqlite(string environmentName)
    {
        var configuration = BuildConfiguration(requirePostgreSqlInProduction: true);
        var databaseOptions = new DatabaseOptions { Provider = "Sqlite" };

        var failure = WebStartupGuards.Evaluate(configuration, databaseOptions, environmentName);

        Assert.Null(failure);
    }

    [Fact]
    public void Evaluate_ConEntornoNulo_NoActivaLaGuarda()
    {
        var configuration = BuildConfiguration(requirePostgreSqlInProduction: true);
        var databaseOptions = new DatabaseOptions { Provider = "Sqlite" };

        var failure = WebStartupGuards.Evaluate(configuration, databaseOptions, environmentName: null);

        Assert.Null(failure);
    }

    [Fact]
    public void Evaluate_ConEntornoProductionEnMinusculas_SiActivaLaGuarda()
    {
        var configuration = BuildConfiguration(requirePostgreSqlInProduction: true);
        var databaseOptions = new DatabaseOptions { Provider = "Sqlite" };

        var failure = WebStartupGuards.Evaluate(configuration, databaseOptions, environmentName: "production");

        Assert.NotNull(failure);
        Assert.Contains("SUPABASE_DB_CONNECTION", failure);
    }

    [Fact]
    public void Evaluate_ConLaConfiguracionEfectivaDeDockerComposeYml_NoActivaLaGuarda()
    {
        // Réplica de tasks.md 3.6 (spec docker-compose-orchestration): tras este cambio,
        // docker-compose.yml pasa a ASPNETCORE_ENVIRONMENT=Staging por defecto (ya no Production)
        // manteniendo su ConnectionStrings__DefaultConnection fijo a SQLite. Sin sobrescribir
        // Database:RequirePostgreSqlInProduction (el defecto de la guarda es true), ese entorno
        // local debe seguir arrancando sin abortar.
        var configuration = BuildConfiguration();
        var databaseOptions = new DatabaseOptions { Provider = "Sqlite" };

        var failure = WebStartupGuards.Evaluate(configuration, databaseOptions, environmentName: "Staging");

        Assert.Null(failure);
    }
}
