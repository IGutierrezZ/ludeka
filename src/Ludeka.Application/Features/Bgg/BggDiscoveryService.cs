using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Microsoft.Extensions.Logging;

namespace Ludeka.Application.Features.Bgg;

/// <summary>
/// Servicio de auto-descubrimiento proactivo de tendencias y lanzamientos en BoardGameGeek.
/// Detecta títulos en Hotness y novedades recientes, aplica triple filtro anti-duplicados
/// (Catálogo, Cola y Staging) e inserta nuevos descubrimientos en PendingBggImports.
/// </summary>
public class BggDiscoveryService : IBggDiscoveryService
{
    private readonly IBggClient _bggClient;
    private readonly IGameRepository _gameRepo;
    private readonly IPendingBggImportRepository _pendingRepo;
    private readonly IBggCatalogStagingRepository? _stagingRepo;
    private readonly ILogger<BggDiscoveryService> _logger;

    public BggDiscoveryService(
        IBggClient bggClient,
        IGameRepository gameRepo,
        IPendingBggImportRepository pendingRepo,
        ILogger<BggDiscoveryService> logger,
        IBggCatalogStagingRepository? stagingRepo = null)
    {
        _bggClient = bggClient ?? throw new ArgumentNullException(nameof(bggClient));
        _gameRepo = gameRepo ?? throw new ArgumentNullException(nameof(gameRepo));
        _pendingRepo = pendingRepo ?? throw new ArgumentNullException(nameof(pendingRepo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stagingRepo = stagingRepo;
    }

    public async Task<BggDiscoveryResultDto> DiscoverAndEnqueueBggTrendsAsync(int maxItems = 50, CancellationToken ct = default)
    {
        if (maxItems <= 0) maxItems = 50;

        _logger.LogInformation("Iniciando escaneo de tendencias mundiales (Hotness) en BGG (límite: {MaxItems}).", maxItems);

        IReadOnlyList<BggTopGameDto> hotGames;
        try
        {
            hotGames = await _bggClient.FetchTopGamesAsync(maxItems, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al consultar el Hotness de BGG: {Message}", ex.Message);
            return new BggDiscoveryResultDto(0, 0, 0, 0, 0, []);
        }

        return await ProcessDiscoveredCandidatesAsync(hotGames, filterYearOnly: false, targetYear: null, ct);
    }

    public async Task<BggDiscoveryResultDto> DiscoverAndEnqueueNewReleasesAsync(int? targetYear = null, int maxItems = 50, CancellationToken ct = default)
    {
        if (maxItems <= 0) maxItems = 50;
        int year = targetYear ?? DateTime.UtcNow.Year;

        _logger.LogInformation("Iniciando escaneo de novedades y lanzamientos BGG para el año {Year} y adyacentes.", year);

        IReadOnlyList<BggTopGameDto> hotGames;
        try
        {
            hotGames = await _bggClient.FetchTopGamesAsync(maxItems, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al consultar BGG para lanzamientos recientes: {Message}", ex.Message);
            return new BggDiscoveryResultDto(0, 0, 0, 0, 0, []);
        }

        return await ProcessDiscoveredCandidatesAsync(hotGames, filterYearOnly: true, targetYear: year, ct);
    }

    private async Task<BggDiscoveryResultDto> ProcessDiscoveredCandidatesAsync(
        IReadOnlyList<BggTopGameDto> candidates,
        bool filterYearOnly,
        int? targetYear,
        CancellationToken ct)
    {
        int totalScanned = candidates.Count;
        int discoveredCount = 0;
        int enqueuedCount = 0;
        int alreadyCatalogedCount = 0;
        int alreadyInQueueCount = 0;
        var enqueuedTitles = new List<string>();

        int currentYear = targetYear ?? DateTime.UtcNow.Year;

        foreach (var candidate in candidates)
        {
            if (candidate.BggId <= 0 || string.IsNullOrWhiteSpace(candidate.Title))
                continue;

            // Si se solicita exclusivamente filtro de año reciente
            if (filterYearOnly && candidate.YearPublished.HasValue)
            {
                if (candidate.YearPublished.Value < (currentYear - 1))
                {
                    continue;
                }
            }

            // 1. Verificar si ya existe en el catálogo publicado
            var existingGame = await _gameRepo.GetByBggIdAsync(candidate.BggId, ct);
            if (existingGame != null)
            {
                alreadyCatalogedCount++;
                continue;
            }

            // 2. Verificar si ya existe en la cola de importación
            var existingPending = await _pendingRepo.GetByBggIdAsync(candidate.BggId, ct);
            if (existingPending != null)
            {
                if (existingPending.Status == CatalogQueueStatus.Completed)
                {
                    alreadyCatalogedCount++;
                }
                else
                {
                    alreadyInQueueCount++;
                }
                continue;
            }

            // 3. Verificar si ya está en la tabla de staging masivo
            if (_stagingRepo != null)
            {
                var existingStaging = await _stagingRepo.GetByBggIdAsync(candidate.BggId, ct);
                if (existingStaging != null)
                {
                    alreadyInQueueCount++;
                    continue;
                }
            }

            // Es un descubrimiento genuino
            discoveredCount++;

            // Determinar origen: si es del año actual o previo es lanzamiento reciente; si no, hotness general
            bool isRecentRelease = candidate.YearPublished.HasValue && candidate.YearPublished.Value >= (currentYear - 1);
            var origin = isRecentRelease ? CatalogQueueOrigin.BggNewReleases : CatalogQueueOrigin.BggHotness;

            var pendingItem = new PendingBggImport(
                bggId: candidate.BggId,
                title: candidate.Title,
                yearPublished: candidate.YearPublished,
                thumbnailUrl: candidate.ThumbnailUrl,
                coverImageUrl: null,
                origin: origin,
                extractedTitle: candidate.Title
            );

            try
            {
                await _pendingRepo.AddAsync(pendingItem, ct);
                enqueuedCount++;
                enqueuedTitles.Add(candidate.Title);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fallo al insertar en cola el juego descubierto #{BggId} ('{Title}'): {Message}",
                    candidate.BggId, candidate.Title, ex.Message);
            }
        }

        _logger.LogInformation("Descubrimiento BGG finalizado. Escaneados: {Scanned}, Nuevos descubiertos: {Discovered}, Encolados: {Enqueued}, Ya en catálogo: {Cataloged}, Ya en cola: {InQueue}.",
            totalScanned, discoveredCount, enqueuedCount, alreadyCatalogedCount, alreadyInQueueCount);

        return new BggDiscoveryResultDto(
            TotalScanned: totalScanned,
            DiscoveredCount: discoveredCount,
            EnqueuedCount: enqueuedCount,
            AlreadyCatalogedCount: alreadyCatalogedCount,
            AlreadyInQueueCount: alreadyInQueueCount,
            EnqueuedTitles: enqueuedTitles
        );
    }
}
