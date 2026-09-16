using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<NightlyCatalogingOptions> _optionsMonitor;
    private readonly ILogger<NightlyCatalogingHostedService> _logger;
    private DateTimeOffset _lastExecutionDate = DateTimeOffset.MinValue;

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

                // Comprobar si es la hora configurada (o posterior) y no se ha ejecutado hoy
                if (nowUtc.Hour >= options.ExecutionHourUtc && _lastExecutionDate.Date != nowUtc.Date)
                {
                    try
                    {
                        _logger.LogInformation("Disparando ejecución automática de catalogación nocturna a las {Time} UTC.", nowUtc);

                        using var scope = _scopeFactory.CreateScope();
                        var service = scope.ServiceProvider.GetRequiredService<INightlyCatalogingService>();

                        // Ruta de sistema sin sesión: el ciclo programado no pasa por la guarda de la interfaz.
                        await service.RunScheduledCatalogingAsync(options.DailyCatalogingLimit, stoppingToken);
                        _lastExecutionDate = nowUtc;
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
