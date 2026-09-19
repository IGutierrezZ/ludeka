using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Único punto de la aplicación que ejecuta la unidad de trabajo de un trabajo programado bajo
/// idempotencia por ventana temporal (INC-47, R5, diseño §7.4, decisión D4). Los cuatro
/// <c>BackgroundService</c> de <c>Ludeka.Infrastructure</c> (y, desde R6, los cuatro
/// <em>runners</em> de <c>Ludeka.Jobs</c>) quedan finos: calculan su <c>WindowKey</c> con
/// <see cref="Ludeka.Application.Features.Jobs.JobWindowKeyCalculator"/> e invocan a este
/// coordinador, que decide si la ventana ya está resuelta antes de ejecutar nada.
/// </summary>
public interface IJobExecutionCoordinator
{
    /// <summary>Adquiere la concesión de <paramref name="windowKey"/> para
    /// <paramref name="jobName"/> y, solo si la adquiere, ejecuta <paramref name="work"/> y
    /// persiste el resultado. Nunca ejecuta <paramref name="work"/> dos veces para la misma
    /// ventana (diseño §7.3, restricción <c>UNIQUE (JobName, WindowKey)</c>).</summary>
    Task<JobLeaseOutcome> ExecuteWithWindowLeaseAsync(
        string jobName,
        string windowKey,
        Func<IJobHeartbeat, CancellationToken, Task<JobWorkResult>> work,
        CancellationToken ct = default);
}

/// <summary>Desenlace de un intento de ejecución bajo concesión de ventana (diseño §7.4).</summary>
public enum JobLeaseOutcome
{
    /// <summary>La ventana se adquirió, la unidad de trabajo se ejecutó sin lanzar y el
    /// resultado (con o sin fallos parciales) quedó persistido como terminal.</summary>
    Completed,

    /// <summary>La ventana se adquirió pero la unidad de trabajo lanzó una excepción no
    /// controlada; la concesión queda en estado <c>Failed</c>, retomable por una ejecución
    /// posterior (diseño §7.3).</summary>
    Failed,

    /// <summary>La ventana ya estaba marcada como completada por una ejecución anterior; no se
    /// invocó <paramref name="work"/> ninguna vez.</summary>
    SkippedAlreadyCompleted,

    /// <summary>Otra ejecución tiene la ventana (viva, o se adelantó en la toma de control de
    /// una concesión huérfana); no se invocó <paramref name="work"/> ninguna vez.</summary>
    SkippedHeldByOther
}

/// <summary>Resultado de una unidad de trabajo ejecutada bajo concesión de ventana (diseño §7.4).
/// <paramref name="Failed"/> cuenta fallos parciales dentro de un lote (p. ej. algunos elementos
/// no se pudieron catalogar); no implica que la ejecución en sí haya lanzado una excepción.</summary>
public sealed record JobWorkResult(int Processed, int Failed, string? Message);

/// <summary>Latido de una ejecución en curso (diseño §7.3): se llama en las fronteras de fase de
/// la propia unidad de trabajo, nunca desde un temporizador en un hilo aparte.</summary>
public interface IJobHeartbeat
{
    Task BeatAsync(CancellationToken ct = default);
}
