# Delta for health-checks

> Cambio `change-47-workers-cloud-run` (INC-47), fase `sdd-spec`, 2026-09-18. Corrige el componente `notification_queue` del endpoint `/ready` para que reporte salud observable del outbox en lugar de una comprobación vacía. El contexto transversal completo del cambio (resumen ejecutivo, convenciones del documento, tabla de trazabilidad de 19 filas, fuera de alcance, huecos de evidencia y recuento final) vive en [`../background-jobs-scheduling/spec.md`](../background-jobs-scheduling/spec.md), fichero dominante de este cambio para ese contenido compartido.

> **Nota de convención:** la especificación viva actual (`openspec/specs/health-checks/spec.md`) predata la convención canónica `### Requirement:` / `#### Scenario:` y usa un formato propio de "Escenario N" en bloques Gherkin sin agrupación por requisito nombrado. Este delta reformatea, bajo un único requisito nombrado, exactamente la porción de comportamiento que cambia (el componente `notification_queue` del endpoint `/ready`), preservando sin alterar los dos escenarios existentes que no cambian (dependencias saludables y degradación). El endpoint de liveness `/healthz` y los componentes `database`/`storage` de `/ready` no cambian con este incremento y no se repiten aquí. La normalización del resto del fichero a la convención canónica es tarea de `sdd-archive`, siguiendo el mismo patrón ya usado en el delta archivado de `change-46-autenticacion-real`.

## MODIFIED Requirements

### Requirement: Diagnóstico de disponibilidad del endpoint `/ready`

El endpoint de disponibilidad (`/ready`) DEBE informar si los subsistemas clave del servicio —incluidos la base de datos, el almacenamiento en disco y el mecanismo de notificaciones— están en condiciones de recibir tráfico. Para el componente de notificaciones, DEBE exponer una señal de salud observable derivada del estado real del outbox, en lugar de una comprobación vacía.
(Previously: el componente `notification_queue` reportaba siempre `Healthy` en cuanto el servicio `ICommunityNotificationQueue` podía resolverse por inyección de dependencias, informando únicamente `{ "queue_type": _queue.GetType().Name, "operational": true }` — verificado en `src/Ludeka.Web/Health/NotificationQueueHealthCheck.cs:24-30`, registrado en `Program.cs:325` —, sin observar ningún dato real del estado de la cola.)

*Verificación: prueba unitaria (SQLite) — el chequeo de salud lee del repositorio persistido del outbox (profundidad y antigüedad), sin necesitar primitivas específicas de PostgreSQL.*

#### Scenario: Endpoint de disponibilidad con dependencias saludables (sin cambios)

- GIVEN que la base de datos responde correctamente a `CanConnectAsync()`, el directorio de datos tiene permisos de escritura y el mecanismo de notificaciones está operativo
- WHEN se consulta `GET /ready`
- THEN el endpoint responde con código HTTP `200 OK`
- AND el desglose JSON incluye el estado de cada componente (`database`, `storage`, `notification_queue`).

#### Scenario: Endpoint de disponibilidad ante degradación o fallo (sin cambios)

- GIVEN que la base de datos no puede conectarse o el directorio de almacenamiento es inaccesible
- WHEN se consulta `GET /ready`
- THEN el endpoint responde con código HTTP `503 Service Unavailable`
- AND detalla en el JSON el componente con fallo para diagnóstico operativo inmediato.

#### Scenario: El componente `notification_queue` reporta salud observable del outbox (modificado)

- GIVEN un outbox de notificaciones con una profundidad de cola pendiente y una antigüedad del elemento más antiguo pendiente determinadas
- WHEN se consulta `GET /ready`
- THEN el componente `notification_queue` del desglose JSON reporta la profundidad de la cola pendiente y la antigüedad del elemento más antiguo pendiente, en lugar de únicamente el nombre del tipo de la implementación
- AND si esos valores superan un umbral configurado de degradación, el componente `notification_queue` se reporta como no saludable aunque el resto de componentes estén sanos.
