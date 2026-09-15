# INC-47: Trabajos en Segundo Plano Correctos en Google Cloud Run (Jobs, Scheduler y Outbox Persistente)

> **Estado:** ⏳ En progreso (pendiente de aprobación de alcance)
> **Fecha de Inicio:** 2026-09-15
> **Rama de Trabajo:** `inc/workers-cloud-run`
> **Worktree:** `C:\repos\ludeka-wt\workers-cloud-run`
> **Dependencias:** INC-38 (PostgreSQL/Supabase), INC-39 (Docker/Cloud Run), INC-44 (Recolector Social), INC-45 (Radar de Precios), INC-46 (Autenticación Real)
> **Especificación Viva del Sistema:** [09. Arquitectura y Despliegue](file:///c:/repos/Ludeka/docs/specs/sistema/09-arquitectura-y-despliegue.md), [08. Notificaciones y Webhooks](file:///c:/repos/Ludeka/docs/specs/sistema/08-notificaciones-y-webhooks.md)

---

## 1. Motivación y Visión

Los cuatro trabajos en segundo plano de Ludeka se ejecutan **dentro del proceso web**, con **estado en memoria** y **sin coordinación entre instancias**. En la configuración real de despliegue esto es incorrecto por construcción.

### Evidencia verificada en `main` (`a54cb31`)

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

## 3. Decisiones pendientes para el maintainer

1. **Modelo de ejecución.** Se propone **Cloud Run Jobs + Cloud Scheduler**. Alternativa: workers en proceso con `pg_advisory_lock`. La primera es la respuesta nativa de Cloud Run y la única que resuelve la cola; la segunda no añade infraestructura nueva.
2. **Proyecto separado o modo del host.** Se propone un modo *job* dentro del host existente (mismo contenedor, un argumento de entrada) para no duplicar la imagen. Alternativa: proyecto `Ludeka.Jobs` independiente, más limpio pero con un segundo artefacto a construir y desplegar.
3. **Frecuencia de Cloud Scheduler.** Se propone 1×/día para el lote, cada 6 h para el radar, cada 120 min para el recolector social y cada 5-15 min para el outbox. Ajustable por configuración.

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
