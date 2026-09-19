# 34. Trabajos en Segundo Plano Correctos en Google Cloud Run (Jobs, Scheduler y Outbox Persistente)

> **Estado:** 25/26 PR mergeados en `main` (`e8c0554`); PR #60 (R7) abierto y retenido a propósito — ver §9
> **Incremento:** [INC-47](file:///c:/repos/Ludeka/docs/increments/archive/inc-47-workers-cloud-run.md)
> **Módulos relacionados:** [08. Notificaciones y Webhooks de Comunidad](file:///c:/repos/Ludeka/docs/specs/sistema/08-notificaciones-y-webhooks.md) · [09. Arquitectura, Persistencia y Despliegue](file:///c:/repos/Ludeka/docs/specs/sistema/09-arquitectura-y-despliegue.md)

---

## 1. El problema

Los cuatro trabajos de fondo (lote nocturno de catalogación, radar de precios, recolector de canales sociales y despachador de notificaciones) se registraban como `AddHostedService` dentro del proceso web (`src/Ludeka.Web/Program.cs:91,93,95,111`), con deduplicación puramente en memoria (`_lastExecutionDate`, `_lastFridayBulletinDispatched`) y una cola de notificaciones también en memoria (`InMemoryCommunityNotificationQueue`, un `Channel<T>` acotado a 1000 elementos).

Eso era incorrecto contra el perfil de despliegue real: Cloud Run con `--min-instances=0 --max-instances=5` (`.github/workflows/ci-cd.yml:102`). Con varias instancias activas, cada una ejecutaba su propia copia de los cuatro trabajos sin ningún bloqueo distribuido — el mismo lote nocturno, el mismo barrido del radar y el mismo escaneo social podían dispararse varias veces en la misma ventana. Y al escalar a cero, cualquier notificación pendiente en el `Channel<T>` de esa instancia se perdía sin drenado ordenado.

## 2. El modelo nuevo: Cloud Run Jobs y host de vida corta

Los cuatro trabajos se externalizan a *Cloud Run Jobs* disparados por *Cloud Scheduler*, servidos por un proyecto de consola nuevo, `src/Ludeka.Jobs`, que reutiliza la misma composición de dominio que `Ludeka.Web` (`AddLudekaApplicationCore`) sin ningún registro específicamente web.

`src/Ludeka.Jobs/Program.cs` es un host genérico (`Host.CreateApplicationBuilder`) que **nunca llama a `host.RunAsync()`**: selecciona el trabajo, construye el contenedor, evalúa las guardas de arranque, ejecuta exactamente una unidad de trabajo y termina con un código de salida.

### 2.1. Selección del trabajo

`JobSelectionResolver.Resolve` decide el nombre del trabajo **antes** de construir el contenedor, con precedencia determinista entre tres fuentes: argumento posicional, bandera `--job=<nombre>`, y `Workers:JobName` de configuración (leído con el indexador `configuration["Workers:JobName"]`; no existe sección `Workers` en ningún `appsettings.json` del repositorio). La comparación es sensible a mayúsculas y exacta contra `JobNames.All` (`src/Ludeka.Jobs/JobNames.cs`): `nightly-cataloging`, `price-radar`, `social-collector`, `notification-outbox`. Un nombre ausente, vacío o desconocido devuelve el código de salida 2 sin levantar la composición de dominio.

### 2.2. Los cuatro *runners*

Cada trabajo tiene un `IJobRunner` fino (`src/Ludeka.Jobs/Runners/`) que calcula su propia clave de ventana e invoca al coordinador de idempotencia (§3):

| *Runner* | `Name` | Ventana | Invoca |
|---|---|---|---|
| `NightlyCatalogingJobRunner` | `nightly-cataloging` | Diaria (`JobWindowKeyCalculator.DailyUtc`) | `INightlyCatalogingService.RunScheduledCatalogingAsync` |
| `PriceRadarJobRunner` | `price-radar` | Bloque de `PriceRadarOptions.CheckIntervalHours` horas (`HourlyBlock`) | `IPriceRadarService.ScanWantToBuyPricesAsync` |
| `SocialCollectorJobRunner` | `social-collector` | Bloque de `SocialCollectorOptions.IntervalMinutes` minutos, mínimo 5 (`MinuteBlock`) | `ISocialCollectorService.RunScheduledCollectionAsync` |
| `NotificationOutboxJobRunner` | `notification-outbox` | Por segundo (`PerSecond`) | `INotificationOutboxDispatcher.DispatchPendingAsync` |

Los cuatro se registran `AddScoped<IJobRunner, ...>` (`JobRunnerServiceCollectionExtensions.cs`). `JobHostRunner.RunSelectedJobAsync` resuelve el pedido con `GetServices<IJobRunner>().SingleOrDefault(r => r.Name == jobName)`, lo ejecuta una sola vez y traduce el desenlace a código de salida.

### 2.3. Contrato de código de salida

| Código | Significado |
|---|---|
| 0 | Unidad de trabajo completada (`JobLeaseOutcome.Completed`), ventana ya completada, o ventana en manos de otra ejecución viva |
| 1 | Excepción no controlada durante la unidad de trabajo, o agotamiento de `Workers:JobTimeoutMinutes` / SIGTERM (ambos llegan a `JobHostRunner` como `OperationCanceledException`) |
| 2 | Nombre de trabajo ausente, desconocido o ambiguo |
| 3 | Guarda de arranque en rojo (§2.4), o fallo de composición/conectividad antes de la unidad de trabajo |

Hay una divergencia real entre lo diseñado y lo enviado. `design.md` §8.5 documenta el código 1 también para "`FailedCount > 0` según el contrato del runner" — es decir, fallos parciales dentro de un lote sin ninguna excepción. Pero `JobExecutionCoordinator.ExecuteWithWindowLeaseAsync` trata un fallo parcial sin excepción como `JobLeaseOutcome.Completed` (la ventana no se retoma), y `JobHostRunner` solo traduce `Failed` a 1 (`return outcome == JobLeaseOutcome.Failed ? 1 : 0;`). Es exactamente el pseudocódigo de `design.md` §8.3, no la tabla de §8.5.

### 2.4. Guardas de arranque

`StartupGuards.EvaluateAsync` corre después de construir el contenedor y antes de cualquier unidad de trabajo:

1. **Coherencia de proveedor:** si `Workers:RequirePostgreSqlInProduction` (por defecto `true`) y `ASPNETCORE_ENVIRONMENT=Production`, la cadena de conexión resuelta tiene que ser PostgreSQL; si resuelve a SQLite, falla apuntando al secreto `SUPABASE_DB_CONNECTION` ausente.
2. **Esquema al día:** en PostgreSQL, si `Database.GetPendingMigrationsAsync` devuelve alguna migración pendiente, falla. El trabajo **nunca migra el esquema**.

Cualquiera de las dos en rojo, o una excepción de composición/conectividad no anticipada, sale con código 3.

### 2.5. Identidad de sistema sin privilegios

`Ludeka.Jobs` registra `ICurrentUserService` con `SystemCurrentUserService` e `ISessionPermissionGuard` con `DenyAllSessionPermissionGuard`. `SystemCurrentUserService` implementa la semántica "sin sesión" del contrato: `UserId`/`UserName` vacíos, `Roles` vacío, `IsFoundingTeam` y cualquier `HasPermission`/`IsInRole` en `false`. Es necesario porque, según el propio comentario del código, 11 servicios de `Ludeka.Application` exigen `ICurrentUserService` como dependencia obligatoria de constructor, y `ValidateOnBuild` (activado también aquí) valida el grafo completo al construir el contenedor, no solo lo que cada trabajo usa.

## 3. Idempotencia por ventana temporal

### 3.1. `JobExecutionLease` y el índice único

`JobExecutionLease` (`src/Ludeka.Core/Entities/JobExecutionLease.cs`) es una concesión de ejecución por ventana, genérica para los cuatro trabajos. Tabla `JobExecutionLeases` (migración `20260919012754_AddJobExecutionLeases`): `Id` (`uuid`, PK), `JobName` (`varchar(64)`), `WindowKey` (`varchar(32)`), `Status` (`varchar(20)`: `Running`/`Completed`/`Failed`), `StartedAt`/`HeartbeatAt`/`CompletedAt` (`timestamptz`), `ProcessedCount`/`FailedCount` (`integer`, `NOT NULL` sin `DEFAULT` de base de datos), `DurationMs` (`bigint`, nulo mientras corre), `HostIdentifier` (`varchar(128)`, `MachineName:ProcessId`) y `ErrorMessage` (`text`).

**El índice único real es `IX_JobExecutionLeases_JobName_WindowKey` sobre `(JobName, WindowKey)`, en la tabla `JobExecutionLeases`** — no en `NightlyCatalogingExecutionLogs`, que este incremento no tocó (sigue con un único índice no-único sobre `StartedAt`). Es esta restricción la que hace que un reintento de Cloud Scheduler choque contra la base de datos y no contra una comprobación en memoria. Índice secundario `(JobName, StartedAt)` para lectura operativa.

### 3.2. Claves de ventana ancladas al epoch Unix

`JobWindowKeyCalculator` (`src/Ludeka.Application/Features/Jobs/JobWindowKeyCalculator.cs`) centraliza cinco granularidades:

| Método | Trabajo | Forma de la clave |
|---|---|---|
| `DailyUtc` | `nightly-cataloging` | `yyyy-MM-dd` (fecha de calendario UTC) |
| `HourlyBlock(nowUtc, n)` | `price-radar` | `yyyy-MM-ddTHH` del inicio del bloque de `n` horas |
| `MinuteBlock(nowUtc, n)` | `social-collector` | `yyyy-MM-ddTHH:mm` del inicio del bloque de `n` minutos |
| `PerSecond` | `notification-outbox` | `yyyy-MM-ddTHH:mm:ss` |
| `IsoWeek` | `community-weekly-bulletin` (§8) | `yyyy-Www`, con `ISOWeek`, no el calendario gregoriano |

`HourlyBlock` y `MinuteBlock` anclan al epoch Unix (`inicioBloque = epoch + floor((ahora-epoch)/N) × N`) para que el límite de bloque sea estable entre instancias y entre reinicios. `DailyUtc` no necesita ese anclaje: la fecha de calendario UTC ya es estable por sí misma. `PerSecond` no es una ventana de negocio: es la granularidad mínima para que el `UNIQUE` solo choque ante un disparo genuinamente duplicado en el mismo segundo — el despachador de outbox es un drenaje, no un trabajo con ventana propia.

### 3.3. El coordinador

`JobExecutionCoordinator.ExecuteWithWindowLeaseAsync` (`src/Ludeka.Application/Features/Jobs/JobExecutionCoordinator.cs`) es el único punto que ejecuta una unidad de trabajo bajo concesión: adquiere la ventana con `IJobExecutionLeaseRepository.TryAcquireAsync` y, solo si la adquiere, invoca el delegado de trabajo, con latido (`IJobHeartbeat.BeatAsync`) en las fronteras de fase que la propia unidad de trabajo decida. Desenlaces (`JobLeaseOutcome`): `Completed`, `Failed` (excepción no controlada; concesión retomable), `SkippedAlreadyCompleted`, `SkippedHeldByOther` — en los dos últimos el delegado no se invoca nunca. Cubierto además por una prueba de integración dedicada a concurrencia (`tests/Ludeka.IntegrationTests/JobExecutionCoordinatorConcurrencyTests.cs`).

### 3.4. Toma de control de concesiones huérfanas

`JobExecutionLeaseRepository.TryAcquireAsync` intenta primero un `INSERT`. Si choca contra el índice único, `TakeOverOrClassifyAsync` ejecuta un único `UPDATE ... RETURNING` condicional que cubre los dos casos retomables con el mismo predicado:

```sql
WHERE "JobName" = @job AND "WindowKey" = @ventana
  AND (("Status" = 'Running' AND "HeartbeatAt" < @umbral) OR "Status" = 'Failed')
```

El umbral de latido caducado es una constante de 60 minutos (`StaleLeaseMinutes`), no configuración: la sección `Workers` de `appsettings.json` no existe todavía, así que solo `Workers:JobName`, `Workers:JobTimeoutMinutes` y `Workers:RequirePostgreSqlInProduction` se leen hoy de configuración. Si ningún predicado del `UPDATE` aplica, una segunda consulta clasifica la fila existente entre `AlreadyCompleted` y `HeldByOther`.

### 3.5. Clasificación de violación de unicidad por proveedor

No hay una excepción propia de dominio para esto: `IsUniqueViolation` distingue por tipo de excepción del proveedor — `Npgsql.PostgresException.SqlState == "23505"` en PostgreSQL, `Microsoft.Data.Sqlite.SqliteException.SqliteErrorCode == 19` en SQLite — y `TryAcquireAsync` nunca deja escapar la excepción de proveedor hacia `Ludeka.Application`.

## 4. Outbox persistente de notificaciones

### 4.1. Mensaje lógico y sub-entrega por canal

`NotificationOutboxMessage` (tabla `NotificationOutboxMessages`, migración `20260919010426_AddNotificationOutboxMessages`) es una fila por mensaje lógico: `EventType`, `Title`, `Summary`, `TargetUrl`/`ImageUrl` opcionales, `FieldsJson` (persiste los campos del *embed* de Discord y las líneas de Telegram, que antes no sobrevivían un reinicio), `TargetChannel` opcional (canal exclusivo, o difusión si es nulo), `Status` (`Pending`/`Completed`/`Dead`), `Attempts`, `NextAttemptAt`, y `ClaimedAt`/`ClaimedBy` como observabilidad fuera del predicado de reclamación.

Las sub-entregas por canal viven en `CommunityNotificationLog` (tabla `NotificationLogs`, existente desde el Incremento 9), extendida en este incremento con `MessageId`, `Attempts` y `NextAttemptAt` (migración `20260919000613_AddNotificationLogDeliveryColumns`) y el índice único `IX_NotificationLogs_MessageId_Channel` sobre `(MessageId, Channel)`, que hace idempotente `EnsureDeliveryAsync`. `NightlyCatalogingExecutionLog` no participa en absoluto de este esquema.

### 4.2. Reclamación exclusiva

`NotificationOutboxRepository.ClaimPendingAsync` usa SQL crudo parametrizado, con rama por proveedor: en **PostgreSQL**, `SELECT ... FOR UPDATE SKIP LOCKED` sobre las filas `Pending` con `NextAttemptAt <= ahora`, dentro de un único `UPDATE ... RETURNING` (sin transacción explícita: el bloqueo solo dura la sentencia); en **SQLite** (rama degradada), el mismo `UPDATE ... WHERE Id IN (SELECT ...)` sin `FOR UPDATE SKIP LOCKED` — SQLite ya serializa escritores — con el `NextAttemptAt` de la concesión precalculado en C# en vez de aritmética de intervalo en SQL. Ambas ramas desplazan `NextAttemptAt` al futuro al reclamar, dejando el lote invisible para cualquier otro despachador durante la concesión.

### 4.3. Algoritmo de despacho

`NotificationOutboxDispatcher.DispatchPendingAsync` es un ciclo acotado, sin bucle propio: si `OutboxOptions.Enabled` es `false` no reclama nada; si no, reclama hasta `BatchSize` mensajes con `ClaimedBy = MachineName:ProcessId` y concesión `LeaseSeconds`. Para cada mensaje, `ResolveChannels` deriva el conjunto de canales **en ese instante**, releyendo `CommunityNotificationOptions` (si trae `TargetChannel`, solo ese canal si sigue habilitado; si no, difusión a Discord/Telegram según estén habilitados). Sin canales resueltos, el mensaje se libera o se agota como fallo de "ningún canal habilitado". Con canales, `EnsureDeliveryAsync` asegura la sub-entrega por canal (idempotente vía el índice único de §4.1) y `ICommunityNotificationService.DeliverAsync` intenta el envío; si todas las sub-entregas quedan en estado terminal se llama a `CompleteMessageAsync`, si no a `ReleaseMessageAsync` o `MarkMessageDeadAsync` según los intentos acumulados.

### 4.4. Retroceso exponencial

`OutboxOptions.ComputeNextAttempt(attempts)`: `ahora + RetryBackoffSeconds × RetryBackoffMultiplier^(attempts-1)`, con techo en `LeaseSeconds × 12`. Se usa tanto para reintentar la reclamación del mensaje como la sub-entrega por canal.

### 4.5. Por qué el canal se resuelve al despachar y no al encolar

Si el *fan-out* se fijara al encolar, un mensaje encolado con Telegram deshabilitado nunca llegaría a Telegram aunque se habilitase minutos después con el mensaje todavía pendiente. `ResolveChannels` relee `CommunityNotificationOptions` en cada ciclo de despacho sobre `OutboxClaim.TargetChannel`, con la misma rama que ya usa `CommunityNotificationService.BroadcastAsync`.

## 5. *Health check* del outbox

`NotificationQueueHealthCheck` (`src/Ludeka.Web/Health/NotificationQueueHealthCheck.cs`) sustituye la comprobación anterior — que solo confirmaba que `ICommunityNotificationQueue` se resolvía por inyección de dependencias — por datos reales de `INotificationOutboxRepository.GetHealthSnapshotAsync`: `pending_count`, `oldest_pending_age_seconds`, `dead_count`, `provider`. Degrada, nunca falla, cuando `PendingCount > HealthPendingDepthDegraded` (100 por defecto), la antigüedad del pendiente más viejo supera `HealthOldestPendingDegradedMinutes` (30 min) en segundos, o `DeadCount > 0`: un atasco del outbox no debe sacar de rotación el servicio web en Cloud Run por un problema que, con los trabajos ya externalizados, lo drena un Job aparte. `OutboxOptions.HealthQueryTimeoutSeconds` está declarado pero sin consumidor (§8).

## 6. Persistencia: migraciones y reconciliador SQLite

Tres migraciones de PostgreSQL, en orden: `20260919000613_AddNotificationLogDeliveryColumns` (añade `Attempts`/`MessageId`/`NextAttemptAt` a `NotificationLogs` y el índice único `(MessageId, Channel)`); `20260919010426_AddNotificationOutboxMessages` (crea `NotificationOutboxMessages` con los índices `(Status, CreatedAt)` y `(Status, NextAttemptAt, CreatedAt)`); `20260919012754_AddJobExecutionLeases` (crea `JobExecutionLeases` con el índice único `(JobName, WindowKey)` y el secundario `(JobName, StartedAt)`).

`SqliteSchemaMigrator.EnsureSchemaUpToDateAsync` reconstruye a mano los tres cambios contra SQLite (pasos 24 a 26 del método: `ALTER TABLE`/`CREATE TABLE IF NOT EXISTS`/`CREATE INDEX IF NOT EXISTS`), el mismo mecanismo defensivo que ya reconcilia el resto del esquema para bases SQLite existentes.

## 7. Entrega: una imagen, dos modos de arranque

El `Dockerfile` publica ambos proyectos en el mismo `/app/publish`: primero `Ludeka.Web.csproj`, después `Ludeka.Jobs.csproj`. Los ensamblados compartidos (`Ludeka.Core`/`Application`/`Infrastructure.dll`) y el `appsettings.json` enlazado quedan idénticos entre las dos publicaciones. La imagen final (`aspnet:10.0`) arranca por defecto con `ENTRYPOINT ["dotnet", "Ludeka.Web.dll"]`.

`ci-cd.yml` despliega, sobre la misma imagen ya construida y empujada, el servicio web (`deploy-cloudrun@v2`) y, en un paso aparte, cuatro Cloud Run Jobs — uno por nombre de trabajo —, cada uno sobrescribiendo el comando del contenedor:

```
gcloud run jobs deploy "ludeka-job-${JOB}" --image "${IMAGE_NAME}" --command dotnet --args "Ludeka.Jobs.dll,${JOB}"
```

para `JOB` en `nightly-cataloging price-radar social-collector notification-outbox`, con las mismas variables de entorno y secretos que el servicio web. El propio `ci-cd.yml` declara, en comentario junto al paso, un hueco de evidencia: la sintaxis exacta de las banderas de `gcloud run jobs deploy` y la disponibilidad de un modo "job" en `deploy-cloudrun@v2` no se han verificado contra GCP real en este ciclo.

## 8. Deuda y huecos abiertos

1. **El boletín semanal de notificaciones y el escaneo de sorteos próximos a expirar pierden su disparador automático en cuanto mergee el PR #60.** Ambos siguen existiendo y funcionando; sus únicos invocadores automáticos hoy son `CommunityNotificationDispatcherHostedService.cs:118` (`RunExpiringGiveawaysScanAsync`, escaneo cada 60 min, sin concesión de ventana) y `:159` (`RunFridayReleasesBulletinAsync`, cada viernes, bajo `IJobExecutionCoordinator` con `WindowKey` semanal ISO y el nombre de trabajo `community-weekly-bulletin`). Ese servicio deja de registrarse cuando se retiran los cuatro `AddHostedService` del host web. Ambos siguen disponibles a mano desde `/admin/notificaciones` (`TriggerFridayReleasesBulletinAsync`/`TriggerExpiringGiveawaysScanAsync`, ambos tras `RequirePermissionAsync(ModeratorPermission.CanManageNotifications)`, y ejecutados directamente, sin pasar por el coordinador de ventana). **Decisión explícita del maintainer del 2026-09-19: se acepta.** La concesión `community-weekly-bulletin` queda sin consumidor automático.
2. **Con `Outbox:UseInMemoryQueueForLocalDev = true`, la cola en memoria queda sin nadie que la drene.** La opción sí se lee —`LudekaServiceCollectionExtensions.cs:182` elige entre `InMemoryCommunityNotificationQueue` y `OutboxCommunityNotificationQueue`—, pero el único consumidor de `ReadAllAsync` era el `ProcessQueueAsync` del despachador, reescrito para leer del outbox persistente. Hoy `ReadAllAsync` no tiene ningún llamador en `src/`: solo quedan su declaración y dos comentarios. En ese modo, los mensajes entran en un `Channel<T>` que nadie lee y se pierden en silencio. **Producción no está afectada:** la opción es opt-in y su valor por defecto es `false`, así que el camino real es siempre el outbox persistente. El impacto se limita al modo de desarrollo local.
3. `OutboxOptions.HealthQueryTimeoutSeconds` está declarado (valor por defecto 2) pero `NotificationQueueHealthCheck` no lo lee: no existe ninguna condición de *timeout* en el chequeo de salud, pese a que el diseño la contemplaba.
4. Contradicción de diseño ya descrita en §2.3: `design.md` §8.5 documenta el código de salida 1 también para fallos parciales (`FailedCount > 0`) sin excepción; lo que se mergeó sigue el pseudocódigo de §8.3 y solo traduce a código distinto de cero una excepción no controlada.
5. `Workers:StaleLeaseMinutes` y `Workers:Enabled`, ambos propuestos en la sección `Workers` de `design.md` §7.5, no se leen de configuración hoy: el primero es la constante de 60 minutos de §3.4; el segundo no tiene ninguna referencia en el repositorio. La sección `Workers` de `appsettings.json` no existe todavía. **El PR #60 cierra la mitad de este hueco**: introduce `WorkersOptions` y pasa `StaleLeaseMinutes` a configuración; `Enabled` por trabajo sigue sin cablear.
6. `SqliteException.SqliteErrorCode` es 19 tanto para `UNIQUE` como para `NOT NULL`, a diferencia de PostgreSQL (`23505` frente a `23502`) — aviso para código futuro que dependa de esta clasificación en SQLite.

## 9. Estado de entrega

- **25 de 26 PR mergeados** (#35 a #59, verificado contra el historial de `main`); `main` en `e8c0554`.
- **El PR #60 (R7, "retirar los cuatro `AddHostedService` del host web") está abierto y retenido a propósito**: no se mergea hasta que el maintainer confirme que Cloud Scheduler, el Cloud Run Job y la cuenta de servicio con `roles/run.invoker` están provisionados y disparando de verdad en GCP. Plan de reversión: pausar los cuatro Cloud Scheduler primero, después `git revert`.
- Las siete rebanadas planificadas (R1–R7) se entregaron en 26 PR porque cinco tuvieron que partirse frente al techo de 400 líneas. Una sola `size:exception` en todo el incremento (R2a, 599 líneas).
