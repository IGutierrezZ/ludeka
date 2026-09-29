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
using Ludeka.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Ludeka.Application.Features.Community;

public class SocialIngestionService : ISocialIngestionService
{
    private const string DenialMessage =
        "Se requiere el permiso de moderación 'CanApproveMedia' para gestionar la bandeja de moderación social.";

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
    private readonly ISessionPermissionGuard? _permissionGuard;
    private readonly IGiveawayCoverComposer? _giveawayCoverComposer;
    private readonly IPublisherRepository? _publisherRepository;
    private readonly IStoreRepository? _storeRepository;
    private readonly ICreatorRepository? _creatorRepository;

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
        ILogger<SocialIngestionService> logger,
        ISessionPermissionGuard? permissionGuard = null,
        IGiveawayCoverComposer? giveawayCoverComposer = null,
        IPublisherRepository? publisherRepository = null,
        IStoreRepository? storeRepository = null,
        ICreatorRepository? creatorRepository = null)
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
        _permissionGuard = permissionGuard;
        _giveawayCoverComposer = giveawayCoverComposer;
        _publisherRepository = publisherRepository;
        _storeRepository = storeRepository;
        _creatorRepository = creatorRepository;
    }

    /// <summary>
    /// Revalida sesión y permiso releyendo el <c>AppUser</c> actual (INC-46, W1). La bandeja se alimenta
    /// desde páginas públicas y administrativas: la moderación exige la bandera de medios.
    /// </summary>
    private Task RequirePermissionAsync(CancellationToken ct)
        => _permissionGuard is null
            ? Task.CompletedTask
            : _permissionGuard.RequireAsync(ModeratorPermission.CanApproveMedia, DenialMessage, ct);

    public async Task<SocialInboxItemDto> IngestFromUrlAsync(string url, string? manualCaption = null, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        return await IngestFromCollectorAsync(url, manualCaption, ct);
    }

    public async Task<SocialInboxItemDto> IngestMultimodalAsync(SocialExpressMultimodalInputDto input, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(input.SourceUrl))
            throw new ArgumentException("La URL de origen no puede estar vacía.", nameof(input.SourceUrl));

        bool hasCoverImage = input.CoverImageBytes != null && input.CoverImageBytes.Length > 0;
        bool hasBasesImage = input.BasesImageBytes != null && input.BasesImageBytes.Length > 0;
        bool hasText = !string.IsNullOrWhiteSpace(input.ManualCaption);

        if (!hasCoverImage && !hasBasesImage && !hasText)
        {
            throw new InvalidOperationException("Debes proporcionar al menos la imagen de portada, la captura de bases o el texto descriptivo.");
        }

        if (await _inboxRepository.ExistsBySourceUrlAsync(input.SourceUrl, ct))
        {
            throw new InvalidOperationException($"Ya existe una publicación registrada en la bandeja con la URL '{input.SourceUrl}'.");
        }

        _logger.LogInformation("Iniciando alta exprés multimodal para URL: {Url}", input.SourceUrl);

        var metadata = await _metadataExtractor.ExtractFromUrlAsync(input.SourceUrl, ct);
        var platform = metadata?.Platform ?? DetectPlatform(input.SourceUrl);
        var authorOrChannel = metadata?.AuthorOrChannel ?? "Comunidad";

        // 1. Análisis Multimodal con Gemini Flash Vision
        var analysis = await _aiAnalysisService.AnalyzeMultimodalAsync(
            text: input.ManualCaption,
            basesImageBytes: input.BasesImageBytes,
            basesImageMimeType: input.BasesImageMimeType,
            coverImageBytes: input.CoverImageBytes,
            coverImageMimeType: input.CoverImageMimeType,
            authorOrChannel: authorOrChannel,
            ct: ct);

        // 2. Buscar juego en catálogo si la IA sugiere un título
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
            : metadata?.Title ?? "Sorteo comunitario";

        var organizer = !string.IsNullOrWhiteSpace(analysis.OrganizerOrAuthor)
            ? analysis.OrganizerOrAuthor
            : authorOrChannel;

        var itemId = Guid.NewGuid();
        string? finalThumbnailUrl = null;

        // 3. Componer o guardar portada horizontal 16:9
        if (hasCoverImage && _giveawayCoverComposer != null)
        {
            try
            {
                var composedBytes = _giveawayCoverComposer.ComposeHorizontalCover(input.CoverImageBytes!, analysis.CropBoundingBox);
                using var composedStream = new MemoryStream(composedBytes);
                var storageKey = $"social-inbox/{itemId:N}/thumbnail.webp";
                finalThumbnailUrl = await _imageStorageService.UploadOptimizedImageAsync(composedStream, storageKey, ct: ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al componer carátula horizontal con SkiaSharp. Se intentará almacenamiento directo.");
                using var directStream = new MemoryStream(input.CoverImageBytes!);
                var storageKey = $"social-inbox/{itemId:N}/thumbnail.webp";
                finalThumbnailUrl = await _imageStorageService.UploadOptimizedImageAsync(directStream, storageKey, ct: ct);
            }
        }
        else if (hasCoverImage)
        {
            using var directStream = new MemoryStream(input.CoverImageBytes!);
            var storageKey = $"social-inbox/{itemId:N}/thumbnail.webp";
            finalThumbnailUrl = await _imageStorageService.UploadOptimizedImageAsync(directStream, storageKey, ct: ct);
        }
        else if (!string.IsNullOrWhiteSpace(metadata?.ImageUrl))
        {
            finalThumbnailUrl = await TryDownloadAndOptimizeImageAsync(metadata.ImageUrl, itemId, ct);
        }

        var rawCaption = !string.IsNullOrWhiteSpace(input.ManualCaption)
            ? input.ManualCaption.Trim()
            : metadata?.Description ?? metadata?.Title ?? "Bases capturadas por visión artificial multimodal";

        var rawLocation = analysis.TerritorialScope ?? analysis.Location;
        var location = (analysis.DetectedType == SocialSubmissionType.Giveaway || analysis.DetectedType == SocialSubmissionType.BoardGameEvent || !string.IsNullOrWhiteSpace(rawLocation))
            ? await ResolveCountryAsync(rawLocation, organizer, analysis.Collaborator, ct)
            : null;

        var item = new SocialInboxItem(
            sourceUrl: input.SourceUrl,
            platform: platform,
            detectedType: analysis.DetectedType,
            title: title,
            organizerOrAuthor: organizer,
            collaborator: analysis.Collaborator,
            gameId: matchedGameId,
            gameTitle: matchedGameTitle,
            eventOrReleaseDate: analysis.EventOrReleaseDate,
            eventEndDate: analysis.EventEndDate,
            location: location,
            estimatedPvp: analysis.EstimatedPvp,
            mediaCategory: analysis.MediaCategory,
            playerCountBadge: analysis.PlayerCountBadge,
            originalCaption: rawCaption,
            thumbnailUrl: finalThumbnailUrl,
            isVideo: metadata?.IsVideo ?? false,
            aiAnalysisNotes: analysis.Notes);

        var savedItem = await _inboxRepository.AddAsync(item, ct);
        _logger.LogInformation("Ítem multimodal creado en bandeja de moderación con ID {Id} (Tipo: {Type})", savedItem.Id, savedItem.DetectedType);

        return SocialInboxItemDto.FromEntity(savedItem);
    }

    /// <inheritdoc />
    public async Task<SocialInboxItemDto> IngestFromCollectorAsync(string url, string? manualCaption = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("La URL no puede estar vacía.", nameof(url));

        if (await _inboxRepository.ExistsBySourceUrlAsync(url, ct))
        {
            throw new InvalidOperationException($"Ya existe una publicación registrada en la bandeja con la URL '{url}'.");
        }

        _logger.LogInformation("Iniciando alta exprés para URL: {Url}", url);

        // 1. Extraer metadatos abiertos (OpenGraph / oEmbed / YouTube)
        var metadata = await _metadataExtractor.ExtractFromUrlAsync(url, ct);

        var platform = metadata?.Platform ?? DetectPlatform(url);
        var authorOrChannel = metadata?.AuthorOrChannel ?? "Comunidad";
        var isVideo = metadata?.IsVideo ?? false;
        var rawText = !string.IsNullOrWhiteSpace(manualCaption)
            ? manualCaption.Trim()
            : metadata?.Description ?? metadata?.Title ?? string.Empty;

        // Si es Instagram y no se pudo extraer texto ni imagen de la URL y tampoco se aportó texto manual
        if (platform == SocialPlatform.Instagram && string.IsNullOrWhiteSpace(rawText) && string.IsNullOrWhiteSpace(metadata?.ImageUrl))
        {
            throw new InvalidOperationException("Instagram requiere inicio de sesión para leer esta URL. Por favor, utiliza el Alta Exprés Multimodal adjuntando la captura de bases o foto del sorteo.");
        }

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

        var rawLocation = analysis.Location ?? analysis.TerritorialScope;
        var location = (analysis.DetectedType == SocialSubmissionType.Giveaway || analysis.DetectedType == SocialSubmissionType.BoardGameEvent || !string.IsNullOrWhiteSpace(rawLocation))
            ? await ResolveCountryAsync(rawLocation, organizer, analysis.Collaborator, ct)
            : null;

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
            location: location,
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
        await RequirePermissionAsync(ct);
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(input.SourceUrl))
            throw new ArgumentException("La URL de origen no puede estar vacía.", nameof(input.SourceUrl));

        if (string.IsNullOrWhiteSpace(input.Title))
            throw new ArgumentException("El título no puede estar vacío.", nameof(input.Title));

        if (string.IsNullOrWhiteSpace(input.OrganizerOrAuthor))
            throw new ArgumentException("El organizador o canal no puede estar vacío.", nameof(input.OrganizerOrAuthor));

        if (await _inboxRepository.ExistsBySourceUrlAsync(input.SourceUrl, ct))
        {
            throw new InvalidOperationException($"Ya existe una publicación registrada en la bandeja con la URL '{input.SourceUrl}'.");
        }

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

        var location = !string.IsNullOrWhiteSpace(input.Location)
            ? await ResolveCountryAsync(input.Location, input.OrganizerOrAuthor, null, ct)
            : ((input.SubmissionType == SocialSubmissionType.Giveaway || input.SubmissionType == SocialSubmissionType.BoardGameEvent)
                ? await ResolveCountryAsync(null, input.OrganizerOrAuthor, null, ct)
                : null);

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
            location: location,
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
        await RequirePermissionAsync(ct);
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

    public async Task<SocialInboxItemDto> ReanalyzeWithAiAsync(Guid inboxItemId, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        var item = await _inboxRepository.GetByIdAsync(inboxItemId, ct)
            ?? throw new KeyNotFoundException($"No se encontró ningún ítem en la bandeja con ID '{inboxItemId}'.");

        if (item.Status != SocialInboxStatus.PendingReview)
            throw new InvalidOperationException("Solo se pueden reanalizar publicaciones pendientes de revisión.");

        var textToAnalyze = !string.IsNullOrWhiteSpace(item.OriginalCaption)
            ? item.OriginalCaption
            : null;

        if (string.IsNullOrWhiteSpace(textToAnalyze))
        {
            var metadata = await _metadataExtractor.ExtractFromUrlAsync(item.SourceUrl, ct);
            textToAnalyze = metadata?.Description ?? metadata?.Title;
        }

        if (string.IsNullOrWhiteSpace(textToAnalyze))
        {
            throw new InvalidOperationException("La publicación no contiene texto original ni metadatos extraíbles para reanalizar con IA.");
        }

        var analysis = await _aiAnalysisService.AnalyzeTextAsync(textToAnalyze, item.OrganizerOrAuthor, ct);

        bool isHeuristic = string.IsNullOrWhiteSpace(analysis.Notes) ||
                           analysis.Notes.Contains("heurística", StringComparison.OrdinalIgnoreCase) ||
                           analysis.Notes.Contains("heuristica", StringComparison.OrdinalIgnoreCase) ||
                           analysis.Notes.Contains("manual", StringComparison.OrdinalIgnoreCase);

        if (isHeuristic)
        {
            throw new InvalidOperationException("El servicio de IA no está disponible o no tiene clave configurada en este entorno; no se pudo procesar con IA.");
        }

        Guid? matchedGameId = item.GameId;
        string? matchedGameTitle = item.GameTitle;

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

        var rawLocation = analysis.TerritorialScope ?? analysis.Location ?? item.Location;
        var resolvedLocation = (analysis.DetectedType == SocialSubmissionType.Giveaway || analysis.DetectedType == SocialSubmissionType.BoardGameEvent || !string.IsNullOrWhiteSpace(rawLocation))
            ? await ResolveCountryAsync(rawLocation, analysis.OrganizerOrAuthor ?? item.OrganizerOrAuthor, analysis.Collaborator ?? item.Collaborator, ct)
            : item.Location;

        item.UpdateDetails(
            title: !string.IsNullOrWhiteSpace(analysis.Title) ? analysis.Title : item.Title,
            organizerOrAuthor: !string.IsNullOrWhiteSpace(analysis.OrganizerOrAuthor) ? analysis.OrganizerOrAuthor : item.OrganizerOrAuthor,
            collaborator: analysis.Collaborator ?? item.Collaborator,
            detectedType: analysis.DetectedType,
            gameId: matchedGameId,
            gameTitle: matchedGameTitle,
            eventOrReleaseDate: analysis.EventOrReleaseDate ?? item.EventOrReleaseDate,
            eventEndDate: analysis.EventEndDate ?? item.EventEndDate,
            location: resolvedLocation,
            estimatedPvp: analysis.EstimatedPvp ?? item.EstimatedPvp,
            mediaCategory: analysis.MediaCategory ?? item.MediaCategory,
            playerCountBadge: analysis.PlayerCountBadge ?? item.PlayerCountBadge,
            thumbnailUrl: item.ThumbnailUrl,
            moderatorNotes: item.ModeratorNotes);

        item.SetAiAnalysisNotes(analysis.Notes);

        await _inboxRepository.UpdateAsync(item, ct);
        _logger.LogInformation("Ítem {Id} reanalizado satisfactoriamente con IA. Notas: {Notes}", item.Id, analysis.Notes);

        return SocialInboxItemDto.FromEntity(item);
    }

    public async Task<int> PurgeSimulatedItemsAsync(CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);
        var count = await _inboxRepository.PurgeSimulatedAsync(ct);
        _logger.LogInformation("Se han purgado {Count} publicaciones simuladas de la bandeja de moderación.", count);
        return count;
    }

    public async Task<Guid> ApproveAndPublishAsync(Guid inboxItemId, string reviewerUserId, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

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

                // Regla de negocio: No se puede aprobar un sorteo sin fecha límite, ni con fecha anterior a hoy.
                var rawDeadline = item.EventEndDate ?? item.EventOrReleaseDate;
                if (!rawDeadline.HasValue)
                {
                    throw new InvalidOperationException("Para aprobar un sorteo es obligatorio indicar una fecha de fin igual o posterior a hoy. Por favor, edita la publicación e indica la fecha de fin.");
                }

                if (rawDeadline.Value.Date < DateTime.UtcNow.Date)
                {
                    throw new InvalidOperationException($"No se puede aprobar un sorteo con una fecha límite en el pasado ({rawDeadline.Value:dd/MM/yyyy}). Debe ser igual o posterior a hoy. Por favor, edita la publicación y actualiza la fecha.");
                }

                // Si la fecha cae en el día de hoy pero con hora medianoche (o ya pasada dentro del día), ajustar al fin de hoy (23:59:59 UTC) para que no expire inmediatamente.
                var deadline = (rawDeadline.Value < DateTimeOffset.UtcNow && rawDeadline.Value.Date == DateTime.UtcNow.Date)
                    ? new DateTimeOffset(DateTime.UtcNow.Date.AddDays(1).AddSeconds(-1), TimeSpan.Zero)
                    : rawDeadline.Value;

                var giveawayCountry = await ResolveCountryAsync(item.Location, item.OrganizerOrAuthor, item.Collaborator, ct);

                // Comprobar si ya existe un sorteo idéntico o en colaboración para fusionar colaboradores o extender plazo
                var existingGiveaway = await _giveawayRepository.FindDuplicateOrCollaborativeAsync(
                    item.Title,
                    item.OrganizerOrAuthor,
                    deadline,
                    ct);

                if (existingGiveaway != null)
                {
                    var collaboratorToMerge = !string.IsNullOrWhiteSpace(item.Collaborator)
                        ? item.Collaborator
                        : item.OrganizerOrAuthor;

                    existingGiveaway.MergeCollaborator(collaboratorToMerge);
                    if (deadline > existingGiveaway.DeadlineAt)
                    {
                        existingGiveaway.ExtendDeadline(deadline);
                    }
                    await _giveawayRepository.UpdateAsync(existingGiveaway, ct);
                    createdEntityId = existingGiveaway.Id;
                    _logger.LogInformation("Sorteo existente {Id} actualizado con colaborador {Collaborator}", existingGiveaway.Id, collaboratorToMerge);
                }
                else
                {
                    var giveaway = new Giveaway(
                        title: item.Title,
                        organizer: item.OrganizerOrAuthor,
                        url: item.SourceUrl,
                        platform: giveawayPlatform,
                        deadlineAt: deadline,
                        country: giveawayCountry,
                        gameId: item.GameId,
                        gameTitle: item.GameTitle,
                        collaborator: item.Collaborator,
                        thumbnailUrl: item.ThumbnailUrl);

                    await _giveawayRepository.AddAsync(giveaway, ct);
                    createdEntityId = giveaway.Id;
                    _logger.LogInformation("Sorteo creado desde bandeja con ID {Id}", createdEntityId);
                }
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

                var eventCountry = await ResolveCountryAsync(item.Location, item.OrganizerOrAuthor, item.Collaborator, ct);

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
                    country: eventCountry);

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
        await RequirePermissionAsync(ct);

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

    private async Task<string> ResolveCountryAsync(string? explicitLocation, string? organizer, string? collaborator, CancellationToken ct)
    {
        // 1. Si el ítem ya trae un ámbito geográfico explícito, normalizarlo con CountryCatalog
        if (!string.IsNullOrWhiteSpace(explicitLocation))
        {
            var matched = CountryCatalog.FindByNameOrCode(explicitLocation);
            if (matched != null)
                return matched.Name;

            var lower = explicitLocation.ToLowerInvariant();
            if (lower.Contains("peninsula") || lower.Contains("españa") || lower.Contains("espana") || lower.Contains("spain") || lower.Contains("baleares") || lower.Contains("canarias"))
                return "España";

            if (lower.Contains("inter") || lower.Contains("global") || lower.Contains("mundo") || lower.Contains("world"))
                return "Internacional";

            var normalized = CountryCatalog.Normalize(explicitLocation);
            if (!string.IsNullOrWhiteSpace(normalized))
                return normalized;
        }

        // 2. Si no viene informado el país, consultar si la editorial, tienda o creador está dado de alta en la plataforma
        var candidates = new[] { organizer, collaborator }
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!.Trim())
            .ToList();

        foreach (var candidate in candidates)
        {
            // A. Editoriales registradas
            if (_publisherRepository != null)
            {
                var publisher = await _publisherRepository.GetByNameAsync(candidate, ct);
                if (publisher != null && !string.IsNullOrWhiteSpace(publisher.Country))
                {
                    _logger.LogInformation("Ámbito territorial resuelto desde la editorial '{Name}': {Country}", publisher.Name, publisher.Country);
                    return CountryCatalog.Normalize(publisher.Country);
                }
            }

            // B. Tiendas especializadas registradas
            if (_storeRepository != null)
            {
                var store = await _storeRepository.GetByNameAsync(candidate, ct);
                if (store != null && !string.IsNullOrWhiteSpace(store.Country))
                {
                    _logger.LogInformation("Ámbito territorial resuelto desde la tienda '{Name}': {Country}", store.Name, store.Country);
                    return CountryCatalog.Normalize(store.Country);
                }
            }

            // C. Creadores de contenido / Divulgadores registrados
            if (_creatorRepository != null)
            {
                var creator = await _creatorRepository.GetByNameAsync(candidate, ct);
                if (creator != null && !string.IsNullOrWhiteSpace(creator.Nationality))
                {
                    var normalizedNat = CountryCatalog.Normalize(creator.Nationality);
                    _logger.LogInformation("Ámbito territorial resuelto desde el creador '{Name}': {Country}", creator.Name, normalizedNat);
                    return normalizedNat;
                }
            }
        }

        // 3. Valor por defecto si no hay información explícita ni registro previo
        return "España";
    }
}
