using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Features.Media;

public class MediaService : IMediaService
{
    private readonly IMediaRepository _mediaRepository;
    private readonly IGameRepository _gameRepository;
    private readonly IBrokenLinkCheckerService _brokenLinkChecker;
    private readonly ICurrentUserService? _currentUserService;
    private readonly IAuditService? _auditService;

    public MediaService(
        IMediaRepository mediaRepository,
        IGameRepository gameRepository,
        IBrokenLinkCheckerService brokenLinkChecker,
        ICurrentUserService? currentUserService = null,
        IAuditService? auditService = null)
    {
        _mediaRepository = mediaRepository ?? throw new ArgumentNullException(nameof(mediaRepository));
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _brokenLinkChecker = brokenLinkChecker ?? throw new ArgumentNullException(nameof(brokenLinkChecker));
        _currentUserService = currentUserService;
        _auditService = auditService;
    }

    public async Task<GameMediaHubDto> GetGameMediaAsync(Guid gameId, CancellationToken ct = default)
    {
        var items = await _mediaRepository.GetApprovedByGameIdAsync(gameId, ct);
        var activeItems = items.Where(x => !x.IsBroken).ToList();

        var quickOverviews = activeItems
            .Where(x => x.Category == MediaCategory.QuickOverview || (x.Category == 0 && x.Type == MediaType.QuickOverview))
            .OrderByDescending(x => x.PublishedAt)
            .Select(x => MediaItemDto.FromDomain(x))
            .ToList();

        var tutorials = activeItems
            .Where(x => x.Category == MediaCategory.Tutorial || (x.Category == 0 && x.Type == MediaType.Tutorial))
            .OrderByDescending(x => x.PublishedAt)
            .Select(x => MediaItemDto.FromDomain(x))
            .ToList();

        var playthroughs = activeItems
            .Where(x => x.Category == MediaCategory.Gameplay || (x.Category == 0 && x.Type == MediaType.Playthrough))
            .OrderByDescending(x => x.PublishedAt)
            .Select(x => MediaItemDto.FromDomain(x))
            .ToList();

        var instagramPosts = activeItems
            .Where(x => x.Type == MediaType.InstagramPost)
            .OrderByDescending(x => x.PublishedAt)
            .Select(x => MediaItemDto.FromDomain(x))
            .ToList();

        var shortReels = activeItems
            .Where(x => x.Type == MediaType.ShortReel)
            .OrderByDescending(x => x.PublishedAt)
            .Select(x => MediaItemDto.FromDomain(x))
            .ToList();

        var reviewsAndOpinions = activeItems
            .Where(x => x.Category == MediaCategory.ReviewOpinion && x.Type != MediaType.InstagramPost && x.Type != MediaType.ShortReel)
            .OrderByDescending(x => x.PublishedAt)
            .Select(x => MediaItemDto.FromDomain(x))
            .ToList();

        return new GameMediaHubDto(gameId, tutorials, playthroughs, instagramPosts, shortReels, quickOverviews, reviewsAndOpinions);
    }

    public async Task<IReadOnlyList<MediaItemDto>> GetPendingModerationAsync(CancellationToken ct = default)
    {
        var items = await _mediaRepository.GetPendingModerationAsync(ct);
        return await MapWithGameTitlesAsync(items, ct);
    }

    public async Task<IReadOnlyList<MediaItemDto>> GetOrphansAsync(CancellationToken ct = default)
    {
        var items = await _mediaRepository.GetOrphansAsync(ct);
        return items.Select(x => MediaItemDto.FromDomain(x)).ToList();
    }

    public async Task<IReadOnlyList<MediaItemDto>> GetApprovedAsync(CancellationToken ct = default)
    {
        var items = await _mediaRepository.GetApprovedAsync(ct);
        return await MapWithGameTitlesAsync(items, ct);
    }

    public async Task<MediaItemDto?> ApproveMediaAsync(Guid id, MediaCategory? category = null, CancellationToken ct = default)
    {
        EnsurePermission();

        var item = await _mediaRepository.GetByIdAsync(id, ct);
        if (item == null) return null;

        item.Approve(category);
        await _mediaRepository.UpdateAsync(item, ct);

        string? gameTitle = null;
        if (item.GameId.HasValue)
        {
            var game = await _gameRepository.GetByIdAsync(item.GameId.Value, ct);
            gameTitle = game?.SpanishTitle;
        }

        if (_auditService != null && _currentUserService != null)
        {
            await _auditService.RecordChangeAsync(new RecordAuditCommand(
                UserId: _currentUserService.UserId,
                UserName: _currentUserService.UserName,
                Action: AuditAction.StatusChanged,
                EntityType: AuditEntityType.Media,
                EntityId: item.Id.ToString(),
                EntityName: item.Title,
                Summary: $"Aprobado contenido multimedia '{item.Title}' con categoría '{item.Category}'",
                Changes: category.HasValue ? [new FieldChangeDto("Category", null, category.Value.ToString())] : null
            ), ct);
        }

        return MediaItemDto.FromDomain(item, gameTitle);
    }

    public async Task<MediaItemDto?> RejectMediaAsync(Guid id, CancellationToken ct = default)
    {
        EnsurePermission();

        var item = await _mediaRepository.GetByIdAsync(id, ct);
        if (item == null) return null;

        item.Reject();
        await _mediaRepository.UpdateAsync(item, ct);

        string? gameTitle = null;
        if (item.GameId.HasValue)
        {
            var game = await _gameRepository.GetByIdAsync(item.GameId.Value, ct);
            gameTitle = game?.SpanishTitle;
        }

        if (_auditService != null && _currentUserService != null)
        {
            await _auditService.RecordChangeAsync(new RecordAuditCommand(
                UserId: _currentUserService.UserId,
                UserName: _currentUserService.UserName,
                Action: AuditAction.StatusChanged,
                EntityType: AuditEntityType.Media,
                EntityId: item.Id.ToString(),
                EntityName: item.Title,
                Summary: $"Descartado contenido multimedia '{item.Title}' ({item.Type})"
            ), ct);
        }

        return MediaItemDto.FromDomain(item, gameTitle);
    }

    public async Task<MediaItemDto?> AssignOrphanMediaAsync(Guid id, Guid gameId, CancellationToken ct = default)
    {
        EnsurePermission();

        var item = await _mediaRepository.GetByIdAsync(id, ct);
        if (item == null) return null;

        var game = await _gameRepository.GetByIdAsync(gameId, ct);
        if (game == null)
            throw new ArgumentException("El juego especificado no existe en el catálogo.", nameof(gameId));

        item.AssignToGame(gameId);
        await _mediaRepository.UpdateAsync(item, ct);

        return MediaItemDto.FromDomain(item, game.SpanishTitle);
    }

    public async Task<BrokenLinkReportDto> CheckBrokenLinksAsync(CancellationToken ct = default)
    {
        // La verificación marca recursos rotos: es una escritura administrativa y revalida permiso.
        EnsurePermission();

        return await _brokenLinkChecker.CheckLinksAsync(ct);
    }

    public async Task<MediaItemDto> CreateMediaItemAsync(MediaItem item, CancellationToken ct = default)
    {
        await _mediaRepository.AddAsync(item, ct);

        string? gameTitle = null;
        if (item.GameId.HasValue)
        {
            var game = await _gameRepository.GetByIdAsync(item.GameId.Value, ct);
            gameTitle = game?.SpanishTitle;
        }

        return MediaItemDto.FromDomain(item, gameTitle);
    }

    public async Task<MediaItemDto?> UpdateMediaCategoryAsync(Guid id, MediaCategory newCategory, CancellationToken ct = default)
    {
        EnsurePermission();

        var item = await _mediaRepository.GetByIdAsync(id, ct);
        if (item == null) return null;

        var oldCategory = item.Category;
        if (oldCategory == newCategory)
        {
            string? currentTitle = null;
            if (item.GameId.HasValue)
            {
                var g = await _gameRepository.GetByIdAsync(item.GameId.Value, ct);
                currentTitle = g?.SpanishTitle;
            }
            return MediaItemDto.FromDomain(item, currentTitle);
        }

        item.ChangeCategory(newCategory);
        await _mediaRepository.UpdateAsync(item, ct);

        string? gameTitle = null;
        if (item.GameId.HasValue)
        {
            var game = await _gameRepository.GetByIdAsync(item.GameId.Value, ct);
            gameTitle = game?.SpanishTitle;
        }

        if (_auditService != null && _currentUserService != null)
        {
            await _auditService.RecordChangeAsync(new RecordAuditCommand(
                UserId: _currentUserService.UserId,
                UserName: _currentUserService.UserName,
                Action: AuditAction.Updated,
                EntityType: AuditEntityType.Media,
                EntityId: item.Id.ToString(),
                EntityName: item.Title,
                Summary: $"Cambiada categoría de '{oldCategory}' a '{newCategory}' para el vídeo '{item.Title}'",
                Changes: [new FieldChangeDto("Category", oldCategory.ToString(), newCategory.ToString())]
            ), ct);
        }

        return MediaItemDto.FromDomain(item, gameTitle);
    }

    public async Task<MediaItemDto?> ReassignMediaGameAsync(Guid id, Guid newGameId, CancellationToken ct = default)
    {
        EnsurePermission();

        var item = await _mediaRepository.GetByIdAsync(id, ct);
        if (item == null) return null;

        var targetGame = await _gameRepository.GetByIdAsync(newGameId, ct);
        if (targetGame == null)
            throw new ArgumentException("El juego de destino especificado no existe en el catálogo.", nameof(newGameId));

        var oldGameId = item.GameId;
        string? oldGameTitle = null;
        if (oldGameId.HasValue)
        {
            var oldGame = await _gameRepository.GetByIdAsync(oldGameId.Value, ct);
            oldGameTitle = oldGame?.SpanishTitle;
        }

        item.ReassignGame(newGameId);
        await _mediaRepository.UpdateAsync(item, ct);

        if (_auditService != null && _currentUserService != null)
        {
            await _auditService.RecordChangeAsync(new RecordAuditCommand(
                UserId: _currentUserService.UserId,
                UserName: _currentUserService.UserName,
                Action: AuditAction.Updated,
                EntityType: AuditEntityType.Media,
                EntityId: item.Id.ToString(),
                EntityName: item.Title,
                Summary: $"Reasignado vídeo '{item.Title}' de '{oldGameTitle ?? "Sin asignar"}' a '{targetGame.SpanishTitle}'",
                Changes: [new FieldChangeDto("GameId", oldGameTitle ?? oldGameId?.ToString(), targetGame.SpanishTitle)]
            ), ct);
        }

        return MediaItemDto.FromDomain(item, targetGame.SpanishTitle);
    }

    public async Task<bool> DeleteMediaAsync(Guid id, CancellationToken ct = default)
    {
        EnsurePermission();

        var item = await _mediaRepository.GetByIdAsync(id, ct);
        if (item == null) return false;

        string? gameTitle = null;
        if (item.GameId.HasValue)
        {
            var game = await _gameRepository.GetByIdAsync(item.GameId.Value, ct);
            gameTitle = game?.SpanishTitle;
        }

        await _mediaRepository.DeleteAsync(id, ct);

        if (_auditService != null && _currentUserService != null)
        {
            await _auditService.RecordChangeAsync(new RecordAuditCommand(
                UserId: _currentUserService.UserId,
                UserName: _currentUserService.UserName,
                Action: AuditAction.Deleted,
                EntityType: AuditEntityType.Media,
                EntityId: item.Id.ToString(),
                EntityName: item.Title,
                Summary: $"Eliminado contenido multimedia '{item.Title}'{(gameTitle != null ? $" de la ficha de '{gameTitle}'" : "")}"
            ), ct);
        }

        return true;
    }

    private void EnsurePermission()
    {
        if (_currentUserService == null) return;

        // Invariante de anonimia: sin sesión no hay permiso que valga ni moderación posible.
        SessionIdentity.Require(_currentUserService);

        if (!_currentUserService.IsFoundingTeam && !_currentUserService.HasPermission(ModeratorPermission.CanApproveMedia))
        {
            throw new UnauthorizedAccessException("Se requiere el permiso de moderación 'CanApproveMedia' para moderar contenido multimedia.");
        }
    }

    private async Task<IReadOnlyList<MediaItemDto>> MapWithGameTitlesAsync(IReadOnlyList<MediaItem> items, CancellationToken ct)
    {
        var result = new List<MediaItemDto>(items.Count);
        foreach (var item in items)
        {
            string? gameTitle = null;
            if (item.GameId.HasValue)
            {
                var game = await _gameRepository.GetByIdAsync(item.GameId.Value, ct);
                gameTitle = game?.SpanishTitle;
            }
            result.Add(MediaItemDto.FromDomain(item, gameTitle));
        }
        return result;
    }
}
