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
        IBggDiscoveryService? discoveryService = null)
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
    }

    public async Task<NightlyCatalogingResultDto> ExecuteNightlyCatalogingAsync(int? customLimit = null, CancellationToken ct = default)
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

            // --- FASE 1.5: Auto-descubrimiento de Novedades y Tendencias BGG ---
            if (_discoveryService != null)
            {
                try
                {
                    var discoveryResult = await _discoveryService.DiscoverAndEnqueueBggTrendsAsync(maxItems: 50, ct);
                    bggDiscoveryCount = discoveryResult.EnqueuedCount;
                    _logger.LogInformation("Fase 1.5 completada: {Count} tendencias/lanzamientos BGG descubiertos y encolados.", bggDiscoveryCount);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Advertencia en Fase 1.5 (Auto-descubrimiento BGG): {Message}", ex.Message);
                }
            }

            // --- FASE 2: Procesamiento de Cola Prioritaria (Usuarios, Novedades y Tendencias BGG) ---
            var topPending = await _pendingRepo.GetTopPendingAsync(limit, ct);
            _logger.LogInformation("Fase 2: {Count} juegos pendientes encontrados en la cola prioritaria.", topPending.Count);

            foreach (var pending in topPending)
            {
                if (catalogedTitles.Count >= limit) break;

                await ApplyPoliteDelayAsync(ct);

                pending.MarkAsProcessing();
                await _pendingRepo.UpdateAsync(pending, ct);

                try
                {
                    var fetchedGame = await _bggClient.FetchGameByBggIdAsync(pending.BggId, ct);
                    if (fetchedGame == null)
                    {
                        throw new InvalidOperationException($"BGG no devolvió información para el juego #{pending.BggId}.");
                    }

                    var existingGame = await _gameRepo.GetByBggIdAsync(pending.BggId, ct);
                    Guid gameId;

                    if (existingGame == null)
                    {
                        await EnrichWithAiSummarySafeAsync(fetchedGame, ct);
                        await _gameRepo.AddRangeAsync([fetchedGame], ct);
                        gameId = fetchedGame.Id;
                    }
                    else
                    {
                        gameId = existingGame.Id;
                        if (existingGame.AiSummary == null)
                        {
                            await EnrichWithAiSummarySafeAsync(existingGame, ct);
                            await _gameRepo.UpdateAsync(existingGame, ct);
                        }
                    }

                    // Promoción atómica de colecciones comunitarias
                    await _collectionRepo.PromotePendingItemsAsync(pending.BggId, gameId, ct);

                    // Vinculación con novedades pendientes que hagan referencia a este juego
                    await LinkPendingReleasesAsync(fetchedGame, gameId, ct);

                    pending.MarkAsCompleted();
                    await _pendingRepo.UpdateAsync(pending, ct);

                    catalogedTitles.Add(fetchedGame.SpanishTitle);
                    queueProcessedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al catalogar juego #{BggId} ('{Title}') de la cola: {Message}",
                        pending.BggId, pending.Title, ex.Message);
                    pending.MarkAsFailed(ex.Message);
                    await _pendingRepo.UpdateAsync(pending, ct);
                    failedCount++;
                }
            }

            // --- FASE 3: Drenaje Progresivo de Staging Masivo y Relleno de Catálogo ---
            if (_massIngestionService != null)
            {
                try
                {
                    _logger.LogInformation("Fase 3: Ejecutando ciclo de drenaje de staging masivo (detalles, fotos GeekDo/R2, IA por lotes y promoción).");
                    var drainResult = await _massIngestionService.RunDrainCycleAsync(ct);
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

                    foreach (var topGame in topGames)
                    {
                        if (catalogedTitles.Count >= limit) break;

                        // Verificar si ya existe en catálogo
                        var inCatalog = await _gameRepo.GetByBggIdAsync(topGame.BggId, ct);
                        if (inCatalog != null) continue;

                        // Verificar si ya está en cola completado
                        var existingQueue = await _pendingRepo.GetByBggIdAsync(topGame.BggId, ct);
                        if (existingQueue != null && existingQueue.Status == CatalogQueueStatus.Completed) continue;

                        await ApplyPoliteDelayAsync(ct);

                        try
                        {
                            var fetchedGame = await _bggClient.FetchGameByBggIdAsync(topGame.BggId, ct);
                            if (fetchedGame == null) continue;

                            await EnrichWithAiSummarySafeAsync(fetchedGame, ct);
                            await _gameRepo.AddRangeAsync([fetchedGame], ct);

                            // Registrar o actualizar en la cola como TopBggBackfill
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
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Fallo al catalogar juego #{BggId} del Top BGG: {Message}", topGame.BggId, ex.Message);
                            failedCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al consultar el Top de BGG en Fase 3: {Message}", ex.Message);
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
