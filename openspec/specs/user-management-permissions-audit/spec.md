# Especificación Canónica: user-management-permissions-audit

## 1. Contexto y Objetivos
El objetivo de esta especificación es definir los requisitos funcionales, de dominio, arquitectura y de aceptación para el **Panel de Gestión de Usuarios, Permisos Granulares de Moderación (RBAC) y Bitácora Universal de Auditoría para la Mesa Fundadora** en Ludeka.

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
  - `CanPublishInstagram = 1 << 7` (128): Componer y publicar posts en la cuenta oficial de Instagram de Ludeka.
  - `CanManageUsers = 1 << 8` (256): Gestionar cuentas de usuario, roles y máscaras de permisos de moderación.
  - `CanViewAuditLog = 1 << 9` (512): Consultar la bitácora de auditoría editorial.
  - `CanManageEvents = 1 << 10` (1024): Dar de alta, editar y eliminar grandes eventos lúdicos y sus carteles.
  - `CanManageNotifications = 1 << 11` (2048): Gestionar los canales, webhooks y disparadores de las notificaciones comunitarias.
  - `All = CanEditGames | CanUploadImages | CanManagePublishers | CanManageCreators | CanApproveMedia | CanResolveReports | CanManageStoreLinks | CanPublishInstagram | CanManageUsers | CanViewAuditLog | CanManageEvents | CanManageNotifications` (4095).
- **`AuditAction`**:
  `Created`, `Updated`, `Deleted`, `StatusChanged`, `RoleChanged`, `PermissionsChanged`, `Published`, `LinkedFounderIdentity`.
- **`AuditEntityType`**:
  `Game`, `Publisher`, `Creator`, `Store`, `Media`, `Report`, `User`, `InstagramPost`.
- **`AuditFieldChange` (Value Object)**:
  - `FieldName` (string): Nombre del campo modificado.
  - `OldValue` (string?): Valor anterior en texto legible.
  - `NewValue` (string?): Nuevo valor en texto legible.

### 2.2 Entidad `AppUser`
- `Id`: string único (slug o nombre canónico de usuario, ej. `"laura_mod"`, `"carlos_fundador"`).
- `UserName`: string con nombre completo o alias legible.
- `Email`: string con dirección de correo electrónico válida.
- `Country`: string opcional con el país de origen del usuario, normalizado mediante `CountryCatalog`.
- `Role`: `UserRole` (`FoundingTeam`, `Moderator`, `CommunityUser`).
- `Status`: `UserStatus` (`Active`, `Suspended`).
- `Permissions`: `ModeratorPermission` (máscara de bits).
- `CreatedAt`: DateTimeOffset en UTC.
- `UpdatedAt`: DateTimeOffset en UTC.

#### Reglas de Dominio de `AppUser`:
1. **Regla de Suspensión (máxima precedencia):** Si `Status == UserStatus.Suspended`, el método `HasPermission(...)` devuelve `false` incondicionalmente, sin importar el rol ni los permisos asignados — incluido un usuario con rol `FoundingTeam`.
2. **Regla de Mesa Fundadora:** Si `Role == UserRole.FoundingTeam` y la cuenta no está suspendida, el método `HasPermission(...)` devuelve `true` para cualquier permiso del sistema.
3. **Regla de Moderador:** Si `Role == UserRole.Moderator` y la cuenta no está suspendida, `HasPermission(perm)` evalúa si el flag solicitado está presente en `Permissions`: `(Permissions & perm) == perm`.
4. **Regla de Usuario Comunitario:** Si `Role == UserRole.CommunityUser`, `HasPermission(...)` devuelve `false`.

### 2.3 Entidad `AuditLogEntry`
- `Id`: Guid único.
- `UserId`: string con el identificador del usuario que realizó la acción.
- `UserName`: string con el nombre del usuario.
- `Timestamp`: DateTimeOffset UTC de la operación.
- `Action`: `AuditAction` (Created, Updated, Deleted, StatusChanged, RoleChanged, PermissionsChanged, Published, LinkedFounderIdentity).
- `EntityType`: `AuditEntityType` (Game, Publisher, Creator, Store, Media, Report, User, InstagramPost).
- `EntityId`: string identificador del elemento modificado.
- `EntityName`: string legible del elemento (ej. título del juego, nombre de la editorial).
- `Summary`: string con descripción breve de la acción.
- `Changes`: Lista de `AuditFieldChange` serializada en JSON nativo.

---

## 3. Contratos de Aplicación (Application Layer)

### 3.1 `IUserManagementService`
- `Task<IReadOnlyList<AppUserDto>> GetUsersAsync(UserFilterDto? filter = null, CancellationToken ct = default);`
- `Task<AppUserDto?> GetUserByIdAsync(string id, CancellationToken ct = default);`
- `Task<AppUserDto> UpdateUserRoleAndPermissionsAsync(UpdateUserRoleAndPermissionsCommand command, CancellationToken ct = default);`
- `Task<AppUserDto> UpdateUserStatusAsync(UpdateUserStatusCommand command, CancellationToken ct = default);`
- `Task<AppUserDto> CreateUserAsync(CreateUserCommand command, CancellationToken ct = default);`

### 3.2 `IAuditService`
- `Task RecordChangeAsync(RecordAuditCommand command, CancellationToken ct = default);`
- `Task<AuditLogPageDto> GetAuditLogsAsync(AuditLogFilterDto filter, CancellationToken ct = default);`

### 3.3 `ICurrentUserService`
- `string UserId { get; }`
- `string UserName { get; }`
- `IReadOnlyList<string> Roles { get; }`
- `bool IsFoundingTeam { get; }`
- `bool IsInRole(string role);`
- `bool HasPermission(ModeratorPermission permission);`
- NO expone `SwitchUser` ni `SwitchRole`: no existe conmutador de rol ni de usuario, ni identidad simulada (INC-46).

---

## 4. Requisitos y Criterios de Aceptación

### Requirement: Asignación de rol y permisos granulares por la Mesa Fundadora

Un usuario autenticado con rol `FoundingTeam` DEBE poder, desde `/admin/usuarios`, promover a un usuario comunitario al rol `Moderator` y activar una combinación específica de banderas `ModeratorPermission` para él. Al guardar los cambios, el usuario promovido DEBE quedar registrado con el rol y exactamente los permisos asignados, y el sistema DEBE generar una entrada de auditoría de tipo `PermissionsChanged`.

#### Scenario: Mesa Fundadora asigna permisos específicos a un moderador

- DADO un usuario autenticado con rol "FoundingTeam"
- CUANDO navega a "/admin/usuarios"
- Y promueve al usuario comunitario "laura_mod" al rol "Moderator"
- Y activa los permisos "CanEditGames" y "CanUploadImages"
- Y guarda los cambios
- ENTONCES el usuario "laura_mod" queda registrado con rol "Moderator"
- Y posee exactamente los permisos "CanEditGames" y "CanUploadImages"
- Y se genera una entrada de auditoría de tipo "PermissionsChanged"

### Requirement: Evaluación de permisos de moderación según rol y estado de la cuenta

El método `HasPermission` de `AppUser` DEBE evaluarse según las reglas de dominio del apartado 2.2: la suspensión de la cuenta tiene precedencia máxima y anula cualquier rol; a igualdad de estado activo, la Mesa Fundadora obtiene siempre el permiso, un moderador solo lo obtiene si su máscara `Permissions` lo contiene, y un usuario comunitario nunca lo obtiene. Una operación denegada por falta de permiso NO DEBE persistir ningún cambio.

#### Scenario: Moderador sin permiso intenta ejecutar una acción restringida

- DADO un moderador autenticado con permiso "CanEditGames" pero sin "CanManagePublishers"
- CUANDO invoca el método para crear una nueva editorial en "PublisherService"
- ENTONCES el servicio lanza una excepción "UnauthorizedAccessException"
- Y no se persiste ningún cambio en la base de datos

#### Scenario: Miembro de la Mesa Fundadora tiene todos los permisos implícitos

- DADO un usuario con rol "FoundingTeam" y permisos explícitos en "None"
- CUANDO se evalúa "HasPermission" para cualquier permiso ("CanManagePublishers", "CanResolveReports", etc.)
- ENTONCES la evaluación retorna "true"

#### Scenario: Usuario suspendido pierde todo privilegio de moderación

- DADO un moderador que posee todos los permisos asignados
- CUANDO su estado es actualizado a "Suspended"
- ENTONCES "HasPermission" devuelve "false" para todas las operaciones
- Y se le deniega el acceso a las funciones de moderación

### Requirement: Bitácora de auditoría con diff estructurado de campos

Toda edición de una entidad del catálogo por un moderador autorizado DEBE registrarse en la bitácora de auditoría (`IAuditService`) como un `AuditLogEntry`, con el `EntityType` y la `Action` correspondientes y un diff estructurado (`AuditFieldChange`) por cada campo modificado, indicando su valor anterior y su valor nuevo. La bitácora resultante DEBE ser consultable en `/admin/auditoria` para la Mesa Fundadora.

#### Scenario: Registro y consulta de auditoría con diff de campos

- DADO un moderador con permiso "CanEditGames" que actualiza la duración del juego "Catan" de "60-90" a "45-75"
- CUANDO se completa la edición
- ENTONCES se almacena un "AuditLogEntry" con EntityType "Game", Action "Updated"
- Y el registro contiene un cambio en "Duration" con valor anterior "60-90" y valor nuevo "45-75"
- Y la entrada es visible en "/admin/auditoria" para la Mesa Fundadora
