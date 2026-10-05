using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludeka.Application.Features.Bgg;

/// <summary>
/// Orquestador inteligente de catalogación nocturna y bajo demanda.
/// Ejecuta la detección en novedades editoriales, procesa la cola de usuarios y novedades,
/// y completa el cupo diario con los mejores juegos de BGG respetando límites de API y Gemini.
/// </summary>
public class NightlyCatalogingService : INightlyCatalogingService
{
    private const string DenialMessage =
        "Se requiere el permiso de moderación 'CanEditGames' para ejecutar el ciclo nocturno de catalogación.";

    private readonly IPendingBggImportRepository _pendingRepo;
    private readonly IBggClient _bggClient;
    private readonly IGameRepository _gameRepo;
    private readonly IUserCollectionRepository _collectionRepo;
    private readonly IWeeklyReleaseRepository _releaseRepo;
    private readonly INewsGameExtractor _newsExtractor;
    private readonly INightlyCatalogingLogRepository _logRepo;
    private readonly IAiGameSummaryService? _aiSummaryService;
    private readonly IBggMassIngestionService? _massIngestionService;
    private readonly IBggDiscoveryService? _discoveryService;
    private readonly NightlyCatalogingOptions _options;
    private readonly ILogger<NightlyCatalogingService> _logger;
    private readonly ISessionPermissionGuard? _permissionGuard;

    public NightlyCatalogingService(
        IPendingBggImportRepository pendingRepo,
        IBggClient bggClient,
        IGameRepository gameRepo,
        IUserCollectionRepository collectionRepo,
        IWeeklyReleaseRepository releaseRepo,
        INewsGameExtractor newsExtractor,
        INightlyCatalogingLogRepository logRepo,
        IOptions<NightlyCatalogingOptions> options,
        ILogger<NightlyCatalogingService> logger,
        IAiGameSummaryService? aiSummaryService = null,
        IBggMassIngestionService? massIngestionService = null,
        IBggDiscoveryService? discoveryService = null,
        ISessionPermissionGuard? permissionGuard = null)
    {
        _pendingRepo = pendingRepo ?? throw new ArgumentNullException(nameof(pendingRepo));
        _bggClient = bggClient ?? throw new ArgumentNullException(nameof(bggClient));
        _gameRepo = gameRepo ?? throw new ArgumentNullException(nameof(gameRepo));
        _collectionRepo = collectionRepo ?? throw new ArgumentNullException(nameof(collectionRepo));
        _releaseRepo = releaseRepo ?? throw new ArgumentNullException(nameof(releaseRepo));
        _newsExtractor = newsExtractor ?? throw new ArgumentNullException(nameof(newsExtractor));
        _logRepo = logRepo ?? throw new ArgumentNullException(nameof(logRepo));
        _options = options?.Value ?? new NightlyCatalogingOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _aiSummaryService = aiSummaryService;
        _massIngestionService = massIngestionService;
        _discoveryService = discoveryService;
        _permissionGuard = permissionGuard;
    }

    /// <summary>
    /// Revalida sesión y permiso releyendo el <c>AppUser</c> actual (INC-46, W1) antes de escribir la
    /// bitácora: el ciclo se lanza a mano desde el panel; el servicio hospedado usa la ruta de sistema.
    /// </summary>
    private Task RequirePermissionAsync(CancellationToken ct)
        => _permissionGuard is null
            ? Task.CompletedTask
            : _permissionGuard.RequireAsync(ModeratorPermission.CanEditGames, DenialMessage, ct);

    public async Task<NightlyCatalogingResultDto> ExecuteNightlyCatalogingAsync(int? customLimit = null, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        return await RunScheduledCatalogingAsync(customLimit, ct);
    }

    /// <inheritdoc />
    public async Task<NightlyCatalogingResultDto> RunScheduledCatalogingAsync(int? customLimit = null, CancellationToken ct = default)
    {
        int limit = customLimit.HasValue && customLimit.Value > 0 ? customLimit.Value : _options.DailyCatalogingLimit;
        var startedAt = DateTimeOffset.UtcNow;
        var log = new NightlyCatalogingExecutionLog(startedAt);
        await _logRepo.AddAsync(log, ct);

        int queueProcessedCount = 0;
        int newsDiscoveryCount = 0;
        int bggDiscoveryCount = 0;
        int topBackfillCount = 0;
        int failedCount = 0;
        var catalogedTitles = new List<string>();

        _logger.LogInformation("Iniciando ciclo nocturno de catalogación inteligente. Cupo diario: {Limit} juegos.", limit);

        try
        {
            // --- FASE 1: Detección Automática en Novedades Editoriales ---
            try
            {
                newsDiscoveryCount = await _newsExtractor.DiscoverAndEnqueueFromReleasesAsync(ct);
                _logger.LogInformation("Fase 1 completada: {Count} novedades procesadas/encoladas.", newsDiscoveryCount);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Advertencia en Fase 1 (Detección en Novedades): {Message}", ex.Message);
            }

            // --- FASE 1.5: Sincronización de Tendencias BGG e Ingesta Inmediata ---
            if (_discoveryService != null)
            {
                try
                {
                    var syncResult = await _discoveryService.RunDailyTrendingSyncAsync(maxItems: 50, ct);
                    bggDiscoveryCount = syncResult.NewlyCatalogedCount;
                    _logger.LogInformation("Fase 1.5 completada: {Count} títulos de tendencias catalogados de inmediato ({Total} procesados en instantánea).",
                        bggDiscoveryCount, syncResult.TotalTrendingProcessed);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Advertencia en Fase 1.5 (Sincronización de Tendencias BGG): {Message}", ex.Message);
                }
            }

            // --- FASE 2: Procesamiento de Cola Prioritaria (Usuarios, Novedades y Tendencias BGG) ---
            var topPending = await _pendingRepo.GetTopPendingAsync(limit, ct);
            _logger.LogInformation("Fase 2: {Count} juegos pendientes encontrados en la cola prioritaria.", topPending.Count);

            var pendingChunks = topPending
                .Select((item, index) => new { item, index })
                .GroupBy(x => x.index / 20)
                .Select(g => g.Select(x => x.item).ToList())
                .ToList();

            foreach (var chunk in pendingChunks)
            {
                if (catalogedTitles.Count >= limit) break;

                await ApplyPoliteDelayAsync(ct);

                foreach (var p in chunk)
                {
                    p.MarkAsProcessing();
                    await _pendingRepo.UpdateAsync(p, ct);
                }

                try
                {
                    var chunkBggIds = chunk.Select(p => p.BggId).ToList();
                    var fetchedGames = await _bggClient.FetchGamesByBggIdsAsync(chunkBggIds, includeVersions: true, ct);
                    var fetchedMap = fetchedGames.ToDictionary(g => g.BggId);

                    var newGamesToCatalog = new List<(PendingBggImport Pending, Game FetchedGame)>();
                    var existingGamesToUpdate = new List<(PendingBggImport Pending, Game ExistingGame)>();

                    foreach (var pending in chunk)
                    {
                        if (!fetchedMap.TryGetValue(pending.BggId, out var fetchedGame) || fetchedGame == null)
                        {
                            pending.MarkAsFailed($"BGG no devolvió información para el juego #{pending.BggId}.");
                            await _pendingRepo.UpdateAsync(pending, ct);
                            failedCount++;
                            continue;
                        }

                        var existingGame = await _gameRepo.GetByBggIdAsync(pending.BggId, ct);
                        if (existingGame == null)
                        {
                            newGamesToCatalog.Add((pending, fetchedGame));
                        }
                        else
                        {
                            existingGamesToUpdate.Add((pending, existingGame));
                        }
                    }

                    // Sintetizar resúmenes de IA en lotes de hasta 10 juegos
                    var gamesNeedingAi = new List<Game>();
                    foreach (var (_, game) in newGamesToCatalog)
                    {
                        if (game.AiSummary == null) gamesNeedingAi.Add(game);
                    }
                    foreach (var (_, game) in existingGamesToUpdate)
                    {
                        if (game.AiSummary == null) gamesNeedingAi.Add(game);
                    }

                    if (gamesNeedingAi.Count > 0)
                    {
                        await EnrichGamesWithAiSummaryBatchAsync(gamesNeedingAi, ct);
                    }

                    // Guardar juegos nuevos
                    foreach (var (pending, fetchedGame) in newGamesToCatalog)
                    {
                        if (catalogedTitles.Count >= limit) break;

                        await _gameRepo.AddRangeAsync([fetchedGame], ct);
                        var gameId = fetchedGame.Id;

                        await _collectionRepo.PromotePendingItemsAsync(pending.BggId, gameId, ct);
                        await LinkPendingReleasesAsync(fetchedGame, gameId, ct);

                        pending.MarkAsCompleted();
                        await _pendingRepo.UpdateAsync(pending, ct);

                        catalogedTitles.Add(fetchedGame.SpanishTitle);
                        queueProcessedCount++;
                    }

                    // Actualizar juegos existentes
                    foreach (var (pending, existingGame) in existingGamesToUpdate)
                    {
                        await _gameRepo.UpdateAsync(existingGame, ct);

                        await _collectionRepo.PromotePendingItemsAsync(pending.BggId, existingGame.Id, ct);
                        await LinkPendingReleasesAsync(existingGame, existingGame.Id, ct);

                        pending.MarkAsCompleted();
                        await _pendingRepo.UpdateAsync(pending, ct);

                        catalogedTitles.Add(existingGame.SpanishTitle);
                        queueProcessedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al procesar bloque de {Count} juegos de la cola: {Message}", chunk.Count, ex.Message);
                    foreach (var pending in chunk)
                    {
                        if (pending.Status == CatalogQueueStatus.Processing)
                        {
                            pending.MarkAsFailed(ex.Message);
                            await _pendingRepo.UpdateAsync(pending, ct);
                            failedCount++;
                        }
                    }
                }
            }

            // --- FASE 3: Drenaje Progresivo de Staging Masivo y Relleno de Catálogo ---
            if (_massIngestionService != null)
            {
                try
                {
                    // Auto-siembra condicional (INC-53): si Staging está vacío, disparar descarga autónoma inicial
                    var stagingMetrics = await _massIngestionService.GetMetricsAsync(ct);
                    if (stagingMetrics.TotalInStaging == 0)
                    {
                        _logger.LogInformation("Fase 3: Staging está vacío. Disparando auto-siembra inicial autónoma de catálogo BGG.");
                        int seededCount = await _massIngestionService.RunScheduledDownloadAndIngestLatestRanksAsync(ct: ct);
                        _logger.LogInformation("Auto-siembra completada: {Count} títulos incorporados a staging.", seededCount);
                    }

                    _logger.LogInformation("Fase 3: Ejecutando ciclo de drenaje de staging masivo (detalles, fotos GeekDo/R2, IA por lotes y promoción).");
                    var drainResult = await _massIngestionService.RunScheduledDrainCycleAsync(ct);
                    topBackfillCount += drainResult.PromotedToCatalogCount;
                    _logger.LogInformation("Fase 3 (Staging): {Result}", drainResult.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Advertencia durante el ciclo de drenaje de staging: {Message}", ex.Message);
                }
            }

            int remainingQuota = limit - catalogedTitles.Count;
            if (remainingQuota > 0)
            {
                _logger.LogInformation("Fase 3: Rellenando cupo restante ({Remaining}) con los mejores juegos de BGG.", remainingQuota);

                try
                {
                    var topGames = await _bggClient.FetchTopGamesAsync(remainingQuota + 30, ct);
                    var candidates = new List<BggTopGameDto>();

                    foreach (var topGame in topGames)
                    {
                        var inCatalog = await _gameRepo.GetByBggIdAsync(topGame.BggId, ct);
                        if (inCatalog != null) continue;

                        var existingQueue = await _pendingRepo.GetByBggIdAsync(topGame.BggId, ct);
                        if (existingQueue != null && existingQueue.Status == CatalogQueueStatus.Completed) continue;

                        candidates.Add(topGame);
                        if (candidates.Count >= remainingQuota) break;
                    }

                    var topChunks = candidates
                        .Select((item, index) => new { item, index })
                        .GroupBy(x => x.index / 20)
                        .Select(g => g.Select(x => x.item).ToList())
                        .ToList();

                    foreach (var chunk in topChunks)
                    {
                        if (catalogedTitles.Count >= limit) break;

                        await ApplyPoliteDelayAsync(ct);

                        var chunkIds = chunk.Select(c => c.BggId).ToList();
                        var fetchedGames = await _bggClient.FetchGamesByBggIdsAsync(chunkIds, includeVersions: true, ct);
                        var fetchedMap = fetchedGames.ToDictionary(g => g.BggId);

                        var gamesNeedingAi = fetchedGames.Where(g => g.AiSummary == null).ToList();
                        if (gamesNeedingAi.Count > 0)
                        {
                            await EnrichGamesWithAiSummaryBatchAsync(gamesNeedingAi, ct);
                        }

                        foreach (var topGame in chunk)
                        {
                            if (catalogedTitles.Count >= limit) break;

                            if (!fetchedMap.TryGetValue(topGame.BggId, out var fetchedGame) || fetchedGame == null)
                            {
                                continue;
                            }

                            await _gameRepo.AddRangeAsync([fetchedGame], ct);

                            var existingQueue = await _pendingRepo.GetByBggIdAsync(topGame.BggId, ct);
                            if (existingQueue == null)
                            {
                                var backfillItem = new PendingBggImport(
                                    bggId: topGame.BggId,
                                    title: topGame.Title,
                                    yearPublished: topGame.YearPublished,
                                    thumbnailUrl: topGame.ThumbnailUrl,
                                    coverImageUrl: null,
                                    origin: CatalogQueueOrigin.TopBggBackfill,
                                    extractedTitle: topGame.Title
                                );
                                backfillItem.MarkAsCompleted();
                                await _pendingRepo.AddAsync(backfillItem, ct);
                            }
                            else
                            {
                                existingQueue.MarkAsCompleted();
                                await _pendingRepo.UpdateAsync(existingQueue, ct);
                            }

                            await LinkPendingReleasesAsync(fetchedGame, fetchedGame.Id, ct);

                            catalogedTitles.Add(fetchedGame.SpanishTitle);
                            topBackfillCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al rellenar catálogo con el Top de BGG: {Message}", ex.Message);
                }
            }

            // --- FASE 4: Guardado de Bitácora y Finalización Exitosa ---
            log.Complete(
                queueProcessed: queueProcessedCount,
                newsDiscovery: newsDiscoveryCount,
                topBackfill: topBackfillCount,
                totalCataloged: catalogedTitles.Count,
                failed: failedCount,
                titles: catalogedTitles,
                bggDiscovery: bggDiscoveryCount
            );
            await _logRepo.UpdateAsync(log, ct);

            _logger.LogInformation("Ciclo nocturno finalizado con éxito: {Total} catalogados ({Queue} cola, {Backfill} Top BGG, {Failed} fallos, {BggDisc} descubiertos BGG).",
                catalogedTitles.Count, queueProcessedCount, topBackfillCount, failedCount, bggDiscoveryCount);

            return new NightlyCatalogingResultDto(
                LogId: log.Id,
                StartedAt: log.StartedAt,
                CompletedAt: log.CompletedAt ?? DateTimeOffset.UtcNow,
                QueueProcessedCount: queueProcessedCount,
                NewsDiscoveryCount: newsDiscoveryCount,
                TopBackfillCount: topBackfillCount,
                TotalCatalogedCount: catalogedTitles.Count,
                FailedCount: failedCount,
                CatalogedTitles: catalogedTitles,
                Status: log.Status,
                ErrorMessage: null,
                BggDiscoveryCount: bggDiscoveryCount
            );
        }
        catch (Exception fatalEx)
        {
            _logger.LogError(fatalEx, "Error crítico durante la catalogación nocturna: {Message}", fatalEx.Message);
            log.Fail(fatalEx.Message);
            await _logRepo.UpdateAsync(log, ct);

            return new NightlyCatalogingResultDto(
                LogId: log.Id,
                StartedAt: log.StartedAt,
                CompletedAt: DateTimeOffset.UtcNow,
                QueueProcessedCount: queueProcessedCount,
                NewsDiscoveryCount: newsDiscoveryCount,
                TopBackfillCount: topBackfillCount,
                TotalCatalogedCount: catalogedTitles.Count,
                FailedCount: failedCount,
                CatalogedTitles: catalogedTitles,
                Status: "Failed",
                ErrorMessage: fatalEx.Message,
                BggDiscoveryCount: bggDiscoveryCount
            );
        }
    }

    public async Task<IReadOnlyList<NightlyCatalogingExecutionLogDto>> GetExecutionHistoryAsync(int limit = 20, CancellationToken ct = default)
    {
        var logs = await _logRepo.GetRecentLogsAsync(limit, ct);
        return logs.Select(l =>
        {
            IReadOnlyList<string> titles;
            try
            {
                titles = JsonSerializer.Deserialize<List<string>>(l.CatalogedTitlesJson) ?? [];
            }
            catch
            {
                titles = [];
            }

            return new NightlyCatalogingExecutionLogDto(
                Id: l.Id,
                StartedAt: l.StartedAt,
                CompletedAt: l.CompletedAt,
                QueueProcessedCount: l.QueueProcessedCount,
                NewsDiscoveryCount: l.NewsDiscoveryCount,
                TopBackfillCount: l.TopBackfillCount,
                TotalCatalogedCount: l.TotalCatalogedCount,
                FailedCount: l.FailedCount,
                CatalogedTitles: titles,
                Status: l.Status,
                ErrorMessage: l.ErrorMessage,
                BggDiscoveryCount: l.BggDiscoveryCount
            );
        }).ToList();
    }

    public Task<NightlyCatalogingOptions> GetOptionsAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_options);
    }

    private async Task ApplyPoliteDelayAsync(CancellationToken ct)
    {
        if (_options.MinDelaySecondsBetweenCalls > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(_options.MinDelaySecondsBetweenCalls), ct);
        }
    }

    private async Task EnrichGamesWithAiSummaryBatchAsync(List<Game> games, CancellationToken ct)
    {
        if (_aiSummaryService == null || games.Count == 0) return;

        var aiChunks = games
            .Select((game, index) => new { game, index })
            .GroupBy(x => x.index / 10)
            .Select(g => g.Select(x => x.game).ToList())
            .ToList();

        foreach (var aiChunk in aiChunks)
        {
            try
            {
                var inputs = aiChunk.Select(g => new AiGameBatchInputDto(
                    BggId: g.BggId,
                    SpanishTitle: g.SpanishTitle,
                    OriginalTitle: g.OriginalTitle,
                    Designer: g.Designer,
                    Publisher: g.Publisher,
                    YearPublished: g.YearPublished,
                    Description: g.Description,
                    Rating: g.BggRating,
                    MinPlayers: g.Scalability.Count > 0 ? g.Scalability.Min(s => s.PlayerCount) : 1,
                    MaxPlayers: g.Scalability.Count > 0 ? g.Scalability.Max(s => s.PlayerCount) : 4,
                    MinAge: g.Age?.BoxAge ?? 10
                )).ToList();

                var batchResult = await _aiSummaryService.GenerateBatchSummariesAsync(inputs, ct);
                if (batchResult.Success && batchResult.Summaries.Count > 0)
                {
                    foreach (var g in aiChunk)
                    {
                        if (batchResult.Summaries.TryGetValue(g.BggId, out var summaryDto))
                        {
                            var aiVo = new AiGameSummary(
                                summaryDto.GeneralVerdict,
                                summaryDto.ScalabilitySummary,
                                summaryDto.AgeSummary,
                                summaryDto.FootprintSummary,
                                summaryDto.Model,
                                summaryDto.GeneratedAt ?? DateTime.UtcNow
                            );
                            g.SetAiSummary(aiVo);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error al generar resumen IA en lote para {Count} juegos. Se reintentará individualmente: {Message}",
                    aiChunk.Count, ex.Message);
            }

            // Fallback para juegos que no hayan obtenido resumen en el lote
            foreach (var g in aiChunk)
            {
                if (g.AiSummary == null)
                {
                    await EnrichWithAiSummarySafeAsync(g, ct);
                }
            }
        }
    }

    private async Task EnrichWithAiSummarySafeAsync(Game game, CancellationToken ct)
    {
        if (_aiSummaryService == null) return;

        try
        {
            var summary = await _aiSummaryService.GenerateSummaryAsync(game, ct);
            game.SetAiSummary(new AiGameSummary(
                summary.GeneralVerdict,
                summary.ScalabilitySummary,
                summary.AgeSummary,
                summary.FootprintSummary,
                summary.Model,
                summary.GeneratedAt ?? DateTime.UtcNow
            ));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "No se pudo generar síntesis de IA para '{Title}', continuando sin ella.", game.SpanishTitle);
        }
    }

    private async Task LinkPendingReleasesAsync(Game game, Guid gameId, CancellationToken ct)
    {
        try
        {
            var allReleases = await _releaseRepo.GetReleasesAsync(fromDate: null, ct: ct);
            var unlinked = allReleases.Where(r => !r.GameId.HasValue).ToList();

            foreach (var rel in unlinked)
            {
                var extracted = _newsExtractor.ExtractGameTitle(rel.Title, rel.Notes);
                if (!string.IsNullOrWhiteSpace(extracted) &&
                    (string.Equals(extracted, game.SpanishTitle, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(extracted, game.OriginalTitle, StringComparison.OrdinalIgnoreCase)))
                {
                    rel.LinkGame(gameId);
                    await _releaseRepo.UpdateAsync(rel, ct);
                    _logger.LogInformation("Lanzamiento #{RelId} vinculado retrospectivamente al juego '{Title}'.", rel.Id, game.SpanishTitle);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error al vincular lanzamientos retrospectivos para '{Title}'.", game.SpanishTitle);
        }
    }
}
