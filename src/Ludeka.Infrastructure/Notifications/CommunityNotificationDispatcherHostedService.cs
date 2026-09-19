using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ludeka.Infrastructure.Notifications;

public class CommunityNotificationDispatcherHostedService : BackgroundService
{
    // INC-47 (R4b, tasks.md 7.9): cadencia de sondeo del outbox. El diseño no fija un valor
    // exacto; decisión de sdd-apply, deliberadamente corta frente a LeaseSeconds (300s por
    // defecto) porque gobierna solo la frecuencia de sondeo, no la ventana de exclusión mutua.
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CommunityNotificationDispatcherHostedService> _logger;
    private DateTimeOffset _lastFridayBulletinDispatched = DateTimeOffset.MinValue;

    public CommunityNotificationDispatcherHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<CommunityNotificationDispatcherHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Iniciando despachador en segundo plano de notificaciones comunitarias.");

        // Ejecutar paralelamente el consumidor de la cola y el temporizador periódico
        var queueConsumerTask = ProcessQueueAsync(stoppingToken);
        var periodicScanTask = RunPeriodicScanAsync(stoppingToken);

        await Task.WhenAll(queueConsumerTask, periodicScanTask);
    }

    /// <summary>
    /// Ciclo de sondeo acotado del outbox (INC-47, R4b, diseño §6.5): sustituye el consumo
    /// infinito de <c>ICommunityNotificationQueue.ReadAllAsync</c> (retirado de la interfaz en
    /// tasks.md 6.7) por <see cref="INotificationOutboxDispatcher.DispatchPendingAsync"/>, uno
    /// por vuelta. El servicio ya no inyecta ninguna dependencia con ámbito por constructor —
    /// tanto el despachador como la cola que este consume internamente son <c>Scoped</c>
    /// (tasks.md 6.11/7.10) — y las resuelve aquí a través de <see cref="_scopeFactory"/>, el
    /// mismo mecanismo que ya usa <see cref="RunPeriodicScanAsync"/> para
    /// <c>ICommunityNotificationService</c>.
    /// </summary>
    private async Task ProcessQueueAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationOutboxDispatcher>();

                var result = await dispatcher.DispatchPendingAsync(stoppingToken);
                if (result.ClaimedCount > 0)
                {
                    _logger.LogDebug(
                        "Ciclo del outbox: {Claimed} reclamados, {Completed} completados, {Retried} reprogramados, {Dead} agotados.",
                        result.ClaimedCount, result.CompletedCount, result.RetriedCount, result.DeadCount);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error al despachar el outbox de notificaciones: {Message}", ex.Message);
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunPeriodicScanAsync(CancellationToken stoppingToken)
    {
        // Espera inicial de cortesía antes del primer escaneo periódico
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ICommunityNotificationService>();

                // 1. Escaneo de sorteos próximos a expirar (24h) — ruta de sistema sin sesión.
                await service.RunExpiringGiveawaysScanAsync(stoppingToken);

                // 2. Boletín de viernes (si es viernes y no se ha emitido hoy)
                var now = DateTimeOffset.UtcNow;
                if (now.DayOfWeek == DayOfWeek.Friday && _lastFridayBulletinDispatched.Date != now.Date)
                {
                    await service.RunFridayReleasesBulletinAsync(stoppingToken);
                    _lastFridayBulletinDispatched = now;
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Error en el escaneo periódico de notificaciones: {Message}", ex.Message);
            }

            try
            {
                // Escaneo cada 60 minutos
                await Task.Delay(TimeSpan.FromMinutes(60), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
