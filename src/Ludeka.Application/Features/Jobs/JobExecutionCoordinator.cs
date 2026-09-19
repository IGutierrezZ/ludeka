using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Features.Jobs;

/// <summary>
/// Implementación única de <see cref="IJobExecutionCoordinator"/> (INC-47, R5, diseño §7.4,
/// decisión D4): "un solo sitio con esta lógica". Un fallo parcial dentro del lote (algunos
/// elementos no se procesan) es un desenlace <see cref="JobLeaseOutcome.Completed"/> — la
/// ventana no se retoma; solo una excepción no controlada durante <c>work</c> marca la concesión
/// como <see cref="JobLeaseOutcome.Failed"/> y la deja retomable (diseño §7.3).
/// </summary>
public class JobExecutionCoordinator : IJobExecutionCoordinator
{
    private readonly IJobExecutionLeaseRepository _leaseRepository;

    public JobExecutionCoordinator(IJobExecutionLeaseRepository leaseRepository)
    {
        _leaseRepository = leaseRepository ?? throw new ArgumentNullException(nameof(leaseRepository));
    }

    public async Task<JobLeaseOutcome> ExecuteWithWindowLeaseAsync(
        string jobName,
        string windowKey,
        Func<IJobHeartbeat, CancellationToken, Task<JobWorkResult>> work,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        // Identificador de instancia (diseño §7.3, columna HostIdentifier): mismo patrón que
        // NotificationOutboxDispatcher.DispatchPendingAsync ya usa para "claimedBy".
        var hostIdentifier = $"{Environment.MachineName}:{Environment.ProcessId}";
        var acquisition = await _leaseRepository.TryAcquireAsync(jobName, windowKey, hostIdentifier, ct);

        if (acquisition.Outcome == LeaseAcquisitionOutcome.AlreadyCompleted)
        {
            return JobLeaseOutcome.SkippedAlreadyCompleted;
        }

        if (acquisition.Outcome == LeaseAcquisitionOutcome.HeldByOther)
        {
            return JobLeaseOutcome.SkippedHeldByOther;
        }

        var leaseId = acquisition.LeaseId!.Value;
        var heartbeat = new LeaseHeartbeat(_leaseRepository, leaseId);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await work(heartbeat, ct);
            stopwatch.Stop();
            await _leaseRepository.MarkCompletedAsync(
                leaseId, result.Processed, result.Failed, stopwatch.ElapsedMilliseconds, ct);
            return JobLeaseOutcome.Completed;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            stopwatch.Stop();
            await _leaseRepository.MarkFailedAsync(
                leaseId, ex.Message, processedCount: 0, failedCount: 0, stopwatch.ElapsedMilliseconds, ct);
            return JobLeaseOutcome.Failed;
        }
    }

    private sealed class LeaseHeartbeat : IJobHeartbeat
    {
        private readonly IJobExecutionLeaseRepository _repository;
        private readonly Guid _leaseId;

        public LeaseHeartbeat(IJobExecutionLeaseRepository repository, Guid leaseId)
        {
            _repository = repository;
            _leaseId = leaseId;
        }

        public Task BeatAsync(CancellationToken ct = default) => _repository.TouchAsync(_leaseId, ct);
    }
}
