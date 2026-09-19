using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Jobs;

namespace Ludeka.Jobs.Runners;

/// <summary>
/// Trabajo fino del despachador del outbox de notificaciones (INC-47, R6, diseño §8.1/§8.2/§7.2).
/// No es un trabajo con ventana temporal de negocio: es un drenaje, con ventana de un segundo
/// (diseño §7.2, "por qué el outbox tiene ventana de un segundo y no de cinco minutos") — la misma
/// mecánica de concesión que los otros tres trabajos, para que la restricción <c>UNIQUE</c> se
/// aplique de forma uniforme a los cuatro sin ningún caso especial en el código.
/// </summary>
public sealed class NotificationOutboxJobRunner : IJobRunner
{
    private readonly IJobExecutionCoordinator _coordinator;
    private readonly INotificationOutboxDispatcher _dispatcher;

    public NotificationOutboxJobRunner(IJobExecutionCoordinator coordinator, INotificationOutboxDispatcher dispatcher)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public string Name => JobNames.NotificationOutbox;

    public Task<JobLeaseOutcome> RunAsync(CancellationToken ct)
    {
        var windowKey = JobWindowKeyCalculator.PerSecond(DateTimeOffset.UtcNow);

        return _coordinator.ExecuteWithWindowLeaseAsync(
            Name,
            windowKey,
            async (_, workCt) =>
            {
                // Decisión de sdd-apply (el diseño no fija esta correspondencia exacta):
                // Processed = CompletedCount (entregados con éxito), Failed = DeadCount (agotados,
                // estado terminal). RetriedCount no cuenta como fallo de ESTE intento: el mensaje
                // sigue vivo, reprogramado para un intento futuro (diseño §6.5/§7.4).
                var result = await _dispatcher.DispatchPendingAsync(workCt);
                return new JobWorkResult(result.CompletedCount, result.DeadCount, null);
            },
            ct);
    }
}
