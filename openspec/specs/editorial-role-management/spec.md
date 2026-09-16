# Especificación: editorial-role-management

## Propósito
Definir la gestión de roles de usuario en la capa de aplicación, habilitando la distinción entre usuarios estándar y miembros del equipo fundador (`FoundingTeam`) o moderadores (`Moderator`), e incorporando un conmutador de rol demostrativo en la interfaz para facilitar la auditoría y verificación.

## Requerimientos

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

### Requirement: Acceso Rápido de Moderación en Navegación
La cabecera de la aplicación (`MainLayout.razor`) DEBE mostrar un botón o enlace `[ 🎬 Moderar Medios ]` cuando el usuario activo posea el rol `FoundingTeam` o `Moderator`.

#### Scenario: Moderador visualiza acceso a moderación
- DADO un usuario activo con rol `Moderator` o `FoundingTeam`
- CUANDO visualiza cualquier pantalla en Ludeka
- ENTONCES en la barra superior DEBE estar visible el acceso directo `[ 🎬 Moderar Medios ]` que enlaza a `/moderacion/multimedia`.

#### Scenario: Usuario regular no visualiza acceso
- DADO un usuario con rol regular `User`
- CUANDO navega por la plataforma
- ENTONCES el enlace de moderación NO DEBE ser visible ni accesible en la barra superior.

