using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludeka.Application.Features.Maintenance;

/// <summary>
/// Implementación de la política de retención y purga de entidades vencidas (sorteos, eventos y novedades)
/// y liberación del almacenamiento de imágenes asociadas.
/// </summary>
public class DataRetentionService : IDataRetentionService
{
    private readonly IGiveawayRepository _giveawayRepository;
    private readonly IBoardGameEventRepository _eventRepository;
    private readonly IWeeklyReleaseRepository _releaseRepository;
    private readonly IImageStorageService _imageStorageService;
    private readonly IOptions<DataRetentionOptions> _options;
    private readonly ILogger<DataRetentionService> _logger;
    private readonly TimeProvider _timeProvider;

    public DataRetentionService(
        IGiveawayRepository giveawayRepository,
        IBoardGameEventRepository eventRepository,
        IWeeklyReleaseRepository releaseRepository,
        IImageStorageService imageStorageService,
        IOptions<DataRetentionOptions> options,
        ILogger<DataRetentionService> logger,
        TimeProvider? timeProvider = null)
    {
        _giveawayRepository = giveawayRepository ?? throw new ArgumentNullException(nameof(giveawayRepository));
        _eventRepository = eventRepository ?? throw new ArgumentNullException(nameof(eventRepository));
        _releaseRepository = releaseRepository ?? throw new ArgumentNullException(nameof(releaseRepository));
        _imageStorageService = imageStorageService ?? throw new ArgumentNullException(nameof(imageStorageService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<DataRetentionResult> PurgeExpiredDataAsync(CancellationToken ct = default)
    {
        var options = _options.Value;
        if (!options.Enabled)
        {
            _logger.LogInformation("La purga automática de retención de datos está deshabilitada por configuración.");
            return new DataRetentionResult(0, 0, 0, 0, 0);
        }

        var nowUtc = _timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(nowUtc.UtcDateTime);

        var giveawayCutoff = nowUtc.AddDays(-Math.Max(0, options.GiveawayGracePeriodDays));
        var eventCutoff = today.AddDays(-Math.Max(0, options.EventGracePeriodDays));
        var releaseCreatedCutoff = nowUtc.AddDays(-Math.Max(0, options.ReleaseRetentionDays));

        int purgedGiveaways = 0;
        int purgedEvents = 0;
        int purgedReleases = 0;
        int deletedImages = 0;
        int failedImages = 0;

        // 1. Purga de sorteos vencidos con margen de gracia
        try
        {
            var giveaways = await _giveawayRepository.GetGiveawaysAsync(includeExpired: true, ct);
            var expiredGiveaways = giveaways.Where(g => g.DeadlineAt <= giveawayCutoff).ToList();

            foreach (var giveaway in expiredGiveaways)
            {
                if (ManagedImageKeyExtractor.TryExtract(giveaway.ThumbnailUrl, out var key))
                {
                    try
                    {
                        var deleted = await _imageStorageService.DeleteImageAsync(key!, ct);
                        if (deleted) deletedImages++;
                        else failedImages++;
                    }
                    catch (Exception ex)
                    {
                        failedImages++;
                        _logger.LogWarning(ex, "Error al eliminar imagen de almacenamiento '{Key}' para sorteo {GiveawayId}", key, giveaway.Id);
                    }
                }

                try
                {
                    await _giveawayRepository.DeleteAsync(giveaway.Id, ct);
                    purgedGiveaways++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al eliminar de base de datos el sorteo {GiveawayId}", giveaway.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante la consulta y purga de sorteos vencidos.");
        }

        // 2. Purga de eventos finalizados con margen de gracia
        try
        {
            var events = await _eventRepository.GetAllEventsAsync(ct);
            var expiredEvents = events.Where(e => e.EndDate <= eventCutoff).ToList();

            foreach (var ev in expiredEvents)
            {
                if (ManagedImageKeyExtractor.TryExtract(ev.ImageUrl, out var key))
                {
                    try
                    {
                        var deleted = await _imageStorageService.DeleteImageAsync(key!, ct);
                        if (deleted) deletedImages++;
                        else failedImages++;
                    }
                    catch (Exception ex)
                    {
                        failedImages++;
                        _logger.LogWarning(ex, "Error al eliminar imagen de almacenamiento '{Key}' para evento {EventId}", key, ev.Id);
                    }
                }

                try
                {
                    await _eventRepository.DeleteAsync(ev.Id, ct);
                    purgedEvents++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al eliminar de base de datos el evento {EventId}", ev.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante la consulta y purga de eventos vencidos.");
        }

        // 3. Purga de novedades editoriales antiguas (antigüedad >= días límite y (sin fecha o fecha pasada))
        try
        {
            var releases = await _releaseRepository.GetReleasesAsync(fromDate: null, ct);
            var expiredReleases = releases.Where(r =>
                r.CreatedAt <= releaseCreatedCutoff &&
                (!r.ReleaseDate.HasValue || r.ReleaseDate.Value < today)
            ).ToList();

            foreach (var release in expiredReleases)
            {
                if (ManagedImageKeyExtractor.TryExtract(release.CoverImageUrl, out var key))
                {
                    try
                    {
                        var deleted = await _imageStorageService.DeleteImageAsync(key!, ct);
                        if (deleted) deletedImages++;
                        else failedImages++;
                    }
                    catch (Exception ex)
                    {
                        failedImages++;
                        _logger.LogWarning(ex, "Error al eliminar imagen de almacenamiento '{Key}' para novedad {ReleaseId}", key, release.Id);
                    }
                }

                try
                {
                    await _releaseRepository.DeleteAsync(release.Id, ct);
                    purgedReleases++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al eliminar de base de datos la novedad {ReleaseId}", release.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante la consulta y purga de novedades editoriales caducadas.");
        }

        var result = new DataRetentionResult(purgedGiveaways, purgedEvents, purgedReleases, deletedImages, failedImages);
        _logger.LogInformation(
            "Purga de retención completada: {Giveaways} sorteos, {Events} eventos, {Releases} novedades eliminados. {ImagesDeleted} imágenes liberadas ({ImagesFailed} fallos).",
            result.PurgedGiveawaysCount, result.PurgedEventsCount, result.PurgedReleasesCount, result.DeletedImagesCount, result.FailedImagesCount);

        return result;
    }
}
