# Especificación: media-moderation-panel

## Propósito
Definir el panel táctil móvil de moderación rápida en `/moderacion/multimedia`, permitiendo a moderadores y miembros del equipo fundador aprobar o descartar contenidos en 1 clic, asignar elementos huérfanos a juegos del catálogo y verificar enlaces rotos.

## Requerimientos

### Requirement: Control de Acceso y Vista Móvil Adaptada
El acceso a la ruta `/moderacion/multimedia` DEBE estar restringido a usuarios con rol `FoundingTeam` o `Moderator`. Si un usuario sin privilegios intenta acceder, el sistema DEBE mostrar un mensaje de acceso denegado.

#### Scenario: Acceso autorizado
- DADO un usuario autenticado con rol `FoundingTeam` o `Moderator`
- CUANDO navega a `/moderacion/multimedia`
- ENTONCES el sistema DEBE renderizar el panel con pestañas de filtrado:
  - *Pendientes de Aprobación* (conteo numérico)
  - *Bandeja de Huérfanos* (conteo numérico)
  - *Aprobados* (conteo numérico)

#### Scenario: Acceso denegado
- DADO un usuario con rol regular `User`
- CUANDO intenta abrir la vista de moderación
- ENTONCES el sistema DEBE mostrar una advertencia de permisos insuficientes.

---

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
