# Especificación: media-moderation-panel

## Propósito
Definir el panel táctil móvil de moderación rápida en `/moderacion/multimedia`, permitiendo a moderadores y miembros del equipo fundador aprobar o descartar contenidos en 1 clic, asignar elementos huérfanos a juegos del catálogo y verificar enlaces rotos.

## Requerimientos

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
### Requirement: Acciones de Moderación Rápida en 1 Clic
Cada tarjeta de contenido en la cola de moderación DEBE incluir botones táctiles de acción inmediata.

#### Scenario: Aprobación en 1 clic
- DADO un elemento en la pestaña *Pendientes de Aprobación*
- CUANDO el moderador pulsa `[ ✅ Aprobar ]`
- ENTONCES el elemento DEBE cambiar a estado `Approved` inmediatamente y pasar a mostrarse en la ficha del juego asignado.

#### Scenario: Descarte en 1 clic
- DADO un elemento en la cola de moderación
- CUANDO el moderador pulsa `[ ❌ Descartar ]`
- ENTONCES el elemento DEBE cambiar a estado `Rejected` y dejar de aparecer en las listas públicas.

---

### Requirement: Bandeja de Contenidos Huérfanos y Asignación de Juego
Los contenidos capturados sin juego asignado (`GameId == null`) DEBEN agruparse en la *Bandeja de Huérfanos* con un control para asociarlos a un título existente.

#### Scenario: Asignar juego a contenido huérfano
- DADO un elemento en la *Bandeja de Huérfanos*
- CUANDO el moderador selecciona `[ 🔗 Asignar Juego ]` y escoge un título del catálogo (ej. *Catan*)
- ENTONCES el sistema DEBE vincular el elemento a dicho juego (`GameId = catan.Id`), permitiendo a continuación su aprobación directa.

---

### Requirement: Detector de Enlaces Rotos
El panel DEBE proveer una herramienta de comprobación de enlaces que identifique vídeos o posts eliminados o privados (HTTP 404).

#### Scenario: Ejecución de verificación de enlaces
- DADO el panel de moderación con elementos multimedia activos
- CUANDO el moderador pulsa `[ 🔍 Comprobar Enlaces ]`
- ENTONCES el servicio DEBE verificar la accesibilidad de los recursos, reportando los elementos inaccesibles o 404 y marcándolos con la insignia `⚠️ Enlace roto`, permitiendo despublicarlos en 1 clic.
