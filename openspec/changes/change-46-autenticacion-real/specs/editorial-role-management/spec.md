# Delta for editorial-role-management

> Delta de la spec viva `openspec/specs/editorial-role-management/spec.md` (INC-46: retirada de la identidad simulada).
> Nota de archivo: la spec canónica usa el encabezado legado `### Requerimiento:`; normalícese a `### Requirement:` antes de ejecutar `sdd-archive-compose`.

## MODIFIED Requirements

### Requirement: Abstracción de Roles en `ICurrentUserService`

El contrato `ICurrentUserService` DEBE resolver la identidad del usuario autenticado desde la sesión y exponer la información de roles (`UserId`, `UserName`, `Roles`, `IsFoundingTeam`, `IsInRole(string role)`) junto con `HasPermission(ModeratorPermission)`. NO DEBE declarar `SwitchRole` ni `SwitchUser`. Sin sesión, `IsFoundingTeam`, `IsInRole` y `HasPermission` DEBEN devolver `false` y ninguna escritura con identidad puede ejecutarse.
(Previously: la identidad era un singleton mutable con conmutadores de rol y usuario, y cualquier visitante era `FoundingTeam` con todos los permisos.)

#### Scenario: Usuario con rol FoundingTeam desde la sesión

- GIVEN una sesión autenticada cuyo `AppUser` tiene rol `FoundingTeam`
- WHEN se consulta `IsInRole("FoundingTeam")` o `IsFoundingTeam`
- THEN el resultado DEBE ser `true`.

#### Scenario: Usuario estándar desde la sesión

- GIVEN una sesión autenticada con rol `CommunityUser`
- WHEN se consulta `IsInRole("FoundingTeam")` o `IsInRole("Moderator")`
- THEN el resultado DEBE ser `false`.

#### Scenario: Contrato sin conmutadores

- GIVEN el contrato `ICurrentUserService`
- WHEN se compila la solución
- THEN no existen referencias a `SwitchRole` ni `SwitchUser` (lo garantiza la compilación).

#### Scenario: Visitante anónimo sin identidad

- GIVEN una petición sin sesión
- WHEN se evalúan `IsFoundingTeam`, `IsInRole` o `HasPermission`
- THEN el resultado DEBE ser `false` en todos los casos.

## REMOVED Requirements

### Requirement: Conmutador de Roles en Tiempo de Ejecución

(Reason: era la vía pública de escalada de privilegios; la identidad pasa a exigir sesión autenticada y el rol se lee de `AppUser`.)
(Migration: eliminar `SwitchRole`/`SwitchUser` del contrato y sus cinco puntos de UI —`MainLayout.razor`, `UserManagement.razor`, `AuditLogViewer.razor`, `MediaModeration.razor` y `GameReportsModeration.razor`—; los 12 dobles de test implementan el contrato sin conmutadores.)
