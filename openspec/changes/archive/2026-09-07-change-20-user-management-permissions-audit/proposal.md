# Propuesta de Cambio: change-20-user-management-permissions-audit

## 1. Resumen Ejecutivo
El Incremento 20 dota a la **Mesa Fundadora** de Ludeka de un **Panel Centralizado de Gestión de Usuarios y Permisos Granulares de Moderación** (`/admin/usuarios`), acompañado de un **Centro Integral de Auditoría y Trazabilidad Editorial** (`/admin/auditoria`). Este sistema implanta un modelo de autorización RBAC basado en permisos específicos por área de moderación (juegos, imágenes, editoriales, autores, multimedia, reportes comunitarios y tiendas) y asegura que cada mutación en los datos del sistema quede registrada con fecha UTC, autor, tipo de acción y desglose estructurado (*diff*) de los valores antes y después del cambio.

---

## 2. Justificación y Valor para el Ecosistema
1. **Gobernanza Editorial Segura:** La Mesa Fundadora puede delegar tareas a moderadores de confianza sin concederles acceso indiscriminado a todas las áreas de la plataforma, acotando el radio de impacto ante errores operativos o descuidos.
2. **Especialización de Roles Comunitarios:** Permite asignar permisos según la especialidad del voluntario (ej. un moderador audiovisual centrado en `CanApproveMedia`, una redactora de fichas con `CanEditGames` y `CanUploadImages`, o un gestor de industria con `CanManagePublishers` y `CanManageCreators`).
3. **Trazabilidad Total e Indeleble:** Cualquier modificación sobre juegos, editoriales, autores, enlaces de tiendas, reportes o roles de usuario genera automáticamente una entrada de auditoría inmutable en `AuditLogEntry`, garantizando transparencia absoluta frente a vandalismo o controversias.
4. **Control de Acceso Reactivo y Defensivo:** La interfaz oculta de forma proactiva botones y formularios no autorizados, mientras que la capa de aplicación valida severamente cada comando lanzando `UnauthorizedAccessException` si no se cumplen los privilegios necesarios.
5. **Simulación y Demostración Fluida:** Se habilita un conmutador rápido de usuario simulado en el panel para que los administradores y evaluadores puedan experimentar la plataforma bajo la perspectiva de diferentes moderadores o usuarios comunitarios.

---

## 3. Alcance de la Propuesta

### 3.1 Dominio (`Ludeka.Core`)
- Enums:
  - `UserRole`: `FoundingTeam`, `Moderator`, `CommunityUser`.
  - `UserStatus`: `Active`, `Suspended`.
  - `ModeratorPermission` con `[Flags]`: `None`, `CanEditGames`, `CanUploadImages`, `CanManagePublishers`, `CanManageCreators`, `CanApproveMedia`, `CanResolveReports`, `CanManageStoreLinks`, `All`.
  - `AuditAction`: `Created`, `Updated`, `Deleted`, `StatusChanged`, `RoleChanged`, `PermissionsChanged`.
  - `AuditEntityType`: `Game`, `Publisher`, `Creator`, `Store`, `Media`, `Report`, `User`.
- Entidad `AppUser`:
  - Identificador único (`Id`), `UserName`, `Email`, `Role`, `Status`, `Permissions`, `CreatedAt`, `UpdatedAt`.
  - Métodos de dominio: `UpdateRoleAndPermissions`, `UpdateStatus`, `HasPermission(ModeratorPermission permission)`.
- Entidad `AuditLogEntry`:
  - `Id`, `UserId`, `UserName`, `Timestamp` (UTC), `Action`, `EntityType`, `EntityId`, `EntityName`, `Summary`, colección de `AuditFieldChange`.
- Value Object `AuditFieldChange`:
  - `FieldName`, `OldValue`, `NewValue`.

### 3.2 Aplicación (`Ludeka.Application`)
- Repositorios: `IUserRepository`, `IAuditLogRepository`.
- Contratos de servicio: `IUserManagementService`, `IAuditService`.
- Ampliación de `ICurrentUserService`: `HasPermission(ModeratorPermission permission)` y `SwitchUser(string userId)`.
- DTOs y comandos: `AppUserDto`, `UserFilterDto`, `UpdateUserRoleAndPermissionsCommand`, `UpdateUserStatusCommand`, `CreateUserCommand`, `AuditLogDto`, `AuditLogFilterDto`, `AuditLogPageDto`, `RecordAuditCommand`, `FieldChangeDto`.
- Refuerzo en servicios existentes:
  - `GameEditorService`: Exige `CanEditGames` y `CanUploadImages` (si cambia carátula) y registra en auditoría.
  - `PublisherService`: Exige `CanManagePublishers` y audita altas/ediciones.
  - `CreatorService`: Exige `CanManageCreators` y audita altas/ediciones.
  - `StoreService`: Exige `CanManageStoreLinks` y audita altas/ediciones.
  - `GameIssueReportService`: Exige `CanResolveReports` y audita resolución/descarte.
  - `MediaService`: Exige `CanApproveMedia` y audita moderación de vídeos.

### 3.3 Infraestructura (`Ludeka.Infrastructure`)
- Persistencia en SQLite con EF Core 10 (`DbSet<AppUser>`, `DbSet<AuditLogEntry>`), índices de búsqueda y serialización JSON nativa para `Changes`.
- Repositorios SQLite: `SqliteUserRepository`, `SqliteAuditLogRepository`.
- Seeder de usuarios iniciales (`UserManagementSeeder`) con perfiles de la Mesa Fundadora, moderadores especializados y usuarios comunitarios, además de registros de auditoría iniciales.
- Actualización de `DefaultCurrentUserService` conectada al repositorio de usuarios y conmutador en memoria.

### 3.4 Presentación Web (`Ludeka.Web`)
- `/admin/usuarios` (`UserManagement.razor`):
  - Vista exclusiva para `FoundingTeam`.
  - Buscador reactivo por usuario/email, filtros por rol y estado.
  - Tarjetas editoriales con badges de permisos y estado.
  - Modal accesible de edición de rol y conmutadores de permisos granulares (`UserPermissionsModal.razor`).
  - Acción para suspender/reactivar usuario y conmutador para simular sesión en caliente.
- `/admin/auditoria` (`AuditLogViewer.razor`):
  - Vista exclusiva para `FoundingTeam`.
  - Filtros multicriterio por usuario, entidad, acción y rango de fechas.
  - Paginación fluida y visor expandible de cambios campo por campo (*diff* con badges de color).
- Control de visibilidad de botones en toda la web según permisos específicos de moderador.
- Enlaces de administración en `MainLayout.razor` bajo `IsFoundingTeam`.

---

## 4. Criterios de Aceptación Clave
1. Un usuario de la Mesa Fundadora puede listar los usuarios del sistema, filtrar por rol/estado, cambiar el rol de un usuario comunitario a moderador y configurar con precisión sus permisos granulares.
2. Un moderador solo puede ejecutar aquellas acciones para las que dispone de permiso explícito; de lo contrario, la interfaz oculta los controles y los servicios lanzan `UnauthorizedAccessException`.
3. Los usuarios con rol `FoundingTeam` conservan inherentemente todos los permisos del sistema sin requerir configuración individual.
4. Si un usuario es marcado como `Suspended`, el sistema deniega cualquier operación de moderación independientemente de sus permisos anteriores.
5. Cada operación de alta, edición o resolución genera automáticamente un registro en `AuditLogEntry` que puede consultarse en `/admin/auditoria` con su correspondiente desglose de campos (*diff*).
6. Todas las pruebas unitarias y de integración pasan satisfactoriamente al 100% sin regresiones.
