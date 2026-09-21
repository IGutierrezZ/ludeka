using System.Collections.Generic;
using Ludeka.Jobs;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ludeka.UnitTests.Jobs;

/// <summary>
/// RED de las tareas 10.1/10.2 (INC-47, R6, diseño §8.4): análisis de argumentos y precedencia
/// determinista de selección de trabajo (posicional → <c>--job=&lt;nombre&gt;</c> →
/// <c>Workers:JobName</c> de configuración). <see cref="JobSelectionResolver"/> es una decisión de
/// nombre/ubicación de <c>sdd-apply</c>: el diseño fija la precedencia, no la clase que la aplica.
/// </summary>
public class JobSelectionResolverTests
{
    private static IConfiguration EmptyConfiguration() => new ConfigurationBuilder().Build();

    private static IConfiguration ConfigurationWithJobName(string jobName) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Workers:JobName"] = jobName })
            .Build();

    [Theory]
    [InlineData(JobNames.NightlyCataloging)]
    [InlineData(JobNames.PriceRadar)]
    [InlineData(JobNames.SocialCollector)]
    [InlineData(JobNames.NotificationOutbox)]
    [InlineData(JobNames.SeedStaging)]
    public void Resolve_ConArgumentoPosicionalValido_DevuelveEseTrabajo(string jobName)
    {
        var result = JobSelectionResolver.Resolve([jobName], EmptyConfiguration());

        Assert.True(result.IsValid);
        Assert.Equal(jobName, result.JobName);
    }

    [Fact]
    public void Resolve_ConFlagJobValido_DevuelveEseTrabajo()
    {
        var result = JobSelectionResolver.Resolve([$"--job={JobNames.PriceRadar}"], EmptyConfiguration());

        Assert.True(result.IsValid);
        Assert.Equal(JobNames.PriceRadar, result.JobName);
    }

    [Fact]
    public void Resolve_SinArgumentos_LeeWorkersJobNameDeConfiguracion()
    {
        var configuration = ConfigurationWithJobName(JobNames.SocialCollector);

        var result = JobSelectionResolver.Resolve([], configuration);

        Assert.True(result.IsValid);
        Assert.Equal(JobNames.SocialCollector, result.JobName);
    }

    [Theory]
    [InlineData("nombre-desconocido")]
    [InlineData("")]
    [InlineData("nightly cataloging")]
    [InlineData("Nightly-Cataloging")]
    public void Resolve_ConNombreInvalido_DevuelveInvalido(string candidato)
    {
        var result = JobSelectionResolver.Resolve([candidato], EmptyConfiguration());

        Assert.False(result.IsValid);
        Assert.Null(result.JobName);
    }

    [Fact]
    public void Resolve_SinArgumentosNiConfiguracion_DevuelveInvalido()
    {
        var result = JobSelectionResolver.Resolve([], EmptyConfiguration());

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Resolve_ConPosicionalYFlagEnConflicto_ElPosicionalGana()
    {
        string[] args = [JobNames.NightlyCataloging, $"--job={JobNames.PriceRadar}"];

        var result = JobSelectionResolver.Resolve(args, EmptyConfiguration());

        Assert.True(result.IsValid);
        Assert.Equal(JobNames.NightlyCataloging, result.JobName);
    }

    [Fact]
    public void Resolve_ConFlagYConfiguracionEnConflicto_ElFlagGana()
    {
        var configuration = ConfigurationWithJobName(JobNames.SocialCollector);

        var result = JobSelectionResolver.Resolve([$"--job={JobNames.PriceRadar}"], configuration);

        Assert.True(result.IsValid);
        Assert.Equal(JobNames.PriceRadar, result.JobName);
    }
}
