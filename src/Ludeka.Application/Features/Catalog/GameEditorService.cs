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

namespace Ludeka.Application.Features.Catalog;

/// <summary>
/// Orquesta la edición de datos de catálogo por parte de moderadores y miembros fundadores,
/// garantizando auditoría, control de acceso por permisos granulares, persistencia y resolución de reportes.
/// </summary>
public class GameEditorService : IGameEditorService
{
    private readonly IGameRepository _gameRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IGameEditLogRepository _editLogRepository;
    private readonly ICatalogService _catalogService;
    private readonly IGameIssueReportService? _issueReportService;
    private readonly IAuditService? _auditService;

    public GameEditorService(
        IGameRepository gameRepository,
        ICurrentUserService currentUserService,
        IGameEditLogRepository editLogRepository,
        ICatalogService catalogService,
        IGameIssueReportService? issueReportService = null,
        IAuditService? auditService = null)
    {
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _editLogRepository = editLogRepository ?? throw new ArgumentNullException(nameof(editLogRepository));
        _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        _issueReportService = issueReportService;
        _auditService = auditService;
    }

    public async Task<GameDetailDto> UpdateGameAsync(UpdateGameDetailsCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Invariante de anonimia: editar el catálogo exige sesión real, aunque las banderas digan otra cosa.
        string editorUserId = SessionIdentity.Require(_currentUserService);

        // 1. Control de acceso granular
        if (!_currentUserService.IsFoundingTeam)
        {
            if (!_currentUserService.IsInRole("Moderator") || !_currentUserService.HasPermission(ModeratorPermission.CanEditGames))
            {
                throw new UnauthorizedAccessException("Se requiere el permiso de moderación 'CanEditGames' para editar fichas del catálogo.");
            }
        }

        // 2. Obtener el juego
        var game = await _gameRepository.GetByIdAsync(command.GameId, ct);
        if (game == null)
        {
            throw new KeyNotFoundException($"No se encontró ningún juego con ID '{command.GameId}'.");
        }

        // 3. Validar permiso específico para carga/reemplazo de imágenes
        bool isChangingCover = !string.IsNullOrWhiteSpace(command.CoverImageUrl) &&
                               !string.Equals(game.CoverImageUrl, command.CoverImageUrl, StringComparison.Ordinal);
        bool isChangingBackCover = !string.Equals(game.BackCoverImageUrl, command.BackCoverImageUrl, StringComparison.Ordinal);
        bool isChangingTable = !string.Equals(game.TableImageUrl, command.TableImageUrl, StringComparison.Ordinal);
        bool isChangingImages = isChangingCover || isChangingBackCover || isChangingTable;

        if (isChangingImages && !_currentUserService.IsFoundingTeam && !_currentUserService.HasPermission(ModeratorPermission.CanUploadImages))
        {
            throw new UnauthorizedAccessException("Se requiere el permiso de moderación 'CanUploadImages' para actualizar la carátula o imágenes del juego.");
        }

        // 4. Generar diff estructurado para la auditoría
        var changes = new List<string>();
        var fieldChanges = new List<FieldChangeDto>();

        if (!string.Equals(game.SpanishTitle, command.SpanishTitle, StringComparison.Ordinal))
        {
            changes.Add($"Título en español: '{game.SpanishTitle}' -> '{command.SpanishTitle}'");
            fieldChanges.Add(new FieldChangeDto("SpanishTitle", game.SpanishTitle, command.SpanishTitle));
        }

        if (!string.Equals(game.OriginalTitle, command.OriginalTitle, StringComparison.Ordinal))
        {
            changes.Add($"Título original: '{game.OriginalTitle}' -> '{command.OriginalTitle}'");
            fieldChanges.Add(new FieldChangeDto("OriginalTitle", game.OriginalTitle, command.OriginalTitle));
        }

        if (!string.Equals(game.Designer, command.Designer, StringComparison.Ordinal))
        {
            changes.Add($"Diseñador: '{game.Designer}' -> '{command.Designer}'");
            fieldChanges.Add(new FieldChangeDto("Designer", game.Designer, command.Designer));
        }

        if (!string.Equals(game.Publisher, command.Publisher, StringComparison.Ordinal))
        {
            changes.Add($"Editorial: '{game.Publisher}' -> '{command.Publisher}'");
            fieldChanges.Add(new FieldChangeDto("Publisher", game.Publisher, command.Publisher));
        }

        if (game.YearPublished != command.YearPublished)
        {
            changes.Add($"Año: {game.YearPublished} -> {command.YearPublished}");
            fieldChanges.Add(new FieldChangeDto("YearPublished", game.YearPublished.ToString(), command.YearPublished.ToString()));
        }

        if (isChangingCover)
        {
            changes.Add("Carátula frontal actualizada");
            fieldChanges.Add(new FieldChangeDto("CoverImageUrl", game.CoverImageUrl, command.CoverImageUrl));
        }

        if (isChangingBackCover)
        {
            changes.Add("Trasera de caja actualizada");
            fieldChanges.Add(new FieldChangeDto("BackCoverImageUrl", game.BackCoverImageUrl, command.BackCoverImageUrl));
        }

        if (isChangingTable)
        {
            changes.Add("Despliegue en mesa actualizado");
            fieldChanges.Add(new FieldChangeDto("TableImageUrl", game.TableImageUrl, command.TableImageUrl));
        }

        if (command.Sleeves != null)
        {
            changes.Add($"Fundas de cartas actualizadas ({command.Sleeves.Count} formatos)");
            fieldChanges.Add(new FieldChangeDto("Sleeves", $"{game.Sleeves.Count} formatos", $"{command.Sleeves.Count} formatos"));
        }

        string summary = changes.Count > 0 ? string.Join("; ", changes) : "Edición editorial de parámetros y metadatos";

        // 5. Aplicar modificaciones de dominio
        var age = new AgeRating(command.BoxAge, command.CommunityAge);
        var duration = new GameDuration(command.MinDurationMinutes, command.MaxDurationMinutes, command.EstimatedPerPlayerMinutes);

        game.UpdateCatalogInformation(
            command.SpanishTitle,
            command.OriginalTitle,
            command.Designer,
            command.Publisher,
            command.YearPublished,
            command.Description,
            command.Confrontation,
            command.Style,
            command.IsOfficialSolo,
            age,
            command.Language,
            command.Footprint,
            duration,
            command.MinPlayers,
            command.MaxPlayers
        );

        game.UpdateMediaUrls(
            !string.IsNullOrWhiteSpace(command.CoverImageUrl) ? command.CoverImageUrl : game.CoverImageUrl,
            game.ThumbnailUrl ?? command.CoverImageUrl,
            command.BackCoverImageUrl,
            command.TableImageUrl
        );

        if (command.Sleeves != null)
        {
            game.UpdateSleeves(command.Sleeves);
        }

        // 6. Persistir en repositorio
        await _gameRepository.UpdateAsync(game, ct);

        // 7. Registrar auditoría editorial específica de juego (INC-18)
        var log = new GameEditLog(
            game.Id,
            editorUserId,
            _currentUserService.UserName,
            summary,
            command.AssociatedReportId
        );
        await _editLogRepository.AddAsync(log, ct);

        // 8. Registrar en el servicio centralizado de auditoría (INC-20)
        if (_auditService != null)
        {
            await _auditService.RecordChangeAsync(new RecordAuditCommand(
                UserId: editorUserId,
                UserName: _currentUserService.UserName,
                Action: AuditAction.Updated,
                EntityType: AuditEntityType.Game,
                EntityId: game.Slug,
                EntityName: game.SpanishTitle,
                Summary: summary,
                Changes: fieldChanges
            ), ct);
        }

        // 9. Resolución en cascada de reporte de INC-17 si procede
        if (command.AssociatedReportId.HasValue && _issueReportService != null)
        {
            var note = !string.IsNullOrWhiteSpace(command.ResolutionNotes)
                ? command.ResolutionNotes.Trim()
                : $"Ficha corregida por {_currentUserService.UserName}: {summary}";

            await _issueReportService.ChangeStatusAsync(
                command.AssociatedReportId.Value,
                new UpdateGameReportStatusCommand(GameReportStatus.Resolved, editorUserId, note),
                ct
            );
        }

        // 10. Invalidar caché L1 de catálogo
        if (_catalogService is CachedCatalogService cached)
        {
            cached.Invalidate(game.Slug);
        }

        return GameDetailDto.FromEntity(game);
    }

    public async Task<IReadOnlyList<GameEditLogDto>> GetEditLogsAsync(Guid gameId, CancellationToken ct = default)
    {
        var logs = await _editLogRepository.GetByGameIdAsync(gameId, ct);
        return logs.Select(l => new GameEditLogDto(
            l.Id,
            l.GameId,
            l.EditorUserId,
            l.EditorName,
            l.SummaryOfChanges,
            l.AssociatedReportId,
            l.EditedAt
        )).ToList();
    }
}
