using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ludeka.UnitTests.Jobs;

/// <summary>
/// RED de las tareas 10.5/10.6 (INC-47, R6): "Contrato de código de salida por ejecución" y
/// "Ejecución de vida corta — una unidad de trabajo por disparo" (background-jobs-scheduling).
/// <see cref="JobHostRunner"/> es una decisión de sdd-apply: extrae de <c>Program.cs</c> la
/// selección + ejecución + mapeo a código de salida para que sea comprobable con xUnit — un
/// <c>Program.cs</c> de sentencias de nivel superior no se puede invocar desde una prueba.
/// </summary>
public class JobHostRunnerTests
{
    private sealed class FakeJobRunner : IJobRunner
    {
        private readonly Func<CancellationToken, Task<JobLeaseOutcome>> _behavior;

        public FakeJobRunner(string name, Func<CancellationToken, Task<JobLeaseOutcome>> behavior)
        {
            Name = name;
            _behavior = behavior;
        }

        public string Name { get; }
        public int CallCount { get; private set; }

        public Task<JobLeaseOutcome> RunAsync(CancellationToken ct)
        {
            CallCount++;
            return _behavior(ct);
        }
    }

    private static ServiceProvider BuildProvider(IJobRunner runner)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => runner);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task RunSelectedJobAsync_CuandoElTrabajoLanzaUnaExcepcionNoControlada_DevuelveCodigoDeSalidaDistintoDeCero()
    {
        var runner = new FakeJobRunner(JobNames.NightlyCataloging,
            _ => throw new InvalidOperationException("fallo observable simulado"));
        using var provider = BuildProvider(runner);

        var exitCode = await JobHostRunner.RunSelectedJobAsync(provider, JobNames.NightlyCataloging, CancellationToken.None);

        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public async Task RunSelectedJobAsync_ConDesenlaceFailed_DevuelveCodigoDeSalidaDistintoDeCero()
    {
        var runner = new FakeJobRunner(JobNames.NotificationOutbox, _ => Task.FromResult(JobLeaseOutcome.Failed));
        using var provider = BuildProvider(runner);

        var exitCode = await JobHostRunner.RunSelectedJobAsync(provider, JobNames.NotificationOutbox, CancellationToken.None);

        Assert.NotEqual(0, exitCode);
    }

    [Theory]
    [InlineData(JobLeaseOutcome.Completed)]
    [InlineData(JobLeaseOutcome.SkippedAlreadyCompleted)]
    [InlineData(JobLeaseOutcome.SkippedHeldByOther)]
    public async Task RunSelectedJobAsync_ConFinalizacionCorrecta_DevuelveCero(JobLeaseOutcome outcome)
    {
        var runner = new FakeJobRunner(JobNames.PriceRadar, _ => Task.FromResult(outcome));
        using var provider = BuildProvider(runner);

        var exitCode = await JobHostRunner.RunSelectedJobAsync(provider, JobNames.PriceRadar, CancellationToken.None);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunSelectedJobAsync_EjecutaElTrabajoExactamenteUnaVezYTermina()
    {
        var runner = new FakeJobRunner(JobNames.SocialCollector, _ => Task.FromResult(JobLeaseOutcome.Completed));
        using var provider = BuildProvider(runner);

        var exitCode = await JobHostRunner.RunSelectedJobAsync(provider, JobNames.SocialCollector, CancellationToken.None);

        Assert.Equal(1, runner.CallCount);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunSelectedJobAsync_ConNombreDeTrabajoNoRegistrado_DevuelveDos()
    {
        var runner = new FakeJobRunner(JobNames.PriceRadar, _ => Task.FromResult(JobLeaseOutcome.Completed));
        using var provider = BuildProvider(runner);

        var exitCode = await JobHostRunner.RunSelectedJobAsync(provider, JobNames.SocialCollector, CancellationToken.None);

        Assert.Equal(2, exitCode);
        Assert.Equal(0, runner.CallCount);
    }
}
