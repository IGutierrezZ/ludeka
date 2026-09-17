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

namespace Ludeka.Application.Features.Admin;

public class AuditService : IAuditService
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ICurrentUserService _currentUserService;

    public AuditService(
        IAuditLogRepository auditLogRepository,
        ICurrentUserService currentUserService)
    {
        _auditLogRepository = auditLogRepository ?? throw new ArgumentNullException(nameof(auditLogRepository));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task RecordChangeAsync(RecordAuditCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Invariante de anonimia: la bitácora solo registra identidades reales de sesión, nunca
        // identidades vacías ni simuladas.
        string actorUserId = SessionIdentity.Require(command.UserId);
        if (string.IsNullOrWhiteSpace(command.UserName))
        {
            throw new UnauthorizedAccessException(SessionIdentity.SessionRequiredMessage);
        }

        var changes = command.Changes?
            .Select(c => new AuditFieldChange(c.FieldName, c.OldValue, c.NewValue))
            .ToList();

        var entry = new AuditLogEntry(
            userId: actorUserId,
            userName: command.UserName.Trim(),
            action: command.Action,
            entityType: command.EntityType,
            entityId: command.EntityId,
            entityName: command.EntityName,
            summary: command.Summary,
            changes: changes
        );

        await _auditLogRepository.AddAsync(entry, ct);
    }

    public async Task<AuditLogPageDto> GetAuditLogsAsync(AuditLogFilterDto filter, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (!_currentUserService.IsFoundingTeam)
        {
            throw new UnauthorizedAccessException("Acceso denegado: La consulta de la bitácora de auditoría está reservada a la Mesa Fundadora.");
        }

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        var total = await _auditLogRepository.CountLogsAsync(
            filter.UserId,
            filter.EntityType,
            filter.Action,
            filter.FromDate,
            filter.ToDate,
            ct);

        var entries = await _auditLogRepository.GetLogsAsync(
            filter.UserId,
            filter.EntityType,
            filter.Action,
            filter.FromDate,
            filter.ToDate,
            skip,
            pageSize,
            ct);

        var dtos = entries.Select(MapToDto).ToList();
        var totalPages = total == 0 ? 1 : (int)Math.Ceiling(total / (double)pageSize);

        return new AuditLogPageDto(dtos, total, page, pageSize, totalPages);
    }

    public static AuditLogDto MapToDto(AuditLogEntry entry)
    {
        var changes = entry.Changes.Select(c => new FieldChangeDto(c.FieldName, c.OldValue, c.NewValue)).ToList();

        return new AuditLogDto(
            Id: entry.Id,
            UserId: entry.UserId,
            UserName: entry.UserName,
            Timestamp: entry.Timestamp,
            Action: entry.Action,
            ActionDisplayName: GetActionDisplayName(entry.Action),
            EntityType: entry.EntityType,
            EntityTypeDisplayName: GetEntityTypeDisplayName(entry.EntityType),
            EntityId: entry.EntityId,
            EntityName: entry.EntityName,
            Summary: entry.Summary,
            Changes: changes
        );
    }

    public static string GetActionDisplayName(AuditAction action) => action switch
    {
        AuditAction.Created => "Creación",
        AuditAction.Updated => "Modificación",
        AuditAction.Deleted => "Eliminación",
        AuditAction.StatusChanged => "Cambio de Estado",
        AuditAction.RoleChanged => "Cambio de Rol",
        AuditAction.PermissionsChanged => "Permisos Modificados",
        AuditAction.Published => "Publicación",
        AuditAction.LinkedProvider => "Vinculación de Proveedor",
        AuditAction.UnlinkedProvider => "Desvinculación de Proveedor",
        _ => "Operación"
    };

    public static string GetEntityTypeDisplayName(AuditEntityType type) => type switch
    {
        AuditEntityType.Game => "Juego",
        AuditEntityType.Publisher => "Editorial",
        AuditEntityType.Creator => "Creador de Contenido",
        AuditEntityType.Store => "Tienda",
        AuditEntityType.Media => "Multimedia",
        AuditEntityType.Report => "Reporte",
        AuditEntityType.User => "Usuario",
        AuditEntityType.InstagramPost => "Post de Instagram",
        _ => "Entidad"
    };
}
