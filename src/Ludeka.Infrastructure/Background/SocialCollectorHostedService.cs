using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
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
                    _logger.LogInformation("Ejecutando lote desatendido de recolección de canales sociales...");

                    using var scope = _scopeFactory.CreateScope();
                    var collectorService = scope.ServiceProvider.GetRequiredService<ISocialCollectorService>();

                    var result = await collectorService.CollectAllAccountsAsync(options.MaxItemsPerAccount, stoppingToken);

                    _logger.LogInformation(
                        "Lote desatendido de recolección completado: {Imported} publicaciones importadas a moderación ({Skipped} duplicados omitidos).",
                        result.ItemsImported,
                        result.ItemsSkippedDuplicates);
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
