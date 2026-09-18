# Especificación: `background-jobs-scheduling`

> **Cambio:** `change-47-workers-cloud-run` · **Fase:** `sdd-spec` · **Fecha:** 2026-09-18
> **Worktree:** `C:\repos\ludeka-wt\workers-cloud-run` (rama `inc/workers-cloud-run`, commit base `56c02b9`)
> **Entradas:** [`proposal.md`](../../proposal.md) (aprobada por el maintainer el 2026-09-18) · [`exploration.md`](../../exploration.md) · [`docs/increments/inc-47-workers-cloud-run.md`](../../../../../docs/increments/inc-47-workers-cloud-run.md) (§4 y §2.6, con las correcciones de línea de la exploración)
> **Almacén de artefactos:** `hybrid` — los seis ficheros bajo `specs/` de este cambio (uno por capacidad) más la observación Engram `sdd/change-47-workers-cloud-run/spec`
> **Decisiones de diseño abiertas (no presupuestas en este documento):** empaquetado del host de trabajos y modelo de fila del outbox (propuesta §8, decisiones 1 y 2). Cada requisito de este documento permanece válido bajo cualquiera de las dos resoluciones; corresponde a `sdd-design` fijarlas.
> **Nota de reestructuración (2026-09-18):** esta especificación se escribió inicialmente como un único fichero plano (`openspec/changes/change-47-workers-cloud-run/spec.md`), en contra de la convención canónica de `openspec-convention.md` (`sdd-spec` crea `specs/<capacidad>/spec.md` por capacidad). Se redistribuyó, sin alterar su contenido, en los seis ficheros de capacidad de este directorio. Este fichero es el **dominante**: aloja el contexto transversal del cambio (resumen ejecutivo, convenciones, tabla de trazabilidad completa, fuera de alcance, huecos de evidencia y recuento final); los otros cinco ficheros (`notification-outbox`, `postgres-integration-testing`, `health-checks`, `dockerfile-build`, `nightly-batch-continuous-ingest`) lo referencian en lugar de duplicarlo.

---

## 0. Resumen ejecutivo

Esta especificación fija el comportamiento observable que debe cumplirse tras aplicar INC-47: los cuatro trabajos de fondo de Ludeka dejan de depender de estado en memoria del proceso web y pasan a una ejecución de vida corta, idempotente por ventana temporal y respaldada por restricciones de base de datos; las notificaciones encoladas sobreviven al apagado o escalado a cero de la instancia que las generó, gracias a un outbox real con reclamación exclusiva entre despachadores concurrentes; y la suite de pruebas gana la capacidad de ejercitar primitivas específicas de PostgreSQL —el habilitador del que dependen las dos capacidades anteriores—.

Ninguno de los veinte requisitos de este documento presupone cómo se empaqueta el host de trabajos (proyecto `Ludeka.Jobs` independiente frente a modo trabajo del host existente) ni cómo se modela la fila del outbox (fila por canal frente a fila por mensaje con sub-entregas): ambas son decisiones abiertas que corresponden a `sdd-design` (propuesta §8). Cada requisito declara honestamente su nivel de verificación real: 22 escenarios se prueban hoy con SQLite, 7 exigen PostgreSQL real y dependen de la rebanada R1 (habilitador de pruebas), y 7 quedan como verificación manual del maintainer por tratarse de infraestructura de Google Cloud sin acceso autorizado en este ciclo.

---

## 1. Convenciones de este documento

- **DEBE / DEBEN** — obligación absoluta (equivalente a MUST/SHALL de RFC 2119).
- **NO DEBE / NO DEBEN** — prohibición absoluta (MUST NOT/SHALL NOT).
- **DEBERÍA** — recomendado, con excepciones justificables (SHOULD).
- **PUEDE** — opcional (MAY).
- Los marcadores estructurales `### Requirement:`, `#### Scenario:`, `## Requirements`/`## ADDED Requirements`/`## MODIFIED Requirements`, `GIVEN`/`WHEN`/`THEN`/`AND`, `(Previously: …)` y `(Reason: …)` se mantienen **literales en inglés**: son el vocabulario de formato que las fases posteriores (`sdd-design`, `sdd-archive`) parsean mecánicamente, y siguen exactamente la misma convención ya usada en los deltas archivados de `change-46-autenticacion-real` y `change-49-vinculacion-cuentas` de este mismo almacén. Todo lo demás —nombres de requisito, nombres de escenario y el cuerpo de cada uno— está en español.
- Cada requisito declara su **nivel de verificación**: prueba unitaria (SQLite), prueba de integración (PostgreSQL real, dependiente de la rebanada R1 del habilitador de pruebas) o verificación manual del maintainer (infraestructura de Google Cloud, sin acceso autorizado en este ciclo).

---

## 2. Tabla de trazabilidad y verificación

| # | Criterio de origen | Capacidad | Requisito | Nivel de verificación |
|---|---|---|---|---|
| 1 | No duplicación por ventana con hasta 5 instancias y reintentos de Scheduler | `background-jobs-scheduling` | Ejecución única por ventana temporal bajo escalado y reintentos del planificador | Integración PostgreSQL (R1) |
| 2 | Supervivencia de notificaciones al apagado de la instancia que las encoló | `notification-outbox` | Supervivencia de notificaciones pendientes ante apagado de la instancia | Unitaria SQLite |
| 3 | `EnqueueAsync` persiste la fila antes de cualquier intento de envío | `notification-outbox` | Persistencia del registro de entrega pendiente en el punto de encolado | Unitaria SQLite |
| 4 | Cero dependencia de estado en memoria (`_lastExecutionDate`, `_lastFridayBulletinDispatched`) | `background-jobs-scheduling` | Ausencia de estado en memoria para el control de ejecución | Unitaria SQLite |
| 5 | El host web no registra ningún `IHostedService` de negocio | `background-jobs-scheduling` | El host web no ejecuta ningún trabajo de negocio en proceso | Unitaria SQLite |
| 6 | Código de salida distinto de cero ante fallo observable | `background-jobs-scheduling` | Contrato de código de salida por ejecución | Unitaria SQLite |
| 7 | Idempotencia: la segunda ejecución de una ventana choca con `UNIQUE`, no con un `if` en memoria | `background-jobs-scheduling` | Ejecución única por ventana temporal... (Escenario 2) | Integración PostgreSQL (R1) |
| 8 | Reclamación concurrente: dos consumidores nunca reclaman la misma fila | `notification-outbox` | Reclamación exclusiva de unidades de entrega pendientes entre despachadores concurrentes | Integración PostgreSQL (R1) |
| 9 | Reintento con contador de intentos y marcado final `Failed` | `notification-outbox` | Reintento con contador de intentos y estado terminal al agotarse | Unitaria SQLite |
| 10 | Corrección de `NotificationQueueHealthCheck` | `health-checks` | Diagnóstico de disponibilidad del endpoint `/ready` (MODIFICADO) | Unitaria SQLite |
| 11 | Métricas mínimas por ejecución persistidas en bitácora | `background-jobs-scheduling` | Métricas mínimas persistidas por ejecución | Unitaria SQLite |
| 12 | Migración en tres pasos, base SQLite preexistente sin pérdida de datos | `notification-outbox` + `background-jobs-scheduling` | Migración del esquema que respalda el outbox / Migración del esquema de idempotencia por ventana | Integración PostgreSQL (R1) + Unitaria SQLite |
| 13 | Habilitador de pruebas de integración contra PostgreSQL real (R1) | `postgres-integration-testing` | Ejecución de pruebas de integración contra PostgreSQL real dentro del runner contractual | Integración PostgreSQL / CI |
| 14 | Fallo ruidoso, nunca silencioso, si no se pueden ejecutar las pruebas de integración | `postgres-integration-testing` | Fallo ruidoso cuando no es posible ejecutar las pruebas de integración | Unitaria (guarda) + Integración (CI) |
| 15 | Documentación de despliegue (Cloud Run Jobs, Cloud Scheduler, IAM) | `background-jobs-scheduling` | Documentación operativa de despliegue del modelo externalizado | Manual del maintainer |
| 16 | Cloud Scheduler dispara e IAM autoriza la invocación del Job | `background-jobs-scheduling` | Disparo real por Cloud Scheduler e invocación autorizada del Job | Manual del maintainer |
| 17 | Reinicio del contenedor en el entorno real sin pérdida de notificaciones pendientes | `notification-outbox` | Reinicio del contenedor en el entorno real sin pérdida de notificaciones pendientes | Manual del maintainer |
| 18 | El artefacto de contenedor arranca en modo web o en modo trabajo | `dockerfile-build` | Arranque del contenedor en modo servicio web o modo trabajo de fondo (MODIFICADO) | Manual/mixto |
| 19 | Disparo e idempotencia específicos del lote nocturno | `nightly-batch-continuous-ingest` | Disparo de vida corta e idempotencia por ventana del lote nocturno (AÑADIDO) | Unitaria SQLite |

---

## Propósito

Modelo de ejecución externalizado para los cuatro trabajos de negocio de Ludeka (catalogación nocturna, radar de precios, recolector social y despachador de notificaciones): ejecuciones de vida corta disparadas por un planificador externo, sin bucles de sondeo infinitos, con idempotencia por ventana temporal respaldada por una restricción única en base de datos, contrato de código de salida y métricas persistidas por ejecución. El host web deja de alojar ningún ejecutor de negocio.

## Requirements

### Requirement: Ejecución única por ventana temporal bajo escalado y reintentos del planificador

El sistema DEBE garantizar como máximo una ejecución exitosa de la unidad de trabajo de un trabajo programado para una ventana temporal dada, incluso cuando varias instancias de proceso concurrentes (hasta el máximo configurado de escalado) intentan ejecutarla simultáneamente, y también cuando el planificador externo reintenta el disparo de esa misma ventana.

*Verificación: prueba de integración contra PostgreSQL real (depende de R1) para ambos escenarios — la garantía de exclusión depende de una restricción `UNIQUE` real en base de datos, que SQLite no puede ejercitar de forma equivalente bajo concurrencia genuina.*

#### Scenario: Cinco instancias concurrentes disputan la misma ventana

- GIVEN el trabajo del lote nocturno configurado con su ventana de ejecución diaria
- AND cinco instancias del proceso lanzadas simultáneamente para esa misma ventana, simulando el escalado hasta `--max-instances=5`
- WHEN las cinco intentan reservar y ejecutar la unidad de trabajo de esa ventana
- THEN como máximo una completa el procesamiento de esa ventana
- AND las demás detectan que la ventana ya fue reclamada o completada y terminan sin procesar ningún trabajo duplicado.

#### Scenario: El planificador reintenta el disparo de una ventana ya completada

- GIVEN una ventana temporal ya marcada como ejecutada con éxito en la bitácora persistida
- WHEN el planificador externo reintenta el disparo para esa misma ventana (por ejemplo, tras un timeout de red)
- THEN el intento de reservar de nuevo esa ventana falla al chocar contra una restricción `UNIQUE` en base de datos —nunca contra una comprobación `if` en memoria—
- AND el trabajo no repite el procesamiento ni duplica ninguna notificación asociada a esa ventana.

### Requirement: Ausencia de estado en memoria para el control de ejecución

Ningún trabajo DEBE depender de un campo en memoria del proceso para determinar si su unidad de trabajo ya se ejecutó en una ventana dada. Toda la información necesaria para responder "¿esto ya se ejecutó?" DEBE leerse de almacenamiento persistente antes de decidir ejecutar.

*Verificación: prueba unitaria (SQLite) — el comportamiento a probar es la ausencia de acoplamiento a estado de proceso y la lectura correcta del estado persistido; no requiere ejercitar la restricción `UNIQUE` bajo concurrencia real.*

#### Scenario: Instancia recién iniciada sin estado previo — lote nocturno

- GIVEN una instancia de proceso recién iniciada, equivalente a un reinicio completo del host, sin ningún estado previo en memoria
- AND una ventana del lote nocturno ya completada según la bitácora persistida por una instancia anterior
- WHEN se le pide ejecutar la unidad de trabajo de esa ventana
- THEN la nueva instancia consulta la bitácora persistida, detecta que la ventana ya está completada, y no repite el trabajo
- AND no existe en el código fuente ningún campo equivalente a `_lastExecutionDate`.

#### Scenario: Instancia recién iniciada sin estado previo — boletín semanal de notificaciones

- GIVEN una instancia de proceso recién iniciada, sin ningún estado previo en memoria
- AND el boletín semanal de una semana concreta ya marcado como despachado en la bitácora persistida
- WHEN esa instancia evalúa si debe despachar el boletín de esa semana
- THEN no lo despacha de nuevo
- AND no existe en el código fuente ningún campo equivalente a `_lastFridayBulletinDispatched`.

### Requirement: El host web no ejecuta ningún trabajo de negocio en proceso

El host web (`Ludeka.Web`) NO DEBE registrar ninguno de los cuatro trabajos de negocio (catalogación nocturna, radar de precios, recolector social, despachador de notificaciones comunitarias) como `IHostedService`/`BackgroundService`, bajo ninguna configuración ni entorno. La ejecución de estos trabajos DEBE ocurrir exclusivamente a través del ejecutor externalizado. Este requisito es el sustituto verificable desde el repositorio de la afirmación no verificable "el host web no consume CPU de fondo" (no auditable sin acceso a Google Cloud).

*Verificación: prueba unitaria (SQLite o dobles de prueba) — se resuelve la composición real de servicios del host web en un contenedor de pruebas y se afirma sobre el conjunto de `IHostedService` registrados; no requiere PostgreSQL.*

#### Scenario: El contenedor de inyección de dependencias del host web no resuelve trabajos de negocio

- GIVEN la composición de servicios del host web tal como queda tras este incremento
- WHEN se construye el `IServiceProvider` a partir de esa composición y se resuelve `IEnumerable<IHostedService>`
- THEN el conjunto resuelto no contiene ninguno de los cuatro tipos de trabajo de negocio, en ningún entorno ni configuración evaluada.

#### Scenario: Ausencia estructural de registro en el código fuente

- GIVEN el código fuente de `src/` tras aplicar este incremento
- WHEN se buscan invocaciones a `AddHostedService` para los cuatro tipos de trabajo de negocio
- THEN la búsqueda no devuelve ningún resultado.

### Requirement: Contrato de código de salida por ejecución

Toda ejecución de un trabajo DEBE finalizar el proceso con un código de salida distinto de cero cuando encuentra un fallo observable que impide completar correctamente su unidad de trabajo, y con código de salida cero en caso de finalización correcta —incluido el caso en que la ventana ya estaba completada y la ejecución no tiene nada que hacer.

*Verificación: prueba unitaria (SQLite o dobles de prueba) — se invoca el punto de entrada de la ejecución del trabajo y se afirma sobre el código de salida devuelto ante fallo inducido y ante éxito; no requiere PostgreSQL.*

#### Scenario: Fallo observable durante la unidad de trabajo

- GIVEN una ejecución del trabajo que lanza una excepción no controlada o detecta un fallo observable durante su unidad de trabajo
- WHEN el proceso finaliza
- THEN el código de salida del proceso es distinto de cero.

#### Scenario: Finalización correcta, incluido el caso sin trabajo pendiente

- GIVEN una ejecución del trabajo que completa su unidad de trabajo sin errores, incluido el caso en que la ventana ya estaba completada y no hay nada que hacer
- WHEN el proceso finaliza
- THEN el código de salida es cero.

### Requirement: Ejecución de vida corta — una unidad de trabajo por disparo

Cada uno de los cuatro trabajos de negocio DEBE ejecutar exactamente una unidad de trabajo por invocación y terminar el proceso a continuación. NO DEBE entrar en un bucle de sondeo persistente ni bloquearse indefinidamente esperando un disparo futuro.

*Verificación: prueba unitaria (SQLite o dobles de prueba) — se afirma que el punto de entrada de la ejecución completa y retorna en un tiempo acotado, sin necesidad de inyectar una señal de cancelación para detener un bucle.*

#### Scenario: El proceso termina por sí mismo tras su unidad de trabajo

- GIVEN el proceso del trabajo invocado para una ventana pendiente
- WHEN completa su unidad de trabajo, con o sin fallos
- THEN el proceso termina por sí mismo sin esperar ninguna señal externa adicional y sin reentrar en un bucle de sondeo.

### Requirement: Métricas mínimas persistidas por ejecución

Toda ejecución de un trabajo DEBE persistir en su bitácora, como mínimo: marca de inicio, marca de fin, número de elementos procesados, número de fallos y duración.

*Verificación: prueba unitaria (SQLite) — se ejecuta la orquestación del trabajo contra un repositorio de pruebas y se afirma sobre los campos de la fila de bitácora persistida.*

#### Scenario: Bitácora completa tras una ejecución con fallos parciales

- GIVEN una ejecución del lote nocturno que procesa un conjunto de elementos, algunos de los cuales fallan
- WHEN la ejecución finaliza
- THEN la bitácora persistida para esa ejecución contiene marca de inicio, marca de fin, número de elementos procesados, número de fallos y la duración calculada.

### Requirement: Migración del esquema de idempotencia por ventana sin pérdida de datos

Añadir la clave de ventana temporal y su restricción única al esquema de seguimiento de ejecuciones DEBE preservar todas las filas de bitácora ya persistidas, tanto en el historial de producción de PostgreSQL como en cualquier base de datos SQLite ya existente en disco.

*Verificación: Escenario 1, prueba de integración contra PostgreSQL real (depende de R1) — es la única forma de demostrar que una migración de Entity Framework Core se aplica sin pérdida sobre datos reales. Escenario 2, prueba unitaria (SQLite) — se siembra una base SQLite con el esquema anterior y datos, se ejecuta el reconciliador, y se comprueba la preservación.*

#### Scenario: Migración Npgsql sobre datos de producción existentes

- GIVEN una base de datos PostgreSQL con filas de bitácora de ejecución ya existentes, anteriores a este incremento
- WHEN se aplica la migración de Entity Framework Core que añade la clave de ventana y su restricción única
- THEN todas las filas existentes conservan sus valores originales
- AND quedan disponibles con la columna nueva en su valor por defecto.

#### Scenario: Reconciliación de una base SQLite ya existente en disco

- GIVEN una base de datos SQLite ya existente en disco, creada antes de este incremento
- WHEN el reconciliador de esquema SQLite se ejecuta al arrancar la aplicación
- THEN la base se actualiza con la columna nueva
- AND ninguna fila ni dato ya presente se pierde.

### Requirement: Documentación operativa de despliegue del modelo externalizado

La documentación de despliegue DEBE describir los recursos de Cloud Run Jobs, los Cloud Scheduler y la cuenta de servicio con permiso de invocación necesarios para operar el modelo de ejecución externalizado, y el pipeline de integración continua DEBE publicar la revisión del artefacto de trabajos además de la del servicio web.

*Verificación: manual del maintainer — es inspección documental y de configuración de pipeline, no ejercitable con `dotnet test`.*

#### Scenario: Guías de despliegue documentan la infraestructura nueva

- GIVEN el incremento aplicado
- WHEN se revisan `docs/deployment/DEPLOYMENT_GUIDE.md` y `docs/deployment/google-cloud-run.md`
- THEN ambos documentan los Cloud Run Jobs, los cuatro Cloud Scheduler y la cuenta de servicio con el rol de invocación necesario.

#### Scenario: El pipeline publica también la revisión del artefacto de trabajos

- GIVEN el pipeline `.github/workflows/ci-cd.yml` tras el incremento
- WHEN se ejecuta un despliegue
- THEN se publica una revisión del artefacto de trabajos además de la revisión del servicio web.

### Requirement: Disparo real por Cloud Scheduler e invocación autorizada del Job (verificación manual)

En el entorno real de Google Cloud, Cloud Scheduler DEBE disparar la ejecución del Job de Cloud Run correspondiente en la cadencia configurada, y la cuenta de servicio invocadora DEBE estar autorizada mediante el rol de invocación para que la invocación se acepte.

*Verificación: manual del maintainer — no hay acceso autorizado a Google Cloud en este ciclo; no verificable desde el repositorio.*

#### Scenario: Cloud Scheduler dispara el Job y la invocación es autorizada

- GIVEN los cuatro recursos de Cloud Scheduler y el Job de Cloud Run desplegados según la documentación de despliegue
- WHEN llega la cadencia configurada de un Scheduler
- THEN Cloud Scheduler invoca el Job, IAM autoriza la invocación mediante el rol de invocador, y el Job arranca y ejecuta su unidad de trabajo.

---

## 5. Fuera de alcance de esta especificación

- **Extracción de la composición de DI de dominio (rebanada R2 de la propuesta).** La propuesta la declara explícitamente sin cambio de comportamiento ("rebanada propia, mecánica y sin cambio de comportamiento"). Al no haber comportamiento observable nuevo ni modificado, no genera ningún requisito de especificación; es trabajo previo real para `sdd-design`/`sdd-tasks`, no una capacidad.
- **Aprovisionamiento efectivo de recursos de Google Cloud** (Jobs, los cuatro Cloud Scheduler, la cuenta de servicio). Solo se cubre como verificación manual del maintainer (requisitos "Disparo real por Cloud Scheduler..." y "Reinicio del contenedor en el entorno real..."); no hay acceso autorizado a GCP en este ciclo.
- **El comportamiento real de *CPU throttling* de Cloud Run con `--min-instances=0`.** Es un supuesto de plataforma (propuesta, riesgo 9), no un hecho verificable desde el repositorio; no se usa como criterio de aceptación en ningún requisito de este documento. El sustituto verificable es el requisito "El host web no ejecuta ningún trabajo de negocio en proceso".
- **"Prueba con navegador real"** (documento de incremento §4, criterio 7, primera mitad). No se ha encontrado ninguna evidencia verificada en la propuesta ni en la exploración que ate esta frase a un comportamiento concreto de INC-47 (a diferencia de la segunda mitad del mismo criterio —reinicio del contenedor sin pérdida de notificaciones—, que sí está cubierta). Se declara como hueco de evidencia explícito en la sección siguiente en lugar de inventar un requisito de interfaz sin respaldo.
- **Migrar el resto de `tests/` a PostgreSQL.** Explícitamente fuera de alcance en la propuesta (§2.2); el habilitador de `postgres-integration-testing` se dimensiona solo para lo que este incremento necesita.
- **Rediseño de `SqliteCommunityNotificationRepository.GetRecentLogsAsync`** más allá de lo que exija la reclamación de lotes (propuesta §2.2). Su uso de lectura para el panel no es objeto de este incremento.
- **Suite completa en verde con `dotnet test Ludeka.sln`.** Es una política de verificación transversal, no un requisito de comportamiento del sistema; se aplica igualmente a todos los requisitos anteriores y corresponde a `sdd-verify`, no a un requisito individual de este documento.

---

## 6. Huecos de evidencia y notas para fases posteriores

1. **Formato legado de las especificaciones vivas afectadas.** `health-checks/spec.md` y `dockerfile-build/spec.md` predatan la convención canónica `### Requirement:` / `#### Scenario:` (usan un formato propio de "Escenario N" sin agrupación por requisito nombrado). El mapeo de sus deltas a un único requisito nombrado en este documento es una interpretación razonable y documentada, no una operación mecánica de "copiar el bloque completo": se ha preservado explícitamente el comportamiento no tocado (liveness, componentes `database`/`storage`, y las dos etapas de compilación del Dockerfile) sin repetirlo, y se ha señalado la porción exacta que cambia. `sdd-archive` deberá normalizar el resto de cada fichero a la convención canónica al fusionar, siguiendo el precedente ya aplicado al delta archivado de `change-46-autenticacion-real` sobre `editorial-role-management`.
2. **"Prueba con navegador real"** — hueco de evidencia declarado en la sección 5. No se fuerza un mapeo sin respaldo verificado.
3. **Estimaciones de líneas por rebanada (propuesta §9).** No son objeto de esta especificación; `sdd-tasks` debe medirlas contra el diff real, como la propia propuesta indica.
4. **No se ejecutó `dotnet test Ludeka.sln` en esta fase.** Igual que en la propuesta y la exploración, no se confirma en esta fase el recuento exacto de pruebas en verde sobre `56c02b9`; queda como responsabilidad de `sdd-verify`.

---

## 7. Recuento de esta especificación

- **20 requisitos**: 9 en `background-jobs-scheduling`, 6 en `notification-outbox`, 2 en `postgres-integration-testing`, 1 MODIFICADO en `health-checks`, 1 MODIFICADO en `dockerfile-build`, 1 AÑADIDO en `nightly-batch-continuous-ingest`.
- **36 escenarios** en total.
- **Verificación automatizada: 29 escenarios** (22 con prueba unitaria sobre SQLite + 7 con prueba de integración contra PostgreSQL real, dependientes de la rebanada R1).
- **Verificación manual del maintainer: 7 escenarios**, todos ligados a infraestructura real de Google Cloud (Cloud Scheduler, Cloud Run Jobs, IAM, reinicio real de contenedor, documentación de despliegue y arranque real del contenedor en modo trabajo) sin acceso autorizado a GCP en este ciclo.
