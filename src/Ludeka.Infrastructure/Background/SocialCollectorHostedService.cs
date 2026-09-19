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
/// Servicio en segundo plano que sondea periódicamente los canales sociales monitorizados
/// (YouTube, Telegram, feeds RSS de blogs e Instagram) e ingesta publicaciones recientes
/// en la bandeja de moderación.
/// </summary>
public class SocialCollectorHostedService : BackgroundService
{
    // INC-47, R5, diseño §7.2: nombre estable del trabajo en JobExecutionLeases.
    private const string JobName = "social-collector";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<SocialCollectorOptions> _optionsMonitor;
    private readonly ILogger<SocialCollectorHostedService> _logger;

    public SocialCollectorHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<SocialCollectorOptions> optionsMonitor,
        ILogger<SocialCollectorHostedService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Iniciando servicio en segundo plano de recolección de canales sociales.");

        var initialOptions = _optionsMonitor.CurrentValue;
        var initialDelay = Math.Max(5, initialOptions.InitialDelaySeconds);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(initialDelay), stoppingToken);
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
                    var blockMinutes = Math.Max(5, options.IntervalMinutes);
                    var windowKey = JobWindowKeyCalculator.MinuteBlock(nowUtc, blockMinutes);

                    // Una única concesión de ámbito por intento: IJobExecutionCoordinator es
                    // Scoped y este servicio es Singleton (AddHostedService); inyectarlo por
                    // constructor sería una dependencia cautiva (mismo motivo que 7.9).
                    using var scope = _scopeFactory.CreateScope();
                    var coordinator = scope.ServiceProvider.GetRequiredService<IJobExecutionCoordinator>();

                    var outcome = await coordinator.ExecuteWithWindowLeaseAsync(
                        JobName, windowKey,
                        async (heartbeat, ct) =>
                        {
                            _logger.LogInformation("Ejecutando lote desatendido de recolección de canales sociales...");

                            var collectorService = scope.ServiceProvider.GetRequiredService<ISocialCollectorService>();

                            // Ruta de sistema sin sesión: el ciclo desatendido no pasa por la guarda de la interfaz.
                            var result = await collectorService.RunScheduledCollectionAsync(options.MaxItemsPerAccount, ct);

                            _logger.LogInformation(
                                "Lote desatendido de recolección completado: {Imported} publicaciones importadas a moderación ({Skipped} duplicados omitidos).",
                                result.ItemsImported,
                                result.ItemsSkippedDuplicates);

                            return new JobWorkResult(result.ItemsImported, result.ErrorsCount, null);
                        },
                        stoppingToken);

                    if (outcome == JobLeaseOutcome.Failed)
                    {
                        _logger.LogError("Error durante la ejecución del lote de recolección social para la ventana {WindowKey}.", windowKey);
                    }
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Error durante la ejecución del lote de recolección social: {Message}", ex.Message);
                }
            }
            else
            {
                _logger.LogDebug("Worker de recolección social deshabilitado temporalmente en configuración.");
            }

            try
            {
                var interval = Math.Max(5, options.IntervalMinutes);
                await Task.Delay(TimeSpan.FromMinutes(interval), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Servicio en segundo plano de recolección de canales sociales detenido.");
    }
}
