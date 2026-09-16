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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICommunityNotificationQueue _queue;
    private readonly ILogger<CommunityNotificationDispatcherHostedService> _logger;
    private DateTimeOffset _lastFridayBulletinDispatched = DateTimeOffset.MinValue;

    public CommunityNotificationDispatcherHostedService(
        IServiceScopeFactory scopeFactory,
        ICommunityNotificationQueue queue,
        ILogger<CommunityNotificationDispatcherHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
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

    private async Task ProcessQueueAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in _queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<ICommunityNotificationService>();

                    _logger.LogDebug("Despachando mensaje de cola: {Title}", message.Title);
                    await service.BroadcastAsync(message, stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Error al procesar mensaje de notificación en segundo plano: {Message}", ex.Message);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Apagado limpio
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
