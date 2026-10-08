using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Features.Bgg;
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
    private readonly IBggClient? _bggClient;
    private readonly IBggRawSnapshotRepository? _snapshotRepo;

    public GameEditorService(
        IGameRepository gameRepository,
        ICurrentUserService currentUserService,
        IGameEditLogRepository editLogRepository,
        ICatalogService catalogService,
        IGameIssueReportService? issueReportService = null,
        IAuditService? auditService = null,
        IBggClient? bggClient = null,
        IBggRawSnapshotRepository? snapshotRepo = null)
    {
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _editLogRepository = editLogRepository ?? throw new ArgumentNullException(nameof(editLogRepository));
        _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        _issueReportService = issueReportService;
        _auditService = auditService;
        _bggClient = bggClient;
        _snapshotRepo = snapshotRepo;
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

        if (command.Ean != null)
        {
            var normalizedEan = string.IsNullOrWhiteSpace(command.Ean) ? null : command.Ean.Trim();
            if (!string.Equals(game.Ean, normalizedEan, StringComparison.Ordinal))
            {
                changes.Add($"Código de barras (EAN-13): '{game.Ean}' -> '{normalizedEan}'");
                fieldChanges.Add(new FieldChangeDto("Ean", game.Ean, normalizedEan));
            }
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

        if (command.Ean != null)
        {
            game.UpdateEan(command.Ean);
        }

        if (command.BggId.HasValue && command.BggId.Value > 0 && command.BggId.Value != game.BggId)
        {
            changes.Add($"BGG ID: '{game.BggId}' -> '{command.BggId.Value}'");
            fieldChanges.Add(new FieldChangeDto("BggId", game.BggId.ToString(), command.BggId.Value.ToString()));
            game.UpdateBggId(command.BggId.Value);
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

    public async Task<GameDetailDto> AssociateBggIdAsync(Guid gameId, int bggId, CancellationToken ct = default)
    {
        string editorUserId = SessionIdentity.Require(_currentUserService);

        if (!_currentUserService.IsFoundingTeam)
        {
            if (!_currentUserService.IsInRole("Moderator") || !_currentUserService.HasPermission(ModeratorPermission.CanEditGames))
            {
                throw new UnauthorizedAccessException("Se requiere el permiso de moderación 'CanEditGames' para asociar el BGG ID del juego.");
            }
        }

        if (bggId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bggId), "El BGG ID debe ser un entero positivo.");
        }

        var game = await _gameRepository.GetByIdAsync(gameId, ct);
        if (game == null)
        {
            throw new KeyNotFoundException($"No se encontró ningún juego con ID '{gameId}'.");
        }

        int oldBggId = game.BggId;
        game.UpdateBggId(bggId);
        await _gameRepository.UpdateAsync(game, ct);

        var summary = $"Asociado nuevo BGG ID: '{oldBggId}' -> '{bggId}'";
        var log = new GameEditLog(
            game.Id,
            editorUserId,
            _currentUserService.UserName,
            summary,
            null
        );
        await _editLogRepository.AddAsync(log, ct);

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
                Changes: [new FieldChangeDto("BggId", oldBggId.ToString(), bggId.ToString())]
            ), ct);
        }

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

    public async Task<GameBggSyncResultDto> ForceSyncFromBggAsync(Guid gameId, CancellationToken ct = default)
    {
        string editorUserId = SessionIdentity.Require(_currentUserService);
        if (!_currentUserService.IsFoundingTeam)
        {
            if (!_currentUserService.IsInRole("Moderator") || !_currentUserService.HasPermission(ModeratorPermission.CanEditGames))
            {
                throw new UnauthorizedAccessException("Se requiere el permiso de moderación 'CanEditGames' para forzar la sincronización con BGG.");
            }
        }

        var game = await _gameRepository.GetByIdAsync(gameId, ct);
        if (game == null)
        {
            throw new KeyNotFoundException($"No se encontró ningún juego con ID '{gameId}'.");
        }

        if (game.BggId <= 0)
        {
            throw new InvalidOperationException("El juego no tiene asignado un identificador válido de BoardGameGeek (BGG ID).");
        }

        if (_bggClient == null)
        {
            throw new InvalidOperationException("El cliente de BGG no está configurado.");
        }

        string? rawJson = await _bggClient.FetchRawThingJsonAsync(game.BggId, includeVersions: true, ct);
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return new GameBggSyncResultDto(
                Success: false,
                BggId: game.BggId,
                OldSpanishTitle: game.SpanishTitle,
                NewSpanishTitle: game.SpanishTitle,
                OldSpanishPublisher: game.SpanishPublisher,
                NewSpanishPublisher: game.SpanishPublisher,
                OldEan: game.Ean,
                NewEan: game.Ean,
                CoverImageUrl: game.CoverImageUrl,
                ThumbnailUrl: game.ThumbnailUrl,
                UpdatedFields: [],
                Message: $"No se pudo obtener información desde BGG para el ID {game.BggId}."
            );
        }

        if (_snapshotRepo != null)
        {
            var snapshot = new BggRawSnapshot(game.BggId, rawJson, apiVersion: 2, fetchedAt: DateTimeOffset.UtcNow);
            await _snapshotRepo.UpsertAsync(snapshot, ct);
        }


        var vInfo = BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson(rawJson);
        var (rootCover, rootThumb) = BggRawSnapshotParser.ExtractRootImagesFromJson(rawJson);

        var updatedFields = new List<string>();
        var fieldChanges = new List<FieldChangeDto>();

        string oldTitle = game.SpanishTitle;
        string? oldPublisher = game.SpanishPublisher;
        string? oldEan = game.Ean;
        string? oldCover = game.CoverImageUrl;
        string? oldThumb = game.ThumbnailUrl;

        // 1. Título en español
        if (vInfo != null && !string.IsNullOrWhiteSpace(vInfo.Title))
        {
            string candidateTitle = BggRawSnapshotParser.CleanVersionTitle(vInfo.Title) ?? vInfo.Title;
            if (!BggRawSnapshotParser.IsGenericEditionTitle(candidateTitle) && candidateTitle != game.SpanishTitle)
            {
                fieldChanges.Add(new FieldChangeDto("SpanishTitle", game.SpanishTitle, candidateTitle));
                game.UpdateSpanishTitle(candidateTitle);
                updatedFields.Add("Título en español");
            }
        }
        else if (BggRawSnapshotParser.IsGenericEditionTitle(game.SpanishTitle))
        {
            if (game.SpanishTitle != game.OriginalTitle)
            {
                fieldChanges.Add(new FieldChangeDto("SpanishTitle", game.SpanishTitle, game.OriginalTitle));
                game.UpdateSpanishTitle(game.OriginalTitle);
                updatedFields.Add("Título en español");
            }
        }

        // 2. Editorial española
        string? candidatePublisher = vInfo?.Publisher?.Trim();


        if (!string.IsNullOrWhiteSpace(candidatePublisher) && candidatePublisher != game.SpanishPublisher)
        {
            fieldChanges.Add(new FieldChangeDto("SpanishPublisher", game.SpanishPublisher ?? string.Empty, candidatePublisher));
            game.UpdateSpanishPublisher(candidatePublisher);
            updatedFields.Add("Editorial española");
        }

        // 3. Código EAN-13
        if (vInfo != null && !string.IsNullOrWhiteSpace(vInfo.Ean) && vInfo.Ean != game.Ean)
        {
            fieldChanges.Add(new FieldChangeDto("Ean", game.Ean ?? string.Empty, vInfo.Ean));
            game.UpdateEan(vInfo.Ean);
            updatedFields.Add("Código EAN");
        }

        // 4. Portada y Miniatura
        string? targetCover = (!string.IsNullOrWhiteSpace(vInfo?.CoverImageUrl))
            ? vInfo.CoverImageUrl
            : (!string.IsNullOrWhiteSpace(rootCover) ? rootCover : game.CoverImageUrl);

        string? targetThumb = (!string.IsNullOrWhiteSpace(vInfo?.ThumbnailUrl))
            ? vInfo.ThumbnailUrl
            : (!string.IsNullOrWhiteSpace(rootThumb) ? rootThumb : game.ThumbnailUrl);

        if (string.IsNullOrWhiteSpace(game.CoverImageUrl) && !string.IsNullOrWhiteSpace(targetCover))
        {
            fieldChanges.Add(new FieldChangeDto("CoverImageUrl", game.CoverImageUrl ?? string.Empty, targetCover));
            game.UpdateImages(targetCover, targetThumb ?? game.ThumbnailUrl);
            updatedFields.Add("Imagen de portada");
        }
        else if (!string.IsNullOrWhiteSpace(vInfo?.CoverImageUrl) && vInfo.CoverImageUrl != game.CoverImageUrl)
        {
            fieldChanges.Add(new FieldChangeDto("CoverImageUrl", game.CoverImageUrl ?? string.Empty, vInfo.CoverImageUrl));
            game.UpdateImages(vInfo.CoverImageUrl, vInfo.ThumbnailUrl ?? targetThumb);
            updatedFields.Add("Imagen de portada");
        }

        // 5. Persistencia y Auditoría si hubo cambios
        if (updatedFields.Count > 0)
        {
            await _gameRepository.UpdateAsync(game, ct);

            string summary = $"Sincronización forzada desde BGG #{game.BggId}: {string.Join(", ", updatedFields)}";
            var log = new GameEditLog(
                game.Id,
                editorUserId,
                _currentUserService.UserName,
                summary,
                null
            );
            await _editLogRepository.AddAsync(log, ct);

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

            if (_catalogService is CachedCatalogService cached)
            {
                cached.Invalidate(game.Slug);
            }
        }

        string message = updatedFields.Count > 0
            ? $"Sincronización con BGG exitosa. Se actualizaron: {string.Join(", ", updatedFields)}."
            : "Sincronización con BGG completada: el snapshot se actualizó y los datos de la ficha ya estaban al día.";

        return new GameBggSyncResultDto(
            Success: true,
            BggId: game.BggId,
            OldSpanishTitle: oldTitle,
            NewSpanishTitle: game.SpanishTitle,
            OldSpanishPublisher: oldPublisher,
            NewSpanishPublisher: game.SpanishPublisher,
            OldEan: oldEan,
            NewEan: game.Ean,
            CoverImageUrl: game.CoverImageUrl,
            ThumbnailUrl: game.ThumbnailUrl,
            UpdatedFields: updatedFields,
            Message: message
        );
    }
}


