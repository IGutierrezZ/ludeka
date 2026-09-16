using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Features.Instagram;

public class InstagramPublisherService : IInstagramPublisherService
{
    private const string DenialMessage =
        "Se requiere el permiso de moderaciÃ³n 'CanPublishInstagram' para gestionar las publicaciones de Instagram.";

    private readonly IInstagramPostDraftRepository _draftRepository;
    private readonly IGiveawayRepository _giveawayRepository;
    private readonly IWeeklyReleaseRepository _releaseRepository;
    private readonly IGameRepository _gameRepository;
    private readonly IInstagramComposerService _composerService;
    private readonly IInstagramApiClient _apiClient;
    private readonly IAuditService _auditService;
    private readonly ISessionPermissionGuard? _permissionGuard;

    public InstagramPublisherService(
        IInstagramPostDraftRepository draftRepository,
        IGiveawayRepository giveawayRepository,
        IWeeklyReleaseRepository releaseRepository,
        IGameRepository gameRepository,
        IInstagramComposerService composerService,
        IInstagramApiClient apiClient,
        IAuditService auditService,
        ISessionPermissionGuard? permissionGuard = null)
    {
        _draftRepository = draftRepository ?? throw new ArgumentNullException(nameof(draftRepository));
        _giveawayRepository = giveawayRepository ?? throw new ArgumentNullException(nameof(giveawayRepository));
        _releaseRepository = releaseRepository ?? throw new ArgumentNullException(nameof(releaseRepository));
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _composerService = composerService ?? throw new ArgumentNullException(nameof(composerService));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _permissionGuard = permissionGuard;
    }

    /// <summary>
    /// Revalida sesiÃ³n y permiso releyendo el <c>AppUser</c> actual (INC-46, W1): el panel de Instagram
    /// crea, edita, publica y descarta borradores, asÃ­ que todas sus escrituras pasan por aquÃ­.
    /// </summary>
    private Task RequirePermissionAsync(CancellationToken ct)
        => _permissionGuard is null
            ? Task.CompletedTask
            : _permissionGuard.RequireAsync(ModeratorPermission.CanPublishInstagram, DenialMessage, ct);

    public async Task<InstagramPostDraftDto> CreateDraftFromGiveawayAsync(
        Guid giveawayId,
        string userId,
        string userName,
        string theme = "Dark",
        CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        var existing = await _draftRepository.GetBySourceAsync(InstagramPostSourceType.Giveaway, giveawayId.ToString(), ct);
        if (existing != null)
            return InstagramPostDraftDto.FromEntity(existing);

        var giveaway = await _giveawayRepository.GetByIdAsync(giveawayId, ct);
        if (giveaway == null)
            throw new KeyNotFoundException($"No se encontrÃ³ el sorteo con identificador '{giveawayId}'.");

        var svg = _composerService.ComposeSvg(InstagramPostSourceType.Giveaway, giveaway, theme);
        var caption = _composerService.GenerateCaption(InstagramPostSourceType.Giveaway, giveaway);

        var draft = new InstagramPostDraft(
            title: giveaway.Title,
            caption: caption,
            sourceType: InstagramPostSourceType.Giveaway,
            sourceId: giveawayId.ToString(),
            createdByUserId: userId,
            createdByUserName: userName,
            imageUrl: giveaway.ThumbnailUrl,
            svgContent: svg,
            theme: theme
        );

        await _draftRepository.AddAsync(draft, ct);
        return InstagramPostDraftDto.FromEntity(draft);
    }

    public async Task<InstagramPostDraftDto> CreateDraftFromWeeklyReleaseAsync(
        Guid releaseId,
        string userId,
        string userName,
        string theme = "Dark",
        CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        var existing = await _draftRepository.GetBySourceAsync(InstagramPostSourceType.WeeklyRelease, releaseId.ToString(), ct);
        if (existing != null)
            return InstagramPostDraftDto.FromEntity(existing);

        var release = await _releaseRepository.GetByIdAsync(releaseId, ct);
        if (release == null)
            throw new KeyNotFoundException($"No se encontrÃ³ la novedad editorial con identificador '{releaseId}'.");

        var svg = _composerService.ComposeSvg(InstagramPostSourceType.WeeklyRelease, release, theme);
        var caption = _composerService.GenerateCaption(InstagramPostSourceType.WeeklyRelease, release);

        var draft = new InstagramPostDraft(
            title: release.Title,
            caption: caption,
            sourceType: InstagramPostSourceType.WeeklyRelease,
            sourceId: releaseId.ToString(),
            createdByUserId: userId,
            createdByUserName: userName,
            imageUrl: release.CoverImageUrl,
            svgContent: svg,
            theme: theme
        );

        await _draftRepository.AddAsync(draft, ct);
        return InstagramPostDraftDto.FromEntity(draft);
    }

    public async Task<InstagramPostDraftDto> CreateDraftFromGameAsync(
        Guid gameId,
        string userId,
        string userName,
        string theme = "Dark",
        CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        var existing = await _draftRepository.GetBySourceAsync(InstagramPostSourceType.Game, gameId.ToString(), ct);
        if (existing != null)
            return InstagramPostDraftDto.FromEntity(existing);

        var game = await _gameRepository.GetByIdAsync(gameId, ct);
        if (game == null)
            throw new KeyNotFoundException($"No se encontrÃ³ el juego con identificador '{gameId}'.");

        var svg = _composerService.ComposeSvg(InstagramPostSourceType.Game, game, theme);
        var caption = _composerService.GenerateCaption(InstagramPostSourceType.Game, game);

        var draft = new InstagramPostDraft(
            title: game.SpanishTitle,
            caption: caption,
            sourceType: InstagramPostSourceType.Game,
            sourceId: gameId.ToString(),
            createdByUserId: userId,
            createdByUserName: userName,
            imageUrl: game.CoverImageUrl,
            svgContent: svg,
            theme: theme
        );

        await _draftRepository.AddAsync(draft, ct);
        return InstagramPostDraftDto.FromEntity(draft);
    }

    public async Task<InstagramPostDraftDto?> GetDraftByIdAsync(Guid draftId, CancellationToken ct = default)
    {
        var draft = await _draftRepository.GetByIdAsync(draftId, ct);
        return draft == null ? null : InstagramPostDraftDto.FromEntity(draft);
    }

    public async Task<IReadOnlyList<InstagramPostDraftDto>> GetDraftsAsync(
        InstagramPostDraftStatus? status = null,
        CancellationToken ct = default)
    {
        var drafts = await _draftRepository.GetDraftsAsync(status, ct);
        return drafts.Select(InstagramPostDraftDto.FromEntity).ToList();
    }

    public async Task<InstagramPostDraftDto> UpdateDraftAsync(
        Guid draftId,
        UpdateInstagramDraftCommand command,
        CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        ArgumentNullException.ThrowIfNull(command);

        var draft = await _draftRepository.GetByIdAsync(draftId, ct);
        if (draft == null)
            throw new KeyNotFoundException($"No se encontrÃ³ el borrador con identificador '{draftId}'.");

        // Si cambiÃ³ el tema y no se pasÃ³ svgContent manual, regenerar el SVG
        string? newSvg = command.SvgContent;
        if (string.IsNullOrWhiteSpace(newSvg) && !string.Equals(draft.Theme, command.Theme, StringComparison.OrdinalIgnoreCase))
        {
            newSvg = await RegenerateSvgAsync(draft.SourceType, draft.SourceId, command.Theme, ct);
        }

        draft.UpdateDraft(command.Caption, command.Theme, command.ImageUrl, newSvg);
        await _draftRepository.UpdateAsync(draft, ct);

        return InstagramPostDraftDto.FromEntity(draft);
    }

    public async Task<InstagramPublishResultDto> PublishDraftAsync(
        Guid draftId,
        string userId,
        string userName,
        CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        var draft = await _draftRepository.GetByIdAsync(draftId, ct);
        if (draft == null)
            throw new KeyNotFoundException($"No se encontrÃ³ el borrador con identificador '{draftId}'.");

        if (draft.Status == InstagramPostDraftStatus.Published)
        {
            return new InstagramPublishResultDto(
                Success: true,
                MediaId: draft.InstagramMediaId,
                Permalink: draft.InstagramPermalink,
                ErrorMessage: null
            );
        }

        try
        {
            draft.MarkPublishing();
            await _draftRepository.UpdateAsync(draft, ct);

            // Determinar la URL pÃºblica de la imagen a enviar a Meta
            var imageUrl = !string.IsNullOrWhiteSpace(draft.ImageUrl)
                ? draft.ImageUrl
                : $"https://ludeka.es/api/instagram/card/{draft.Id}.svg";

            // Fase 1: CreaciÃ³n del contenedor de medio
            var creationId = await _apiClient.CreateMediaContainerAsync(imageUrl, draft.Caption, ct);

            // Fase 2: PublicaciÃ³n del contenedor
            var mediaId = await _apiClient.PublishMediaAsync(creationId, ct);

            // ObtenciÃ³n del enlace permanente
            var permalink = await _apiClient.GetPermalinkAsync(mediaId, ct)
                ?? $"https://www.instagram.com/p/{mediaId}/";

            draft.MarkPublished(mediaId, permalink);
            await _draftRepository.UpdateAsync(draft, ct);

            // Marcar en la entidad origen
            await MarkSourceAsPublishedAsync(draft.SourceType, draft.SourceId, mediaId, permalink, ct);

            // Registrar en auditorÃ­a
            await _auditService.RecordChangeAsync(new RecordAuditCommand(
                UserId: userId,
                UserName: userName,
                Action: AuditAction.Published,
                EntityType: AuditEntityType.InstagramPost,
                EntityId: draft.Id.ToString(),
                EntityName: draft.Title,
                Summary: $"Publicado post en Instagram con permalink: {permalink}",
                Changes: new[]
                {
                    new FieldChangeDto("InstagramMediaId", null, mediaId),
                    new FieldChangeDto("InstagramPermalink", null, permalink),
                    new FieldChangeDto("Status", InstagramPostDraftStatus.Draft.ToString(), InstagramPostDraftStatus.Published.ToString())
                }
            ), ct);

            return new InstagramPublishResultDto(
                Success: true,
                MediaId: mediaId,
                Permalink: permalink,
                ErrorMessage: null
            );
        }
        catch (Exception ex)
        {
            draft.MarkFailed(ex.Message);
            await _draftRepository.UpdateAsync(draft, ct);

            return new InstagramPublishResultDto(
                Success: false,
                MediaId: null,
                Permalink: null,
                ErrorMessage: ex.Message
            );
        }
    }

    public async Task<bool> DeleteDraftAsync(Guid draftId, CancellationToken ct = default)
    {
        await RequirePermissionAsync(ct);

        var draft = await _draftRepository.GetByIdAsync(draftId, ct);
        if (draft == null)
            return false;

        await _draftRepository.DeleteAsync(draftId, ct);
        return true;
    }

    private async Task<string?> RegenerateSvgAsync(
        InstagramPostSourceType sourceType,
        string sourceId,
        string theme,
        CancellationToken ct)
    {
        if (sourceType == InstagramPostSourceType.Giveaway && Guid.TryParse(sourceId, out var gId))
        {
            var g = await _giveawayRepository.GetByIdAsync(gId, ct);
            if (g != null) return _composerService.ComposeSvg(sourceType, g, theme);
        }
        else if (sourceType == InstagramPostSourceType.WeeklyRelease && Guid.TryParse(sourceId, out var rId))
        {
            var r = await _releaseRepository.GetByIdAsync(rId, ct);
            if (r != null) return _composerService.ComposeSvg(sourceType, r, theme);
        }
        else if (sourceType == InstagramPostSourceType.Game && Guid.TryParse(sourceId, out var gameId))
        {
            var game = await _gameRepository.GetByIdAsync(gameId, ct);
            if (game != null) return _composerService.ComposeSvg(sourceType, game, theme);
        }

        return null;
    }

    private async Task MarkSourceAsPublishedAsync(
        InstagramPostSourceType sourceType,
        string sourceId,
        string mediaId,
        string permalink,
        CancellationToken ct)
    {
        if (sourceType == InstagramPostSourceType.Giveaway && Guid.TryParse(sourceId, out var gId))
        {
            var giveaway = await _giveawayRepository.GetByIdAsync(gId, ct);
            if (giveaway != null)
            {
                giveaway.MarkPublishedOnInstagram(mediaId, permalink);
                await _giveawayRepository.UpdateAsync(giveaway, ct);
            }
        }
        else if (sourceType == InstagramPostSourceType.WeeklyRelease && Guid.TryParse(sourceId, out var rId))
        {
            var release = await _releaseRepository.GetByIdAsync(rId, ct);
            if (release != null)
            {
                release.MarkPublishedOnInstagram(mediaId, permalink);
                await _releaseRepository.UpdateAsync(release, ct);
            }
        }
    }
}
