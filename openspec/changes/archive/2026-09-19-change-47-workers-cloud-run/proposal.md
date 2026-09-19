# Propuesta — INC-47: Trabajos en Segundo Plano Correctos en Google Cloud Run

> **Cambio:** `change-47-workers-cloud-run` · **Fase:** `sdd-propose` · **Fecha:** 2026-09-18
> **Worktree:** `C:\repos\ludeka-wt\workers-cloud-run` (rama `inc/workers-cloud-run`, commit base `56c02b9`)
> **Entradas:** [`exploration.md`](exploration.md) (evidencia verificada contra `56c02b9`) · [`docs/increments/inc-47-workers-cloud-run.md`](../../../docs/increments/inc-47-workers-cloud-run.md)
> **Almacén de artefactos:** `hybrid` — este fichero más la observación Engram `sdd/change-47-workers-cloud-run/proposal`
> **Modelo de ejecución:** **decidido por el maintainer el 2026-09-18** (Rama A). No se reabre.

---

## 1. Intención

Los cuatro trabajos en segundo plano de Ludeka se ejecutan dentro del proceso web, con estado de planificación en memoria y sin ninguna coordinación entre instancias. El despliegue real es `--min-instances=0 --max-instances=5` (`.github/workflows/ci-cd.yml:102`), así que **el diseño actual es incorrecto por construcción**, no por un defecto puntual.

Las tres fallas siguen vivas hoy, confirmadas por evidencia negativa exhaustiva: la búsqueda de `pg_advisory`, `advisory_lock`, `IDistributedCache`, *leader election*, `SKIP LOCKED`, `FOR UPDATE` e `IHostApplicationLifetime` en todo `src/` devuelve **cero resultados**.

| # | Falla | Evidencia |
|---|---|---|
| 1 | **Ejecución duplicada al escalar.** Hasta 5 instancias ejecutan el lote nocturno, el radar y el recolector social a la vez. | `Program.cs:166,215,244,319` (los cuatro y únicos `AddHostedService`); sin ningún mecanismo de bloqueo en `src/` |
| 2 | **Pérdida de estado de planificación.** «Ya se ejecutó hoy» vive en campos de instancia. | `NightlyCatalogingHostedService.cs:22,67` (`_lastExecutionDate`), `CommunityNotificationDispatcherHostedService.cs:16,92` (`_lastFridayBulletinDispatched`) |
| 3 | **Pérdida de notificaciones.** Un mensaje encolado en la instancia A solo lo consume A; si A escala a cero antes de drenar, desaparece sin rastro. | `InMemoryCommunityNotificationQueue` es un `Channel<T>` acotado registrado con `AddSingleton` (`Program.cs:241`); sin drenado ordenado |

**Por qué ahora.** La falla 3 es la más grave y la peor diagnosticada. El documento de partida la daba por medio-resuelta («`CommunityNotificationLog` ya persiste un registro por envío»), pero la exploración demostró que **ese registro no es un outbox**: la fila se crea *dentro* de `CommunityNotificationService.SendToDiscordAsync`/`SendToTelegramAsync` (`src/Ludeka.Application/Features/Community/CommunityNotificationService.cs:108` y `:157`), es decir **después** de que el despachador ya leyó el mensaje del `Channel` en memoria. Hoy, si la instancia muere antes de drenar, **no queda ninguna fila que lo delate**: la pérdida es silenciosa y no auditable.

**Cómo se ve el éxito.** Ningún trabajo se ejecuta dos veces para la misma ventana temporal; ninguna notificación encolada se pierde al escalar a cero; ningún trabajo depende de estado en memoria para saber si ya se ejecutó; y todo ello demostrado con pruebas automáticas que se ejecutan en el *runner* contractual `dotnet test Ludeka.sln`.

---

## 2. Alcance

### 2.1. En alcance (ordenado por dependencia real)

1. **Habilitador de pruebas de integración contra PostgreSQL real.** Infraestructura de pruebas (por ejemplo Testcontainers con PostgreSQL) más una prueba que ejercite `SELECT ... FOR UPDATE SKIP LOCKED` contra una base real. **Va primero porque el outbox no se puede probar sin él** (ver §6, riesgo 1).
2. **Extracción de la composición de DI de dominio** de `Program.cs` a una extensión reutilizable invocable desde un host que no sea web, sin cambio de comportamiento.
3. **Cambio de esquema y migración:** columnas `Attempts`/`RetryCount` y `NextAttemptAt` en `CommunityNotificationLog`, clave de ventana con restricción `UNIQUE` para la idempotencia, índices de reclamación. Con los **tres pasos obligatorios** de §4.
4. **Outbox real:** la fila se crea en el punto de `EnqueueAsync`, no tras la lectura; el despachador pasa a reclamar lotes con `FOR UPDATE SKIP LOCKED`, marca `Sent`/`Failed`, cuenta intentos y reprograma con `NextAttemptAt`. `InMemoryCommunityNotificationQueue` se conserva para desarrollo local y pruebas.
5. **Idempotencia por ventana temporal** en los cuatro trabajos, construida desde cero (§3.3), de modo que un reintento de Cloud Scheduler no duplique trabajo.
6. **Observabilidad y contrato de salida:** cada ejecución persiste inicio, fin, elementos procesados, fallos y duración; código de salida distinto de cero ante fallo observable.
7. **Host de trabajos de vida corta:** una ejecución por disparo, sin bucle `while` ni `Task.Delay` infinito. El empaquetado concreto queda abierto (§7, decisión 1).
8. **Retirada de los cuatro `AddHostedService`** del host web (`Program.cs:166,215,244,319`), **al final de la cadena** para que ningún estado intermedio mergeado deje producción sin ejecutor.
9. **Configuración:** secciones `Workers` y `Outbox` en `appsettings.json`. Sin colisión: ninguna de las 14 clases de opciones existentes usa esos nombres de sección.
10. **Pipeline y documentación de despliegue:** paso nuevo en `ci-cd.yml` para publicar la revisión del Job, y actualización de `docs/deployment/DEPLOYMENT_GUIDE.md` y `docs/deployment/google-cloud-run.md` con Cloud Run Jobs, los cuatro Cloud Scheduler y la cuenta de servicio con `roles/run.invoker`. Hoy ninguno de los dos documentos menciona nada de eso.

### 2.2. Fuera de alcance

- **Rama B (workers en proceso con `pg_advisory_lock`).** Descartada por el maintainer el 2026-09-18: no resuelve la falla 3 por sí sola, no ahorra la inversión en pruebas contra PostgreSQL real y mantiene el consumo de CPU de fondo.
- **Provisión efectiva de los recursos en Google Cloud.** Esta fase y el incremento entregan pipeline y documentación; **no hay acceso autorizado a GCP**, de modo que la verificación de que Scheduler dispara, el Job arranca e IAM autoriza la invocación queda como paso manual del maintainer, explícitamente fuera de la verificación automatizada.
- **Migrar el resto de la suite a PostgreSQL.** El habilitador se dimensiona para lo que este incremento necesita (outbox e idempotencia). Los ~40 ficheros de `tests/` que usan `UseSqlite` se quedan como están.
- **Rediseñar `SqliteCommunityNotificationRepository.GetRecentLogsAsync`** más allá de lo que exija la reclamación de lotes. Su patrón actual (`.ToListAsync()` y ordenación en memoria) es inadecuado para reclamar, pero su uso de lectura para el panel no es objeto de este incremento.
- **Sincronizar el módulo 18 de la especificación viva** con la columna `BggDiscoveryCount` de `NightlyCatalogingExecutionLog`, no documentada. Hueco preexistente y ajeno a INC-47; se deja registrado, no se arregla aquí.
- **Tocar `FoundingVerdictService` y `RuleQAService`.** No es una renuncia: es que *no hace falta*. Ambos productores (`FoundingVerdictService.cs:217`, `RuleQAService.cs:199`) llaman únicamente a la interfaz `ICommunityNotificationQueue`, nunca a la implementación concreta. Si el outbox preserva `EnqueueAsync`, esos dos ficheros no cambian. Esto invalida el riesgo «Migrar la cola a outbox toca `FoundingVerdictService` y `RuleQAService`» del §5 del documento de partida.

---

## 3. Enfoque

**Modelo de ejecución: Rama A.** Los cuatro trabajos se externalizan a Cloud Run Jobs disparados por Cloud Scheduler. Cada trabajo se ejecuta una vez, termina y reporta código de salida. La persistencia pasa a ser la única fuente de verdad tanto de «¿ya se ejecutó esta ventana?» como de «¿qué notificaciones quedan pendientes?».

Esto resuelve la falla 1 por construcción (una ejecución por disparo) y da CPU dedicada durante la vida de cada ejecución, sin depender de tráfico HTTP concurrente. Las fallas 2 y 3 **no** las resuelve la externalización: las resuelve el outbox y la idempotencia por ventana, que son trabajo obligatorio con independencia del modelo de ejecución.

### 3.1. Extracción de la composición de DI — trabajo previo real

`Program.cs` es hoy un único fichero de *top-level statements* que entrelaza unas **230 líneas** de registro de infraestructura de dominio con registro específicamente web (`AddRazorComponents`, autenticación por cookie, *output caching*, *antiforgery*, *health checks*, endpoints Minimal API). **No existe ninguna extensión reutilizable** del tipo `AddLudekaDomainServices(...)` que un host de consola pueda invocar sin duplicar esas líneas, y el `Dockerfile` tiene `ENTRYPOINT ["dotnet", "Ludeka.Web.dll"]` fijo, sin lógica de argumentos.

Extraer esa composición es trabajo previo real y **necesario en ambas sub-opciones de empaquetado**, no el «un argumento de entrada» que sugería el documento de partida. Se aborda como una rebanada propia, mecánica y sin cambio de comportamiento, con los cuatro `AddHostedService` todavía registrados.

Punto a favor: los cuatro `BackgroundService` ya resuelven su servicio de dominio vía `IServiceScopeFactory.CreateScope().GetRequiredService<T>()`, exactamente el patrón que reutilizaría un host de consola sin tocar la capa de aplicación.

### 3.2. Outbox real — mover la escritura, no solo la lectura

El trabajo no es «cambiar el mecanismo de lectura del despachador». Son dos movimientos:

1. **Adelantar la creación de la fila** al punto de `EnqueueAsync`, en la misma transacción que el cambio de dominio que la origina, en lugar de crearla en `CommunityNotificationService.cs:108,157`.
2. **Reescribir `CommunityNotificationDispatcherHostedService.ProcessQueueAsync`**, que hoy consume `ReadAllAsync` como un `IAsyncEnumerable` infinito (`:43`) — forma incompatible con reclamar lotes acotados.

**Implicación de contrato a resolver en diseño:** `ICommunityNotificationQueue` tiene exactamente dos métodos (`src/Ludeka.Application/Contracts/ICommunityNotificationQueue.cs:10-11`): `EnqueueAsync` y `ReadAllAsync`. Preservar `EnqueueAsync` mantiene intactos a los dos productores, pero `ReadAllAsync` no encaja con la reclamación por lotes. La vía que respeta esa asimetría es conservar el contrato de escritura y añadir un contrato de reclamación separado para el despachador, en lugar de deformar `ReadAllAsync`. Se señala como implicación del enfoque; la forma exacta la fija `sdd-design`.

Los índices EF Core existentes sobre `NotificationLogs` (`LudekaDbContext.cs:281-285`: `Status`, `Channel`, `CreatedAt`) ya están alineados con el patrón `WHERE Status = Queued ORDER BY CreatedAt` que necesita el despachador.

### 3.3. Idempotencia — se construye entera

`NightlyCatalogingExecutionLog` **no tiene ninguna restricción única por ventana temporal**. La única configuración EF Core es `nightlyLog.HasIndex(l => l.StartedAt)` (`LudekaDbContext.cs:403`), índice **no** único y solo de rendimiento. No hay columna de clave de ventana. No hay nada que reaprovechar: la idempotencia se construye desde cero, con clave de ventana explícita y restricción `UNIQUE` en base de datos, de modo que un reintento de Cloud Scheduler choque contra la base y no contra un `if` en memoria.

### 3.4. Habilitador de pruebas — por qué va primero, y por qué es viable

Con `strict_tdd: true`, el test que el documento de partida exige en su §2.6 («dos consumidores concurrentes no reclaman la misma fila») **no se puede escribir hoy contra código real**: `SQLite` no implementa `FOR UPDATE SKIP LOCKED` y EF Core no la traduce desde LINQ. La suite es 100 % SQLite.

**Dato verificado que abarata este habilitador:** el job `build-and-test` corre en `ubuntu-latest` (`ci-cd.yml:20-22`) y ejecuta `docker build -t ludeka:ci .` en el paso de `ci-cd.yml:47`, dentro del **mismo job** que `dotnet test` (`:44`). Es evidencia del propio repositorio de que ese *runner* dispone de demonio Docker operativo, así que una infraestructura tipo Testcontainers es viable en CI **sin cambiar de runner ni de job**. Detalle a respetar: `dotnet test` corre con `--no-build` (`:44`) sobre lo compilado en `:41`, de modo que cualquier proyecto de pruebas nuevo debe formar parte de `Ludeka.sln` para ser compilado y recogido.

### 3.5. Frecuencias de Cloud Scheduler (propuestas, configurables)

Se arrastran los valores del documento de partida, **todos configurables** y ninguno bloqueante: lote nocturno 1×/día; radar de precios cada 6 h (coincide con `PriceRadarOptions.cs:18`); recolector social cada 120 min (coincide con `appsettings.json:120`); despachador de outbox cada 5-15 min.

---

## 4. Coste de migraciones — tres pasos obligatorios

El repositorio **no soporta dos historiales de migración paralelos**: soporta un único historial (Npgsql) más un reconciliador manual para SQLite. Cualquier cambio de esquema de este incremento exige los tres pasos, sin atajos:

| Paso | Acción | Destino |
|---|---|---|
| 1 | `dotnet ef migrations add` | Npgsql / producción (`src/Ludeka.Infrastructure/Migrations/`, 3 migraciones reales más el snapshot) |
| 2 | Actualizar `SqliteSchemaMigrator.cs` **a mano** | Bases SQLite ya existentes en disco (desarrollo) |
| 3 | Nada adicional | Las pruebas recrean el esquema con `EnsureCreatedAsync()`, que recoge automáticamente las columnas nuevas |

---

## 5. Capacidades

> Contrato entre esta propuesta y `sdd-spec`.
>
> **Nota de convención:** los cambios recientes de este almacén (`change-44`, `change-45`, `docker-prod-cloudrun`) archivaron un único `spec.md` plano en la carpeta del cambio, no `specs/<capacidad>/spec.md`. `sdd-spec` debe seguir la convención vigente del almacén; los nombres de capacidad de abajo son el contrato de *contenido*, no una imposición de disposición de ficheros.

### Capacidades nuevas

- `background-jobs-scheduling`: modelo de ejecución externalizado — trabajos de vida corta disparados por planificador, una ejecución por disparo, idempotencia por ventana temporal con restricción única, código de salida distinto de cero ante fallo, y métricas por ejecución persistidas. Incluye el requisito de que el host web no registre ningún `IHostedService` de negocio.
- `notification-outbox`: outbox persistente real — la fila se crea al encolar, la reclamación es exclusiva entre consumidores concurrentes (`FOR UPDATE SKIP LOCKED`), con contador de intentos, reprogramación de reintento y estados terminales `Sent`/`Failed`.
- `postgres-integration-testing`: capacidad de la suite para ejercitar primitivas específicas de PostgreSQL contra una base real dentro del *runner* contractual `dotnet test Ludeka.sln`. Es el habilitador del que dependen las dos capacidades anteriores.

### Capacidades modificadas

- `health-checks`: el chequeo `notification_queue` (registrado en `Program.cs:325`) es hoy un chequeo vacío — `NotificationQueueHealthCheck.cs:26` solo reporta `_queue.GetType().Name` y devuelve `Healthy` siempre que el servicio esté resuelto. Con outbox debe reportar salud observable (por ejemplo, profundidad de la cola pendiente y antigüedad del elemento más viejo) en lugar del nombre de un tipo.
- `dockerfile-build`: el `ENTRYPOINT` fijo deja de ser suficiente; el artefacto debe poder arrancar en modo trabajo. La forma depende de la decisión de empaquetado (§7, decisión 1).
- `nightly-batch-continuous-ingest`: cambia el requisito de disparo y de idempotencia del lote nocturno — la bitácora persistida sustituye a `_lastExecutionDate` como fuente de verdad.

---

## 6. Áreas afectadas

| Área | Impacto | Descripción |
|---|---|---|
| `src/Ludeka.Web/Program.cs` | Modificado | Extraer la composición de DI de dominio; retirar los 4 `AddHostedService` (`:166,215,244,319`); el registro de la cola (`:241`) pasa a resolver la implementación de outbox |
| Extensión de DI de dominio (ruta por decidir) | Nuevo | `AddLudekaDomainServices(...)` o equivalente, invocable desde un host no web |
| Host de trabajos (`src/Ludeka.Jobs` o modo del host) | Nuevo | Depende de la decisión de empaquetado (§7, decisión 1). `src/Ludeka.Jobs` no existe hoy |
| `src/Ludeka.Infrastructure/Background/NightlyCatalogingHostedService.cs` | Modificado | La lógica pasa a unidad de trabajo de un disparo; desaparecen el sondeo de 15 min (`:79-80`) y `_lastExecutionDate` (`:22,67`) |
| `src/Ludeka.Infrastructure/Background/PriceRadarHostedService.cs` | Modificado | Desaparece el bucle de `CheckIntervalHours` (`:69-72`) |
| `src/Ludeka.Infrastructure/Background/SocialCollectorHostedService.cs` | Modificado | Desaparece el bucle de `IntervalMinutes` (`:82-83`) |
| `src/Ludeka.Infrastructure/Notifications/CommunityNotificationDispatcherHostedService.cs` | Modificado | Reescritura de `ProcessQueueAsync`: de `ReadAllAsync` infinito (`:43`) a reclamación de lotes; desaparecen `_lastFridayBulletinDispatched` (`:16,92`) y el retardo de 60 min (`:103`) |
| `src/Ludeka.Application/Features/Community/CommunityNotificationService.cs` | Modificado | Mover la creación de `CommunityNotificationLog` (`:108`, `:157`) fuera del camino de envío |
| `src/Ludeka.Application/Contracts/ICommunityNotificationQueue.cs` | Modificado | Se preserva `EnqueueAsync` (`:10`); `ReadAllAsync` (`:11`) no sirve para reclamar lotes → contrato de reclamación separado (forma exacta en `sdd-design`) |
| `src/Ludeka.Core/Entities/CommunityNotificationLog.cs` | Modificado | Añadir `Attempts`/`RetryCount` y `NextAttemptAt`, hoy inexistentes |
| `src/Ludeka.Core/Entities/NightlyCatalogingExecutionLog.cs` | Modificado | Añadir clave de ventana temporal |
| `src/Ludeka.Infrastructure/Data/LudekaDbContext.cs` | Modificado | `UNIQUE` por ventana (hoy `:403` es índice no único); revisar índices de reclamación (`:281-285`) |
| `src/Ludeka.Infrastructure/Migrations/` | Nuevo | Migración Npgsql (paso 1 de §4) |
| `src/Ludeka.Infrastructure/Data/SqliteSchemaMigrator.cs` | Modificado | Reconciliación manual (paso 2 de §4) |
| `src/Ludeka.Infrastructure/Notifications/InMemoryCommunityNotificationQueue.cs` | Conservado | Solo desarrollo local y pruebas |
| `src/Ludeka.Web/Health/NotificationQueueHealthCheck.cs` | Modificado | Hoy solo reporta el nombre del tipo (`:26`); pasa a reportar salud observable del outbox |
| `tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj` o proyecto nuevo | Modificado / Nuevo | Referencias del habilitador; debe estar en `Ludeka.sln` por el `--no-build` de `ci-cd.yml:44` |
| `.github/workflows/ci-cd.yml` | Modificado | Paso nuevo para publicar la revisión del Job; hoy hay un único job `deploy-cloudrun` (`:52-118`) con un solo artefacto y un solo `deploy-cloudrun@v2` |
| `Dockerfile` | Modificado | `ENTRYPOINT ["dotnet", "Ludeka.Web.dll"]` fijo, sin lógica de argumentos |
| `docs/deployment/DEPLOYMENT_GUIDE.md`, `docs/deployment/google-cloud-run.md` | Modificado | Hoy no mencionan Cloud Scheduler, Cloud Run Jobs ni roles IAM de invocación |
| `appsettings.json` | Modificado | Secciones `Workers` y `Outbox` nuevas, siguiendo el patrón establecido (`Enabled` como primera propiedad) |
| Recursos de Google Cloud | Nuevo (externo) | Job(s) Cloud Run, 4 Cloud Scheduler, cuenta de servicio con `roles/run.invoker`. No verificable desde el repositorio |

---

## 7. Riesgos

| # | Severidad | Riesgo | Mitigación |
|---|---|---|---|
| 1 | **Alto** | **Sin infraestructura de pruebas contra PostgreSQL real no se puede cumplir Strict TDD para el outbox.** `tests/` es 100 % SQLite (~40 ficheros con `UseSqlite`, cero con `UseNpgsql`/Testcontainers/Respawn; el único fichero que menciona `Npgsql`, `DatabaseProviderTests.cs`, solo prueba *parsing* de cadenas). SQLite no implementa `FOR UPDATE SKIP LOCKED` y EF Core no la traduce. Bloquea el criterio §4.6 del documento de partida tal como está redactado | Se trata como **trabajo de primera clase dentro del alcance** (§2.1, punto 1) y se ordena **antes** del outbox. Abaratado por evidencia verificada: el *runner* de CI ya ejecuta `docker build` en el mismo job que `dotnet test` (`ci-cd.yml:44,47`) |
| 2 | Medio | La extracción de ~230 líneas de composición de DI puede alterar el orden de registro y romper resoluciones que hoy funcionan por accidente de orden | Rebanada propia, mecánica, sin cambio de comportamiento, con los 4 `AddHostedService` aún registrados y la suite completa en verde como red |
| 3 | Medio | **El modelo de fila del outbox está sin decidir.** Con Discord y Telegram habilitados, un mensaje lógico genera **dos** filas (una por canal). Tocar el esquema antes de decidir obliga a una segunda migración | Decisión explícita de `sdd-design` (§8, decisión 2), **antes** de la rebanada de esquema |
| 4 | Medio | El incremento excede claramente el presupuesto de 400 líneas por PR | Cadena de PRs con corte propuesto en §9 |
| 5 | Medio | Un estado intermedio mergeado que retire los `AddHostedService` antes de que los Jobs existan y disparen deja producción **sin ningún ejecutor** | La retirada es la **última** rebanada, detrás de configuración, y solo después de que el host de trabajos y el pipeline estén mergeados |
| 6 | Medio | Cloud Scheduler puede reintentar un disparo y duplicar trabajo | La idempotencia por ventana con `UNIQUE` en base de datos es obligatoria, no opcional (§3.3) |
| 7 | Medio | La infraestructura nueva de Google Cloud (Jobs, Scheduler, IAM) **no es verificable desde el repositorio**: no hay acceso autorizado a GCP | El incremento entrega pipeline y documentación; la verificación del despliegue real se declara paso manual del maintainer, fuera de los criterios automatizados (§2.2) |
| 8 | Medio | Incorporar pruebas de integración al *runner* contractual alarga `dotnet test Ludeka.sln` y añade dependencia de Docker en las máquinas de desarrollo. Con `strict_tdd: true` no se puede «saltar silenciosamente» la prueba de concurrencia cuando no hay Docker, porque es justo la que demuestra el criterio | Tensión real: `sdd-design` debe fijar la política (categoría de integración diferenciada frente a fallo explícito y ruidoso cuando falta Docker). **No se resuelve saltando la prueba en silencio** |
| 9 | Bajo | El comportamiento real de *CPU throttling* de Cloud Run con `--min-instances=0` es un **supuesto de plataforma**, no un hecho verificado (sin acceso a GCP) | No se usa como justificación decisoria: el modelo de ejecución ya está decidido y la Rama A no depende de ese supuesto para resolver la falla 1 |
| 10 | Bajo | El documento de partida contiene citas obsoletas y una ruta errónea (`Infrastructure/Notifications/CommunityNotificationService.cs`, que no existe) | Corregido en la exploración y en esta propuesta; las fases posteriores deben citar la exploración, nunca el documento de partida |

---

## 8. Decisiones de diseño pendientes

**No se deciden en esta propuesta.** Corresponden a `sdd-design` y el orquestador debe plantearlas al maintainer en ese momento.

### Decisión 1 — Empaquetado del host de trabajos

| Opción | A favor | En contra |
|---|---|---|
| Proyecto `src/Ludeka.Jobs` independiente | Separación limpia; el host web no carga código de trabajos | Enteramente nuevo (no existe hoy); segundo artefacto a construir y desplegar |
| Modo *job* del host existente seleccionado por argumento | Un solo contenedor y una sola imagen | Mezcla responsabilidades en el mismo artefacto |

**Dato duro común a ambas:** `Program.cs` entrelaza ~230 líneas de composición de DI de dominio con la composición web **sin ninguna extensión reutilizable**, y el `Dockerfile` tiene `ENTRYPOINT ["dotnet", "Ludeka.Web.dll"]` fijo sin lógica de argumentos. Extraer esa composición y añadir lógica de argumentos es trabajo previo real **en las dos opciones**: no es un factor que desempate.

### Decisión 2 — Modelo de fila del outbox

| Opción | A favor | En contra |
|---|---|---|
| `SKIP LOCKED` reclama por **fila-canal** | Compatible con el esquema actual; cambio mínimo | Conflacta «mensaje pendiente» con «intento de entrega por canal»; un mensaje lógico son dos unidades independientes |
| Una fila por **mensaje** con sub-entregas por canal | Modela el mensaje lógico una sola vez; reintento por canal sin duplicar el mensaje | Cambio de esquema mayor; tabla o columnas nuevas |

Hoy un mensaje lógico con Discord y Telegram habilitados genera **dos** filas de `CommunityNotificationLog`, creadas por separado en `CommunityNotificationService.cs:108` y `:157`. **Esta decisión debe resolverse antes de la rebanada de esquema** (§9, R3) para no pagar dos migraciones.

### No bloqueante

Las **frecuencias de Cloud Scheduler** de §3.5 se arrastran como propuestas configurables. No requieren decisión previa a la implementación.

---

## 9. Secuenciación por rebanadas revisables

`delivery_strategy` es `auto-chain` con presupuesto **fijo de 400 líneas cambiadas** (adiciones + eliminaciones) por PR.

**Cadena de PRs prevista: sí.** Siete rebanadas. El orden respeta dos dependencias duras: el habilitador de pruebas va primero porque el outbox no se puede probar sin él, y la retirada de los `AddHostedService` va última para que ningún estado intermedio mergeado deje producción sin ejecutor.

| Rebanada | Contenido | Estimación | Riesgo de presupuesto |
|---|---|---|---|
| **R1** | Habilitador de pruebas contra PostgreSQL real, con una prueba que ejercite `FOR UPDATE SKIP LOCKED` sobre una tabla real. No toca código de producción | ~150-250 líneas | Bajo |
| **R2** | Extracción de la composición de DI de dominio a extensión reutilizable, sin cambio de comportamiento | ~400-460 líneas | **Alto** — ver nota |
| **R3** | Esquema: `Attempts`/`NextAttemptAt`, clave de ventana, `UNIQUE`, índices, migración Npgsql y `SqliteSchemaMigrator` | ~200-300 líneas | Medio |
| **R4** | Outbox real: fila en `EnqueueAsync`, contrato de reclamación, reescritura de `ProcessQueueAsync` con `SKIP LOCKED`, reintentos y *health check* | ~300-400 líneas | Medio-alto |
| **R5** | Idempotencia por ventana en los cuatro trabajos, observabilidad y contrato de código de salida | ~250-350 líneas | Medio |
| **R6** | Host de trabajos (según decisión 1), `Dockerfile` y paso nuevo de pipeline | ~250-350 líneas | Medio |
| **R7** | Retirada de los cuatro `AddHostedService`, secciones `Workers`/`Outbox` en `appsettings.json` y documentación de `docs/deployment/` | ~150-250 líneas | Bajo |

**Nota sobre R2 (para que `sdd-tasks` lo aterrice).** Mover ~230 líneas de `Program.cs` a una extensión cuenta aproximadamente como ~460 líneas cambiadas (adiciones más eliminaciones) y **supera el presupuesto por sí sola**. Dos salidas, ninguna elegida aquí: partirla en dos sub-rebanadas por área (servicios de dominio frente a integraciones y clientes HTTP), o pedir un `size:exception` explícito argumentando que el diff es traslado puro sin cambio de comportamiento. `sdd-tasks` debe medir el diff real y proponer una de las dos, no asumir que cabe.

Las rebanadas R3 y R4 dependen de la **decisión 2** (§8). R6 depende de la **decisión 1**. R1 y R2 no dependen de ninguna decisión abierta y pueden arrancar de inmediato.

---

## 10. Plan de reversión

La reversión es por rebanada, y el orden importa porque una de ellas cambia el estado de producción.

1. **Si falla R7 (la retirada de los `AddHostedService`) en producción:** primero **pausar los cuatro Cloud Scheduler** (operación externa, sin cambio de código ni redespliegue), después `git revert` del PR de R7 y redesplegar. Eso restaura los cuatro trabajos en proceso — el estado actual, defectuoso pero operativo. Pausar antes de revertir evita la ventana en la que Jobs y workers en proceso se ejecutarían a la vez.
2. **R4/R5 (outbox e idempotencia):** `git revert` del PR. Las columnas nuevas quedan en la base de datos sin usar, lo cual es inocuo: el código revertido no las lee. **No se ejecuta el `Down` de la migración** salvo necesidad explícita.
3. **R3 (esquema):** si hay que revertir el esquema, se aplica el `Down` de la migración Npgsql **y** se revierte a mano el cambio de `SqliteSchemaMigrator.cs`. Los dos pasos, o las bases SQLite en disco quedan divergentes.
4. **R6 (host de trabajos):** `git revert` del PR y retirada del paso nuevo de `ci-cd.yml`. El host web no depende del host de trabajos, de modo que la reversión es aislada.
5. **R1/R2 (habilitador y extracción de DI):** `git revert` del PR. Ninguno altera comportamiento de producción; R2 es traslado puro y R1 solo añade pruebas.

**Punto sin retorno:** ninguno. Todos los cambios son reversibles por código más pausa de Scheduler. Los recursos de Google Cloud (Jobs, Scheduler, cuenta de servicio) se pueden pausar o eliminar sin afectar al servicio web, que mantiene su propio despliegue.

---

## 11. Dependencias

- **Incrementos previos (ya archivados):** INC-38 (PostgreSQL/Supabase), INC-39 (Docker/Cloud Run), INC-44 (Recolector Social), INC-45 (Radar de Precios), INC-46 (Autenticación Real).
- **Decisión de producto ya resuelta:** modelo de ejecución = Rama A (maintainer, 2026-09-18).
- **Decisiones de diseño abiertas:** las dos de §8. R3, R4 y R6 no pueden empezar sin ellas.
- **Dependencia externa no controlada por el repositorio:** recursos de Google Cloud (Cloud Run Jobs, cuatro Cloud Scheduler, cuenta de servicio con `roles/run.invoker`). Sin acceso autorizado a GCP en este ciclo, su creación y verificación son responsabilidad manual del maintainer.
- **Dependencia de herramienta en CI:** demonio Docker en el *runner*, ya evidenciado por `ci-cd.yml:47`.

---

## 12. Criterios de éxito

Verificables con `dotnet test Ludeka.sln`:

- [ ] La suite ejecuta pruebas de integración contra PostgreSQL real dentro del *runner* contractual, y **falla de forma ruidosa** si no puede hacerlo (nunca en silencio).
- [ ] **Prueba de concurrencia:** dos consumidores concurrentes no reclaman nunca la misma fila del outbox (`FOR UPDATE SKIP LOCKED`), verificado contra PostgreSQL real.
- [ ] `EnqueueAsync` persiste la fila **antes** de cualquier intento de envío, demostrado con una prueba que nunca ejecuta el despachador.
- [ ] Un mensaje en estado `Queued` sobrevive a un reinicio simulado del host y acaba enviándose.
- [ ] Ejecutar dos veces la misma ventana temporal no duplica trabajo ni notificación, y el segundo intento choca contra la restricción `UNIQUE`, no contra un `if` en memoria.
- [ ] El host web no registra **ningún** `IHostedService` de negocio; `AddHostedService` en `src/` devuelve cero resultados para los cuatro trabajos.
- [ ] Ningún trabajo consulta estado en memoria para saber si ya se ejecutó: `_lastExecutionDate` y `_lastFridayBulletinDispatched` ya no existen.
- [ ] Los trabajos devuelven código de salida distinto de cero ante fallo observable.
- [ ] Cada ejecución persiste inicio, fin, elementos procesados, fallos y duración.
- [ ] Migración Npgsql aplicada y `SqliteSchemaMigrator.cs` actualizado: una base SQLite preexistente en disco se actualiza sin pérdida de datos.
- [ ] Suite completa en verde con `dotnet test Ludeka.sln`.

Verificables por inspección documental:

- [ ] `docs/deployment/DEPLOYMENT_GUIDE.md` y `docs/deployment/google-cloud-run.md` documentan los Cloud Run Jobs, los cuatro Cloud Scheduler y la cuenta de servicio con `roles/run.invoker`.
- [ ] `ci-cd.yml` publica la revisión del Job además de la del servicio web.

Verificación manual del maintainer (fuera del alcance automatizado, sin acceso a GCP):

- [ ] Cloud Scheduler dispara, el Job arranca e IAM autoriza la invocación.
- [ ] Prueba de reinicio del contenedor en el entorno real sin pérdida de notificaciones pendientes.

---

## 13. Huecos de evidencia declarados

- No se ejecutó `dotnet test Ludeka.sln` en esta fase: no se confirma el recuento exacto de pruebas en verde sobre `56c02b9`.
- El comportamiento real de *CPU throttling* de Cloud Run con `--min-instances=0` es un supuesto de plataforma, no un hecho verificado: no hay acceso autorizado a GCP.
- El estado real del despliegue en Google Cloud no es observable desde el repositorio. Todas las afirmaciones de despliegue de esta propuesta se ciñen a lo que declaran `ci-cd.yml` y `docs/deployment/`.
- Las estimaciones de líneas de §9 son estimaciones de propuesta, no mediciones de diff. `sdd-tasks` debe medirlas.
