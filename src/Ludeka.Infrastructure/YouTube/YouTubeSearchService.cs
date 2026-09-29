using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ludeka.Infrastructure.YouTube;

public class YouTubeSearchService : IYouTubeSearchService
{
    private const string DenialMessage =
        "Se requiere el permiso de moderación 'CanApproveMedia' para vincular vídeos al catálogo multimedia.";

    private readonly HttpClient _httpClient;
    private readonly YouTubeOptions _options;
    private readonly IChannelFocusProvider _channelFocus;
    private readonly IGameRepository _gameRepository;
    private readonly IMediaRepository _mediaRepository;
    private readonly ILogger<YouTubeSearchService> _logger;
    private readonly ISessionPermissionGuard? _permissionGuard;

    public YouTubeSearchService(
        HttpClient httpClient,
        IOptions<YouTubeOptions> options,
        IChannelFocusProvider channelFocus,
        IGameRepository gameRepository,
        IMediaRepository mediaRepository,
        ILogger<YouTubeSearchService> logger,
        ISessionPermissionGuard? permissionGuard = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? new YouTubeOptions();
        _channelFocus = channelFocus ?? throw new ArgumentNullException(nameof(channelFocus));
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _mediaRepository = mediaRepository ?? throw new ArgumentNullException(nameof(mediaRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _permissionGuard = permissionGuard;

        if (_httpClient.BaseAddress == null && Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            _httpClient.BaseAddress = baseUri;
        }
    }

    /// <summary>
    /// Revalida sesión y permiso releyendo el <c>AppUser</c> actual (INC-46, W1): la vinculación de
    /// vídeos escribe piezas multimedia y vive en el panel de moderación.
    /// </summary>
    private Task RequirePermissionAsync(CancellationToken ct)
        => _permissionGuard is null
            ? Task.CompletedTask
            : _permissionGuard.RequireAsync(ModeratorPermission.CanApproveMedia, DenialMessage, ct);

    public async Task<IReadOnlyList<YouTubeSearchResultDto>> SearchVideosForGameAsync(Guid gameId, CancellationToken ct = default)
    {
        var game = await _gameRepository.GetByIdAsync(gameId, ct);
        if (game == null)
        {
            return Array.Empty<YouTubeSearchResultDto>();
        }

        var searchTitle = !string.IsNullOrWhiteSpace(game.SpanishTitle) ? game.SpanishTitle : game.OriginalTitle;

        var quickTask = SearchQuickOverviewsAsync(searchTitle, ct);
        var tutTask = SearchTutorialsAsync(searchTitle, ct);
        var playTask = SearchPlaythroughsAsync(searchTitle, game.Id, ct);

        await Task.WhenAll(quickTask, tutTask, playTask);

        var combined = new List<YouTubeSearchResultDto>();
        combined.AddRange(quickTask.Result);
        combined.AddRange(tutTask.Result);
        combined.AddRange(playTask.Result);

        return combined
            .GroupBy(x => x.VideoId)
            .Select(g => g.First())
            .OrderByDescending(x => x.RelevanceScore)
            .ToList();
    }

    public async Task<IReadOnlyList<YouTubeSearchResultDto>> SearchQuickOverviewsAsync(string gameTitle, CancellationToken ct = default)
    {
        var query = $"{gameTitle} cómo funciona mecánicas en 2 minutos";
        var results = await ExecuteSearchAsync(query, gameTitle, MediaType.QuickOverview, null, ct);

        // Filtrar y priorizar vídeos breves (<= 180s / 3 min)
        return results
            .Select(r => r with
            {
                SuggestedType = MediaType.QuickOverview,
                RelevanceScore = r.DurationSeconds is <= 210 ? r.RelevanceScore + 20 : r.RelevanceScore
            })
            .OrderByDescending(r => r.RelevanceScore)
            .Take(5)
            .ToList();
    }

    public async Task<IReadOnlyList<YouTubeSearchResultDto>> SearchTutorialsAsync(string gameTitle, CancellationToken ct = default)
    {
        var query = $"{gameTitle} cómo jugar tutorial español";
        var results = await ExecuteSearchAsync(query, gameTitle, MediaType.Tutorial, null, ct);

        // Ponderar rango de 8 a 25 min (480s a 1500s)
        return results
            .Select(r => r with
            {
                SuggestedType = MediaType.Tutorial,
                RelevanceScore = r.DurationSeconds is >= 480 and <= 1500 ? r.RelevanceScore + 20 : r.RelevanceScore
            })
            .OrderByDescending(r => r.RelevanceScore)
            .Take(5)
            .ToList();
    }

    public async Task<IReadOnlyList<YouTubeSearchResultDto>> SearchPlaythroughsAsync(string gameTitle, Guid? gameId = null, CancellationToken ct = default)
    {
        Game? game = null;
        if (gameId.HasValue)
        {
            game = await _gameRepository.GetByIdAsync(gameId.Value, ct);
        }

        var query = $"{gameTitle} partida completa español";
        var results = await ExecuteSearchAsync(query, gameTitle, MediaType.Playthrough, game, ct);

        return results
            .Select(r =>
            {
                var badge = PlayerCountExtractor.ExtractPlayerBadge(r.Title, r.Description, game);
                return r with
                {
                    SuggestedType = MediaType.Playthrough,
                    ExtractedPlayerBadge = badge,
                    RelevanceScore = r.RelevanceScore + (badge != "Partida a 2" ? 10 : 0)
                };
            })
            .OrderByDescending(r => r.RelevanceScore)
            .Take(5)
            .ToList();
    }

    public async Task<MediaItemDto> IngestVideoAsync(YouTubeIngestRequestDto request, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);
        ArgumentNullException.ThrowIfNull(request);

        var game = await _gameRepository.GetByIdAsync(request.GameId, ct);
        if (game == null)
        {
            throw new InvalidOperationException($"El juego con ID '{request.GameId}' no existe en el catálogo.");
        }

        // Comprobación de duplicados
        if (await _mediaRepository.ExistsByUrlAsync(request.Url, ct))
        {
            var existingItems = await _mediaRepository.GetAllAsync(ct);
            var found = existingItems.FirstOrDefault(m => string.Equals(m.Url, request.Url.Trim(), StringComparison.OrdinalIgnoreCase));
            if (found != null)
            {
                return MediaItemDto.FromDomain(found, game.SpanishTitle);
            }
        }

        // Invariante obligatoria para partidas completas
        var badge = request.PlayerCountBadge;
        if (request.Type == MediaType.Playthrough && string.IsNullOrWhiteSpace(badge))
        {
            badge = PlayerCountExtractor.ExtractPlayerBadge(request.Title, null, game);
        }

        var status = request.AutoApprove ? ModerationStatus.Approved : ModerationStatus.PendingApproval;
        var category = MediaClassifier.Classify(request.Title);

        var mediaItem = new MediaItem(
            type: request.Type,
            platform: MediaPlatform.YouTube,
            title: request.Title,
            url: request.Url,
            thumbnailUrl: request.ThumbnailUrl,
            authorChannel: request.ChannelTitle,
            gameId: request.GameId,
            durationSeconds: request.DurationSeconds,
            playerCountBadge: badge,
            status: status,
            category: category
        );

        await _mediaRepository.AddAsync(mediaItem, ct);
        return MediaItemDto.FromDomain(mediaItem, game.SpanishTitle);
    }

    public async Task<IReadOnlyList<MediaItemDto>> AutoSuggestAndIngestForGameAsync(Guid gameId, bool autoApprove = false, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        var game = await _gameRepository.GetByIdAsync(gameId, ct);
        if (game == null) return Array.Empty<MediaItemDto>();

        var candidates = await SearchVideosForGameAsync(gameId, ct);
        var ingested = new List<MediaItemDto>();

        // 1. Mejor QuickOverview
        var topQuick = candidates.FirstOrDefault(c => c.SuggestedType == MediaType.QuickOverview);
        if (topQuick != null)
        {
            var item = await TryIngestCandidateAsync(game.Id, topQuick, MediaType.QuickOverview, null, autoApprove, ct);
            if (item != null) ingested.Add(item);
        }

        // 2. Mejor Tutorial
        var topTut = candidates.FirstOrDefault(c => c.SuggestedType == MediaType.Tutorial);
        if (topTut != null)
        {
            var item = await TryIngestCandidateAsync(game.Id, topTut, MediaType.Tutorial, null, autoApprove, ct);
            if (item != null) ingested.Add(item);
        }

        // 3. Mejor Playthrough
        var topPlay = candidates.FirstOrDefault(c => c.SuggestedType == MediaType.Playthrough);
        if (topPlay != null)
        {
            var item = await TryIngestCandidateAsync(game.Id, topPlay, MediaType.Playthrough, topPlay.ExtractedPlayerBadge, autoApprove, ct);
            if (item != null) ingested.Add(item);
        }

        return ingested;
    }

    private async Task<MediaItemDto?> TryIngestCandidateAsync(
        Guid gameId,
        YouTubeSearchResultDto candidate,
        MediaType type,
        string? badge,
        bool autoApprove,
        CancellationToken ct)
    {
        try
        {
            if (await _mediaRepository.ExistsByUrlAsync(candidate.Url, ct))
            {
                return null;
            }

            var request = new YouTubeIngestRequestDto(
                GameId: gameId,
                VideoId: candidate.VideoId,
                Type: type,
                Title: candidate.Title,
                Url: candidate.Url,
                ThumbnailUrl: candidate.ThumbnailUrl,
                ChannelTitle: candidate.ChannelTitle,
                DurationSeconds: candidate.DurationSeconds,
                PlayerCountBadge: badge,
                AutoApprove: autoApprove
            );

            return await IngestVideoAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al auto-ingestar vídeo candidato '{VideoId}' para el juego '{GameId}'", candidate.VideoId, gameId);
            return null;
        }
    }

    private async Task<List<YouTubeSearchResultDto>> ExecuteSearchAsync(
        string query,
        string gameTitle,
        MediaType targetType,
        Game? game,
        CancellationToken ct)
    {
        if (_options.ShouldSimulate)
        {
            return FilterSimulated(YouTubeSimulationDataset.GetCuratedOrGeneratedVideos(gameTitle, game), targetType);
        }

        try
        {
            // 1. Llamada a YouTube search
            var searchUrl = $"search?part=snippet&q={Uri.EscapeDataString(query)}&type=video&relevanceLanguage=es&maxResults=5&key={Uri.EscapeDataString(_options.ApiKey!)}";
            var searchResponse = await _httpClient.GetFromJsonAsync<YouTubeSearchListResponse>(searchUrl, ct);

            if (searchResponse?.Items == null || searchResponse.Items.Count == 0)
            {
                _logger.LogInformation("Búsqueda en YouTube no arrojó resultados para query '{Query}'.", query);
                return [];
            }

            var videoIds = searchResponse.Items
                .Where(i => i.Id?.VideoId != null)
                .Select(i => i.Id!.VideoId!)
                .ToList();

            if (videoIds.Count == 0)
            {
                return [];
            }

            // 2. Llamada a YouTube videos para obtener duration exacta e info enriquecida
            var idsJoined = string.Join(",", videoIds);
            var videosUrl = $"videos?part=snippet,contentDetails&id={idsJoined}&key={Uri.EscapeDataString(_options.ApiKey!)}";
            var videosResponse = await _httpClient.GetFromJsonAsync<YouTubeVideoListResponse>(videosUrl, ct);

            var videoDetailsMap = (videosResponse?.Items ?? [])
                .Where(v => !string.IsNullOrWhiteSpace(v.Id))
                .ToDictionary(v => v.Id!, v => v);

            var results = new List<YouTubeSearchResultDto>();

            foreach (var item in searchResponse.Items)
            {
                var vId = item.Id?.VideoId;
                if (string.IsNullOrWhiteSpace(vId)) continue;

                videoDetailsMap.TryGetValue(vId, out var detail);

                var title = detail?.Snippet?.Title ?? item.Snippet?.Title ?? gameTitle;
                var desc = detail?.Snippet?.Description ?? item.Snippet?.Description ?? string.Empty;
                var channel = detail?.Snippet?.ChannelTitle ?? item.Snippet?.ChannelTitle ?? string.Empty;
                var thumb = detail?.Snippet?.Thumbnails?.High?.Url
                    ?? detail?.Snippet?.Thumbnails?.Medium?.Url
                    ?? item.Snippet?.Thumbnails?.High?.Url
                    ?? item.Snippet?.Thumbnails?.Medium?.Url
                    ?? "https://images.unsplash.com/photo-1610890716171-6b1bb98ffd09?auto=format&fit=crop&w=640&q=80";

                int? durationSeconds = null;
                string formattedDuration = string.Empty;

                if (!string.IsNullOrWhiteSpace(detail?.ContentDetails?.Duration))
                {
                    try
                    {
                        var ts = XmlConvert.ToTimeSpan(detail.ContentDetails.Duration);
                        durationSeconds = (int)ts.TotalSeconds;
                        formattedDuration = MediaItem.FormatDuration(durationSeconds);
                    }
                    catch
                    {
                        // Fallback a duración no parseable
                    }
                }

                // Cálculo de relevancia con foco de canales de editoriales, creadores y tiendas
                var isRef = _channelFocus.IsReferenceChannel(channel, out var category, out var bonus);
                var score = 50 + (isRef ? bonus : 0);

                if (title.Contains(gameTitle, StringComparison.OrdinalIgnoreCase)) score += 15;

                var badge = targetType == MediaType.Playthrough
                    ? PlayerCountExtractor.ExtractPlayerBadge(title, desc, game)
                    : null;

                var resultDto = new YouTubeSearchResultDto(
                    VideoId: vId,
                    Title: title,
                    Description: desc,
                    Url: $"https://www.youtube.com/watch?v={vId}",
                    EmbedUrl: $"https://www.youtube-nocookie.com/embed/{vId}",
                    ThumbnailUrl: thumb,
                    ChannelTitle: channel,
                    DurationSeconds: durationSeconds,
                    FormattedDuration: formattedDuration,
                    SuggestedType: targetType,
                    ExtractedPlayerBadge: badge,
                    RelevanceScore: score,
                    IsReferenceChannel: isRef,
                    ChannelCategory: isRef ? category.ToString() : null,
                    PublishedAt: detail?.Snippet?.PublishedAt ?? item.Snippet?.PublishedAt
                );

                results.Add(resultDto);
            }

            return results.OrderByDescending(r => r.RelevanceScore).ToList();
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Búsqueda en YouTube cancelada o tiempo de espera agotado para query '{Query}'.", query);
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error HTTP comunicando con YouTube Data API v3 para query '{Query}'. Código: {StatusCode}", query, ex.StatusCode);
            var detailedMessage = ex.StatusCode switch
            {
                System.Net.HttpStatusCode.Forbidden => "Cuota diaria de YouTube API excedida (límite 10.000 unidades) o clave no autorizada (HTTP 403).",
                System.Net.HttpStatusCode.Unauthorized => "Clave de YouTube Data API v3 no autorizada (HTTP 401).",
                System.Net.HttpStatusCode.BadRequest => "Parámetros de búsqueda rechazados por YouTube (HTTP 400).",
                _ => $"Error en la llamada a YouTube ({ex.StatusCode}): {ex.Message}"
            };
            throw new InvalidOperationException(detailedMessage, ex);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error comunicando con YouTube Data API v3 para query '{Query}'.", query);
            throw new InvalidOperationException($"Error comunicando con YouTube: {ex.Message}", ex);
        }
    }

    private static List<YouTubeSearchResultDto> FilterSimulated(IReadOnlyList<YouTubeSearchResultDto> all, MediaType targetType)
    {
        return all
            .Where(x => x.SuggestedType == targetType)
            .OrderByDescending(x => x.RelevanceScore)
            .ToList();
    }

    // --- Clases para deserialización de YouTube Data API v3 ---
    private class YouTubeSearchListResponse
    {
        [JsonPropertyName("items")]
        public List<YouTubeSearchItem>? Items { get; set; }
    }

    private class YouTubeSearchItem
    {
        [JsonPropertyName("id")]
        public YouTubeResourceId? Id { get; set; }

        [JsonPropertyName("snippet")]
        public YouTubeSnippet? Snippet { get; set; }
    }

    private class YouTubeResourceId
    {
        [JsonPropertyName("videoId")]
        public string? VideoId { get; set; }
    }

    private class YouTubeVideoListResponse
    {
        [JsonPropertyName("items")]
        public List<YouTubeVideoItem>? Items { get; set; }
    }

    private class YouTubeVideoItem
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("snippet")]
        public YouTubeSnippet? Snippet { get; set; }

        [JsonPropertyName("contentDetails")]
        public YouTubeContentDetails? ContentDetails { get; set; }
    }

    private class YouTubeSnippet
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("channelTitle")]
        public string? ChannelTitle { get; set; }

        [JsonPropertyName("thumbnails")]
        public YouTubeThumbnails? Thumbnails { get; set; }

        [JsonPropertyName("publishedAt")]
        public DateTimeOffset? PublishedAt { get; set; }
    }

    private class YouTubeThumbnails
    {
        [JsonPropertyName("medium")]
        public YouTubeThumbnail? Medium { get; set; }

        [JsonPropertyName("high")]
        public YouTubeThumbnail? High { get; set; }
    }

    private class YouTubeThumbnail
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }

    private class YouTubeContentDetails
    {
        [JsonPropertyName("duration")]
        public string? Duration { get; set; }
    }
}
