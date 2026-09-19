using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ludeka.Jobs;

/// <summary>
/// Selecciona el <see cref="IJobRunner"/> pedido, lo ejecuta exactamente una vez y traduce el
/// desenlace al contrato de código de salida del diseño §8.5 (INC-47, R6). Extraído de
/// <c>Program.cs</c> (decisión de <c>sdd-apply</c>) porque un <c>Program.cs</c> de sentencias de
/// nivel superior no se puede invocar desde una prueba: las tareas 10.5/10.6 se comprueban aquí.
/// NUNCA reentra en ningún bucle de sondeo — invoca <c>RunAsync</c> una sola vez y devuelve.
/// </summary>
public static class JobHostRunner
{
    public static async Task<int> RunSelectedJobAsync(IServiceProvider services, string jobName, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(jobName);

        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetService<ILoggerFactory>()?.CreateLogger("Ludeka.Jobs.JobHostRunner");

        var runner = sp.GetServices<IJobRunner>().SingleOrDefault(r => r.Name == jobName);
        if (runner is null)
        {
            Console.Error.WriteLine(
                $"Trabajo desconocido: '{jobName}'. Nombres válidos: {string.Join(", ", JobNames.All)}");
            return 2;
        }

        try
        {
            var outcome = await runner.RunAsync(ct);
            return outcome == JobLeaseOutcome.Failed ? 1 : 0;
        }
        catch (Exception ex)
        {
            // "Fallo observable" (background-jobs-scheduling) incluye tanto una excepción no
            // controlada del propio trabajo como el agotamiento de JobTimeoutMinutes o SIGTERM
            // (que se materializan como OperationCanceledException, diseño §8.3/§8.5): ambos casos
            // deben terminar el proceso con código de salida distinto de cero, nunca con una
            // traza sin controlar.
            logger?.LogError(ex, "Fallo observable durante la unidad de trabajo de '{JobName}'.", jobName);
            return 1;
        }
    }
}
