# Especificación de Requerimientos: change-20-user-management-permissions-audit

## 1. Contexto y Objetivos
El objetivo de esta especificación es definir los requisitos funcionales, de dominio, arquitectura y de aceptación para el **Panel de Gestión de Usuarios, Permisos Granulares de Moderación y Auditoría para la Mesa Fundadora** en Ludeka.

---

## 2. Definición del Dominio y Entidades

### 2.1 Enumerados y Value Objects
- **`UserRole`**:
  `FoundingTeam`, `Moderator`, `CommunityUser`.
- **`UserStatus`**:
  `Active`, `Suspended`.
- **`ModeratorPermission` (`[Flags]`):**
  - `None = 0`
  - `CanEditGames = 1 << 0` (1): Editar metadatos, parámetros y ficha técnica de juegos.
  - `CanUploadImages = 1 << 1` (2): Cargar o actualizar carátulas y fotografías.
  - `CanManagePublishers = 1 << 2` (4): Crear y modificar editoriales y sus redes.
  - `CanManageCreators = 1 << 3` (8): Crear y modificar autores y diseñadores.
  - `CanApproveMedia = 1 << 4` (16): Validar o descartar contenidos en moderación multimedia.
  - `CanResolveReports = 1 << 5` (32): Gestionar y resolver incidencias comunitarias de catálogo.
  - `CanManageStoreLinks = 1 << 6` (64): Dar de alta y editar tiendas y enlaces afiliados.
  - `All = CanEditGames | CanUploadImages | CanManagePublishers | CanManageCreators | CanApproveMedia | CanResolveReports | CanManageStoreLinks` (127).
- **`AuditAction`**:
  `Created`, `Updated`, `Deleted`, `StatusChanged`, `RoleChanged`, `PermissionsChanged`.
- **`AuditEntityType`**:
  `Game`, `Publisher`, `Creator`, `Store`, `Media`, `Report`, `User`.
- **`AuditFieldChange` (Value Object)**:
  - `FieldName` (string): Nombre del campo modificado.
  - `OldValue` (string?): Valor anterior en texto legible.
  - `NewValue` (string?): Nuevo valor en texto legible.

### 2.2 Entidad `AppUser`
- `Id`: string único (slug o nombre canónico de usuario, ej. `"laura_mod"`, `"carlos_fundador"`).
- `UserName`: string con nombre completo o alias legible.
- `Email`: string con dirección de correo electrónico válida.
- `Role`: `UserRole` (`FoundingTeam`, `Moderator`, `CommunityUser`).
- `Status`: `UserStatus` (`Active`, `Suspended`).
- `Permissions`: `ModeratorPermission` (máscara de bits).
- `CreatedAt`: DateTimeOffset en UTC.
- `UpdatedAt`: DateTimeOffset en UTC.

#### Reglas de Dominio de `AppUser`:
1. **Regla de Mesa Fundadora:** Si `Role == UserRole.FoundingTeam`, el método `HasPermission(...)` devuelve `true` incondicionalmente para cualquier permiso del sistema.
2. **Regla de Suspensión:** Si `Status == UserStatus.Suspended`, el método `HasPermission(...)` devuelve `false` incondicionalmente sin importar los permisos asignados.
3. **Regla de Moderador:** Si `Role == UserRole.Moderator`, `HasPermission(perm)` evalúa si el flag solicitado está presente en `Permissions`: `(Permissions & perm) == perm`.
4. **Regla de Usuario Comunitario:** Si `Role == UserRole.CommunityUser`, `HasPermission(...)` devuelve `false`.

### 2.3 Entidad `AuditLogEntry`
- `Id`: Guid único.
- `UserId`: string con el identificador del usuario que realizó la acción.
- `UserName`: string con el nombre del usuario.
- `Timestamp`: DateTimeOffset UTC de la operación.
- `Action`: `AuditAction` (Created, Updated, Deleted, StatusChanged, RoleChanged, PermissionsChanged).
- `EntityType`: `AuditEntityType` (Game, Publisher, Creator, Store, Media, Report, User).
- `EntityId`: string identificador del elemento modificado.
- `EntityName`: string legible del elemento (ej. título del juego, nombre de la editorial).
- `Summary`: string con descripción breve de la acción.
- `Changes`: Lista de `AuditFieldChange` serializada en JSON nativo.

---

## 3. Contratos de Aplicación (Application Layer)

### 3.1 `IUserManagementService`
- `Task<IReadOnlyList<AppUserDto>> GetUsersAsync(UserFilterDto filter, CancellationToken ct = default);`
- `Task<AppUserDto?> GetUserByIdAsync(string id, CancellationToken ct = default);`
- `Task<AppUserDto> UpdateUserRoleAndPermissionsAsync(UpdateUserRoleAndPermissionsCommand command, CancellationToken ct = default);`
- `Task<AppUserDto> UpdateUserStatusAsync(UpdateUserStatusCommand command, CancellationToken ct = default);`
- `Task<AppUserDto> CreateUserAsync(CreateUserCommand command, CancellationToken ct = default);`

### 3.2 `IAuditService`
- `Task RecordChangeAsync(RecordAuditCommand command, CancellationToken ct = default);`
- `Task<AuditLogPageDto> GetAuditLogsAsync(AuditLogFilterDto filter, CancellationToken ct = default);`

### 3.3 `ICurrentUserService`
- Ampliación:
  - `bool HasPermission(ModeratorPermission permission);`
  - `void SwitchUser(string userId);`

---

## 4. Criterios de Aceptación (Gherkin Scenarios)

```gherkin
Escenario: Mesa Fundadora asigna permisos específicos a un moderador
  Dado un usuario autenticado con rol "FoundingTeam"
  Cuando navega a "/admin/usuarios"
  Y promueve al usuario comunitario "laura_mod" al rol "Moderator"
  Y activa los permisos "CanEditGames" y "CanUploadImages"
  Y guarda los cambios
  Entonces el usuario "laura_mod" queda registrado con rol "Moderator"
  Y posee exactamente los permisos "CanEditGames" y "CanUploadImages"
  Y se genera una entrada de auditoría de tipo "PermissionsChanged"

Escenario: Moderador sin permiso intenta ejecutar una acción restringida
  Dado un moderador autenticado con permiso "CanEditGames" pero sin "CanManagePublishers"
  Cuando invoca el método para crear una nueva editorial en "PublisherService"
  Entonces el servicio lanza una excepción "UnauthorizedAccessException"
  Y no se persiste ningún cambio en la base de datos

Escenario: Miembro de la Mesa Fundadora tiene todos los permisos implícitos
  Dado un usuario con rol "FoundingTeam" y permisos explícitos en "None"
  Cuando se evalúa "HasPermission" para cualquier permiso ("CanManagePublishers", "CanResolveReports", etc.)
  Entonces la evaluación retorna "true"

Escenario: Usuario suspendido pierde todo privilegio de moderación
  Dado un moderador que posee todos los permisos asignados
  Cuando su estado es actualizado a "Suspended"
  Entonces "HasPermission" devuelve "false" para todas las operaciones
  Y se le deniega el acceso a las funciones de moderación

Escenario: Registro y consulta de auditoría con diff de campos
  Dado un moderador con permiso "CanEditGames" que actualiza la duración del juego "Catan" de "60-90" a "45-75"
  Cuando se completa la edición
  Entonces se almacena un "AuditLogEntry" con EntityType "Game", Action "Updated"
  Y el registro contiene un cambio en "Duration" con valor anterior "60-90" y valor nuevo "45-75"
  Y la entrada es visible en "/admin/auditoria" para la Mesa Fundadora
```
