using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;

namespace Ludeka.Jobs;

/// <summary>
/// Contrato fino de un trabajo ejecutable por <c>Ludeka.Jobs</c> (INC-47, R6, diseño §8.2). Cada
/// implementación calcula su propia <c>WindowKey</c> e invoca a <see cref="IJobExecutionCoordinator"/>
/// (los tres trabajos de dominio) o a <c>INotificationOutboxDispatcher</c> (el despachador de
/// outbox) como unidad de trabajo bajo concesión de ventana.
/// </summary>
public interface IJobRunner
{
    /// <summary>Nombre del trabajo (uno de <see cref="JobNames.All"/>), usado para la selección
    /// por <c>GetServices&lt;IJobRunner&gt;().SingleOrDefault(r =&gt; r.Name == nombre)</c>.</summary>
    string Name { get; }

    /// <summary>Ejecuta la unidad de trabajo de este trabajo para la ventana actual.</summary>
    Task<JobLeaseOutcome> RunAsync(CancellationToken ct);
}
