using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Ludeka.Application.Features.Bgg;

/// <summary>
/// Servicio de auto-descubrimiento proactivo de tendencias y lanzamientos en BoardGameGeek.
/// Detecta títulos en Hotness y novedades recientes, aplica triple filtro anti-duplicados
/// (Catálogo, Cola y Staging) e inserta nuevos descubrimientos en PendingBggImports.
/// </summary>
public class BggDiscoveryService : IBggDiscoveryService
{
    private const string DenialMessage =
        "Se requiere el permiso de moderación 'CanEditGames' para descubrir y encolar tendencias de BGG.";

    private readonly IBggClient _bggClient;
    private readonly IGameRepository _gameRepo;
    private readonly IPendingBggImportRepository _pendingRepo;
    private readonly IBggCatalogStagingRepository? _stagingRepo;
    private readonly ILogger<BggDiscoveryService> _logger;
    private readonly ISessionPermissionGuard? _permissionGuard;
    private readonly IDailyTrendingGameRepository? _trendingRepo;
    private readonly IBggRawSnapshotSyncService? _snapshotSyncService;
    private readonly IAiGameSummaryService? _aiSummaryService;
    private readonly IUserCollectionRepository? _collectionRepo;

    public BggDiscoveryService(
        IBggClient bggClient,
        IGameRepository gameRepo,
        IPendingBggImportRepository pendingRepo,
        ILogger<BggDiscoveryService> logger,
        IBggCatalogStagingRepository? stagingRepo = null,
        ISessionPermissionGuard? permissionGuard = null,
        IDailyTrendingGameRepository? trendingRepo = null,
        IBggRawSnapshotSyncService? snapshotSyncService = null,
        IAiGameSummaryService? aiSummaryService = null,
        IUserCollectionRepository? collectionRepo = null)
    {
        _bggClient = bggClient ?? throw new ArgumentNullException(nameof(bggClient));
        _gameRepo = gameRepo ?? throw new ArgumentNullException(nameof(gameRepo));
        _pendingRepo = pendingRepo ?? throw new ArgumentNullException(nameof(pendingRepo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stagingRepo = stagingRepo;
        _permissionGuard = permissionGuard;
        _trendingRepo = trendingRepo;
        _snapshotSyncService = snapshotSyncService;
        _aiSummaryService = aiSummaryService;
        _collectionRepo = collectionRepo;
    }

    /// <summary>
    /// Revalida sesión y permiso releyendo el <c>AppUser</c> actual (INC-46, W1): el escaneo encola
    /// descubrimientos desde el panel; el ciclo nocturno usa la ruta de sistema.
    /// </summary>
    private Task RequirePermissionAsync(CancellationToken ct)
        => _permissionGuard is null
            ? Task.CompletedTask
            : _permissionGuard.RequireAsync(ModeratorPermission.CanEditGames, DenialMessage, ct);

    public async Task<BggDiscoveryResultDto> DiscoverAndEnqueueBggTrendsAsync(int maxItems = 50, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        return await RunBggTrendsDiscoveryAsync(maxItems, ct);
    }

    /// <inheritdoc />
    public async Task<BggDiscoveryResultDto> RunBggTrendsDiscoveryAsync(int maxItems = 50, CancellationToken ct = default)
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
        await RequirePermissionAsync(ct);

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

    /// <inheritdoc />
    public async Task<BggTrendingSyncResultDto> SyncDailyTrendingAsync(int maxItems = 50, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        return await RunDailyTrendingSyncAsync(maxItems, ct);
    }

    /// <inheritdoc />
    public async Task<BggTrendingSyncResultDto> RunDailyTrendingSyncAsync(int maxItems = 50, CancellationToken ct = default)
    {
        if (maxItems <= 0) maxItems = 50;
        if (maxItems > 50) maxItems = 50;

        var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
        _logger.LogInformation("Iniciando sincronización de tendencias diarias BGG (Hotness) para {Date} (límite: {MaxItems}).", todayUtc, maxItems);

        IReadOnlyList<BggTopGameDto> hotGames;
        try
        {
            hotGames = await _bggClient.FetchTopGamesAsync(maxItems, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al consultar las tendencias (Hotness) de BGG: {Message}", ex.Message);
            return new BggTrendingSyncResultDto(todayUtc, 0, 0, 0, 0, []);
        }

        int alreadyCatalogedCount = 0;
        int newlyCatalogedCount = 0;
        int failedCount = 0;
        var newlyCatalogedTitles = new List<string>();
        var trendingEntities = new List<DailyTrendingGame>();

        for (int i = 0; i < hotGames.Count; i++)
        {
            int rank = i + 1;
            if (rank > 50) break;

            var candidate = hotGames[i];
            if (candidate.BggId <= 0 || string.IsNullOrWhiteSpace(candidate.Title))
                continue;

            Guid? gameId = null;

            try
            {
                var existingGame = await _gameRepo.GetByBggIdAsync(candidate.BggId, ct);
                if (existingGame != null)
                {
                    gameId = existingGame.Id;
                    alreadyCatalogedCount++;

                    if (_snapshotSyncService != null)
                    {
                        try
                        {
                            await _snapshotSyncService.EnsureSnapshotAsync(candidate.BggId, ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "No se pudo asegurar snapshot para el juego existente #{BggId}: {Message}", candidate.BggId, ex.Message);
                        }
                    }

                    if (existingGame.AiSummary == null && _aiSummaryService != null)
                    {
                        try
                        {
                            var summaryDto = await _aiSummaryService.GenerateSummaryAsync(existingGame, ct);
                            existingGame.SetAiSummary(new AiGameSummary(
                                summaryDto.GeneralVerdict,
                                summaryDto.ScalabilitySummary,
                                summaryDto.AgeSummary,
                                summaryDto.FootprintSummary,
                                summaryDto.Model,
                                summaryDto.GeneratedAt ?? DateTime.UtcNow
                            ));
                            await _gameRepo.UpdateAsync(existingGame, ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Fallo al generar resumen IA para juego existente #{BggId}: {Message}", candidate.BggId, ex.Message);
                        }
                    }
                }
                else
                {
                    // Ingesta inmediata del título ausente
                    if (_snapshotSyncService != null)
                    {
                        try
                        {
                            await _snapshotSyncService.EnsureSnapshotAsync(candidate.BggId, ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Fallo al asegurar snapshot para juego nuevo #{BggId}: {Message}", candidate.BggId, ex.Message);
                        }
                    }

                    var fetchedGame = await _bggClient.FetchGameByBggIdAsync(candidate.BggId, ct);
                    if (fetchedGame == null)
                    {
                        _logger.LogWarning("BGG no devolvió detalle para el juego en tendencia #{BggId} ('{Title}').", candidate.BggId, candidate.Title);
                        failedCount++;
                    }
                    else
                    {
                        if (_aiSummaryService != null)
                        {
                            try
                            {
                                var summaryDto = await _aiSummaryService.GenerateSummaryAsync(fetchedGame, ct);
                                fetchedGame.SetAiSummary(new AiGameSummary(
                                    summaryDto.GeneralVerdict,
                                    summaryDto.ScalabilitySummary,
                                    summaryDto.AgeSummary,
                                    summaryDto.FootprintSummary,
                                    summaryDto.Model,
                                    summaryDto.GeneratedAt ?? DateTime.UtcNow
                                ));
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Fallo al generar resumen IA para #{BggId} durante ingesta de tendencias: {Message}", candidate.BggId, ex.Message);
                            }
                        }

                        await _gameRepo.AddRangeAsync([fetchedGame], ct);
                        gameId = fetchedGame.Id;
                        newlyCatalogedCount++;
                        newlyCatalogedTitles.Add(fetchedGame.SpanishTitle);

                        if (_collectionRepo != null)
                        {
                            try
                            {
                                await _collectionRepo.PromotePendingItemsAsync(candidate.BggId, gameId.Value, ct);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Fallo al promover colecciones para #{BggId}: {Message}", candidate.BggId, ex.Message);
                            }
                        }

                        try
                        {
                            var pending = await _pendingRepo.GetByBggIdAsync(candidate.BggId, ct);
                            if (pending != null && pending.Status != CatalogQueueStatus.Completed)
                            {
                                pending.MarkAsCompleted();
                                await _pendingRepo.UpdateAsync(pending, ct);
                            }
                        }
                        catch
                        {
                            // Ignorar fallo secundario en cola
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al procesar juego #{BggId} ('{Title}') en sincronización de tendencias: {Message}",
                    candidate.BggId, candidate.Title, ex.Message);
                failedCount++;
            }

            var trendingItem = new DailyTrendingGame(
                dateUtc: todayUtc,
                rank: rank,
                bggId: candidate.BggId,
                title: candidate.Title,
                yearPublished: candidate.YearPublished,
                thumbnailUrl: candidate.ThumbnailUrl,
                gameId: gameId
            );
            trendingEntities.Add(trendingItem);
        }

        if (_trendingRepo != null && trendingEntities.Count > 0)
        {
            try
            {
                await _trendingRepo.UpsertDailyTrendingBatchAsync(trendingEntities, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al persistir el lote de tendencias diarias para {Date}: {Message}", todayUtc, ex.Message);
            }
        }

        _logger.LogInformation("Sincronización de tendencias BGG finalizada para {Date}. Total procesados: {Total}, Ya catalogados: {Cataloged}, Nuevos catalogados: {Newly}, Fallidos: {Failed}.",
            todayUtc, trendingEntities.Count, alreadyCatalogedCount, newlyCatalogedCount, failedCount);

        return new BggTrendingSyncResultDto(
            DateUtc: todayUtc,
            TotalTrendingProcessed: trendingEntities.Count,
            AlreadyCatalogedCount: alreadyCatalogedCount,
            NewlyCatalogedCount: newlyCatalogedCount,
            FailedCount: failedCount,
            NewlyCatalogedTitles: newlyCatalogedTitles
        );
    }
}
