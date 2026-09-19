using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Jobs;
using Ludeka.Application.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludeka.Infrastructure.Background;

/// <summary>
/// Servicio en segundo plano que sondea periódicamente precios y disponibilidad de títulos en seguimiento 'Quiero comprar'.
/// </summary>
public class PriceRadarHostedService : BackgroundService
{
    // INC-47, R5, diseño §7.2: nombre estable del trabajo en JobExecutionLeases.
    private const string JobName = "price-radar";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<PriceRadarOptions> _optionsMonitor;
    private readonly ILogger<PriceRadarHostedService> _logger;

    public PriceRadarHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<PriceRadarOptions> optionsMonitor,
        ILogger<PriceRadarHostedService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Iniciando servicio en segundo plano del Radar de Precios.");

        // Retardo inicial de cortesía para permitir que la aplicación arranque completamente
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
            var options = _optionsMonitor.CurrentValue;

            if (options.Enabled)
            {
                try
                {
                    var nowUtc = DateTimeOffset.UtcNow;
                    var blockHours = Math.Max(1, options.CheckIntervalHours);
                    var windowKey = JobWindowKeyCalculator.HourlyBlock(nowUtc, blockHours);

                    // Una única concesión de ámbito por intento: IJobExecutionCoordinator es
                    // Scoped y este servicio es Singleton (AddHostedService); inyectarlo por
                    // constructor sería una dependencia cautiva (mismo motivo que 7.9).
                    using var scope = _scopeFactory.CreateScope();
                    var coordinator = scope.ServiceProvider.GetRequiredService<IJobExecutionCoordinator>();

                    var outcome = await coordinator.ExecuteWithWindowLeaseAsync(
                        JobName, windowKey,
                        async (heartbeat, ct) =>
                        {
                            _logger.LogInformation("Ejecutando ciclo del Radar de Precios para títulos en seguimiento...");

                            var radarService = scope.ServiceProvider.GetRequiredService<IPriceRadarService>();

                            int scanned = await radarService.ScanWantToBuyPricesAsync(options.MaxGamesPerScan, ct);

                            _logger.LogInformation("Ciclo del Radar de Precios completado: {Scanned} títulos analizados.", scanned);
                            return new JobWorkResult(scanned, 0, null);
                        },
                        stoppingToken);

                    if (outcome == JobLeaseOutcome.Failed)
                    {
                        _logger.LogError("Error durante la ejecución del ciclo del Radar de Precios para la ventana {WindowKey}.", windowKey);
                    }
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Error durante la ejecución del ciclo del Radar de Precios.");
                }
            }

            var intervalHours = Math.Max(1, _optionsMonitor.CurrentValue.CheckIntervalHours);
            try
            {
                await Task.Delay(TimeSpan.FromHours(intervalHours), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
