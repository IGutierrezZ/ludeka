using System;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Jobs;
using Microsoft.Extensions.Logging;

namespace Ludeka.Jobs.Runners;

/// <summary>
/// Trabajo autónomo para la sincronización periódica de novedades editoriales oficiales (Devir, Maldito Games).
/// Extrae lanzamientos y reimpresiones con EAN y PVP, enlaza o importa desde BGG e inserta en la cartelera de novedades.
/// </summary>
public sealed class EditorialReleasesSyncJobRunner : IJobRunner
{
    private readonly IJobExecutionCoordinator _coordinator;
    private readonly IEditorialReleasesSyncService _syncService;
    private readonly ILogger<EditorialReleasesSyncJobRunner> _logger;

    public EditorialReleasesSyncJobRunner(
        IJobExecutionCoordinator coordinator,
        IEditorialReleasesSyncService syncService,
        ILogger<EditorialReleasesSyncJobRunner> logger)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => JobNames.EditorialReleasesSync;

    public Task<JobLeaseOutcome> RunAsync(CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var windowKey = JobWindowKeyCalculator.PerSecond(nowUtc);

        return _coordinator.ExecuteWithWindowLeaseAsync(
            Name,
            windowKey,
            async (heartbeat, workCt) =>
            {
                _logger.LogInformation("Iniciando trabajo '{JobName}' para sincronización de novedades editoriales oficiales...", Name);
                await heartbeat.BeatAsync(workCt);

                var summary = await _syncService.SyncAllEditorialReleasesAsync(workCt);

                await heartbeat.BeatAsync(workCt);

                _logger.LogInformation(
                    "Trabajo '{JobName}' finalizado. Novedades encontradas: {TotalFound}, Creadas: {Created}, Actualizadas: {Updated}, Vinculadas a catálogo: {Linked}, Importadas BGG: {Imported}.",
                    Name, summary.TotalFound, summary.CreatedCount, summary.UpdatedCount, summary.GamesLinkedCount, summary.GamesImportedFromBggCount);

                int failedCount = summary.Errors.Count;
                string? message = failedCount > 0 ? string.Join("; ", summary.Errors) : null;

                return new JobWorkResult(summary.CreatedCount + summary.UpdatedCount, failedCount, message);
            },
            ct);
    }
}
