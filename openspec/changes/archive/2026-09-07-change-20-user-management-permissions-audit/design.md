# Diseño Técnico: change-20-user-management-permissions-audit

## 1. Arquitectura de Componentes y Flujo de Datos

```
[ Ludeka.Web (Blazor Interactive Server / SSR) ]
   ├── /admin/usuarios (UserManagement.razor + UserPermissionsModal.razor)
   ├── /admin/auditoria (AuditLogViewer.razor con visor diff expandible)
   ├── Acceso en MainLayout.razor (enlaces solo para IsFoundingTeam)
   └── Renderizado condicional de botones según CurrentUserService.HasPermission(...)
          │
          ▼
[ Ludeka.Application (Casos de Uso & Seguridad Defensiva) ]
   ├── IUserManagementService / UserManagementService
   ├── IAuditService / AuditService
   ├── ICurrentUserService (Ampliación HasPermission + SwitchUser)
   ├── Validación defensiva en servicios existentes (GameEditor, Publisher, Creator, Store, Report, Media)
   └── DTOs & Mappings
          │
          ▼
[ Ludeka.Infrastructure (Persistencia & SQLite) ]
   ├── SqliteUserRepository / IUserRepository
   ├── SqliteAuditLogRepository / IAuditLogRepository
   ├── DefaultCurrentUserService (Gestión en memoria y resolución de permisos)
   ├── UserManagementSeeder (Sembrado de usuarios iniciales y auditorías demo)
   └── LudekaDbContext (DbSet<AppUser>, DbSet<AuditLogEntry> + JSON OwnsMany para Changes)
          │
          ▼
[ Ludeka.Core (Dominio Puro) ]
   ├── Entidades: AppUser, AuditLogEntry
   ├── Value Object: AuditFieldChange
   └── Enums: UserRole, UserStatus, ModeratorPermission ([Flags]), AuditAction, AuditEntityType
```

---

## 2. Dominio (`Ludeka.Core`)

### 2.1 Enums y Flags
```csharp
namespace Ludeka.Core.Enums;

public enum UserRole
{
    FoundingTeam,
    Moderator,
    CommunityUser
}

public enum UserStatus
{
    Active,
    Suspended
}

[Flags]
public enum ModeratorPermission
{
    None = 0,
    CanEditGames = 1 << 0,          // 1
    CanUploadImages = 1 << 1,        // 2
    CanManagePublishers = 1 << 2,    // 4
    CanManageCreators = 1 << 3,      // 8
    CanApproveMedia = 1 << 4,        // 16
    CanResolveReports = 1 << 5,      // 32
    CanManageStoreLinks = 1 << 6,    // 64
    All = CanEditGames | CanUploadImages | CanManagePublishers | CanManageCreators | CanApproveMedia | CanResolveReports | CanManageStoreLinks // 127
}

public enum AuditAction
{
    Created,
    Updated,
    Deleted,
    StatusChanged,
    RoleChanged,
    PermissionsChanged
}

public enum AuditEntityType
{
    Game,
    Publisher,
    Creator,
    Store,
    Media,
    Report,
    User
}
```

### 2.2 Value Object `AuditFieldChange`
```csharp
namespace Ludeka.Core.ValueObjects;

public record AuditFieldChange(string FieldName, string? OldValue, string? NewValue);
```

### 2.3 Entidad `AppUser`
```csharp
namespace Ludeka.Core.Entities;

public class AppUser
{
    public string Id { get; private set; } = string.Empty;
    public string UserName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public UserRole Role { get; private set; } = UserRole.CommunityUser;
    public UserStatus Status { get; private set; } = UserStatus.Active;
    public ModeratorPermission Permissions { get; private set; } = ModeratorPermission.None;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private AppUser() { }

    public AppUser(string id, string userName, string email, UserRole role, ModeratorPermission permissions = ModeratorPermission.None, UserStatus status = UserStatus.Active)
    {
        // Validaciones y asignación
    }

    public bool HasPermission(ModeratorPermission permission)
    {
        if (Status == UserStatus.Suspended) return false;
        if (Role == UserRole.FoundingTeam) return true;
        if (Role == UserRole.Moderator) return (Permissions & permission) == permission;
        return false;
    }

    public void UpdateRoleAndPermissions(UserRole newRole, ModeratorPermission permissions)
    {
        Role = newRole;
        Permissions = (newRole == UserRole.Moderator) ? permissions : (newRole == UserRole.FoundingTeam ? ModeratorPermission.All : ModeratorPermission.None);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateStatus(UserStatus newStatus)
    {
        Status = newStatus;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
```

### 2.4 Entidad `AuditLogEntry`
```csharp
namespace Ludeka.Core.Entities;

public class AuditLogEntry
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string UserId { get; private set; } = string.Empty;
    public string UserName { get; private set; } = string.Empty;
    public DateTimeOffset Timestamp { get; private set; } = DateTimeOffset.UtcNow;
    public AuditAction Action { get; private set; }
    public AuditEntityType EntityType { get; private set; }
    public string EntityId { get; private set; } = string.Empty;
    public string EntityName { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public IReadOnlyList<AuditFieldChange> Changes { get; private set; } = [];

    private AuditLogEntry() { }

    public AuditLogEntry(string userId, string userName, AuditAction action, AuditEntityType entityType, string entityId, string entityName, string summary, IEnumerable<AuditFieldChange>? changes = null)
    {
        // Inicialización y validación
    }
}
```

---

## 3. Capa de Aplicación (`Ludeka.Application`)

### 3.1 Contratos de Repositorio
- `IUserRepository`: `GetByIdAsync`, `GetByEmailAsync`, `GetAllAsync(UserFilterDto)`, `AddAsync`, `UpdateAsync`, `DeleteAsync`.
- `IAuditLogRepository`: `AddAsync`, `GetLogsAsync(AuditLogFilterDto)`, `CountLogsAsync(AuditLogFilterDto)`.

### 3.2 Contratos de Servicio y Seguridad
- `IUserManagementService`: Métodos para consultar y modificar usuarios, roles, estados y permisos.
- `IAuditService`: Registro asíncrono de cambios y consulta paginada para la Mesa Fundadora.
- `ICurrentUserService`:
  ```csharp
  bool HasPermission(ModeratorPermission permission);
  void SwitchUser(string userId);
  ```

---

## 4. Capa de Infraestructura (`Ludeka.Infrastructure`)
- En `LudekaDbContext`:
  - `DbSet<AppUser> AppUsers` y `DbSet<AuditLogEntry> AuditLogs`.
  - Configuración de índices y `OwnsMany(a => a.Changes, b => b.ToJson())`.
- Repositorios SQLite en `Ludeka.Infrastructure.Data`.
- `UserManagementSeeder`:
  - Usuarios: `carlos_fundador` (FoundingTeam), `laura_mod` (Moderator con CanEditGames y CanUploadImages), `pablo_editoriales` (Moderator con CanManagePublishers y CanManageCreators), `david_comunidad` (CommunityUser), `susana_suspendida` (Moderator Suspendido).
  - Logs de auditoría iniciales que evidencien ediciones previas de juegos, altas de editoriales y cambios de permisos.

---

## 5. Capa Web (`Ludeka.Web`)
- `/admin/usuarios`:
  - Listado con tarjetas editoriales, badges de rol (👑 Mesa Fundadora, 🛡️ Moderador, 👤 Comunitario), badges de permisos individuales y conmutador rápido "Simular como este usuario".
  - Modal `UserPermissionsModal.razor` para conmutar permisos con feedback visual inmediato.
- `/admin/auditoria`:
  - Línea de tiempo / tabla con filtros reactivos (por moderador, entidad, acción y fecha).
  - Cada fila expandible despliega tarjetas de cambios de valor (`Antes` en rojo suave vs `Después` en verde suave).
- Visibilidad reactiva de botones:
  - `CanEditGames` para botón de editar ficha en `GameDetail.razor`.
  - `CanManagePublishers` para botón de alta/edición en `PublishersDirectory.razor` y `PublisherDetail.razor`.
  - `CanManageCreators` para botón de alta/edición en `CreatorsDirectory.razor` y `CreatorDetail.razor`.
  - `CanManageStoreLinks` para botón de alta/edición en `StoresDirectory.razor` y `StoreDetail.razor`.
  - `CanResolveReports` para acciones de resolución en `GameReportsModeration.razor`.
  - `CanApproveMedia` para acciones de aprobación en `MediaModeration.razor`.
