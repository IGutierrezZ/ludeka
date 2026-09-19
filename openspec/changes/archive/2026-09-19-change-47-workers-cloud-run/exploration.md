# Exploración — INC-47: Trabajos en Segundo Plano Correctos en Google Cloud Run

> **Cambio:** `change-47-workers-cloud-run` · **Fase:** `sdd-explore` · **Fecha:** 2026-09-18
> **Worktree:** `C:\repos\ludeka-wt\workers-cloud-run` (rama `inc/workers-cloud-run`, commit `56c02b9`, idéntico a `main`)
> **Documento de partida:** [`docs/increments/inc-47-workers-cloud-run.md`](../../../docs/increments/inc-47-workers-cloud-run.md) (evidencia original recogida contra `a54cb31`)
> **Almacén de artefactos:** `hybrid` — este fichero más la observación Engram `sdd/change-47-workers-cloud-run/explore` (id 407)
> **Estado:** exploración completa. La bifurcación del §2.1 del documento de partida se devuelve **sin decidir**: es decisión del maintainer.

---

## 0. Re-verificación de la evidencia del documento de partida contra `56c02b9`

El documento cita evidencia contra `a54cb31`. Desde entonces han entrado INC-46, INC-49, INC-50 e INC-51. Resultado de la reverificación exhaustiva, fichero por fichero:

**Inventario de los 4 workers — sigue completo, sin quinto worker.** Confirmado con dos comprobaciones independientes: `AddHostedService` en `src/` devuelve exactamente 4 resultados (`Program.cs:166,215,244,319`) y `: BackgroundService` devuelve exactamente las mismas 4 clases. No existe ningún `src/Ludeka.Jobs`.

### Divergencias de línea confirmadas

Causa: inserciones de INC-46/49 en `Program.cs`, `FoundingVerdictService.cs` y `RuleQAService.cs`.

| Cita del documento (`a54cb31`) | Línea real en `56c02b9` | Contenido |
|---|---|---|
| `Program.cs:146` — `AddHostedService<NightlyCatalogingHostedService>` | **166** | Coincide; solo cambia la línea |
| `Program.cs:195` — `AddHostedService<PriceRadarHostedService>` | **215** | Coincide |
| `Program.cs:224` — `AddHostedService<CommunityNotificationDispatcherHostedService>` | **244** | Coincide |
| `Program.cs:289` — `AddHostedService<SocialCollectorHostedService>` | **319** | Coincide |
| `Program.cs:221` — `AddSingleton<ICommunityNotificationQueue, InMemoryCommunityNotificationQueue>` | **241** | Coincide |
| `FoundingVerdictService.cs:211` — `_notificationQueue.EnqueueAsync` | **217** | Coincide |
| `RuleQAService.cs:186` — `_notificationQueue.EnqueueAsync` | **199** | Coincide |

### Cita con la ruta equivocada (no solo la línea)

El documento atribuye la persistencia del log de notificación a `Infrastructure/Notifications/CommunityNotificationService.cs:93,102,108,124`. **Ese fichero no existe en esa ruta.** El fichero real es `src/Ludeka.Application/Features/Community/CommunityNotificationService.cs`, donde `new CommunityNotificationLog(...)` aparece en la línea **108** (rama Discord) y en la línea **157** (rama Telegram), con `AddLogAsync` invocado justo después (líneas 117 y 166). La afirmación estructural («se persiste un registro por envío») se sostiene; la ruta y varias de las líneas citadas, no.

### Citas que se sostienen exactas, sin ningún cambio

Estos ficheros no fueron tocados por INC-46/49/50/51:

- `NightlyCatalogingHostedService.cs`: `:22` (campo `_lastExecutionDate`), `:57` (comparación con `ExecutionHourUtc`), `:67` (escritura del campo), `:79-80` (retardo de 15 min).
- `PriceRadarHostedService.cs`: `:69-72` (`CheckIntervalHours`, retardo). Valor por defecto de 6 h confirmado en `PriceRadarOptions.cs:18`.
- `SocialCollectorHostedService.cs`: `:82-83` (`IntervalMinutes`, retardo). Valor por defecto de 120 confirmado en `appsettings.json:120`.
- `CommunityNotificationDispatcherHostedService.cs`: `:16` (campo `_lastFridayBulletinDispatched`), `:43` (`ReadAllAsync` del consumidor), `:59-62` (captura de `OperationCanceledException`), `:92` (escritura del campo), `:103` (retardo de 60 min).
- `.github/workflows/ci-cd.yml:102` — `flags: '--allow-unauthenticated --port=8080 --memory=512Mi --cpu=1 --min-instances=0 --max-instances=5'`. Precisa a la línea.

### Imprecisiones menores sin impacto

`InMemoryCommunityNotificationQueue.cs:10-34` (la clase cierra realmente en la 35) y `NightlyCatalogingExecutionLog.cs:11-62` (cierra en la 63). Desviaciones de una línea, probablemente ya presentes en la exploración original.

### Evidencia negativa confirmada

Búsqueda sin resultados en todo `src/`: `pg_advisory`, `advisory_lock`, `IDistributedCache`, `leader election`, `SKIP LOCKED`, `FOR UPDATE`, `IHostApplicationLifetime`. **Las tres fallas del §1 del documento de partida y la ausencia total de mecanismos de coordinación siguen siendo ciertas hoy.**

---

## 1. La bifurcación del §2.1 — Evidencia por rama

**Rama A:** Cloud Run Jobs + Cloud Scheduler (externalizar los trabajos).
**Rama B:** workers en proceso con `pg_advisory_lock` (conservarlos en el host web).

### 1.1. Superficie de cambio real en el código

**Rama A:**

- Retira los 4 `AddHostedService<...>()` de `Program.cs` (líneas 166, 215, 244, 319).
- **Hallazgo no anticipado por el documento:** `Program.cs` es hoy un único fichero de *top-level statements* que mezcla unas 230 líneas de registro de infraestructura de dominio con registro específico web (`AddRazorComponents`, autenticación por cookie, *output caching*, *antiforgery*, *health checks*, endpoints Minimal API). **No existe ninguna extensión reutilizable** del tipo `AddLudekaDomainServices(...)` que un host de consola pueda invocar sin duplicar esas líneas. Extraerla es trabajo previo real, no incluido en el coste de «un argumento de entrada» que sugiere el documento.
- Punto a favor: los 4 `BackgroundService` ya resuelven su servicio de dominio (`INightlyCatalogingService`, `IPriceRadarService`, `ISocialCollectorService`, `ICommunityNotificationService`) vía `IServiceScopeFactory.CreateScope().GetRequiredService<T>()`, el mismo patrón que reutilizaría un host de consola sin tocar la capa de aplicación.
- El `Dockerfile` tiene `ENTRYPOINT ["dotnet", "Ludeka.Web.dll"]` fijo, sin lógica de argumentos: hay que añadirla en cualquiera de las dos sub-opciones (modo del host frente a proyecto `Ludeka.Jobs` separado).
- `src/Ludeka.Jobs` no existe: sería enteramente nuevo si se elige esa sub-opción.

**Rama B:**

- No retira ningún `AddHostedService`; toca los 4 ficheros de `BackgroundService` para adquirir y liberar un bloqueo alrededor de cada ciclo.
- Requiere una abstracción nueva (`IDistributedLockService` o equivalente) que **no existe hoy en absoluto**. Esa abstracción no puede implementarse contra SQLite: `pg_advisory_lock` es una función nativa de PostgreSQL sin equivalente en SQLite, de modo que necesitaría doble implementación (real y no-op), similar al patrón dual de proveedor de base de datos ya existente.
- Menor recuento de ficheros nuevos que la Rama A, pero no elimina el problema de fondo (estado en memoria) sin combinarse con §2.2 y §2.3.

### 1.2. Superficie de cambio en despliegue e IAM

Evidencia del pipeline real (`ci-cd.yml`, único job `deploy-cloudrun`, líneas 52-118): un solo artefacto Docker, un solo `docker build`/`docker push`, un solo `deploy-cloudrun@v2` con las banderas de la línea 102. `docs/deployment/DEPLOYMENT_GUIDE.md` y `docs/deployment/google-cloud-run.md` **no mencionan** Cloud Scheduler, Cloud Run Jobs ni roles IAM de invocación.

- **Rama A:** requiere como mínimo un recurso Cloud Run Job (uno parametrizado o cuatro), cuatro recursos Cloud Scheduler (uno por cadencia), una cuenta de servicio con `roles/run.invoker` para que Scheduler dispare el Job, y un paso nuevo en `ci-cd.yml` para publicar la revisión del Job. Infraestructura enteramente nueva.
- **Rama B:** cero recursos nuevos de Google Cloud; `ci-cd.yml:102` no cambia. Coste de IAM y despliegue nulo.

### 1.3. Resolución de las tres fallas del §1

| Falla | Rama A | Rama B |
|---|---|---|
| 1. Ejecución duplicada al escalar | Resuelta por construcción: una ejecución por disparo de Scheduler | Resuelta **solo** si el bloqueo se implementa y prueba correctamente; el propio documento marca «reclamación concurrente mal implementada» como riesgo Alto |
| 2. Pérdida de estado de planificación | No la resuelve sola; depende de §2.3 | No la resuelve sola; depende de §2.3 |
| 3. Pérdida de notificaciones en cola en memoria | **No la resuelve por sí sola.** `InMemoryCommunityNotificationQueue` (`Channel<T>` acotado a 1000, `AddSingleton`) no cambia por externalizar la ejecución de los otros 3 workers | **No la resuelve por sí sola**, por el mismo motivo exacto |

**Conclusión explícita:** ninguna rama resuelve la falla 3 sin el outbox del §2.2. El outbox es trabajo obligatorio común, no una consecuencia de elegir la Rama A.

### 1.4. Coste operativo

Evidencia del repositorio: `--min-instances=0 --max-instances=5`, sin ninguna bandera explícita de *CPU throttling* ni en el pipeline ni en la documentación, de modo que se aplica el comportamiento predeterminado de la plataforma.

Conocimiento general de la plataforma (**fuera del repositorio, no verificable en esta fase, que no tiene acceso autorizado a GCP**): por defecto Cloud Run solo garantiza CPU mientras se procesa una petición HTTP entrante, así que un proceso en segundo plano sin peticiones activas puede quedar congelado entre peticiones. Si es así, la Rama B desplegada tal cual no solo competiría por CPU cuando hay tráfico: probablemente no dispararía sus temporizadores de forma fiable cuando **no** hay tráfico, justo el escenario que el nivel gratuito de Cloud Run está pensado para servir. **Es una inferencia de plataforma, no un hecho confirmado.**

La Rama A, en cambio, obtiene CPU dedicada durante la vida de cada ejecución del Job, sin depender de tráfico HTTP concurrente.

### 1.5. Testabilidad bajo Strict TDD — hallazgo más importante de la exploración

Evidencia exhaustiva y concluyente:

- `tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj` no referencia `Npgsql`, `Testcontainers` ni `Respawn`: solo `xunit`, `Microsoft.NET.Test.Sdk`, `coverlet.collector` y las 4 referencias de proyecto de producción.
- `UseSqlite` aparece en unos 40 ficheros de `tests/`. `Npgsql` aparece en exactamente **1**: `Infrastructure/DatabaseProviderTests.cs`, que solo prueba el método textual `DatabaseOptions.IsPostgreSql(connectionString)` con cadenas de ejemplo. Nunca abre una conexión real ni ejecuta `UseNpgsql(...)`.
- `LudekaDbContextFactory.cs` (fábrica de diseño de EF Core) fuerza `UseNpgsql(...)` con una cadena ficticia solo para `dotnet ef migrations add`; nunca corre en tiempo de prueba.
- **Hoy no existe ninguna prueba de integración contra PostgreSQL real, y la suite no puede ejercitar ni `pg_advisory_lock` ni `SELECT ... FOR UPDATE SKIP LOCKED`:** SQLite no implementa ninguna de las dos primitivas y EF Core no las traduce desde LINQ (ambas exigen SQL crudo específico de Npgsql).

**Esto no diferencia la Rama A de la Rama B: es un hueco común.** El outbox del §2.2 (`FOR UPDATE SKIP LOCKED`) tiene exactamente el mismo problema de testabilidad que el `pg_advisory_lock` de la Rama B. Con `strict_tdd: true`, cualquiera de las dos rutas exige resolver primero esta brecha de infraestructura de pruebas (por ejemplo, Testcontainers con PostgreSQL real) antes de poder escribir el test de concurrencia que el propio documento exige en su §2.6 («dos consumidores concurrentes no reclaman la misma fila»). Sin esa infraestructura, ese test no se puede escribir hoy contra código real.

### 1.6. Trabajo común a ambas ramas

El outbox persistente (§2.2), la idempotencia por ventana (§2.3), la observabilidad y el código de salida (§2.4) y la mayor parte de la configuración (§2.5, salvo `Workers__RunInProcess` y `Workers__JobName`, que son específicos de la Rama A) son idénticos en ambas ramas.

**Implicación de secuenciación:** el outbox y la idempotencia pueden proponerse, especificarse y empezar a implementarse —en la medida en que lo permita el hueco de pruebas contra PostgreSQL real— antes de que el maintainer resuelva la bifurcación del §2.1.

---

## 2. Resto del alcance explorado

### 2.1. Outbox (§2.2 del documento de partida)

- `CommunityNotificationLog` (`src/Ludeka.Core/Entities/CommunityNotificationLog.cs:6-75`): columnas `Id`, `EventType`, `Channel`, `Title`, `Summary`, `TargetUrl`, `ImageUrl`, `Status`, `ErrorDetails`, `CreatedAt`, `SentAt`. **Faltan `Attempts`/`RetryCount` y `NextAttemptAt`**, confirmado por lectura completa.
- **Hallazgo estructural no anticipado por el documento:** la fila de `CommunityNotificationLog` se crea **dentro** de `CommunityNotificationService.SendToDiscordAsync` / `SendToTelegramAsync` (líneas 108 y 157), es decir, **después** de que el despachador ya leyó el mensaje del `Channel` en memoria, no en el momento de `EnqueueAsync`. Hoy el «outbox» no protege nada: si la instancia muere antes de drenar el `Channel`, no queda ninguna fila que lo delate. Convertirlo en outbox real exige mover la creación de la fila al punto de `EnqueueAsync`, no solo cambiar el mecanismo de lectura del despachador.
- Si Discord y Telegram están ambos habilitados, un solo mensaje lógico genera **dos filas** (una por canal). El diseño debe decidir si `SKIP LOCKED` reclama por fila-canal (compatible con el esquema actual) o si se introduce una fila por mensaje con sub-entregas. Decisión para `sdd-design`.
- `ICommunityNotificationQueue` tiene solo 2 métodos (`EnqueueAsync`, `ReadAllAsync`). Los productores (`FoundingVerdictService.cs:217`, `RuleQAService.cs:199`) llaman solo a la interfaz, nunca a la implementación concreta: si el outbox respeta la misma interfaz, **esos dos ficheros no necesitan tocarse**. El riesgo real está en `CommunityNotificationDispatcherHostedService.ProcessQueueAsync` (usa `ReadAllAsync` como `IAsyncEnumerable` infinito, incompatible con reclamar lotes con `SKIP LOCKED`) y en `CommunityNotificationService` (crea el log tras la lectura).
- `SqliteCommunityNotificationRepository.GetRecentLogsAsync` materializa todas las filas con `.ToListAsync()` y ordena en memoria: patrón inadecuado para «reclamar lote pendiente»; necesitará SQL específico de Npgsql, no reutilizable tal cual.
- Índices EF Core existentes sobre `NotificationLogs` (`LudekaDbContext.cs:281-285`): `HasIndex(Status)`, `HasIndex(Channel)`, `HasIndex(CreatedAt)`. No únicos, pero alineados con el patrón `WHERE Status = Queued ORDER BY CreatedAt` que necesitaría un despachador de outbox.

### 2.2. Idempotencia (§2.3 del documento de partida)

- `NightlyCatalogingExecutionLog` (`src/Ludeka.Core/Entities/NightlyCatalogingExecutionLog.cs:11-63`): columnas `Id`, `StartedAt`, `CompletedAt`, `QueueProcessedCount`, `NewsDiscoveryCount`, `BggDiscoveryCount`, `TopBackfillCount`, `TotalCatalogedCount`, `FailedCount`, `CatalogedTitlesJson`, `Status`, `ErrorMessage`. `BggDiscoveryCount` no está documentada ni en el documento de partida ni en el módulo 18 de la especificación viva: la entidad siguió evolucionando sin sincronizar esa spec (hueco ajeno a este incremento).
- **Cero restricción única por ventana temporal.** La única configuración EF Core es `nightlyLog.HasIndex(l => l.StartedAt)` (`LudekaDbContext.cs:403`), índice **no** único, solo de rendimiento. No hay columna de «clave de ventana» ni `UNIQUE`. Confirma exactamente lo que el documento pedía investigar.
- Última migración real: `20260917112154_AddProviderEmailVerifiedAtToExternalLogins` (INC-46/49). No toca ninguna tabla relevante para INC-47.

### 2.3. Migraciones EF Core

- `src/Ludeka.Infrastructure/Migrations/`: 3 migraciones reales más el snapshot. `LudekaDbContextFactory.cs` fuerza `UseNpgsql(...)` con cadena ficticia para el diseño, de modo que **las migraciones son exclusivamente para PostgreSQL/Npgsql**; no hay carpeta paralela para SQLite.
- El camino SQLite nunca ejecuta migraciones (`Program.cs:365-376`): usa `EnsureCreatedAsync()` (deriva el esquema del modelo EF Core actual y recoge automáticamente columnas nuevas en bases creadas desde cero, incluidas las `:memory:` de los tests) más `SqliteSchemaMigrator.EnsureSchemaUpToDateAsync(db)` (reconcilia solo bases SQLite ya existentes en disco).
- **Conclusión:** el repositorio no soporta dos historiales de migración paralelos; soporta un único historial (Npgsql) más un reconciliador manual para SQLite. Cualquier cambio de esquema de este incremento exige tres pasos: (1) `dotnet ef migrations add` para Npgsql/producción, (2) actualizar `SqliteSchemaMigrator.cs` a mano para el SQLite de desarrollo ya existente en disco, (3) nada adicional para los tests, que recrean el esquema vía `EnsureCreatedAsync()`.

### 2.4. Configuración (§2.5 del documento de partida)

- Sin colisión: ninguna de las 14 clases de opciones existentes usa `SectionName = "Workers"` ni `"Outbox"`. Los nombres propuestos por el documento son seguros.
- `appsettings.json` no tiene hoy secciones `Workers` ni `Outbox`; se añadirían siguiendo el patrón ya establecido (`Enabled` como primera propiedad).
- Patrón de enlace: los 3 `BackgroundService` con bucle (`Nightly`, `PriceRadar`, `SocialCollector`) usan `IOptionsMonitor<T>` (refresco sin reinicio). `CommunityNotificationDispatcherHostedService` es la excepción: no inyecta ningún `IOptionsMonitor` propio, y el chequeo de `Enabled` para notificaciones vive dentro de `CommunityNotificationService.BroadcastAsync`, no en el bucle del despachador.

---

## 3. Riesgos

| # | Severidad | Riesgo |
|---|---|---|
| 1 | **Alto** | Sin infraestructura de pruebas de integración contra PostgreSQL real (por ejemplo Testcontainers), ninguna de las dos ramas puede cumplir Strict TDD para el outbox (`SKIP LOCKED`) ni para `pg_advisory_lock` si se elige la Rama B. Bloquea el criterio de aceptación §4.6 tal como está redactado. |
| 2 | Medio | `Program.cs` mezcla registro de dominio y registro web sin extensión reutilizable: la Rama A exige extraer esa composición antes de ser «un argumento de entrada». |
| 3 | Medio | El outbox actual conflacta «fila de outbox» con «intento de envío por canal» (una fila por canal, no por mensaje). Hay que decidir el modelo antes de tocar el esquema. |
| 4 | Medio | La ruta citada por el documento de partida para la persistencia del log (`Infrastructure/Notifications/CommunityNotificationService.cs`) es incorrecta; el fichero real vive en `Ludeka.Application/Features/Community/`. Corregir el documento al proponer. |
| 5 | Bajo | El comportamiento real de *CPU throttling* de Cloud Run con `--min-instances=0` no es verificable desde el repositorio ni desde esta fase (sin acceso a GCP): pesa sobre el punto 1.4 pero es un supuesto, no un hecho confirmado. |

---

## 4. Huecos de evidencia explícitos

- No se pudo ejecutar `dotnet test Ludeka.sln` en esta fase (sin herramienta de shell disponible): no se confirma el recuento exacto de pruebas en verde.
- No se pudo verificar el comportamiento real de *CPU throttling* en Google Cloud Run (sin acceso autorizado a GCP).
- No se comparó línea a línea contra el commit histórico `a54cb31` real: la reverificación comparó las citas del documento contra `56c02b9`, asumiendo que las citas originales eran correctas en su momento.

---

## 5. Decisión de producto pendiente

**La bifurcación del §2.1 sigue sin resolver.** Los cuatro datos duros que más deberían pesar en la decisión del maintainer:

1. **Ninguna rama resuelve la pérdida de notificaciones (falla 3) por sí sola.** El outbox del §2.2 es obligatorio en ambos casos, así que ese trabajo puede arrancar sin esperar la decisión.
2. **La brecha de pruebas contra PostgreSQL real es común a ambas ramas**, no un coste exclusivo de la Rama B: hoy ningún camino de la suite (100 % SQLite en `tests/`) puede ejercitar `SKIP LOCKED` (necesario para el outbox) ni `pg_advisory_lock`. Elegir la Rama B para «ahorrar infraestructura» no ahorra esta inversión en pruebas.
3. **La Rama A tiene coste de infraestructura enteramente nuevo** (Jobs, 4 Schedulers, IAM y un paso de pipeline nuevo), mientras que la Rama B tiene coste de infraestructura cero pero introduce una abstracción de bloqueo nueva sin equivalente en SQLite.
4. **`Program.cs` no está hoy preparado para un «modo job» barato:** la composición de DI de dominio está entrelazada con la composición web, así que la Rama A es más cara de lo que sugiere «un argumento de entrada» hasta que se extraiga esa composición.

---

## 6. Nota de persistencia

La fase `sdd-explore` persistió la observación Engram `sdd/change-47-workers-cloud-run/explore` (id 407) pero **no pudo escribir este fichero**: el tipo de agente `sdd-explore` no dispone de herramienta de escritura. El orquestador completó la mitad de openspec transcribiendo la observación, tras verificar por muestreo directo contra el worktree las afirmaciones clave (líneas 166/215/241/244/319 de `Program.cs`, ruta real de `CommunityNotificationService.cs`, `ci-cd.yml:102`, y el recuento de 40 ficheros `UseSqlite` frente a 0 con `UseNpgsql`/Testcontainers/Respawn).
