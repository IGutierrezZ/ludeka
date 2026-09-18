# INC-47: Trabajos en Segundo Plano Correctos en Google Cloud Run (Jobs, Scheduler y Outbox Persistente)

> **Estado:** ⏳ En progreso — alcance **aprobado por el maintainer el 2026-09-18**; ciclo SDD en `sdd-spec`
> **Fecha de Inicio:** 2026-09-15
> **Rama de Trabajo:** `inc/workers-cloud-run`
> **Worktree:** `C:\repos\ludeka-wt\workers-cloud-run`
> **Dependencias:** INC-38 (PostgreSQL/Supabase), INC-39 (Docker/Cloud Run), INC-44 (Recolector Social), INC-45 (Radar de Precios), INC-46 (Autenticación Real)
> **Especificación Viva del Sistema:** [09. Arquitectura y Despliegue](file:///c:/repos/Ludeka/docs/specs/sistema/09-arquitectura-y-despliegue.md), [08. Notificaciones y Webhooks](file:///c:/repos/Ludeka/docs/specs/sistema/08-notificaciones-y-webhooks.md)

---

## 1. Motivación y Visión

Los cuatro trabajos en segundo plano de Ludeka se ejecutan **dentro del proceso web**, con **estado en memoria** y **sin coordinación entre instancias**. En la configuración real de despliegue esto es incorrecto por construcción.

### Evidencia verificada en `main` (`a54cb31`)

> ⚠️ **Las citas de esta sección están obsoletas.** Se recogieron contra `a54cb31`; después entraron INC-46, INC-49, INC-50 e INC-51. `sdd-explore` las reverificó contra `56c02b9` el 2026-09-18 y encontró tres correcciones: los cuatro `AddHostedService` están en `Program.cs:166,215,244,319` (no 146/195/224/289), el registro de la cola en `:241` (no 221), los productores en `FoundingVerdictService.cs:217` y `RuleQAService.cs:199` (no 211 y 186), y **la ruta `Infrastructure/Notifications/CommunityNotificationService.cs` no existe**: el fichero vive en `src/Ludeka.Application/Features/Community/`. El inventario de cuatro workers sigue completo y las tres fallas siguen vivas.
>
> **Fuente de verdad para las fases posteriores:** [`openspec/changes/change-47-workers-cloud-run/exploration.md`](../../openspec/changes/change-47-workers-cloud-run/exploration.md), no esta sección.

**Registro de los cuatro workers** — `Program.cs:146,195,224,289`. Son los únicos `AddHostedService` del proyecto.

| Worker | Archivo | Periodo | Estado de planificación |
|---|---|---|---|
| `NightlyCatalogingHostedService` | `Infrastructure/Background/NightlyCatalogingHostedService.cs` | Sondeo cada 15 min (`:79`); ejecuta al alcanzar `ExecutionHourUtc` (`:57`) | `_lastExecutionDate` **en memoria** (`:22`, escrito en `:67`) |
| `PriceRadarHostedService` | `Infrastructure/Background/PriceRadarHostedService.cs` | Cada `CheckIntervalHours` (`:69-72`), por defecto 6 h | **Ninguno** |
| `SocialCollectorHostedService` | `Infrastructure/Background/SocialCollectorHostedService.cs` | Cada `IntervalMinutes` (`:82-83`), por defecto 120 | **Ninguno** |
| `CommunityNotificationDispatcherHostedService` | `Infrastructure/Notifications/CommunityNotificationDispatcherHostedService.cs` | Escaneo periódico cada 60 min (`:103`) | `_lastFridayBulletinDispatched` **en memoria** (`:16`, escrito en `:92`) |

**Cola de notificaciones por proceso** — `InMemoryCommunityNotificationQueue` (`Infrastructure/Notifications/InMemoryCommunityNotificationQueue.cs:10-34`), un `Channel<T>` acotado a 1000 elementos, registrado como `AddSingleton` (`Program.cs:221`).

- Productores: `FoundingVerdictService.cs:211` y `RuleQAService.cs:186`.
- Consumidor: `CommunityNotificationDispatcherHostedService.cs:43`.

### Las tres fallas concretas

1. **Ejecución duplicada al escalar.** Cloud Run despliega con `--min-instances=0 --max-instances=5` (`.github/workflows/ci-cd.yml:102`). Hasta 5 instancias ejecutarían el lote nocturno, el radar y el recolector social a la vez. No existe bloqueo distribuido: la búsqueda de `IDistributedCache`, `pg_advisory`, *leader election* o tabla de locks en `src/` devuelve **cero resultados**.
2. **Pérdida de trabajo al escalar a cero.** El estado de planificación vive en memoria (`_lastExecutionDate`, `_lastFridayBulletinDispatched`). Al apagarse la instancia se pierde la noción de "ya se ejecutó hoy", de modo que un arranque posterior puede reejecutar el lote o saltárselo según el minuto.
3. **Pérdida de notificaciones.** Un mensaje encolado en la instancia A solo lo consume el dispatcher de A. Si A escala a cero antes de drenar, el mensaje desaparece. No existe drenado ordenado: no hay `IHostApplicationLifetime` ni cierre del `Channel`, y `ProcessQueueAsync` solo captura `OperationCanceledException` (`:59-62`).

**Activo reutilizable existente:** `CommunityNotificationLog` ya persiste un registro por envío (`Infrastructure/Notifications/CommunityNotificationService.cs:93,102,108,124`) y `NightlyCatalogingExecutionLog` (`Core/Entities/NightlyCatalogingExecutionLog.cs:11-62`) ya registra ejecuciones. El problema no es la falta de tablas: es que el disparo depende de una cola y de un reloj en memoria.

---

## 2. Objetivos y Alcance Técnico

### 2.1. Modelo de ejecución propuesto: Cloud Run Jobs + Cloud Scheduler

Se propone **externalizar los cuatro trabajos** a *Cloud Run Jobs* disparados por *Cloud Scheduler*, en lugar de mantenerlos dentro del proceso web con bloqueo distribuido.

- Nuevo proyecto `src/Ludeka.Jobs` (aplicación de consola) o modo *job* del host existente seleccionado por argumento (`--job=nightly-cataloging`), reutilizando el mismo contenedor y las mismas dependencias.
- Cada trabajo se ejecuta una vez, termina y reporta código de salida. Sin bucles `while`, sin `Task.Delay` infinito, sin estado de planificación en memoria.
- `Cloud Scheduler` invoca: lote nocturno 1×/día, radar de precios cada `CheckIntervalHours`, recolector social cada `IntervalMinutes`, despachador de notificaciones cada 5-15 min.
- Los `AddHostedService` de los cuatro workers se retiran del host web (`Program.cs:146,195,224,289`), eliminando de raíz la ejecución duplicada.

**Alternativa considerada y no propuesta como principal:** conservar los workers en proceso y añadir un bloqueo distribuido con `pg_advisory_lock` de PostgreSQL. Es más barata en infraestructura pero mantiene el consumo de CPU permanente, impide el escalado a cero real y no resuelve la pérdida de la cola en memoria. Queda documentada por si el maintainer prefiere no introducir Cloud Run Jobs.

### 2.2. Outbox persistente para notificaciones

- Sustituir `InMemoryCommunityNotificationQueue` por un **outbox en PostgreSQL**: `EnqueueAsync` inserta una fila `CommunityNotificationLog` con estado `Queued` en la misma transacción que el cambio de dominio que la origina.
- El despachador pasa a **reclamar filas pendientes** (`Queued`) con `SELECT ... FOR UPDATE SKIP LOCKED` y a marcarlas `Sent` o `Failed` con reintento y contador de intentos.
- Efectos: los mensajes sobreviven al escalado a cero, dos instancias nunca envían el mismo mensaje, y un fallo de red deja el mensaje recuperable en lugar de destruirlo.
- Se conserva `InMemoryCommunityNotificationQueue` únicamente para desarrollo local y tests.

### 2.3. Idempotencia y planificación persistente

- `NightlyCatalogingExecutionLog` pasa a ser la fuente de verdad de "¿ya se ejecutó hoy?" en lugar de `_lastExecutionDate`. El trabajo comprueba la bitácora persistida antes de ejecutar.
- El boletín de viernes usa el outbox y una marca persistida en lugar de `_lastFridayBulletinDispatched`.
- Cada trabajo adquiere una marca de ejecución por periodo (clave + ventana temporal) con restricción única en base de datos, de modo que un reintento de Cloud Scheduler no duplica trabajo.

### 2.4. Apagado ordenado y observabilidad

- El host web deja de necesitar drenado de cola; los trabajos, al ser de vida corta, terminan su unidad de trabajo y salen.
- Si se conserva algún worker en proceso para desarrollo, se implementa `IHostApplicationLifetime` con drenado del `Channel` y *timeout* de cortesía.
- Los trabajos emiten código de salida distinto de cero ante fallo, para que Cloud Scheduler y las alertas puedan detectarlo.
- Métricas mínimas por ejecución: inicio, fin, elementos procesados, fallos y duración, persistidas en la bitácora.

### 2.5. Configuración

| Variable | Uso |
|---|---|
| `Workers__RunInProcess` | `false` en producción (Cloud Run), `true` en desarrollo local |
| `Workers__JobName` | Identifica el trabajo a ejecutar en el contenedor de jobs |
| `Outbox__BatchSize` | Filas reclamadas por ciclo del despachador |
| `Outbox__MaxAttempts` | Reintentos antes de marcar `Failed` |

### 2.6. Pruebas (Strict TDD activo)

- Runner contractual: `dotnet test Ludeka.sln`.
- Test de outbox: encolar persiste la fila; dos consumidores concurrentes no reclaman la misma fila (`SKIP LOCKED`).
- Test de idempotencia: ejecutar dos veces la misma ventana no duplica trabajo ni notificación.
- Test de recuperación: un mensaje `Queued` sobrevive a un reinicio simulado del host.
- Test de regresión: el host web **no** registra ningún `IHostedService` de negocio cuando `Workers__RunInProcess=false`.
- Test de reinicio del lote nocturno sin estado en memoria.

---

## 3. Decisiones del maintainer

### ✅ Resueltas

1. **Modelo de ejecución — decidido el 2026-09-18: Cloud Run Jobs + Cloud Scheduler (Rama A).** Los cuatro trabajos se externalizan a ejecuciones de vida corta disparadas por planificador y se retiran los cuatro `AddHostedService` del host web. **Descartada** la alternativa de conservarlos en proceso con `pg_advisory_lock`. Los dos datos que decidieron la elección, ambos aportados por `sdd-explore`: ninguna de las dos ramas resuelve la pérdida de notificaciones por sí sola (el outbox es obligatorio en ambas), y la brecha de pruebas contra PostgreSQL real es común a ambas —40 ficheros de `tests/` usan `UseSqlite` y ninguno `UseNpgsql`—, de modo que la Rama B no ahorraba esa inversión.
2. **Alcance — aprobado el 2026-09-18.** Aprobada [`proposal.md`](../../openspec/changes/change-47-workers-cloud-run/proposal.md), que amplía el alcance con un **habilitador de pruebas de integración contra PostgreSQL real como primera rebanada** (sin él, el criterio de aceptación §4.6 de este documento no es verificable) y prevé una cadena de siete PRs.
3. **Frecuencias de Cloud Scheduler — no bloqueante.** Se arrastran los valores propuestos (1×/día para el lote, cada 6 h para el radar, cada 120 min para el recolector social, cada 5-15 min para el despachador de outbox), todos configurables.
4. **Modelo de fila del outbox — decidido el 2026-09-18: una fila por mensaje lógico con sub-entregas por canal.** Descartada la alternativa de reclamar por fila-canal sobre el esquema actual. **El dato que decidió la elección:** el *fan-out* por canal se decide **al enviar, no al encolar** — `CommunityNotificationService.BroadcastAsync` consulta `_options.Enabled` (línea 66) y `_options.DiscordEnabled` / `_options.TelegramEnabled` (líneas 89 y 94), de modo que hoy un mensaje se encola sin canal resuelto. Reclamar por fila-canal habría obligado a mover esa decisión a `EnqueueAsync`, introduciendo una pérdida silenciosa: un mensaje encolado con Telegram deshabilitado nunca llegaría a Telegram aunque se habilitase minutos después con la fila pendiente. Coste aceptado: esquema mayor, migración más grande y revisión de `SqliteCommunityNotificationRepository.GetRecentLogsAsync`.

5. **Empaquetado del host de trabajos — decidido el 2026-09-18: proyecto `src/Ludeka.Jobs` independiente** (aplicación de consola). Descartado el modo *job* del host web por argumento.

   El argumento con el que este documento proponía el modo del host («mismo contenedor, un argumento de entrada, para no duplicar la imagen») **era falso**, corregido por inspección del `Dockerfile` y de `Ludeka.sln`: **un proyecto separado no obliga a una segunda imagen ni a un segundo artefacto.** Cloud Run Jobs permite sobrescribir el comando del contenedor, así que el servicio web arranca con el `ENTRYPOINT ["dotnet", "Ludeka.Web.dll"]` por defecto y el Job lo sobrescribe; basta publicar ambos proyectos en el mismo `/app/publish`. El runtime `mcr.microsoft.com/dotnet/aspnet:10.0` ejecuta una aplicación de consola sin necesitar una segunda base.

   **Motivo real de la elección:** con un proyecto separado el compilador custodia la frontera de la extracción de DI de la rebanada R2, porque el proyecto de trabajos solo puede consumir lo que la extensión expone públicamente. Con el modo del host, R2 deja de ser una frontera y se convierte en un `if` dentro de `Program.cs`, y nada impediría que una edición futura colara registro exclusivamente web en el camino del trabajo. Ventaja adicional: el proceso de trabajo no carga componentes Razor, antiforgery, *output caching*, autenticación por cookie ni los endpoints de salud.

   **Coste medido, para no subestimarlo ni inflarlo:** `Ludeka.sln` tiene 5 proyectos y ninguno de consola; el `Dockerfile` solo restaura y publica `Ludeka.Web.csproj`, así que hay que añadir sus líneas de `COPY` del `.csproj`, restauración y publicación hacia el mismo `/app/publish`; y el `--no-build` de `ci-cd.yml:43` obliga a que el proyecto esté en la solución. Tres puntos de cambio acotados, no un segundo pipeline.

6. **Estrategia de entrega — decidida el 2026-09-18: `auto-chain` con `stacked-to-main`.** Cada una de las siete rebanadas abre su PR contra `main` y mergea al estar verde, con ramas `inc/workers-cloud-run-NN-<nombre>`. Sigue el precedente de INC-49 (ocho PRs, #19 y #23-#30, todos a `main`). Descartada `feature-branch-chain` por no retener seis rebanadas ni hacer divergir `main` mientras INC-48, INC-50 e INC-51 siguen abiertos.

   > 🚨 **CONDICIÓN DE SEGURIDAD EN PRODUCCIÓN — INSEPARABLE DE ESTA DECISIÓN**
   >
   > **El PR de la rebanada R7 se abre pero NO se mergea** hasta que el maintainer confirme que Cloud Scheduler, el Cloud Run Job y la cuenta de servicio con `roles/run.invoker` están provisionados y **disparando de verdad** en Google Cloud.
   >
   > Motivo: R7 retira los cuatro `AddHostedService` del host web. Mergearla dispara un despliegue que deja el host **sin ningún ejecutor de trabajos**, y los Jobs que deben sustituirlos se provisionan a mano, fuera del repositorio, porque este ciclo no tiene acceso autorizado a GCP. R1 a R6 son aditivas y conviven con los workers en proceso todavía registrados, así que mergean sin riesgo: el peligro está concentrado entero en R7.

### ⏳ Abiertas

Ninguna. Las seis decisiones están resueltas y el diseño (`openspec/changes/change-47-workers-cloud-run/design.md`) está fijado, pendiente de la puerta de aprobación de AGENTS.md §1.2 antes de `sdd-apply`.

---

## 4. Criterios de Aceptación y Verificación

1. Con `--max-instances=5`, ningún trabajo se ejecuta dos veces para la misma ventana temporal.
2. Un mensaje de notificación encolado durante una instancia se envía aunque esa instancia se apague antes de drenar.
3. Ningún trabajo depende de estado en memoria para saber si ya se ejecutó.
4. El host web no consume CPU de fondo con `Workers__RunInProcess=false`.
5. Los trabajos devuelven código de salida distinto de cero ante fallo observable.
6. Suite completa en verde con `dotnet test Ludeka.sln`.
7. Prueba con navegador real y prueba de reinicio del contenedor sin pérdida de notificaciones pendientes.

---

## 5. Riesgos

| Riesgo | Impacto | Mitigación |
|---|---|---|
| Introducir Cloud Run Jobs añade superficie de despliegue | Medio | Reutilizar el mismo contenedor en modo *job*; un solo `Dockerfile` |
| Migrar la cola a outbox toca `FoundingVerdictService` y `RuleQAService` | Medio | Mantener la interfaz `ICommunityNotificationQueue` y cambiar solo la implementación |
| El outbox puede reenviar un mensaje si falla entre envío y marcado | Medio | Idempotencia por `CommunityNotificationLog.Id` y verificación previa al envío |
| Reclamación concurrente mal implementada | Alto | `FOR UPDATE SKIP LOCKED` con test de concurrencia obligatorio |
| Cloud Scheduler requiere cuenta de servicio con permiso de invocación | Medio | Documentar IAM en `docs/deployment/google-cloud-run.md` |
