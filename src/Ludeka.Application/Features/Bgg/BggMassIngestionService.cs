using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
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
/// Orquestador principal de la ingesta masiva de catálogo de BGG, imágenes comunitarias de GeekDo,
/// síntesis IA con Gemini Flash en lotes y promoción transaccional a la tabla definitiva de catálogo Games.
/// </summary>
public class BggMassIngestionService : IBggMassIngestionService
{
    private const string DenialMessage =
        "Se requiere el permiso de moderación 'CanEditGames' para ejecutar el drenaje del staging de catálogo.";

    private readonly IBggCatalogStagingRepository _stagingRepo;
    private readonly IBggClient _bggClient;
    private readonly IGeekDoImagesClient _geekDoClient;
    private readonly IImageStorageService _imageStorageService;
    private readonly IAiGameSummaryService _aiSummaryService;
    private readonly IGameRepository _gameRepo;
    private readonly HttpClient _httpClient;
    private readonly BggMassIngestionOptions _options;
    private readonly ILogger<BggMassIngestionService> _logger;
    private readonly ISessionPermissionGuard? _permissionGuard;

    public BggMassIngestionService(
        IBggCatalogStagingRepository stagingRepo,
        IBggClient bggClient,
        IGeekDoImagesClient geekDoClient,
        IImageStorageService imageStorageService,
        IAiGameSummaryService aiSummaryService,
        IGameRepository gameRepo,
        HttpClient httpClient,
        IOptions<BggMassIngestionOptions> options,
        ILogger<BggMassIngestionService> logger,
        ISessionPermissionGuard? permissionGuard = null)
    {
        _stagingRepo = stagingRepo ?? throw new ArgumentNullException(nameof(stagingRepo));
        _bggClient = bggClient ?? throw new ArgumentNullException(nameof(bggClient));
        _geekDoClient = geekDoClient ?? throw new ArgumentNullException(nameof(geekDoClient));
        _imageStorageService = imageStorageService ?? throw new ArgumentNullException(nameof(imageStorageService));
        _aiSummaryService = aiSummaryService ?? throw new ArgumentNullException(nameof(aiSummaryService));
        _gameRepo = gameRepo ?? throw new ArgumentNullException(nameof(gameRepo));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? new BggMassIngestionOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _permissionGuard = permissionGuard;
    }

    /// <summary>
    /// Revalida sesión y permiso releyendo el <c>AppUser</c> actual (INC-46, W1): el drenaje se dispara
    /// desde el panel de cola; el ciclo nocturno usa la ruta de sistema. Las fases por lote son
    /// primitivas internas del propio ciclo y no se exponen a la interfaz.
    /// </summary>
    private Task RequirePermissionAsync(CancellationToken ct)
        => _permissionGuard is null
            ? Task.CompletedTask
            : _permissionGuard.RequireAsync(ModeratorPermission.CanEditGames, DenialMessage, ct);

    public async Task<int> IngestRanksDumpAsync(Stream dumpStream, int minUsersRated = 1000, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dumpStream);
        if (minUsersRated <= 0) minUsersRated = _options.MinUsersRated;

        _logger.LogInformation("Iniciando procesamiento de volcado BGG con umbral usersrated >= {MinVotes}", minUsersRated);

        int totalIngested = 0;
        var batch = new List<BggCatalogStagingItem>();
        var seenBggIds = new HashSet<int>();

        await foreach (var row in BggDumpParser.ParseRanksDumpAsync(dumpStream, minUsersRated, ct))
        {
            if (!seenBggIds.Add(row.BggId))
            {
                continue;
            }

            var item = new BggCatalogStagingItem(
                bggId: row.BggId,
                originalTitle: row.Title,
                yearPublished: row.YearPublished,
                bggRank: row.BggRank,
                usersRated: row.UsersRated,
                bayesAverage: row.BayesAverage,
                averageRating: row.AverageRating,
                spanishTitle: row.Title
            );

            batch.Add(item);

            if (batch.Count >= 100)
            {
                await _stagingRepo.UpsertBatchAsync(batch, ct);
                totalIngested += batch.Count;
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await _stagingRepo.UpsertBatchAsync(batch, ct);
            totalIngested += batch.Count;
            batch.Clear();
        }

        _logger.LogInformation("Volcado BGG finalizado. Se han insertado/actualizado {Count} títulos en staging.", totalIngested);
        return totalIngested;
    }

    public async Task<int> ProcessPendingDetailsBatchAsync(int batchSize = 20, CancellationToken ct = default)
    {
        if (batchSize <= 0) batchSize = _options.FetchBatchSize;

        var pendingItems = await _stagingRepo.GetPendingFetchBatchAsync(batchSize, ct);
        if (pendingItems.Count == 0) return 0;

        _logger.LogInformation("Obteniendo detalles Thing de BGG para {Count} juegos en staging.", pendingItems.Count);
        int processedCount = 0;

        foreach (var item in pendingItems)
        {
            if (ct.IsCancellationRequested) break;

            item.MarkFetchInProgress();
            try
            {
                var fetchedGame = await _bggClient.FetchGameByBggIdAsync(item.BggId, ct);
                if (fetchedGame != null)
                {
                    item.MarkFetched(
                        rawXml: fetchedGame.Description,
                        spanishTitle: fetchedGame.SpanishTitle,
                        designer: fetchedGame.Designer,
                        publisher: fetchedGame.Publisher,
                        description: fetchedGame.Description,
                        minPlayers: fetchedGame.Scalability.Count > 0 ? fetchedGame.Scalability.Min(s => s.PlayerCount) : 1,
                        maxPlayers: fetchedGame.Scalability.Count > 0 ? fetchedGame.Scalability.Max(s => s.PlayerCount) : 4,
                        playingTimeMinutes: fetchedGame.Duration.EstimatedPerPlayerMinutes,
                        minAge: fetchedGame.Age.BoxAge,
                        bggRating: fetchedGame.BggRating
                    );

                    // Si ya viene con carátula de BGG Thing, se preasignan URLs iniciales si aún no hay fotos
                    if (!string.IsNullOrWhiteSpace(fetchedGame.CoverImageUrl) && string.IsNullOrWhiteSpace(item.CoverImageUrl))
                    {
                        item.MarkImagesCompleted(fetchedGame.CoverImageUrl, fetchedGame.ThumbnailUrl);
                    }

                    processedCount++;
                }
                else
                {
                    item.MarkStageFailed("fetch", "BGG no devolvió información para este título.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener detalles Thing para #{BggId} ('{Title}'): {Message}", item.BggId, item.OriginalTitle, ex.Message);
                item.MarkStageFailed("fetch", ex.Message);
            }
        }

        await _stagingRepo.UpdateBatchAsync(pendingItems, ct);
        return processedCount;
    }

    public async Task<int> ProcessPendingImagesBatchAsync(int batchSize = 10, CancellationToken ct = default)
    {
        if (batchSize <= 0) batchSize = _options.ImagesBatchSize;

        var pendingItems = await _stagingRepo.GetPendingImagesBatchAsync(batchSize, ct);
        if (pendingItems.Count == 0) return 0;

        _logger.LogInformation("Procesando galería comunitaria GeekDo y R2 para {Count} juegos en staging.", pendingItems.Count);
        int processedCount = 0;

        foreach (var item in pendingItems)
        {
            if (ct.IsCancellationRequested) break;

            item.MarkImagesInProgress();
            try
            {
                var gallery = await _geekDoClient.GetTopVotedImagesAsync(item.BggId, ct);

                string? coverUrl = item.CoverImageUrl;
                string? thumbUrl = item.ThumbnailUrl;
                string? backUrl = item.BackCoverImageUrl;
                string? tableUrl = item.TableImageUrl;

                // 1. Portada frontal
                if (!string.IsNullOrWhiteSpace(gallery.FrontCoverUrl))
                {
                    try
                    {
                        using var imgStream = await DownloadImageStreamAsync(gallery.FrontCoverUrl, ct);
                        if (imgStream != null)
                        {
                            var variants = await _imageStorageService.UploadGameImageVariantsAsync(imgStream, item.BggId, "cover", ct);
                            coverUrl = variants.FullUrl;
                            thumbUrl = variants.ThumbUrl;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "No se pudo optimizar portada GeekDo para #{BggId}: {Msg}", item.BggId, ex.Message);
                    }
                }

                // 2. Contraportada / trasera
                if (!string.IsNullOrWhiteSpace(gallery.BackCoverUrl))
                {
                    try
                    {
                        using var imgStream = await DownloadImageStreamAsync(gallery.BackCoverUrl, ct);
                        if (imgStream != null)
                        {
                            var variants = await _imageStorageService.UploadGameImageVariantsAsync(imgStream, item.BggId, "back", ct);
                            backUrl = variants.FullUrl;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "No se pudo optimizar contraportada GeekDo para #{BggId}: {Msg}", item.BggId, ex.Message);
                    }
                }

                // 3. Mesa / componentes
                if (!string.IsNullOrWhiteSpace(gallery.TableOrGameplayUrl))
                {
                    try
                    {
                        using var imgStream = await DownloadImageStreamAsync(gallery.TableOrGameplayUrl, ct);
                        if (imgStream != null)
                        {
                            var variants = await _imageStorageService.UploadGameImageVariantsAsync(imgStream, item.BggId, "table", ct);
                            tableUrl = variants.FullUrl;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "No se pudo optimizar foto en mesa GeekDo para #{BggId}: {Msg}", item.BggId, ex.Message);
                    }
                }

                if (!string.IsNullOrWhiteSpace(coverUrl))
                {
                    item.MarkImagesCompleted(coverUrl, thumbUrl, backUrl, tableUrl);
                }
                else
                {
                    item.MarkImagesSkipped();
                }

                processedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fallo al procesar imágenes de GeekDo para #{BggId}: {Message}", item.BggId, ex.Message);
                item.MarkStageFailed("images", ex.Message);
            }
        }

        await _stagingRepo.UpdateBatchAsync(pendingItems, ct);
        return processedCount;
    }

    public async Task<AiBatchProcessingResultDto> ProcessPendingAiBatchAsync(int gamesPerBatch = 8, int maxBatches = 5, CancellationToken ct = default)
    {
        if (gamesPerBatch <= 0) gamesPerBatch = _options.AiBatchSize;
        if (maxBatches <= 0) maxBatches = 1;

        int totalProcessed = 0;
        int successCount = 0;
        int failedCount = 0;
        var summarizedTitles = new List<string>();

        for (int b = 0; b < maxBatches; b++)
        {
            if (ct.IsCancellationRequested) break;

            var pendingItems = await _stagingRepo.GetPendingAiBatchAsync(gamesPerBatch, ct);
            if (pendingItems.Count == 0) break;

            var inputs = pendingItems.Select(item => new AiGameBatchInputDto(
                BggId: item.BggId,
                SpanishTitle: item.SpanishTitle ?? item.OriginalTitle,
                OriginalTitle: item.OriginalTitle,
                Designer: item.Designer,
                Publisher: item.Publisher,
                YearPublished: item.YearPublished ?? 0,
                Description: item.Description,
                Rating: item.AverageRating > 0 ? item.AverageRating : item.BayesAverage,
                MinPlayers: item.MinPlayers > 0 ? item.MinPlayers : 1,
                MaxPlayers: item.MaxPlayers > 0 ? item.MaxPlayers : 4,
                MinAge: item.MinAge
            )).ToList();

            foreach (var p in pendingItems) p.MarkAiInProgress();

            var batchResult = await _aiSummaryService.GenerateBatchSummariesAsync(inputs, ct);

            if (batchResult.QuotaExhausted)
            {
                _logger.LogWarning("Gemini Flash reportó cuota diaria alcanzada (HTTP 429). Marcando {Count} juegos en QuotaExceeded para reanudar mañana.", pendingItems.Count);
                foreach (var p in pendingItems)
                {
                    p.MarkAiQuotaExceeded();
                }
                await _stagingRepo.UpdateBatchAsync(pendingItems, ct);
                break;
            }

            if (batchResult.Success)
            {
                foreach (var item in pendingItems)
                {
                    if (batchResult.Summaries.TryGetValue(item.BggId, out var summary))
                    {
                        string json = JsonSerializer.Serialize(summary);
                        item.MarkAiCompleted(json);
                        summarizedTitles.Add(item.SpanishTitle ?? item.OriginalTitle);
                        successCount++;
                    }
                    else
                    {
                        item.MarkAiSkipped();
                    }
                }
            }
            else
            {
                foreach (var item in pendingItems)
                {
                    item.MarkStageFailed("ai", batchResult.ErrorMessage ?? "Error en lote de IA");
                    failedCount++;
                }
            }

            totalProcessed += pendingItems.Count;
            await _stagingRepo.UpdateBatchAsync(pendingItems, ct);
        }

        return new AiBatchProcessingResultDto(
            ProcessedCount: totalProcessed,
            SuccessCount: successCount,
            FailedCount: failedCount,
            SummarizedTitles: summarizedTitles
        );
    }

    public async Task<int> PromoteReadyToCatalogBatchAsync(int batchSize = 50, CancellationToken ct = default)
    {
        if (batchSize <= 0) batchSize = _options.PromotionBatchSize;

        var readyItems = await _stagingRepo.GetPendingPromotionBatchAsync(batchSize, ct);
        if (readyItems.Count == 0) return 0;

        _logger.LogInformation("Promoviendo {Count} juegos listos de staging hacia el catálogo definitivo.", readyItems.Count);
        int promotedCount = 0;

        foreach (var item in readyItems)
        {
            if (ct.IsCancellationRequested) break;

            item.MarkPromotionInProgress();

            try
            {
                var existing = await _gameRepo.GetByBggIdAsync(item.BggId, ct);
                if (existing == null)
                {
                    var newGame = new Game(
                        bggId: item.BggId,
                        originalTitle: item.OriginalTitle,
                        spanishTitle: item.SpanishTitle ?? item.OriginalTitle,
                        designer: item.Designer ?? "Desconocido",
                        publisher: item.Publisher ?? "Desconocida",
                        yearPublished: item.YearPublished ?? 2000,
                        coverImageUrl: item.CoverImageUrl,
                        thumbnailUrl: item.ThumbnailUrl,
                        description: item.Description,
                        bggRating: item.AverageRating > 0 ? item.AverageRating : item.BayesAverage,
                        bggRank: item.BggRank,
                        ludistRating: 0.0,
                        confrontation: ConfrontationType.Competitive,
                        style: GameStyle.Eurogame,
                        isOfficialSolo: item.MinPlayers == 1,
                        age: new AgeRating(item.MinAge > 0 ? item.MinAge : 10, item.MinAge > 0 ? item.MinAge : 10),
                        language: LanguageDependence.Low,
                        footprint: TableFootprint.StandardTable,
                        duration: new GameDuration(
                            item.PlayingTimeMinutes > 0 ? item.PlayingTimeMinutes : 30,
                            item.PlayingTimeMinutes > 0 ? (int)(item.PlayingTimeMinutes * 1.5) : 60,
                            item.PlayingTimeMinutes > 0 ? Math.Max(15, item.PlayingTimeMinutes / Math.Max(1, item.MaxPlayers)) : 30),
                        backCoverImageUrl: item.BackCoverImageUrl,
                        tableImageUrl: item.TableImageUrl
                    );

                    // Rehidratar síntesis de IA si existe
                    if (!string.IsNullOrWhiteSpace(item.AiSummaryJson))
                    {
                        try
                        {
                            var parsedAi = JsonSerializer.Deserialize<AiGameSummaryDto>(item.AiSummaryJson);
                            if (parsedAi != null)
                            {
                                newGame.SetAiSummary(new AiGameSummary(
                                    GeneralVerdict: parsedAi.GeneralVerdict,
                                    ScalabilitySummary: parsedAi.ScalabilitySummary,
                                    AgeSummary: parsedAi.AgeSummary,
                                    FootprintSummary: parsedAi.FootprintSummary,
                                    Model: parsedAi.Model,
                                    GeneratedAt: parsedAi.GeneratedAt ?? DateTime.UtcNow
                                ));
                            }
                        }
                        catch
                        {
                            // Ignorar error de deserialización de IA
                        }
                    }

                    await _gameRepo.AddRangeAsync([newGame], ct);
                }
                else
                {
                    existing.UpdateMediaUrls(
                        item.CoverImageUrl ?? existing.CoverImageUrl,
                        item.ThumbnailUrl ?? existing.ThumbnailUrl,
                        item.BackCoverImageUrl ?? existing.BackCoverImageUrl,
                        item.TableImageUrl ?? existing.TableImageUrl
                    );
                    await _gameRepo.UpdateAsync(existing, ct);
                }

                item.MarkPromoted();
                promotedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al promover juego #{BggId} ('{Title}') al catálogo: {Message}", item.BggId, item.OriginalTitle, ex.Message);
                item.MarkStageFailed("promotion", ex.Message);
            }
        }

        await _stagingRepo.UpdateBatchAsync(readyItems, ct);
        return promotedCount;
    }

    public async Task<BggStagingMetricsDto> GetMetricsAsync(CancellationToken ct = default)
    {
        return await _stagingRepo.GetMetricsAsync(ct);
    }

    public async Task<BggMassIngestionCycleResultDto> RunDrainCycleAsync(CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        return await RunScheduledDrainCycleAsync(ct);
    }

    /// <inheritdoc />
    public async Task<BggMassIngestionCycleResultDto> RunScheduledDrainCycleAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Iniciando ciclo progresivo de drenaje de staging de catálogo...");

        // 1. Detalles Thing (hasta 20)
        int detailsFetched = await ProcessPendingDetailsBatchAsync(_options.FetchBatchSize, ct);

        // 2. Galería de imágenes (hasta 10)
        int imagesProcessed = await ProcessPendingImagesBatchAsync(_options.ImagesBatchSize, ct);

        // 3. Síntesis IA en lote
        var aiResult = await ProcessPendingAiBatchAsync(_options.AiBatchSize, maxBatches: 2, ct);

        // 4. Promoción a Games
        int promoted = await PromoteReadyToCatalogBatchAsync(_options.PromotionBatchSize, ct);

        string message = $"Ciclo de drenaje: {detailsFetched} detalles, {imagesProcessed} imágenes, {aiResult.SuccessCount} síntesis IA, {promoted} promovidos al catálogo.";
        _logger.LogInformation(message);

        return new BggMassIngestionCycleResultDto(
            DetailsFetchedCount: detailsFetched,
            ImagesProcessedCount: imagesProcessed,
            AiProcessedCount: aiResult.SuccessCount,
            PromotedToCatalogCount: promoted,
            AiQuotaExhausted: aiResult.FailedCount > 0 && aiResult.ProcessedCount == 0,
            Message: message
        );
    }

    private async Task<Stream?> DownloadImageStreamAsync(string imageUrl, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, imageUrl);
            req.Headers.Add("User-Agent", "Ludeka/1.0");

            var response = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) return null;

            var ms = new MemoryStream();
            await response.Content.CopyToAsync(ms, ct);
            ms.Position = 0;
            return ms;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<int> DownloadAndIngestLatestRanksAsync(int? minUsersRated = null, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);
        return await ExecuteDownloadAndIngestAsync(minUsersRated, ct);
    }

    /// <inheritdoc />
    public Task<int> RunScheduledDownloadAndIngestLatestRanksAsync(int? minUsersRated = null, CancellationToken ct = default)
    {
        return ExecuteDownloadAndIngestAsync(minUsersRated, ct);
    }

    /// <inheritdoc />
    public async Task ClearStagingAsync(CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);
        _logger.LogInformation("Vaciando completamente la tabla BggCatalogStaging por petición administrativa.");
        await _stagingRepo.ClearStagingAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> ResetQuotaExceededStatusAsync(CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);
        _logger.LogInformation("Restableciendo el estado de títulos en QuotaExceeded para reanudar la síntesis IA.");
        await _stagingRepo.ResetQuotaExceededStatusAsync(ct);
        var metrics = await _stagingRepo.GetMetricsAsync(ct);
        return metrics.PendingAiCount;
    }

    private async Task<int> ExecuteDownloadAndIngestAsync(int? minUsersRated, CancellationToken ct)
    {
        int threshold = minUsersRated.HasValue && minUsersRated.Value > 0 ? minUsersRated.Value : _options.MinUsersRated;

        if (_options.Simulate)
        {
            _logger.LogInformation("Modo simulado activo: ingiriendo dataset sintético de catálogo BGG en staging.");
            return await IngestSyntheticSampleAsync(threshold, ct);
        }

        var (stream, resolvedDate) = await OpenLatestRanksStreamAsync(ct);
        await using (stream.ConfigureAwait(false))
        {
            _logger.LogInformation("Procesando volcado BGG correspondiente a la fecha {Date:yyyy-MM-dd} con umbral usersrated >= {MinVotes}",
                resolvedDate, threshold);

            return await IngestRanksDumpAsync(stream, threshold, ct);
        }
    }

    private async Task<(Stream Stream, DateTime ResolvedDate)> OpenLatestRanksStreamAsync(CancellationToken ct)
    {
        var currentDate = DateTime.UtcNow.Date;
        int maxFallback = Math.Max(0, _options.MaxFallbackDays);
        var testedUrls = new List<string>();

        for (int attempt = 0; attempt <= maxFallback; attempt++)
        {
            var date = currentDate.AddDays(-attempt);
            string url = string.Format(CultureInfo.InvariantCulture, _options.RanksDumpUrlPattern, date);
            testedUrls.Add(url);

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", "Ludeka/1.0");

                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Volcado BGG encontrado exitosamente en {Url} tras {Attempts} intento(s).", url, attempt + 1);
                    var stream = await response.Content.ReadAsStreamAsync(ct);
                    return (stream, date);
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("Volcado BGG no encontrado (404) para {Date:yyyy-MM-dd} en {Url}. Probando día anterior...", date, url);
                    response.Dispose();
                    continue;
                }

                _logger.LogWarning("Respuesta inesperada {StatusCode} al consultar volcado BGG en {Url}.", response.StatusCode, url);
                response.Dispose();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Error al intentar obtener volcado BGG para {Date:yyyy-MM-dd} en {Url}.", date, url);
            }
        }

        throw new InvalidOperationException(
            $"No se pudo obtener ningún volcado de clasificación de BGG tras comprobar {testedUrls.Count} fecha(s). URLs probadas: {string.Join(", ", testedUrls)}");
    }

    private async Task<int> IngestSyntheticSampleAsync(int minUsersRated, CancellationToken ct)
    {
        string sampleCsv = """
            id,name,yearpublished,rank,bayesaverage,average,usersrated
            174430,"Gloomhaven",2017,1,8.42,8.61,62000
            224517,"Brass: Birmingham",2018,2,8.41,8.60,48000
            342942,"Ark Nova",2021,3,8.35,8.53,42000
            167791,"Terraforming Mars",2016,4,8.25,8.39,95000
            999999,"Prototipo Olvidado",2024,15000,5.1,5.2,14
            """;

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(sampleCsv));
        return await IngestRanksDumpAsync(ms, minUsersRated, ct);
    }
}
