# Especificación: editorial-role-management

## Propósito
Definir la gestión de roles de usuario en la capa de aplicación, habilitando la distinción entre usuarios estándar y miembros del equipo fundador (`FoundingTeam`) o moderadores (`Moderator`), e incorporando un conmutador de rol demostrativo en la interfaz para facilitar la auditoría y verificación.

## Requerimientos

### Requirement: Abstracción de Roles en `ICurrentUserService`
El contrato `ICurrentUserService` DEBE exponer información de roles del usuario autenticado:
- Lista de roles asignados (`Roles`).
- Helper booleano `IsFoundingTeam`.
- Método de consulta `IsInRole(string role)`.

#### Scenario: Usuario con rol FoundingTeam
- DADO un usuario configurado con el rol `"FoundingTeam"`
- CUANDO se consulta `IsInRole("FoundingTeam")` o `IsFoundingTeam`
- ENTONCES el resultado DEBE ser `true`.

#### Scenario: Usuario estándar
- DADO un usuario configurado como usuario regular (`"User"`)
- CUANDO se consulta `IsInRole("FoundingTeam")` o `IsInRole("Moderator")`
- ENTONCES el resultado DEBE ser `false`.

---

### Requirement: Conmutador de Roles en Tiempo de Ejecución
Para posibilitar la verificación manual y demostración fluida en el MVP sin requerir un servidor OAuth externo, el servicio DEBE permitir alternar el rol activo del usuario en caliente (`SwitchRole`).

#### Scenario: Cambio dinámico de rol en la barra de navegación
- DADO un usuario navegando por la aplicación en modo `"User"`
- CUANDO pulsa el conmutador de la cabecera seleccionando `"🛡️ Modo Mesa Fundadora"`
- ENTONCES el rol activo del servicio DEBE pasar a `"FoundingTeam"`
- Y los componentes reactivos DEBEN actualizar su visualización inmediatamente.

---

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

