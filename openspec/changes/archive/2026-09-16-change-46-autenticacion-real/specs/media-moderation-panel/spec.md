# Delta for media-moderation-panel

> Delta de la spec viva `openspec/specs/media-moderation-panel/spec.md` (INC-46: autorización efectiva).
> Nota de archivo: la spec canónica usa el encabezado legado `### Requerimiento:`; normalícese a `### Requirement:` antes de ejecutar `sdd-archive-compose`.

## MODIFIED Requirements

### Requirement: Control de Acceso y Vista Móvil Adaptada

El acceso a `/moderacion/multimedia` y sus alias (`/moderacion-media`, `/admin/moderacion-medios`, `/admin/multimedia`) DEBE estar protegido por autorización efectiva: la página DEBE declarar `[Authorize(Policy = "PermisoAprobarMedios")]` y el pipeline DEBE autenticar y autorizar en servidor. Sin sesión, el sistema DEBE redirigir al inicio de sesión; con sesión sin el permiso, DEBE mostrar acceso denegado; una cuenta `Suspended` DEBE quedar denegada. Las comprobaciones de marcado quedan solo como ocultación de acciones.
(Previously: el acceso se comprobaba solo en el marcado, sobre los roles `FoundingTeam` o `Moderator`, sin autorización en servidor.)

#### Scenario: Acceso autorizado

- GIVEN una sesión autenticada con el permiso `CanApproveMedia`
- WHEN navega a `/moderacion/multimedia`
- THEN el sistema DEBE renderizar el panel con pestañas de filtrado:
  - *Pendientes de Aprobación* (conteo numérico)
  - *Bandeja de Huérfanos* (conteo numérico)
  - *Aprobados* (conteo numérico)

#### Scenario: Acceso denegado sin el permiso

- GIVEN una sesión autenticada sin `CanApproveMedia`
- WHEN intenta abrir la vista de moderación
- THEN el sistema DEBE mostrar una advertencia de permisos insuficientes
- AND no se ejecuta ningún handler de moderación.

#### Scenario: Visitante anónimo redirigido

- GIVEN un visitante sin sesión
- WHEN solicita la vista de moderación o cualquiera de sus alias
- THEN es redirigido al inicio de sesión, sin renderizar la cola.

#### Scenario: Cuenta suspendida denegada

- GIVEN una sesión cuyo `AppUser` está `Suspended`
- WHEN solicita la vista de moderación
- THEN la autorización la deniega aunque conserve el permiso.
