# Especificación: policy-based-authorization

> Capacidad nueva (INC-46). Pipeline de autenticación/autorización, políticas por permiso, protección de rutas, revalidación en servicios de escritura e invalidación de circuito.
> Fuente: `docs/increments/inc-46-autenticacion-real.md` §2.3, §2.6 y §2.8.

## Propósito

Convertir el modelo de permisos de INC-20 en autorización efectiva sobre una sesión real: una política por permiso, rutas protegidas en servidor, revalidación en cada servicio de escritura y revocación en caliente.

## Requirements

### Requirement: Pipeline de autenticación y autorización en Blazor SSR interactivo

`Program.cs` DEBE llamar a `UseAuthentication()` y `UseAuthorization()` antes de `UseAntiforgery()`, DEBE registrar `AddCascadingAuthenticationState()` y `Routes.razor` DEBE usar `AuthorizeRouteView` con vistas `NotAuthorized` y `RedirectToLogin`.

#### Scenario: Anónimo en ruta protegida es redirigido a login

- GIVEN un visitante sin sesión
- WHEN solicita `/admin/auditoria`
- THEN es redirigido al inicio de sesión, sin renderizar contenido administrativo.

#### Scenario: Orden del pipeline

- GIVEN el pipeline HTTP compilado
- WHEN se inspecciona su orden
- THEN `UseAuthentication` y `UseAuthorization` preceden a `UseAntiforgery`.

#### Scenario: Estado de autenticación en cascada en SSR interactivo

- GIVEN un componente con `AuthorizeView` dentro del circuito
- WHEN la sesión cambia entre anónima y autenticada
- THEN la vista refleja el estado en cascada.

### Requirement: Una política por permiso evaluada sobre el AppUser de sesión

DEBE existir una política por permiso —`PermisoEditarFichas`, `PermisoAprobarMedios`, `PermisoResolverReportes`, `PermisoGestionarEditores`, `PermisoGestionarCreadores`, `PermisoGestionarTiendas`, `PermisoPublicarInstagram`, `PermisoVerAuditoria`, `PermisoGestionarUsuarios`— que evalúe `ModeratorPermission` sobre el `AppUser` de la sesión mediante `HasPermission`, respetando la invariante de suspensión.

#### Scenario: Denegación de anónimos y cuentas sin privilegio

- GIVEN un visitante anónimo o un `CommunityUser`
- WHEN evalúa cualquier política de permiso
- THEN el resultado es denegado.

#### Scenario: Moderador sin el permiso concreto

- GIVEN un `Moderator` con `CanApproveMedia` pero sin `CanResolveReports`
- WHEN evalúa `PermisoResolverReportes`
- THEN el resultado es denegado
- AND `PermisoAprobarMedios` es permitido.

#### Scenario: Fundador y moderador con el permiso exacto

- GIVEN un `FoundingTeam`, o un `Moderator` con el permiso de la política
- WHEN evalúa la política correspondiente
- THEN el resultado es permitido.

#### Scenario: Cuenta suspendida denegada

- GIVEN un `AppUser` con `Status=Suspended`
- WHEN evalúa cualquier política de permiso
- THEN el resultado es denegado aunque conserve permisos.

### Requirement: Protección de las rutas administrativas y de moderación

Las 11 páginas protegidas —`/admin/usuarios`, `/admin/ingesta-social`, `/admin/notificaciones`, `/admin/canales-monitorizados`, `/admin/cola-catalogacion`, `/admin/auditoria`, `/admin/instagram`, `/admin/eventos`, `/admin/multimedia` (con sus alias `/moderacion-media`, `/moderacion/multimedia` y `/admin/moderacion-medios`), y `/moderacion/reportes` (alias `/admin/reportes`)— DEBEN declarar `[Authorize(Policy = ...)]` con una política de permiso. Las comprobaciones de marcado quedan solo como mejora de experiencia (ocultar acciones), nunca como control de acceso.

#### Scenario: Matriz anónima de las rutas protegidas

- GIVEN el conjunto de rutas administrativas y de moderación
- WHEN un visitante anónimo solicita cada una
- THEN ninguna renderiza contenido protegido y todas redirigen al inicio de sesión.

#### Scenario: Autenticado sin el permiso requerido

- GIVEN un usuario autenticado sin el permiso de la política de la página
- WHEN solicita la ruta
- THEN recibe acceso denegado, sin ejecutar sus handlers.

#### Scenario: Alias cubiertos

- GIVEN las rutas alias `/admin/reportes` y `/moderacion/multimedia`
- WHEN un anónimo las solicita
- THEN se comportan igual que sus rutas principales.

### Requirement: Revalidación del permiso en servicios de escritura

Como `InteractiveServer` no reejecuta el pipeline HTTP por evento, cada servicio de escritura DEBE revalidar el permiso sobre la identidad de la sesión antes de mutar. Una llamada sin permiso DEBE arrojar `UnauthorizedAccessException` sin escribir datos ni auditoría.

#### Scenario: Llamada sin permiso no muta

- GIVEN una sesión sin el permiso requerido por el servicio
- WHEN se invoca su operación de escritura
- THEN arroja `UnauthorizedAccessException` y no persiste cambios ni auditoría.

#### Scenario: Llamada autorizada muta con identidad real

- GIVEN una sesión con el permiso requerido
- WHEN se invoca la operación
- THEN la mutación se persiste y la auditoría registra la identidad de la sesión.

### Requirement: Invalidación del circuito ante suspensión o cambio de permisos

Cuando el `AppUser` de una sesión cambie de `Status` a `Suspended` o muten sus permisos, el sistema DEBE invalidar el circuito activo para que el cambio sea efectivo sin esperar al cierre de sesión.

#### Scenario: Suspensión con sesión abierta

- GIVEN un usuario con sesión activa y circuito abierto
- WHEN su cuenta pasa a `Suspended`
- THEN su siguiente operación privilegiada es denegada sin esperar la caducidad de la cookie.

#### Scenario: Permisos revocados en caliente

- GIVEN una sesión con un permiso concedido
- WHEN el permiso se retira en `AppUser`
- THEN la política y los servicios dejan de autorizarla en esa misma sesión.
