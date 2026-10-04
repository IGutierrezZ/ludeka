using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Options;
using Ludeka.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludeka.Application.Features.Media;

/// <summary>
/// Orquestador inteligente de auto-ingesta gradual de vídeos de YouTube para el catálogo prioritario sin cobertura audiovisual.
/// </summary>
public class YouTubeCatalogAutoIngestService : IYouTubeCatalogAutoIngestService
{
    private const string DenialMessage =
        "Se requiere el permiso de moderación 'CanApproveMedia' para ejecutar la auto-ingesta multimedia de catálogo.";

    private readonly IGameRepository _gameRepository;
    private readonly IYouTubeSearchService _youtubeSearchService;
    private readonly IOptions<YouTubeAutoIngestOptions> _options;
    private readonly ILogger<YouTubeCatalogAutoIngestService> _logger;
    private readonly ISessionPermissionGuard? _permissionGuard;

    public YouTubeCatalogAutoIngestService(
        IGameRepository gameRepository,
        IYouTubeSearchService youtubeSearchService,
        IOptions<YouTubeAutoIngestOptions> options,
        ILogger<YouTubeCatalogAutoIngestService> logger,
        ISessionPermissionGuard? permissionGuard = null)
    {
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _youtubeSearchService = youtubeSearchService ?? throw new ArgumentNullException(nameof(youtubeSearchService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _permissionGuard = permissionGuard;
    }

    private Task RequirePermissionAsync(CancellationToken ct)
        => _permissionGuard is null
            ? Task.CompletedTask
            : _permissionGuard.RequireAsync(ModeratorPermission.CanApproveMedia, DenialMessage, ct);

    public async Task<YouTubeCatalogAutoIngestResultDto> ExecuteAutoIngestAsync(int? customLimit = null, int? maxRank = null, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);
        return await RunScheduledAutoIngestAsync(customLimit, maxRank, ct);
    }

    public async Task<YouTubeCatalogAutoIngestResultDto> RunScheduledAutoIngestAsync(int? customLimit = null, int? maxRank = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var opts = _options.Value;

        if (!opts.Enabled)
        {
            _logger.LogInformation("La auto-ingesta periódica de YouTube está deshabilitada en la configuración.");
            return new YouTubeCatalogAutoIngestResultDto(0, 0, 0, 0, TimeSpan.Zero, DateTimeOffset.UtcNow);
        }

        var limit = customLimit.HasValue && customLimit.Value > 0 ? customLimit.Value : opts.DailyGamesLimit;
        var rank = maxRank.HasValue && maxRank.Value > 0 ? maxRank.Value : opts.MaxBggRank;

        _logger.LogInformation(
            "Iniciando ciclo de auto-ingesta de YouTube para juegos sin vídeos (Límite: {Limit}, MaxRank: {MaxRank}, AutoApprove: {AutoApprove}).",
            limit, rank, opts.AutoApprove);

        var candidateGames = await _gameRepository.GetTopRankedGamesWithoutVideosAsync(maxRank: rank, limit: limit, ct: ct);

        int gamesEvaluated = candidateGames.Count;
        int videosIngested = 0;
        int skippedCount = 0;
        int errorsCount = 0;

        foreach (var game in candidateGames)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                var ingested = await _youtubeSearchService.AutoSuggestAndIngestConsolidatedForGameAsync(
                    game.Id,
                    autoApprove: opts.AutoApprove,
                    ct: ct);

                if (ingested.Count > 0)
                {
                    videosIngested += ingested.Count;
                    _logger.LogInformation(
                        "Auto-ingestados {Count} vídeos para '{GameTitle}' (ID {GameId}, Rank #{Rank}).",
                        ingested.Count, game.SpanishTitle ?? game.OriginalTitle, game.Id, game.BggRank);
                }
                else
                {
                    skippedCount++;
                    _logger.LogDebug(
                        "No se hallaron candidatos adecuados de YouTube para '{GameTitle}' (ID {GameId}).",
                        game.SpanishTitle ?? game.OriginalTitle, game.Id);
                }

                if (opts.DelayBetweenGamesMs > 0 && !ct.IsCancellationRequested)
                {
                    await Task.Delay(opts.DelayBetweenGamesMs, ct);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errorsCount++;
                _logger.LogWarning(ex,
                    "Error al auto-ingestar vídeos para el juego '{GameTitle}' (ID {GameId}).",
                    game.SpanishTitle ?? game.OriginalTitle, game.Id);
            }
        }

        sw.Stop();

        var result = new YouTubeCatalogAutoIngestResultDto(
            GamesEvaluated: gamesEvaluated,
            VideosIngested: videosIngested,
            SkippedCount: skippedCount,
            ErrorsCount: errorsCount,
            Duration: sw.Elapsed,
            ExecutedAt: DateTimeOffset.UtcNow);

        _logger.LogInformation(
            "Ciclo de auto-ingesta de YouTube finalizado en {Duration:F1}s: {Evaluated} juegos evaluados, {Ingested} vídeos ingestados, {Skipped} sin vídeos encontrados, {Errors} errores.",
            result.Duration.TotalSeconds, result.GamesEvaluated, result.VideosIngested, result.SkippedCount, result.ErrorsCount);

        return result;
    }
}
