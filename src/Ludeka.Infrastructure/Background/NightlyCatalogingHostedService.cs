using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludeka.Infrastructure.Background;

/// <summary>
/// Servicio en segundo plano que programa y dispara la ejecución del orquestador nocturno
/// de catalogación inteligente y descubrimiento de juegos en novedades.
/// </summary>
public class NightlyCatalogingHostedService : BackgroundService
{
    // INC-47, R5, diseño §7.2: nombre estable del trabajo en JobExecutionLeases.
    private const string JobName = "nightly-cataloging";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<NightlyCatalogingOptions> _optionsMonitor;
    private readonly ILogger<NightlyCatalogingHostedService> _logger;

    public NightlyCatalogingHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<NightlyCatalogingOptions> optionsMonitor,
        ILogger<NightlyCatalogingHostedService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Iniciando servicio en segundo plano de catalogación nocturna inteligente.");

        // Breve pausa de inicialización para permitir el arranque completo del servidor
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _optionsMonitor.CurrentValue;

            if (options.Enabled)
            {
                var nowUtc = DateTimeOffset.UtcNow;

                // La hora configurada sigue decidiendo CUÁNDO intentarlo; SI ya se ejecutó hoy lo
                // decide ahora JobExecutionLeases vía el coordinador (INC-47, R5) — el campo
                // _lastExecutionDate desaparece, tal como exige la especificación
                // "Ausencia de estado en memoria para el control de ejecución".
                if (nowUtc.Hour >= options.ExecutionHourUtc)
                {
                    try
                    {
                        var windowKey = JobWindowKeyCalculator.DailyUtc(nowUtc);

                        // Una única concesión de ámbito por intento: IJobExecutionCoordinator es
                        // Scoped (depende de LudekaDbContext) y este servicio es Singleton
                        // (AddHostedService), así que no puede inyectarse por constructor sin
                        // crear una dependencia cautiva — mismo motivo por el que
                        // INotificationOutboxDispatcher ya se resuelve así (tasks.md 7.9).
                        using var scope = _scopeFactory.CreateScope();
                        var coordinator = scope.ServiceProvider.GetRequiredService<IJobExecutionCoordinator>();

                        var outcome = await coordinator.ExecuteWithWindowLeaseAsync(
                            JobName, windowKey,
                            async (heartbeat, ct) =>
                            {
                                _logger.LogInformation("Disparando ejecución automática de catalogación nocturna a las {Time} UTC.", nowUtc);

                                var service = scope.ServiceProvider.GetRequiredService<INightlyCatalogingService>();

                                // Ruta de sistema sin sesión: el ciclo programado no pasa por la guarda de la interfaz.
                                var result = await service.RunScheduledCatalogingAsync(options.DailyCatalogingLimit, ct);
                                return new JobWorkResult(result.TotalCatalogedCount, result.FailedCount, result.Status);
                            },
                            stoppingToken);

                        if (outcome == JobLeaseOutcome.Failed)
                        {
                            _logger.LogError("Error en la ejecución programada de catalogación nocturna para la ventana {WindowKey}.", windowKey);
                        }
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogError(ex, "Error en la ejecución programada de catalogación nocturna: {Message}", ex.Message);
                    }
                }
            }

            try
            {
                // Comprobación periódica cada 15 minutos
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Servicio en segundo plano de catalogación nocturna detenido.");
    }
}
