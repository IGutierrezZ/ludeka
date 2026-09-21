using System;
using System.Collections.Generic;
using System.IO;
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
    private static IConfiguration BuildConfiguration(bool? requirePostgreSqlInProduction = null, string? adminUserEmail = null)
    {
        var values = new Dictionary<string, string?>();
        if (requirePostgreSqlInProduction.HasValue)
        {
            values["Database:RequirePostgreSqlInProduction"] = requirePostgreSqlInProduction.Value ? "true" : "false";
        }

        if (adminUserEmail is not null)
        {
            values["AdminUser:Email"] = adminUserEmail;
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

    // RED de la tarea 5.1 (INC-52, PR5, diseño D8): guarda de identidad del Administrador Fundador,
    // hermana de la guarda de coherencia de proveedor de arriba. Espera un método nuevo
    // (WebStartupGuards.EvaluateAdminUserIdentity) que todavía no existe en esta tarea.

    [Fact]
    public void EvaluateAdminUserIdentity_G1_EnProductionConCorreoInformado_NoActivaLaGuarda()
    {
        var configuration = BuildConfiguration(adminUserEmail: "fundador@ludeka.es");

        var failure = WebStartupGuards.EvaluateAdminUserIdentity(configuration, environmentName: "Production");

        Assert.Null(failure);
    }

    [Fact]
    public void EvaluateAdminUserIdentity_G2_EnProductionSinLaClave_DevuelveFalloNombrandoLaClave()
    {
        var configuration = BuildConfiguration();

        var failure = WebStartupGuards.EvaluateAdminUserIdentity(configuration, environmentName: "Production");

        Assert.NotNull(failure);
        Assert.Contains("AdminUser:Email", failure);
    }

    [Fact]
    public void EvaluateAdminUserIdentity_G3_EnProductionConSoloEspacios_DevuelveFallo()
    {
        var configuration = BuildConfiguration(adminUserEmail: "   ");

        var failure = WebStartupGuards.EvaluateAdminUserIdentity(configuration, environmentName: "Production");

        Assert.NotNull(failure);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    public void EvaluateAdminUserIdentity_G4_FueraDeProductionSinLaClave_NoActivaLaGuarda(string environmentName)
    {
        var configuration = BuildConfiguration();

        var failure = WebStartupGuards.EvaluateAdminUserIdentity(configuration, environmentName);

        Assert.Null(failure);
    }

    [Fact]
    public void EvaluateAdminUserIdentity_G5_ConEntornoProductionEnMinusculasSinLaClave_SiActivaLaGuarda()
    {
        var configuration = BuildConfiguration();

        var failure = WebStartupGuards.EvaluateAdminUserIdentity(configuration, environmentName: "production");

        Assert.NotNull(failure);
    }

    // RED→GREEN de la tarea 5.8 (INC-52, PR5, diseño D8): liga la guarda con el vaciado real de
    // appsettings.json (tarea 5.6). Técnica de tests/Ludeka.UnitTests/Web/WebAuthenticationRegistrationTests.cs:125-127
    // (read-only) — copia propia de GetRepoRoot, no clase de ayudantes compartida (misma decisión
    // que design.md para AuthorizationPipelineContractTests/CiCdWorkflowContractTests).
    [Fact]
    public void EvaluateAdminUserIdentity_G6_ConLaConfiguracionEmpaquetadaYProduction_SiActivaLaGuarda()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(GetRepoRoot(), "src", "Ludeka.Web", "appsettings.json"), optional: false)
            .Build();

        var failure = WebStartupGuards.EvaluateAdminUserIdentity(configuration, environmentName: "Production");

        Assert.NotNull(failure);
        Assert.Contains("AdminUser:Email", failure);
    }

    private static string GetRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Ludeka.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
