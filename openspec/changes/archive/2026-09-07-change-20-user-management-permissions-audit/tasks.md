# Tareas de Implementación: change-20-user-management-permissions-audit

## Fase 1: Dominio (`Ludeka.Core`)
- [ ] 1.1 Crear enumerados `UserRole.cs`, `UserStatus.cs`, `ModeratorPermission.cs` ([Flags]), `AuditAction.cs` y `AuditEntityType.cs` en `Ludeka.Core/Enums`.
- [ ] 1.2 Crear Value Object `AuditFieldChange.cs` en `Ludeka.Core/ValueObjects`.
- [ ] 1.3 Crear entidades `AppUser.cs` y `AuditLogEntry.cs` con métodos de dominio para validación, RBAC y auditoría en `Ludeka.Core/Entities`.
- [ ] 1.4 Crear pruebas unitarias de dominio para `AppUser`, `ModeratorPermission` y `AuditLogEntry` en `Ludeka.UnitTests/Domain`.

## Fase 2: Aplicación y Contratos (`Ludeka.Application`)
- [ ] 2.1 Crear interfaces de repositorio `IUserRepository.cs` e `IAuditLogRepository.cs` en `Ludeka.Application/Contracts`.
- [ ] 2.2 Crear DTOs y comandos para gestión de usuarios y auditoría (`UserManagementDtos.cs`, `AuditDtos.cs`) en `Ludeka.Application/DTOs`.
- [ ] 2.3 Ampliar `ICurrentUserService.cs` con `bool HasPermission(ModeratorPermission permission)` y `void SwitchUser(string userId)`.
- [ ] 2.4 Crear contratos de servicio `IUserManagementService.cs` e `IAuditService.cs` con sus implementaciones `UserManagementService.cs` y `AuditService.cs`.
- [ ] 2.5 Reforzar servicios existentes (`GameEditorService`, `PublisherService`, `CreatorService`, `StoreService`, `GameIssueReportService`, `MediaService`) con comprobaciones de permisos y registro de auditoría (`IAuditService`).
- [ ] 2.6 Crear pruebas unitarias de aplicación para `UserManagementService`, `AuditService` y validaciones de autorización defensiva en `Ludeka.UnitTests/Services`.

## Fase 3: Infraestructura y Persistencia (`Ludeka.Infrastructure`)
- [ ] 3.1 Configurar `DbSet<AppUser>` y `DbSet<AuditLogEntry>` en `LudekaDbContext.cs` con índices optimizados y mapeo JSON nativo de `Changes`.
- [ ] 3.2 Implementar repositorios SQLite: `SqliteUserRepository.cs` y `SqliteAuditLogRepository.cs` en `Ludeka.Infrastructure/Data`.
- [ ] 3.3 Actualizar `DefaultCurrentUserService.cs` para soportar `HasPermission` y resolución dinámica de usuarios y permisos.
- [ ] 3.4 Crear `UserManagementSeeder.cs` para sembrar usuarios iniciales (fundadores, moderadores especializados, usuarios comunitarios) y logs de auditoría iniciales.
- [ ] 3.5 Registrar nuevos repositorios y servicios en la inyección de dependencias en `Ludeka.Web/Program.cs`.

## Fase 4: Componentes Web y Vistas Blazor (`Ludeka.Web`)
- [ ] 4.1 Crear componente modal `UserPermissionsModal.razor` para conmutar el rol y los 7 permisos granulares de moderación.
- [ ] 4.2 Crear vista de gestión de usuarios `UserManagement.razor` (`/admin/usuarios`):
  - Exclusiva para `FoundingTeam`.
  - Buscador reactivo, filtros por rol y estado.
  - Tarjetas editoriales con badges de permisos, edición de permisos, suspensión/activación y conmutador rápido para simular sesión en caliente.
- [ ] 4.3 Crear vista de registro de auditoría `AuditLogViewer.razor` (`/admin/auditoria`):
  - Exclusiva para `FoundingTeam`.
  - Filtros multicriterio (usuario, tipo de entidad, acción, fechas).
  - Listado cronológico con visor expandible de cambios estructurados campo a campo (*diff* visual).
- [ ] 4.4 Agregar enlaces de administración en `MainLayout.razor` bajo `IsFoundingTeam`.
- [ ] 4.5 Condicionar la visibilidad de los botones de edición y alta en toda la web según el permiso específico (`CanEditGames`, `CanManagePublishers`, `CanManageCreators`, `CanManageStoreLinks`, `CanResolveReports`, `CanApproveMedia`).

## Fase 5: Verificación y Cierre
- [ ] 5.1 Ejecutar la suite completa de pruebas unitarias (`dotnet test`) y verificar que todos los casos pasen al 100%.
- [ ] 5.2 Generar reporte de verificación `verify-report.md`.
- [ ] 5.3 Archivar el incremento en `.openspec/changes/archive/2026-09-07-change-20-user-management-permissions-audit/` y sincronizar especificaciones en `.openspec/specs/`.
- [ ] 5.4 Actualizar el estado en `docs/increments/inc-20-user-management-permissions-audit.md` a "Completado y Archivado".
- [ ] 5.5 Registrar decisiones y resumen de sesión en Engram (`mem_save` y `mem_session_summary`).
