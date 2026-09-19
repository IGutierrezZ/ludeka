using Ludeka.Application.Contracts;
using Ludeka.Application.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Ludeka.Web.Health;

/// <summary>
/// Salud observable del outbox de notificaciones comunitarias (INC-47, R4c, diseño §10).
/// Sustituye la comprobación anterior —que solo confirmaba que <c>ICommunityNotificationQueue</c>
/// se resolvía por inyección de dependencias, sin observar dato real alguno— por la profundidad
/// pendiente, la antigüedad del mensaje pendiente más antiguo y los mensajes agotados
/// (<c>Dead</c>) reales del outbox persistido, leídos con la única consulta de
/// <see cref="INotificationOutboxRepository.GetHealthSnapshotAsync"/>.
/// </summary>
public class NotificationQueueHealthCheck : IHealthCheck
{
    private readonly INotificationOutboxRepository _repository;
    private readonly IOptionsMonitor<OutboxOptions> _optionsMonitor;

    public NotificationQueueHealthCheck(
        INotificationOutboxRepository repository,
        IOptionsMonitor<OutboxOptions> optionsMonitor)
    {
        _repository = repository;
        _optionsMonitor = optionsMonitor;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = _optionsMonitor.CurrentValue;
            var snapshot = await _repository.GetHealthSnapshotAsync(cancellationToken);

            var oldestPendingAgeSeconds = snapshot.OldestPendingAt is null
                ? 0d
                : (DateTimeOffset.UtcNow - snapshot.OldestPendingAt.Value).TotalSeconds;

            var data = new Dictionary<string, object>
            {
                { "pending_count", snapshot.PendingCount },
                { "oldest_pending_age_seconds", oldestPendingAgeSeconds },
                { "dead_count", snapshot.DeadCount },
                { "provider", snapshot.Provider }
            };

            // Profundidad, antigüedad y mensajes agotados degradan, nunca tumban el chequeo
            // (diseño §10.1): un atasco del outbox no debe sacar de rotación el servicio web en
            // Cloud Run por un problema que, tras R7, ya no depende de él (lo drena un Job aparte).
            var isDegraded =
                snapshot.PendingCount > options.HealthPendingDepthDegraded ||
                oldestPendingAgeSeconds > options.HealthOldestPendingDegradedMinutes * 60 ||
                snapshot.DeadCount > 0;

            return isDegraded
                ? HealthCheckResult.Degraded(
                    "El outbox de notificaciones supera el umbral de profundidad, antigüedad o mensajes agotados.",
                    data: data)
                : HealthCheckResult.Healthy("Outbox de notificaciones dentro de los umbrales configurados.", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Error al leer el estado del outbox de notificaciones.", ex);
        }
    }
}
