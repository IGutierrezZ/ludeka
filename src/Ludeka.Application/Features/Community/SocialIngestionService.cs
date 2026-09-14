using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Microsoft.Extensions.Logging;

namespace Ludeka.Application.Features.Community;

public class SocialIngestionService : ISocialIngestionService
{
    private readonly ISocialInboxRepository _inboxRepository;
    private readonly ISocialMetadataExtractor _metadataExtractor;
    private readonly ISocialAiAnalysisService _aiAnalysisService;
    private readonly IImageStorageService _imageStorageService;
    private readonly IGameRepository _gameRepository;
    private readonly IGiveawayRepository _giveawayRepository;
    private readonly IWeeklyReleaseRepository _weeklyReleaseRepository;
    private readonly IBoardGameEventRepository _eventRepository;
    private readonly IMediaRepository _mediaRepository;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SocialIngestionService> _logger;

    public SocialIngestionService(
        ISocialInboxRepository inboxRepository,
        ISocialMetadataExtractor metadataExtractor,
        ISocialAiAnalysisService aiAnalysisService,
        IImageStorageService imageStorageService,
        IGameRepository gameRepository,
        IGiveawayRepository giveawayRepository,
        IWeeklyReleaseRepository weeklyReleaseRepository,
        IBoardGameEventRepository eventRepository,
        IMediaRepository mediaRepository,
        HttpClient httpClient,
        ILogger<SocialIngestionService> logger)
    {
        _inboxRepository = inboxRepository ?? throw new ArgumentNullException(nameof(inboxRepository));
        _metadataExtractor = metadataExtractor ?? throw new ArgumentNullException(nameof(metadataExtractor));
        _aiAnalysisService = aiAnalysisService ?? throw new ArgumentNullException(nameof(aiAnalysisService));
        _imageStorageService = imageStorageService ?? throw new ArgumentNullException(nameof(imageStorageService));
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _giveawayRepository = giveawayRepository ?? throw new ArgumentNullException(nameof(giveawayRepository));
        _weeklyReleaseRepository = weeklyReleaseRepository ?? throw new ArgumentNullException(nameof(weeklyReleaseRepository));
        _eventRepository = eventRepository ?? throw new ArgumentNullException(nameof(eventRepository));
        _mediaRepository = mediaRepository ?? throw new ArgumentNullException(nameof(mediaRepository));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SocialInboxItemDto> IngestFromUrlAsync(string url, string? manualCaption = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("La URL no puede estar vacía.", nameof(url));

        _logger.LogInformation("Iniciando alta exprés para URL: {Url}", url);

        // 1. Extraer metadatos abiertos (OpenGraph / oEmbed / YouTube)
        var metadata = await _metadataExtractor.ExtractFromUrlAsync(url, ct);

        var platform = metadata?.Platform ?? DetectPlatform(url);
        var authorOrChannel = metadata?.AuthorOrChannel ?? "Comunidad";
        var isVideo = metadata?.IsVideo ?? false;
        var rawText = !string.IsNullOrWhiteSpace(manualCaption)
            ? manualCaption.Trim()
            : metadata?.Description ?? metadata?.Title ?? string.Empty;

        // 2. Analizar texto con IA (Gemini Flash o heurística)
        var analysis = await _aiAnalysisService.AnalyzeTextAsync(rawText, authorOrChannel, ct);

        // 3. Buscar juego en catálogo si la IA sugiere un título
        Guid? matchedGameId = null;
        string? matchedGameTitle = analysis.SuggestedGameTitle;

        if (!string.IsNullOrWhiteSpace(analysis.SuggestedGameTitle))
        {
            var searchResults = await _gameRepository.SearchAsync(new GameFilterCriteria(SearchTerm: analysis.SuggestedGameTitle), page: 1, pageSize: 1, ct: ct);
            if (searchResults.Items.Count > 0)
            {
                var first = searchResults.Items[0];
                matchedGameId = first.Id;
                matchedGameTitle = first.SpanishTitle;
            }
        }

        var title = !string.IsNullOrWhiteSpace(analysis.Title)
            ? analysis.Title
            : metadata?.Title ?? "Publicación detectada";

        var organizer = !string.IsNullOrWhiteSpace(analysis.OrganizerOrAuthor)
            ? analysis.OrganizerOrAuthor
            : authorOrChannel;

        var item = new SocialInboxItem(
            sourceUrl: url,
            platform: platform,
            detectedType: analysis.DetectedType,
            title: title,
            organizerOrAuthor: organizer,
            collaborator: analysis.Collaborator,
            gameId: matchedGameId,
            gameTitle: matchedGameTitle,
            eventOrReleaseDate: analysis.EventOrReleaseDate,
            eventEndDate: analysis.EventEndDate,
            location: analysis.Location,
            estimatedPvp: analysis.EstimatedPvp,
            mediaCategory: analysis.MediaCategory,
            playerCountBadge: analysis.PlayerCountBadge,
            originalCaption: rawText,
            thumbnailUrl: metadata?.ImageUrl,
            isVideo: isVideo,
            aiAnalysisNotes: analysis.Notes);

        // 4. Procesar y optimizar miniatura a Cloudflare R2 vía SkiaSharp
        if (!string.IsNullOrWhiteSpace(metadata?.ImageUrl))
        {
            var optimizedUrl = await TryDownloadAndOptimizeImageAsync(metadata.ImageUrl, item.Id, ct);
            if (!string.IsNullOrWhiteSpace(optimizedUrl))
            {
                item.UpdateThumbnailUrl(optimizedUrl);
            }
        }

        var savedItem = await _inboxRepository.AddAsync(item, ct);
        _logger.LogInformation("Ítem creado en bandeja de moderación con ID {Id} (Tipo: {Type})", savedItem.Id, savedItem.DetectedType);

        return SocialInboxItemDto.FromEntity(savedItem);
    }

    public async Task<SocialInboxItemDto> IngestManualAdvancedAsync(SocialInboxManualInputDto input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(input.SourceUrl))
            throw new ArgumentException("La URL de origen no puede estar vacía.", nameof(input.SourceUrl));

        if (string.IsNullOrWhiteSpace(input.Title))
            throw new ArgumentException("El título no puede estar vacío.", nameof(input.Title));

        if (string.IsNullOrWhiteSpace(input.OrganizerOrAuthor))
            throw new ArgumentException("El organizador o canal no puede estar vacío.", nameof(input.OrganizerOrAuthor));

        _logger.LogInformation("Iniciando alta manual avanzada para URL: {Url}", input.SourceUrl);

        var platform = DetectPlatform(input.SourceUrl);
        var isVideo = input.SubmissionType == SocialSubmissionType.MediaItem || platform == SocialPlatform.YouTube;

        // Intentar resolver carátula/miniatura si no se especificó una personalizada
        string? imageUrl = input.CustomThumbnailUrl;
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            var metadata = await _metadataExtractor.ExtractFromUrlAsync(input.SourceUrl, ct);
            imageUrl = metadata?.ImageUrl;
        }

        // Si se indicó un GameId pero no GameTitle, resolverlo del repositorio
        var gameTitle = input.GameTitle;
        if (input.GameId.HasValue && string.IsNullOrWhiteSpace(gameTitle))
        {
            var game = await _gameRepository.GetByIdAsync(input.GameId.Value, ct);
            gameTitle = game?.SpanishTitle ?? game?.OriginalTitle;
        }

        var item = new SocialInboxItem(
            sourceUrl: input.SourceUrl,
            platform: platform,
            detectedType: input.SubmissionType,
            title: input.Title,
            organizerOrAuthor: input.OrganizerOrAuthor,
            collaborator: null,
            gameId: input.GameId,
            gameTitle: gameTitle,
            eventOrReleaseDate: input.EventOrReleaseDate,
            eventEndDate: input.EventEndDate,
            location: input.Location,
            estimatedPvp: input.EstimatedPvp,
            mediaCategory: input.MediaCategory,
            playerCountBadge: input.PlayerCountBadge,
            originalCaption: input.Notes,
            thumbnailUrl: imageUrl,
            isVideo: isVideo,
            aiAnalysisNotes: "Alta asistida manual avanzada");

        // Optimizar miniatura si existe
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            var optimizedUrl = await TryDownloadAndOptimizeImageAsync(imageUrl, item.Id, ct);
            if (!string.IsNullOrWhiteSpace(optimizedUrl))
            {
                item.UpdateThumbnailUrl(optimizedUrl);
            }
        }

        var savedItem = await _inboxRepository.AddAsync(item, ct);
        _logger.LogInformation("Ítem manual creado en bandeja con ID {Id}", savedItem.Id);

        return SocialInboxItemDto.FromEntity(savedItem);
    }

    public async Task<SocialInboxItemDto> UpdateItemAsync(SocialInboxUpdateDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var item = await _inboxRepository.GetByIdAsync(dto.Id, ct)
            ?? throw new KeyNotFoundException($"No se encontró ningún ítem en la bandeja con ID '{dto.Id}'.");

        var gameTitle = dto.GameTitle;
        if (dto.GameId.HasValue && string.IsNullOrWhiteSpace(gameTitle))
        {
            var game = await _gameRepository.GetByIdAsync(dto.GameId.Value, ct);
            gameTitle = game?.SpanishTitle ?? game?.OriginalTitle;
        }

        item.UpdateDetails(
            title: dto.Title,
            organizerOrAuthor: dto.OrganizerOrAuthor,
            collaborator: dto.Collaborator,
            detectedType: dto.DetectedType,
            gameId: dto.GameId,
            gameTitle: gameTitle,
            eventOrReleaseDate: dto.EventOrReleaseDate,
            eventEndDate: dto.EventEndDate,
            location: dto.Location,
            estimatedPvp: dto.EstimatedPvp,
            mediaCategory: dto.MediaCategory,
            playerCountBadge: dto.PlayerCountBadge,
            thumbnailUrl: dto.ThumbnailUrl,
            moderatorNotes: dto.ModeratorNotes);

        await _inboxRepository.UpdateAsync(item, ct);
        _logger.LogInformation("Ítem {Id} actualizado por moderador", item.Id);

        return SocialInboxItemDto.FromEntity(item);
    }

    public async Task<Guid> ApproveAndPublishAsync(Guid inboxItemId, string reviewerUserId, CancellationToken ct = default)
    {
        var item = await _inboxRepository.GetByIdAsync(inboxItemId, ct)
            ?? throw new KeyNotFoundException($"No se encontró ningún ítem en la bandeja con ID '{inboxItemId}'.");

        if (item.Status != SocialInboxStatus.PendingReview)
            throw new InvalidOperationException("Solo se pueden aprobar ítems pendientes de revisión.");

        Guid createdEntityId;

        switch (item.DetectedType)
        {
            case SocialSubmissionType.Giveaway:
                var giveawayPlatform = item.Platform switch
                {
                    SocialPlatform.Instagram => GiveawayPlatform.Instagram,
                    SocialPlatform.Twitter => GiveawayPlatform.TwitterX,
                    SocialPlatform.YouTube => GiveawayPlatform.YouTube,
                    _ => GiveawayPlatform.Other
                };

                var giveaway = new Giveaway(
                    title: item.Title,
                    organizer: item.OrganizerOrAuthor,
                    url: item.SourceUrl,
                    platform: giveawayPlatform,
                    deadlineAt: item.EventOrReleaseDate ?? DateTimeOffset.UtcNow.AddDays(7),
                    country: "España",
                    gameId: item.GameId,
                    gameTitle: item.GameTitle,
                    collaborator: item.Collaborator,
                    thumbnailUrl: item.ThumbnailUrl);

                await _giveawayRepository.AddAsync(giveaway, ct);
                createdEntityId = giveaway.Id;
                _logger.LogInformation("Sorteo creado desde bandeja con ID {Id}", createdEntityId);
                break;

            case SocialSubmissionType.WeeklyRelease:
                var releaseDate = DateOnly.FromDateTime(item.EventOrReleaseDate?.DateTime ?? DateTime.UtcNow);
                var release = new WeeklyRelease(
                    title: item.Title,
                    publisher: item.OrganizerOrAuthor,
                    releaseDate: releaseDate,
                    gameId: item.GameId,
                    coverImageUrl: item.ThumbnailUrl,
                    estimatedPvp: item.EstimatedPvp,
                    isReprint: false,
                    notes: item.ModeratorNotes ?? item.OriginalCaption);

                await _weeklyReleaseRepository.AddAsync(release, ct);
                createdEntityId = release.Id;
                _logger.LogInformation("Lanzamiento semanal creado desde bandeja con ID {Id}", createdEntityId);
                break;

            case SocialSubmissionType.BoardGameEvent:
                var startDate = DateOnly.FromDateTime(item.EventOrReleaseDate?.DateTime ?? DateTime.UtcNow);
                var endDate = DateOnly.FromDateTime((item.EventEndDate ?? item.EventOrReleaseDate ?? DateTimeOffset.UtcNow).DateTime);

                var boardGameEvent = new BoardGameEvent(
                    title: item.Title,
                    description: item.OriginalCaption ?? item.Title,
                    imageUrl: item.ThumbnailUrl ?? "/images/default-event.webp",
                    startDate: startDate,
                    endDate: endDate,
                    location: item.Location ?? "España",
                    websiteUrl: item.SourceUrl,
                    organizer: item.OrganizerOrAuthor,
                    isOfficial: true,
                    country: "España");

                await _eventRepository.AddAsync(boardGameEvent, ct);
                createdEntityId = boardGameEvent.Id;
                _logger.LogInformation("Evento lúdico creado desde bandeja con ID {Id}", createdEntityId);
                break;

            case SocialSubmissionType.MediaItem:
                var mediaPlatform = item.Platform == SocialPlatform.YouTube ? MediaPlatform.YouTube : MediaPlatform.Instagram;
                var mediaType = item.IsVideo
                    ? (item.MediaCategory == MediaCategory.Gameplay ? MediaType.Playthrough : MediaType.Tutorial)
                    : MediaType.InstagramPost;

                var mediaItem = new MediaItem(
                    type: mediaType,
                    platform: mediaPlatform,
                    title: item.Title,
                    url: item.SourceUrl,
                    thumbnailUrl: item.ThumbnailUrl ?? "/images/default-media.webp",
                    authorChannel: item.OrganizerOrAuthor,
                    gameId: item.GameId,
                    playerCountBadge: item.PlayerCountBadge,
                    excerpt: item.OriginalCaption,
                    status: ModerationStatus.Approved,
                    publishedAt: item.CreatedAt,
                    category: item.MediaCategory ?? MediaCategory.Tutorial);

                await _mediaRepository.AddAsync(mediaItem, ct);
                createdEntityId = mediaItem.Id;
                _logger.LogInformation("Pieza multimedia creada desde bandeja con ID {Id}", createdEntityId);
                break;

            default:
                throw new InvalidOperationException($"Tipo de envío no soportado: {item.DetectedType}");
        }

        item.Approve(createdEntityId, reviewerUserId);
        await _inboxRepository.UpdateAsync(item, ct);

        return createdEntityId;
    }

    public async Task RejectItemAsync(Guid inboxItemId, string reason, string reviewerUserId, CancellationToken ct = default)
    {
        var item = await _inboxRepository.GetByIdAsync(inboxItemId, ct)
            ?? throw new KeyNotFoundException($"No se encontró ningún ítem en la bandeja con ID '{inboxItemId}'.");

        item.Reject(reason, reviewerUserId);
        await _inboxRepository.UpdateAsync(item, ct);
        _logger.LogInformation("Ítem {Id} descartado por {User}. Motivo: {Reason}", inboxItemId, reviewerUserId, reason);
    }

    public async Task<IReadOnlyList<SocialInboxItemDto>> GetPendingItemsAsync(SocialSubmissionType? typeFilter = null, CancellationToken ct = default)
    {
        var items = await _inboxRepository.GetPendingAsync(typeFilter, ct);
        var result = new List<SocialInboxItemDto>(items.Count);
        foreach (var i in items)
        {
            result.Add(SocialInboxItemDto.FromEntity(i));
        }
        return result;
    }

    public async Task<IReadOnlyList<SocialInboxItemDto>> GetAllItemsAsync(SocialInboxStatus? statusFilter = null, SocialSubmissionType? typeFilter = null, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var items = await _inboxRepository.GetAllAsync(statusFilter, typeFilter, page, pageSize, ct);
        var result = new List<SocialInboxItemDto>(items.Count);
        foreach (var i in items)
        {
            result.Add(SocialInboxItemDto.FromEntity(i));
        }
        return result;
    }

    public async Task<SocialInboxItemDto?> GetItemByIdAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _inboxRepository.GetByIdAsync(id, ct);
        return item != null ? SocialInboxItemDto.FromEntity(item) : null;
    }

    public Task<int> GetPendingCountAsync(CancellationToken ct = default)
    {
        return _inboxRepository.GetPendingCountAsync(ct);
    }

    private static SocialPlatform DetectPlatform(string url)
    {
        if (url.Contains("instagram.com", StringComparison.OrdinalIgnoreCase))
            return SocialPlatform.Instagram;

        if (url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) || url.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
            return SocialPlatform.YouTube;

        if (url.Contains("twitter.com", StringComparison.OrdinalIgnoreCase) || url.Contains("x.com", StringComparison.OrdinalIgnoreCase))
            return SocialPlatform.Twitter;

        if (url.Contains("tiktok.com", StringComparison.OrdinalIgnoreCase))
            return SocialPlatform.TikTok;

        return SocialPlatform.Website;
    }

    private async Task<string?> TryDownloadAndOptimizeImageAsync(string sourceImageUrl, Guid itemId, CancellationToken ct)
    {
        try
        {
            using var response = await _httpClient.GetAsync(sourceImageUrl, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var objectKey = $"social-inbox/{itemId:N}/thumbnail.webp";

            return await _imageStorageService.UploadOptimizedImageAsync(
                inputStream: stream,
                objectKey: objectKey,
                maxWidth: 1000,
                quality: 82,
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo optimizar la imagen remota {Url} a R2. Se usará la URL original.", sourceImageUrl);
            return null;
        }
    }
}
