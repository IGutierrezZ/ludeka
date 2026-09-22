using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.Features.Jobs;
using Microsoft.Extensions.Logging;

namespace Ludeka.Jobs.Runners;

/// <summary>
/// Trabajo autónomo de siembra y sincronización del directorio lúdico (INC-54).
/// Carga exhaustivamente las editoriales, tiendas y creadores desde seed-directory.json.
/// </summary>
public sealed class SeedDirectoryJobRunner : IJobRunner
{
    private readonly IJobExecutionCoordinator _coordinator;
    private readonly IDirectorySeederService _seederService;
    private readonly ILogger<SeedDirectoryJobRunner> _logger;

    public SeedDirectoryJobRunner(
        IJobExecutionCoordinator coordinator,
        IDirectorySeederService seederService,
        ILogger<SeedDirectoryJobRunner> logger)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _seederService = seederService ?? throw new ArgumentNullException(nameof(seederService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => JobNames.SeedDirectory;

    public Task<JobLeaseOutcome> RunAsync(CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var windowKey = JobWindowKeyCalculator.PerSecond(nowUtc);

        return _coordinator.ExecuteWithWindowLeaseAsync(
            Name,
            windowKey,
            async (_, workCt) =>
            {
                _logger.LogInformation("Iniciando siembra y reconciliación del directorio lúdico...");

                var result = await _seederService.SeedDirectoryAsync(workCt);

                _logger.LogInformation(
                    "Siembra del directorio completada. Editoriales: {PubAdded} creadas, {PubUpdated} actualizadas ({TotalPub} total). " +
                    "Tiendas: {StoreAdded} creadas, {StoreUpdated} actualizadas ({TotalStore} total). " +
                    "Creadores: {CreatorAdded} creados, {CreatorUpdated} actualizados ({TotalCreator} total).",
                    result.PublishersAdded, result.PublishersUpdated, result.TotalPublishers,
                    result.StoresAdded, result.StoresUpdated, result.TotalStores,
                    result.CreatorsAdded, result.CreatorsUpdated, result.TotalCreators);

                int totalProcessed = result.PublishersAdded + result.PublishersUpdated +
                                     result.StoresAdded + result.StoresUpdated +
                                     result.CreatorsAdded + result.CreatorsUpdated;

                return new JobWorkResult(
                    totalProcessed,
                    0,
                    $"Directorio sincronizado: {result.TotalPublishers} editoriales, {result.TotalStores} tiendas, {result.TotalCreators} creadores.");
            },
            ct);
    }
}
