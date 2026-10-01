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
/// Servicio en segundo plano que programa y dispara la ejecución periódica de la política de retención
/// y purga de sorteos, eventos y novedades caducados.
/// </summary>
public class DataRetentionHostedService : BackgroundService
{
    private const string JobName = "data-retention";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<DataRetentionOptions> _optionsMonitor;
    private readonly ILogger<DataRetentionHostedService> _logger;

    public DataRetentionHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<DataRetentionOptions> optionsMonitor,
        ILogger<DataRetentionHostedService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Iniciando servicio en segundo plano de retención y purga de datos caducados.");

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
                var nowUtc = DateTimeOffset.UtcNow;

                if (nowUtc.Hour >= options.ExecutionHourUtc)
                {
                    try
                    {
                        var windowKey = JobWindowKeyCalculator.DailyUtc(nowUtc);

                        using var scope = _scopeFactory.CreateScope();
                        var coordinator = scope.ServiceProvider.GetRequiredService<IJobExecutionCoordinator>();

                        var outcome = await coordinator.ExecuteWithWindowLeaseAsync(
                            JobName,
                            windowKey,
                            async (heartbeat, ct) =>
                            {
                                _logger.LogInformation("Disparando ejecución automática de retención y purga de datos caducados.");

                                var service = scope.ServiceProvider.GetRequiredService<IDataRetentionService>();
                                var result = await service.PurgeExpiredDataAsync(ct);
                                var summary = $"{result.TotalPurgedEntities} entidades purgadas, {result.DeletedImagesCount} imágenes liberadas.";
                                return new JobWorkResult(result.TotalPurgedEntities, result.FailedImagesCount, summary);
                            },
                            stoppingToken);

                        if (outcome == JobLeaseOutcome.Failed)
                        {
                            _logger.LogError("Error en la ejecución programada de retención de datos para la ventana {WindowKey}.", windowKey);
                        }
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogError(ex, "Error en la ejecución programada de retención de datos: {Message}", ex.Message);
                    }
                }
            }

            try
            {
                // Sondeo cada 30 minutos
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Servicio en segundo plano de retención y purga de datos caducados detenido.");
    }
}
