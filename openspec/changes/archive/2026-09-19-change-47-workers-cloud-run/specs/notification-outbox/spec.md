# Especificación: `notification-outbox`

> Cambio `change-47-workers-cloud-run` (INC-47), fase `sdd-spec`, 2026-09-18. Outbox persistente real para notificaciones comunitarias, con reclamación exclusiva entre despachadores concurrentes. El contexto transversal completo del cambio (resumen ejecutivo, convenciones del documento, tabla de trazabilidad de 19 filas, fuera de alcance, huecos de evidencia y recuento final) vive en [`../background-jobs-scheduling/spec.md`](../background-jobs-scheduling/spec.md), fichero dominante de este cambio para ese contenido compartido. Decisiones de diseño abiertas (propuesta §8, no presupuestas en este documento): empaquetado del host de trabajos y modelo de fila del outbox.

## Propósito

Outbox persistente real para notificaciones comunitarias: el registro de cada entrega pendiente se crea en el momento de encolar —no tras leerla—, la reclamación es exclusiva entre despachadores concurrentes, cada intento fallido incrementa un contador y se reprograma, y toda entrega alcanza uno de dos estados terminales (`Sent` o `Failed`). Ninguno de estos requisitos presupone si la unidad mínima de entrega pendiente es una fila por canal (esquema actual) o una fila de mensaje con sub-entregas por canal (propuesta §8, decisión 2): se habla deliberadamente de "unidad de entrega pendiente por canal", concepto que existe en ambos modelos.

## Requirements

### Requirement: Persistencia del registro de entrega pendiente en el punto de encolado

El sistema DEBE persistir, para cada canal habilitado, un registro durable de entrega pendiente en el momento en que se invoca la operación de encolado —no tras la lectura del despachador—. Esto DEBE ser demostrable sin ejecutar nunca el despachador.

*Verificación: prueba unitaria (SQLite) — se invoca la operación de encolado y se lee directamente el estado persistido, sin invocar en ningún momento el despachador.*

#### Scenario: El registro persiste inmediatamente al encolar

- GIVEN un servicio de dominio que encola una notificación a través del contrato de encolado
- WHEN se invoca la operación de encolado
- THEN un registro persistente en estado pendiente queda escrito en base de datos inmediatamente
- AND no existe ninguna dependencia temporal de la ejecución del despachador para que ese registro exista.

#### Scenario: El registro sobrevive aunque el despachador nunca llegue a ejecutarse

- GIVEN una notificación recién encolada
- WHEN la instancia que la encoló se apaga inmediatamente después, sin que el despachador haya llegado a ejecutarse ni una sola vez
- THEN el registro pendiente sigue existiendo en base de datos
- AND es reclamable por cualquier despachador posterior.

### Requirement: Supervivencia de notificaciones pendientes ante apagado de la instancia

Una notificación que alcanza el estado `Queued` (pendiente) DEBE sobrevivir al apagado o reinicio de la instancia de proceso que la encoló o que se esperaba que la despachara, y DEBE ser finalmente entregada por cualquier instancia que ejecute después el despachador.

*Verificación: prueba unitaria (SQLite) — se simula el reinicio destruyendo y reconstruyendo el proceso despachador contra el mismo almacén persistido, sin depender de la cola en memoria.*

#### Scenario: Notificación pendiente entregada tras un reinicio simulado del host

- GIVEN una notificación en estado `Queued` en base de datos
- WHEN se simula un reinicio completo del host, destruyendo cualquier estado en memoria, incluida la cola en memoria si estuviera activa
- THEN al arrancar una nueva ejecución del despachador, la notificación pendiente es reclamada
- AND se envía correctamente.

### Requirement: Reclamación exclusiva de unidades de entrega pendientes entre despachadores concurrentes

Cuando dos o más procesos despachadores intentan reclamar unidades de entrega pendientes simultáneamente, cada unidad de entrega DEBE ser reclamada por exactamente un despachador. Ningún par de despachadores concurrentes PUEDE reclamar y procesar la misma unidad de entrega.

*Verificación: prueba de integración contra PostgreSQL real (depende de R1) — es el requisito que exige `SELECT ... FOR UPDATE SKIP LOCKED`, primitiva que SQLite no implementa y que Entity Framework Core no traduce desde LINQ; no existe alternativa unitaria válida para esta garantía.*

#### Scenario: Dos despachadores reclaman lotes al mismo tiempo sin solaparse

- GIVEN un conjunto de unidades de entrega pendientes en el outbox
- WHEN dos procesos despachadores intentan reclamar lotes de unidades pendientes al mismo tiempo
- THEN la intersección de las unidades reclamadas por cada uno es vacía
- AND ninguna unidad de entrega es reclamada ni procesada por ambos despachadores.

### Requirement: Reintento con contador de intentos y estado terminal al agotarse

Un intento de entrega fallido DEBE incrementar un contador de intentos persistido y quedar disponible para un reintento posterior. Cuando el número de intentos fallidos alcanza el máximo configurado, la unidad de entrega DEBE marcarse en estado terminal `Failed` y NO DEBE volver a reclamarse.

*Verificación: prueba unitaria (SQLite) — es lógica de contador y transición de estado, no depende de primitivas específicas de PostgreSQL.*

#### Scenario: Un intento fallido incrementa el contador y programa un reintento

- GIVEN una unidad de entrega pendiente cuyo intento de envío falla
- WHEN el despachador registra el fallo
- THEN el contador de intentos persistido se incrementa
- AND la unidad queda programada para un reintento posterior en un estado no terminal.

#### Scenario: Se agota el número máximo de intentos

- GIVEN una unidad de entrega que ya alcanzó el número máximo configurado de intentos fallidos
- WHEN el despachador evalúa esa unidad tras el último fallo
- THEN la unidad se marca en estado terminal `Failed`
- AND no vuelve a ser reclamada por ejecuciones futuras del despachador.

#### Scenario: Entrega correcta en el primer intento

- GIVEN una unidad de entrega pendiente que se entrega correctamente en el primer intento
- WHEN el despachador confirma el envío
- THEN la unidad queda marcada en estado terminal `Sent`.

### Requirement: Migración del esquema que respalda el outbox sin pérdida de datos

Cualquier cambio de esquema que introduzca el contador de intentos y la marca de siguiente reintento para las unidades de entrega pendientes DEBE preservar todas las filas ya persistidas, tanto en el historial de producción de PostgreSQL como en cualquier base de datos SQLite ya existente en disco. Este requisito no presupone si esas columnas se añaden sobre `CommunityNotificationLog` o sobre una tabla nueva de sub-entregas (decisión 2, propuesta §8).

*Verificación: Escenario 1, prueba de integración contra PostgreSQL real (depende de R1). Escenario 2, prueba unitaria (SQLite) sobre una base sembrada con el esquema anterior.*

#### Scenario: Migración Npgsql sobre notificaciones de producción existentes

- GIVEN una base de datos PostgreSQL con registros de notificación ya existentes, anteriores a este incremento
- WHEN se aplica la migración de Entity Framework Core que añade el contador de intentos y la marca de siguiente reintento
- THEN todos los registros existentes conservan sus valores originales
- AND quedan disponibles con las columnas nuevas en su valor por defecto.

#### Scenario: Reconciliación de una base SQLite ya existente en disco

- GIVEN una base de datos SQLite ya existente en disco, con registros de notificación creados antes de este incremento
- WHEN el reconciliador de esquema SQLite se ejecuta al arrancar la aplicación
- THEN la base se actualiza con las columnas nuevas
- AND ningún registro ni dato ya presente se pierde, trunca ni elimina.

### Requirement: Reinicio del contenedor en el entorno real sin pérdida de notificaciones pendientes (verificación manual)

En el entorno real de despliegue, un reinicio o reemplazo del contenedor que alojaba notificaciones pendientes NO DEBE perder ninguna notificación que ya estuviera en estado `Queued` en el outbox persistente.

*Verificación: manual del maintainer — no hay acceso autorizado a Google Cloud en este ciclo. El equivalente simulado ya queda cubierto, a nivel unitario, por el requisito "Supervivencia de notificaciones pendientes ante apagado de la instancia"; lo que queda fuera del alcance automatizado es la prueba contra el entorno real de Cloud Run.*

#### Scenario: Reinicio real del contenedor sin pérdida de notificaciones

- GIVEN notificaciones pendientes en estado `Queued` en la base de datos de producción
- WHEN el contenedor que las generó se reinicia o es reemplazado por la propia plataforma de Cloud Run
- THEN esas notificaciones siguen presentes en base de datos
- AND son entregadas por la siguiente ejecución del despachador.
