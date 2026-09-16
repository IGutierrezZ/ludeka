using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;

namespace Ludeka.Application.Features.Reports;

public class GameIssueReportService : IGameIssueReportService
{
    private readonly IGameIssueReportRepository _repository;
    private readonly ICurrentUserService? _currentUserService;
    private readonly IAuditService? _auditService;

    public GameIssueReportService(
        IGameIssueReportRepository repository,
        ICurrentUserService? currentUserService = null,
        IAuditService? auditService = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _currentUserService = currentUserService;
        _auditService = auditService;
    }

    public async ValueTask<GameIssueReportDto> CreateReportAsync(CreateGameReportCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.GameId == Guid.Empty)
            throw new ArgumentException("El identificador del juego no puede estar vacío.", nameof(command.GameId));
        if (string.IsNullOrWhiteSpace(command.GameSlug))
            throw new ArgumentException("El slug del juego no puede estar vacío.", nameof(command.GameSlug));
        if (string.IsNullOrWhiteSpace(command.GameTitle))
            throw new ArgumentException("El título del juego no puede estar vacío.", nameof(command.GameTitle));

        // Validación de detalle cuando el tipo es 'Other'
        if (command.IssueType == GameIssueType.Other && string.IsNullOrWhiteSpace(command.Details))
            throw new ArgumentException("Debes detallar la incidencia si seleccionas 'Otro problema'.", nameof(command.Details));

        // Sanitización y límite de longitud
        var details = command.Details?.Trim();
        if (details != null && details.Length > 1000)
            details = details[..1000];

        var reporter = string.IsNullOrWhiteSpace(command.ReporterNameOrAlias) ? "Comunidad anónima" : command.ReporterNameOrAlias.Trim();

        // Invariante de anonimia: el reporte exige sesión y su identidad es siempre la de la sesión.
        string? reportedByUserId = command.UserId;
        if (_currentUserService != null)
        {
            reportedByUserId = SessionIdentity.Require(_currentUserService);
        }

        var report = new GameIssueReport(
            gameId: command.GameId,
            gameSlug: command.GameSlug.Trim(),
            gameTitle: command.GameTitle.Trim(),
            issueType: command.IssueType,
            details: details,
            reporterNameOrAlias: reporter,
            reportedByUserId: reportedByUserId
        );

        await _repository.AddAsync(report, ct);

        return MapToDto(report);
    }

    public async ValueTask<IReadOnlyList<GameIssueReportDto>> GetReportsAsync(GameReportFilter filter, CancellationToken ct = default)
    {
        var reports = await _repository.GetAllAsync(filter ?? new GameReportFilter(), ct);
        return reports.Select(MapToDto).ToList();
    }

    public async ValueTask<GameIssueReportSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        return await _repository.GetSummaryAsync(ct);
    }

    public async ValueTask<GameIssueReportDto?> ChangeStatusAsync(Guid id, UpdateGameReportStatusCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Control de permisos granulares
        if (_currentUserService != null)
        {
            if (!_currentUserService.IsFoundingTeam && !_currentUserService.HasPermission(ModeratorPermission.CanResolveReports))
            {
                throw new UnauthorizedAccessException("Se requiere el permiso de moderación 'CanResolveReports' para gestionar incidencias.");
            }
        }

        var report = await _repository.GetByIdAsync(id, ct);
        if (report == null)
            return null;

        var oldStatus = report.Status;
        var oldNotes = report.ModeratorNotes;

        switch (command.NewStatus)
        {
            case GameReportStatus.InReview:
                report.MarkAsInReview(command.ModeratorId, command.Notes);
                break;
            case GameReportStatus.Resolved:
                report.Resolve(command.ModeratorId, command.Notes);
                break;
            case GameReportStatus.Dismissed:
                report.Dismiss(command.ModeratorId, command.Notes ?? "Descartado por el moderador");
                break;
            case GameReportStatus.Pending:
                report.Reopen();
                break;
        }

        await _repository.UpdateAsync(report, ct);

        // Registro en auditoría
        if (_auditService != null)
        {
            var actorUserId = _currentUserService?.UserId ?? command.ModeratorId;
            var actorUserName = _currentUserService?.UserName ?? command.ModeratorId;

            await _auditService.RecordChangeAsync(new RecordAuditCommand(
                UserId: actorUserId,
                UserName: actorUserName,
                Action: AuditAction.StatusChanged,
                EntityType: AuditEntityType.Report,
                EntityId: report.Id.ToString(),
                EntityName: $"Incidencia: {report.GameTitle}",
                Summary: $"Estado cambiado de '{oldStatus}' a '{command.NewStatus}'",
                Changes: [
                    new FieldChangeDto("Status", oldStatus.ToString(), command.NewStatus.ToString()),
                    new FieldChangeDto("ModeratorNotes", oldNotes, command.Notes)
                ]
            ), ct);
        }

        return MapToDto(report);
    }

    private static GameIssueReportDto MapToDto(GameIssueReport r)
    {
        return new GameIssueReportDto(
            Id: r.Id,
            GameId: r.GameId,
            GameSlug: r.GameSlug,
            GameTitle: r.GameTitle,
            IssueType: r.IssueType,
            IssueTypeName: GetIssueTypeDisplayName(r.IssueType),
            Details: r.Details,
            ReportedByUserId: r.ReportedByUserId,
            ReporterNameOrAlias: r.ReporterNameOrAlias,
            Status: r.Status,
            StatusName: GetStatusDisplayName(r.Status),
            ModeratorNotes: r.ModeratorNotes,
            ResolvedByUserId: r.ResolvedByUserId,
            CreatedAt: r.CreatedAt,
            UpdatedAt: r.UpdatedAt,
            ResolvedAt: r.ResolvedAt
        );
    }

    public static string GetIssueTypeDisplayName(GameIssueType type) => type switch
    {
        GameIssueType.WrongImage => "Imagen incorrecta o de otra edición",
        GameIssueType.BrokenImage => "Imagen no carga o enlace roto",
        GameIssueType.IncorrectPlayerCount => "Número de jugadores o semáforo erróneo",
        GameIssueType.IncorrectDuration => "Duración de partida desajustada",
        GameIssueType.IncorrectAge => "Edad recomendada inconsistente",
        GameIssueType.ErroneousMetadata => "Erratas en metadatos (título, autor, editorial)",
        GameIssueType.BrokenPurchaseLink => "Enlace de compra o tienda defectuoso",
        GameIssueType.Other => "Otro problema o sugerencia libre",
        _ => "Incidencia no especificada"
    };

    public static string GetStatusDisplayName(GameReportStatus status) => status switch
    {
        GameReportStatus.Pending => "Pendiente",
        GameReportStatus.InReview => "En revisión",
        GameReportStatus.Resolved => "Resuelto",
        GameReportStatus.Dismissed => "Descartado",
        _ => "Desconocido"
    };
}
