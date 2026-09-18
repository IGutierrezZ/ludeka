# Diseño — INC-47: Trabajos en Segundo Plano Correctos en Google Cloud Run

> **Cambio:** `change-47-workers-cloud-run` · **Fase:** `sdd-design` · **Fecha:** 2026-09-18
> **Worktree:** `C:\repos\ludeka-wt\workers-cloud-run` (rama `inc/workers-cloud-run`, **commit base real `57e85cc`**)
> **Entradas:** [`proposal.md`](proposal.md) (aprobada el 2026-09-18) · [`specs/`](specs/) (20 requisitos, 36 escenarios, 6 capacidades) · [`exploration.md`](exploration.md) · [`docs/increments/inc-47-workers-cloud-run.md`](../../../docs/increments/inc-47-workers-cloud-run.md) §3
> **Almacén de artefactos:** `hybrid` — este fichero más la observación Engram `sdd/change-47-workers-cloud-run/design`
> **Configuración aplicable:** `openspec/config.yaml` → `mode: hybrid`, `language: es`, `strict_tdd: true`, `testing.workspace_command: dotnet test Ludeka.sln`. Sin `rules.design` declaradas.
> **Decisiones del maintainer (5, cerradas el 2026-09-18):** no se reabre ninguna. Este diseño las ejecuta y declara en §2 las consecuencias que no estaban a la vista.
> **Toda cita de código de este documento se ha verificado leyendo el fichero.** Las citas del §1 del documento de incremento están obsoletas y no se usan.
>
> ⚠️ **Nota de base de commit (corregida por el validador de contrato, 2026-09-18).** Las cabeceras de `exploration.md`, `proposal.md`, `specs/background-jobs-scheduling/spec.md` y la versión original de este fichero declaraban `56c02b9` como commit base. **La base real de la rama es `57e85cc`** (merge del PR #34, `inc/css-regenerado`): el worktree avanzó durante la sesión de planificación. El diff `56c02b9..57e85cc` toca **solo** `.gitignore` y `src/Ludeka.Web/wwwroot/app.css`, ninguno de ellos citado por ninguna de las cuatro fases, de modo que **todas las citas de código siguen siendo válidas**. Consecuencia operativa para `sdd-tasks` y `sdd-verify`: **medid los diffs del presupuesto de 400 líneas contra `57e85cc`**, no contra `56c02b9`, o se contarán de más las 25 líneas de ese merge ajeno al incremento.

---

## 1. Enfoque técnico

Cuatro decisiones de arquitectura sostienen el incremento, y cada una elimina una clase de fallo en lugar de mitigarla:

1. **La exclusión mutua se delega a la base de datos, nunca al proceso.** Dos primitivas, cada una para su problema: una restricción `UNIQUE (JobName, WindowKey)` para «esta ventana temporal ya se ejecutó», y `UPDATE … FROM (SELECT … FOR UPDATE SKIP LOCKED) … RETURNING` para «este mensaje me lo quedo yo». Ningún `if` en memoria decide nada.
2. **El outbox separa el mensaje lógico de su entrega por canal.** Una fila nueva `NotificationOutboxMessages` por mensaje; las filas por canal viven en la tabla ya existente `NotificationLogs`, que pasa a ser la tabla de sub-entregas. El *fan-out* por canal se sigue decidiendo **al despachar**, releyendo las opciones, tal y como exige la decisión 4.
3. **La frontera de composición la custodia el compilador.** La composición de dominio e infraestructura se extrae a extensiones públicas de `Ludeka.Infrastructure`; `src/Ludeka.Jobs`, proyecto de consola independiente, solo puede consumir lo que esas extensiones exponen. Lo que se queda en `Program.cs` es exactamente lo que no compila fuera de un host web.
4. **El proceso de trabajo no tiene bucle.** `Ludeka.Jobs` construye el host genérico, resuelve un `IJobRunner` por nombre, ejecuta una unidad de trabajo y devuelve un código de salida. Nunca llama a `host.RunAsync()`, nunca registra un `IHostedService`.

El corolario operativo: el bloqueo de base de datos vive **solo durante la sentencia de reclamación**, y la propiedad del mensaje durante el envío la sostiene una **concesión temporal** (`NextAttemptAt` desplazado al futuro), no un lock abierto. Eso es lo que permite que la llamada HTTP a Discord o Telegram ocurra sin ninguna transacción abierta, y que una ejecución que muera a mitad de envío libere su trabajo por caducidad y no por intervención manual.

---

## 2. Consecuencias de las cinco decisiones que el maintainer no tenía a la vista

**Ninguna de las cinco decisiones es inviable y ninguna se reabre.** Lo que sigue son cinco consecuencias verificadas que cambian el coste o el alcance, y una imprecisión de la propuesta que este diseño no puede cumplir tal como está redactada. Se declaran aquí, de forma destacada, en lugar de improvisar un desvío silencioso.

### C1 — El outbox durable pierde `Fields` y `TargetChannel` si solo se añaden `Attempts`/`NextAttemptAt`

`CommunityNotificationMessage` (`src/Ludeka.Core/ValueObjects/CommunityNotificationMessage.cs:6-13`) transporta `Fields` (diccionario) y `TargetChannel`. Los dos se consumen de verdad al enviar: `DiscordWebhookClient.cs:64-68` los convierte en los *fields* del *embed* y `TelegramBotClient.cs:107-113` en las líneas del mensaje. **`CommunityNotificationLog` no tiene columna para ninguno de los dos** (lectura completa de `src/Ludeka.Core/Entities/CommunityNotificationLog.cs:8-18`).

La prueba de que esto ya duele hoy: `CommunityNotificationService.RetryFailedNotificationAsync` reconstruye el mensaje desde la fila persistida (`:248-254`) **sin `Fields`**, de modo que el reintento manual del panel ya emite hoy una notificación empobrecida respecto a la original, en silencio.

**Consecuencia:** si el outbox es el único portador durable del mensaje, la tabla de mensajes **debe** persistir `Fields` y `TargetChannel`, o cada notificación que sobreviva a un reinicio llegará degradada. Este diseño añade `FieldsJson` y `TargetChannel` a `NotificationOutboxMessages` (§5). No es un extra: es el requisito «`EnqueueAsync` persiste la fila antes de cualquier intento de envío» tomado en serio.

### C2 — `EnqueueAsync` cambia de perfil de fallo, y los dos productores lo silencian

La decisión 4 se apoya en que preservar `EnqueueAsync` deja intactos a los dos productores. Es cierto **en firma**, no en perfil de fallo: hoy `EnqueueAsync` es una escritura en un `Channel<T>` acotado en memoria (`InMemoryCommunityNotificationQueue.cs:26-29`); mañana es una escritura en base de datos, que puede fallar por red, por indisponibilidad o por *timeout*.

Y los dos productores envuelven la llamada en un `catch` que se lo traga todo: `FoundingVerdictService.cs:219-222` (`catch { /* La notificación no debe impedir guardar el veredicto */ }`) y `RuleQAService.cs:201-204` (`catch { /* No interrumpir la transacción principal */ }`).

**Consecuencia:** sin más cambios, un fallo de base de datos en el encolado produce exactamente la pérdida silenciosa que INC-47 existe para eliminar. Respuesta de diseño, **sin tocar ni una línea de los dos productores**: `OutboxCommunityNotificationQueue.EnqueueAsync` registra el fallo con `ILogger` antes de propagar la excepción (§6.2). El `catch` del productor sigue tragándose la excepción y el veredicto sigue guardándose —comportamiento preservado—, pero la pérdida queda en el registro en lugar de desaparecer.

### C3 — La extensión de DI en un ensamblado no web necesita paquetes que hoy nadie referencia

`Program.cs` invoca `AddHttpClient` **16 veces** (recuento exacto sobre el fichero) y esas 16 registran clientes de infraestructura, no de web. Hoy compilan porque `Ludeka.Web.csproj` usa `Microsoft.NET.Sdk.Web` y hereda el *framework* compartido de ASP.NET Core. **`Microsoft.Extensions.Http` no aparece en ningún `.csproj` de `src/`** (búsqueda sin resultados en todo `src/`).

Hay una dependencia que lo convierte en bloqueante, no en cosmético: `SocialIngestionService` exige un `HttpClient` desnudo del contenedor (`src/Ludeka.Application/Features/Community/SocialIngestionService.cs:43`), y `SocialCollectorService` depende de `ISocialIngestionService` (`:33`). Sin `Microsoft.Extensions.Http` y sin al menos una llamada a `AddHttpClient`, **el trabajo de recolección social no se puede componer**.

**Consecuencia:** `Ludeka.Infrastructure.csproj` gana `PackageReference`s (§4.4). No invalida la decisión 5, la encarece de forma acotada y medible, y la prueba de humo de composición de R2 (§4.6) es la que lo demuestra en rojo antes de arreglarlo.

### C4 — La propuesta pide atomicidad transaccional que el alcance aprobado no permite

La propuesta §3.2, punto 1, dice que la fila se cree «en la misma transacción que el cambio de dominio que la origina». Verificación: en `FoundingVerdictService`, `_verdictRepository.AddAsync`/`UpdateAsync` (`:180`, `:191`) y después `RecalculateLudistRatingWithFoundingWeightAsync` (`:195`) se ejecutan **antes** del encolado (`:217`), y los repositorios de este proyecto confirman cada uno por su cuenta —patrón verificado en `SqliteCommunityNotificationRepository.AddLogAsync:49-50` y `SqliteNightlyCatalogingLogRepository.AddAsync:27-28`, `Add` seguido de `SaveChangesAsync`—.

Es decir: **cuando se llama a `EnqueueAsync`, el cambio de dominio ya está confirmado.** Compartir el `LudekaDbContext` con ámbito no lo arregla; harían falta un `SaveChanges` unificado y un rediseño de los dos productores, que la propuesta §2.2 declara explícitamente fuera de alcance («no hace falta»).

**Consecuencia declarada:** este diseño garantiza **durabilidad desde `EnqueueAsync` en adelante**, no atomicidad con el cambio de dominio originante. Se ha comprobado escenario por escenario que **ninguno de los 36 de la especificación exige la transacción compartida**: el requisito real es «un registro persistente en estado pendiente queda escrito en base de datos inmediatamente… sin ninguna dependencia temporal de la ejecución del despachador» (`specs/notification-outbox/spec.md:21-22`), y eso sí se cumple. La ventana residual es estrecha y honesta: si el proceso muere **entre** el guardado del veredicto y el `EnqueueAsync`, el veredicto queda sin notificación. Cerrarla es un incremento propio (patrón *outbox* transaccional completo con un único `SaveChanges` por caso de uso); registrarla es obligación de esta fase.

### C5 — `SqliteSchemaMigrator` crea `NightlyCatalogingExecutionLogs` sin `BggDiscoveryCount`

Hallazgo colateral y preexistente, ajeno a INC-47 pero directamente en la trayectoria de R3. La entidad tiene `BggDiscoveryCount` (`src/Ludeka.Core/Entities/NightlyCatalogingExecutionLog.cs:18`) y la sentencia `CREATE TABLE` del reconciliador SQLite **no la incluye** (`src/Ludeka.Infrastructure/Data/SqliteSchemaMigrator.cs:543-558`). Una base SQLite en disco que llegue a ese camino obtiene una tabla incompleta y fallará al leer.

**Consecuencia:** R3 toca ese mismo fichero. El diseño **no arregla** el hueco preexistente (fuera de alcance, igual que la desincronización del módulo 18 de la especificación viva), pero obliga a que el trabajo de R3 **no lo replique**: cada columna y cada tabla nuevas se reconcilian con el patrón `PRAGMA table_info` + `ALTER TABLE` ya establecido (`:49-83`, `:887-898`), que es incremental y sí sobrevive a bases existentes, en lugar de depender de un `CREATE TABLE` que solo se ejecuta cuando la tabla falta.

---

## 3. Arquitectura y fronteras

### 3.1. Vista de ensamblados

```
                      ┌──────────────────────────┐   ┌──────────────────────────┐
                      │      Ludeka.Web          │   │   Ludeka.Jobs  (NUEVO)   │
                      │  Microsoft.NET.Sdk.Web   │   │  Microsoft.NET.Sdk, Exe  │
                      │                          │   │                          │
                      │ Razor, cookie+OAuth,     │   │ args → IJobRunner        │
                      │ antiforgery, output      │   │ 1 unidad de trabajo      │
                      │ cache, health endpoints, │   │ código de salida         │
                      │ ICurrentUserService,     │   │ sin bucle, sin host.Run  │
                      │ ISessionPermissionGuard  │   │                          │
                      └────────────┬─────────────┘   └────────────┬─────────────┘
                                   │                               │
                                   │   AddLudekaApplicationCore(configuration)
                                   └───────────────┬───────────────┘
                                                   ▼
                      ┌──────────────────────────────────────────────────────┐
                      │                 Ludeka.Infrastructure                │
                      │  DependencyInjection/LudekaServiceCollection-        │
                      │  Extensions.cs   ← LA FRONTERA (pública)             │
                      │  EF Core dual (Npgsql/SQLite), repositorios,         │
                      │  clientes HTTP, outbox, leases de ventana            │
                      └────────────────────────┬─────────────────────────────┘
                                               ▼
                      ┌──────────────────────────────────────────────────────┐
                      │   Ludeka.Application  (contratos, casos de uso)      │
                      │        └── Ludeka.Core  (entidades, enums)           │
                      └──────────────────────────────────────────────────────┘
```

`Ludeka.Jobs` **no referencia `Ludeka.Web`**. Es la garantía estructural de la decisión 5: si una edición futura cuela un registro exclusivamente web en la extensión, el proyecto de trabajos deja de compilar.

### 3.2. Por qué la extensión vive en `Ludeka.Infrastructure`

| Ubicación | Descartada porque |
|---|---|
| `Ludeka.Web` | `Ludeka.Jobs` tendría que referenciar el proyecto web: destruye el objetivo de la decisión 5 |
| `Ludeka.Application` | No conoce las implementaciones concretas (repositorios, EF Core, clientes HTTP) que debe registrar |
| Proyecto nuevo `Ludeka.Composition` | Séptimo proyecto para cero beneficio: la raíz de composición de dominio pertenece al ensamblado que posee las implementaciones |

**Elegida: `Ludeka.Infrastructure`.** Ya referencia `Ludeka.Application` → `Ludeka.Core` y ya contiene todo lo que hay que registrar. Es el único punto donde la extensión no crea una dependencia nueva entre proyectos.

---

## 4. Decisión D1 — Contrato de la extracción de DI (rebanada R2)

**Elección:** cuatro métodos de extensión públicos en `src/Ludeka.Infrastructure/DependencyInjection/LudekaServiceCollectionExtensions.cs`, con una fachada agregada que los dos hosts invocan en una línea.
**Alternativas consideradas:** (a) un único método monolítico `AddLudekaDomainServices`; (b) un método por incremento histórico (14+ métodos, espejo de los comentarios de `Program.cs`).
**Razón:** (a) no ofrece ninguna costura por donde partir R2, que la propuesta §9 estima en ~400-460 líneas cambiadas, **por encima del presupuesto fijo de 400**; (b) fosiliza el historial de incrementos como si fuera arquitectura y deja 14 puntos de entrada que un host puede olvidar. Cuatro métodos por **responsabilidad técnica** dan una costura natural —persistencia y dominio frente a integraciones externas— y mantienen un solo punto de entrada de uso normal.

```csharp
namespace Ludeka.Infrastructure.DependencyInjection;

public static class LudekaServiceCollectionExtensions
{
    /// <summary>Punto de entrada único de los dos hosts. Registra la composición completa
    /// de dominio e infraestructura, sin ningún registro específicamente web.</summary>
    public static IServiceCollection AddLudekaApplicationCore(
        this IServiceCollection services, IConfiguration configuration)
        => services
            .AddLudekaPersistence(configuration)
            .AddLudekaDomainServices(configuration)
            .AddLudekaExternalIntegrations(configuration);

    /// <summary>Opciones de base de datos, resolución de proveedor y <c>LudekaDbContext</c>.</summary>
    public static IServiceCollection AddLudekaPersistence(
        this IServiceCollection services, IConfiguration configuration);

    /// <summary>Repositorios y servicios de dominio, incluidas las opciones que enlazan.</summary>
    public static IServiceCollection AddLudekaDomainServices(
        this IServiceCollection services, IConfiguration configuration);

    /// <summary>Clientes HTTP tipados y adaptadores de servicios externos
    /// (BGG, Gemini, YouTube, Instagram, Discord, Telegram, R2, tiendas, feeds sociales).</summary>
    public static IServiceCollection AddLudekaExternalIntegrations(
        this IServiceCollection services, IConfiguration configuration);
}
```

`sdd-tasks` decide si usa esa costura para partir R2 o si pide `size:exception`; el diseño solo garantiza que la costura exista y sea limpia.

### 4.1. Frontera exacta: qué se mueve y qué se queda

Clasificación bloque a bloque de `Program.cs` (líneas verificadas en `56c02b9`). «Extensión» = consumible por `Ludeka.Jobs`. «Web» = permanece en `Program.cs`.

| Líneas | Bloque | Destino |
|---|---|---|
| 49, 51-56 | `WebApplication.CreateBuilder`, `PORT` → `UseUrls` | **Web** |
| 59-60 | `AddRazorComponents().AddInteractiveServerComponents()` | **Web** |
| 63-64 | `Configure<DatabaseOptions>`, `Configure<AdminUserOptions>` | `AddLudekaPersistence` |
| 68-70 | lectura imperativa `GetSection(...).Get<AuthenticationOptions>()` | **Web** (no toca el contenedor) |
| 71 | `Configure<AuthenticationOptions>` | `AddLudekaDomainServices` |
| 72 | `AddLudekaAuthentication(authenticationOptions)` | **Web** (cookie + esquemas OAuth) |
| 73-75 | `IExternalLoginRepository`, `IExternalLoginService`, `IAccountConnectionsService` | `AddLudekaDomainServices` |
| 78-101 | cadena de conexión, `IsPostgreSql`, `AddDbContext<LudekaDbContext>` con `EnableRetryOnFailure` | `AddLudekaPersistence` |
| 103 | `IGameRepository` | `AddLudekaDomainServices` |
| 106-111 | `AddMemoryCache`, `CatalogService`, `ICatalogService` decorado | `AddLudekaDomainServices` |
| 114-127 | `AddOutputCache` y sus tres políticas | **Web** |
| 129-141 | `BggOptions`, `BggResilienceAndAuthHandler`, `AddHttpClient<BggXmlApiClient>`, `SimulatedBggClient`, selector `IBggClient` | `AddLudekaExternalIntegrations` |
| 143-158 | colección, préstamos, reseñas, partidas, veredictos, medios, importación BGG | `AddLudekaDomainServices` |
| 160-165 | `NightlyCatalogingOptions` y sus cuatro registros | `AddLudekaDomainServices` |
| **166** | `AddHostedService<NightlyCatalogingHostedService>` | **se retira en R7** |
| 169-170, 193-194 | reportes de incidencias, bitácora de edición | `AddLudekaDomainServices` |
| 173-186 | `CloudflareR2Options`, optimización e `IImageStorageService` (selector por credenciales) | `AddLudekaExternalIntegrations` |
| 188-191 | `BggMassIngestionOptions`, *staging*, `IGeekDoImagesClient`, `IBggMassIngestionService` | `AddLudekaExternalIntegrations` |
| 197-201 | `AffiliateOptions`, `IAffiliateUrlResolver`, `ISleeveStoreUrlResolver` | `AddLudekaDomainServices` |
| 204-209 | `StoreStockOptions`, `SimulationStoreStockClient`, `HtmlSchemaStoreStockClient`, `IStoreStockService` | `AddLudekaExternalIntegrations` — **orden significativo, ver §4.3** |
| 212-214 | `PriceRadarOptions`, `IGamePriceRepository`, `IPriceRadarService` | `AddLudekaDomainServices` |
| **215** | `AddHostedService<PriceRadarHostedService>` | **se retira en R7** |
| 218-224 | sorteos, lanzamientos semanales, Q&A de reglas, tarjetas sociales | `AddLudekaDomainServices` |
| 227-231 | `InstagramOptions`, `IInstagramApiClient`, compositor y publicador | `AddLudekaExternalIntegrations` (cliente) + `AddLudekaDomainServices` (servicios) |
| 234-235 | expansiones | `AddLudekaDomainServices` |
| 238-240 | `CommunityNotificationOptions`, `IDiscordWebhookClient`, `ITelegramBotClient` | `AddLudekaExternalIntegrations` |
| **241** | `AddSingleton<ICommunityNotificationQueue, InMemoryCommunityNotificationQueue>` | `AddLudekaDomainServices` — **cambia a `AddScoped` con la implementación de outbox, ver §6.2** |
| 242-243 | `ICommunityNotificationRepository`, `ICommunityNotificationService` | `AddLudekaDomainServices` |
| **244** | `AddHostedService<CommunityNotificationDispatcherHostedService>` | **se retira en R7** |
| 249 | `AddHttpContextAccessor` | **Web** |
| 250-253 | `UserIdentitySnapshot`, `ICurrentUserService`, `CircuitHandler`, `IUserSessionInvalidator` | **Web** (las implementaciones viven en `src/Ludeka.Web/`) |
| 257 | `ISessionPermissionGuard` → `SessionPermissionGuard` | **Web**, ver §4.2 |
| 258-261 | biblioteca, estadísticas, preferencias, ubicación de usuario | `AddLudekaDomainServices` |
| 264-270 | `GeminiOptions`, `IAiGameSummaryService`, `YouTubeOptions`, `IChannelFocusProvider`, `IYouTubeSearchService` | `AddLudekaExternalIntegrations` |
| 273-296 | directorio, usuarios, auditoría, eventos, *dashboard* de inicio cacheado | `AddLudekaDomainServices` |
| 299-304 | hub de ingesta social (`ISocialInboxRepository`, `IMonitoredAccountRepository`, extractor, análisis IA, `ISocialIngestionService`, `IMonitoredAccountService`) | extractor y análisis a `AddLudekaExternalIntegrations`; el resto a `AddLudekaDomainServices` |
| 307-318 | `SocialCollectorOptions`, cuatro clientes de feed, cuatro `ISocialChannelCollector`, `ISocialCollectorService` | `AddLudekaExternalIntegrations` (feeds) + `AddLudekaDomainServices` (`ISocialCollectorService`) — **orden significativo, ver §4.3** |
| **319** | `AddHostedService<SocialCollectorHostedService>` | **se retira en R7** |
| 322-325 | `AddHealthChecks` y los tres chequeos | **Web** |
| 327-389 | `builder.Build()`, avisos de autenticación, inicialización de esquema y semillado | **Web** — ver §4.5 |

### 4.2. Casos frontera, resueltos uno a uno

**`ISessionPermissionGuard` se queda en `Program.cs`.** `SessionPermissionGuard` depende de `ICurrentUserService` (`src/Ludeka.Application/Contracts/SessionPermissionGuard.cs:17,20`) y la única implementación de ese contrato es `AuthenticatedCurrentUserService`, en `src/Ludeka.Web/Services/`. Mover el registro arrastraría el ensamblado web.

Consecuencia comprobada: **los seis servicios que necesitan la guarda la reciben como parámetro opcional con valor por defecto `null`** — `CommunityNotificationService.cs:39`, `NightlyCatalogingService.cs:54`, `SocialCollectorService.cs:37`, `SocialIngestionService.cs:45`, `GiveawayService.cs:23`, `WeeklyReleaseService.cs:23` — y los caminos de sistema los saltan por diseño (INC-46, hallazgo W1): `RequirePermissionAsync` devuelve `Task.CompletedTask` cuando la guarda es `null` (`CommunityNotificationService.cs:55-58`).

Aquí el diseño **no** se apoya en que el contenedor rellene el valor por defecto de un servicio no registrado. `Ludeka.Jobs` registra explícitamente:

```csharp
// src/Ludeka.Application/Contracts/DenyAllSessionPermissionGuard.cs (NUEVO)
/// <summary>Guarda de permiso para hosts sin sesión (procesos de trabajo). Deniega SIEMPRE:
/// un trabajo programado solo puede usar los puntos de entrada de sistema
/// (<c>Run*</c>/<c>Scan*</c>), nunca los de panel. Deniega en lugar de permitir para que una
/// llamada futura a un método con guarda falle de forma ruidosa y no eluda la autorización.</summary>
public sealed class DenyAllSessionPermissionGuard : ISessionPermissionGuard { /* … throw … */ }
```

Dos beneficios sobre dejar el `null`: elimina la dependencia de un comportamiento del contenedor que este repositorio no ejercita hoy, y convierte una regresión futura en una excepción en lugar de en una autorización eludida en silencio.

**`IOptionsMonitor<T>` no requiere nada especial.** Los tres trabajos con bucle lo usan hoy (`NightlyCatalogingHostedService.cs:20`, `PriceRadarHostedService.cs:19`, `SocialCollectorHostedService.cs:21`) y `SocialCollectorService.cs:35` también. `IOptionsMonitor<T>` se satisface con `Configure<T>(section)` más la infraestructura de opciones que `Host.CreateApplicationBuilder` ya instala, exactamente igual que `WebApplication.CreateBuilder`. En un proceso de vida corta el refresco en caliente es irrelevante, pero la resolución funciona sin cambios de firma.

**Regla dura de opciones: exactamente un `Configure<T>` por tipo, y vive en la extensión.** `Program.cs` conserva solo la **lectura imperativa** de `AuthenticationOptions` (`:68-70`), que no registra nada. Un `Configure<T>` duplicado en los dos sitios acumularía dos `IConfigureOptions<T>` sobre la misma sección: inofensivo en resultado, ruidoso en intención y fuente de divergencia futura.

**Clientes HTTP con nombre y tipados: se mueven todos.** Las 16 llamadas a `AddHttpClient` registran integraciones externas, no web. Se mueven con el coste de paquetes de §4.4. `BggResilienceAndAuthHandler` (`AddTransient`, `Program.cs:130`) se mueve con su cliente: `AddHttpMessageHandler<T>` exige que el manejador esté registrado.

**Los *health checks* se quedan.** `AddHealthChecks()` es del *framework* compartido de ASP.NET Core y los tres chequeos viven en `src/Ludeka.Web/Health/`. Un proceso de trabajo no expone sondas: su señal es el código de salida (`specs/dockerfile-build/spec.md:30-35`).

**Autenticación: se queda entera.** `AddLudekaAuthentication` compone esquemas de cookie y OAuth con paquetes que solo referencia `Ludeka.Web.csproj:14-16`.

### 4.3. El orden de registro: riesgo 2 acotado a dos puntos concretos

`Microsoft.Extensions.DependencyInjection` resuelve de forma perezosa, así que reordenar registros **no** altera la resolución de un servicio suelto. El riesgo 2 de la propuesta es real pero tiene exactamente dos formas, y las dos son enumerables:

1. **Resolución de `IEnumerable<T>`: preserva el orden de registro.** Hay dos casos verificados. `ISocialChannelCollector` con cuatro registros en el orden YouTube, Telegram, RSS, Instagram (`Program.cs:313-316`), consumido como `IEnumerable<ISocialChannelCollector>` por `SocialCollectorService.cs:34`. E `IStoreStockClient` con dos (`:207-208`), consumido como `IEnumerable<IStoreStockClient>` por `StoreStockService.cs:21,27`.
2. **Sobrescritura por último registro.** `IStoreStockClient` está registrado **dos veces como `AddSingleton`** (`:207-208`), de modo que un `GetRequiredService<IStoreStockClient>()` singular devolvería hoy el **segundo**, `HtmlSchemaStoreStockClient`. Invertir esas dos líneas cambiaría ese resultado.

**Respuesta de diseño:** la extracción preserva el orden relativo dentro de cada grupo de registros múltiples, y R2 incorpora una prueba que afirma explícitamente el orden de las dos secuencias de `IEnumerable<T>`. Con esos dos puntos cubiertos, el riesgo 2 deja de ser un temor difuso y pasa a ser una aserción.

### 4.4. Coste de paquetes, medido

| Proyecto | Adición | Motivo verificado |
|---|---|---|
| `Ludeka.Infrastructure.csproj` | `Microsoft.Extensions.Http` | Las 16 llamadas a `AddHttpClient`, `AddHttpMessageHandler` y el `HttpClient` desnudo que exige `SocialIngestionService.cs:43`. Hoy no se referencia en ningún `.csproj` de `src/` |
| `Ludeka.Infrastructure.csproj` | `Microsoft.Extensions.Options.ConfigurationExtensions` | Los ~20 `Configure<T>(IConfigurationSection)` que se mueven |
| `Ludeka.Jobs.csproj` | `Microsoft.Extensions.Hosting` | `Host.CreateApplicationBuilder`; `Ludeka.Infrastructure` solo referencia `…Hosting.Abstractions` (`:14`) |

**Hueco de evidencia declarado:** el conjunto exacto de paquetes no se puede cerrar sin compilar, y esta fase no ejecuta `dotnet build`. Lo verificado es que `Microsoft.Extensions.Http` no se referencia hoy en `src/` y que hay un consumidor que lo exige. `sdd-apply` cierra la lista con el compilador; la prueba de humo de §4.6 es el detector.

### 4.5. Lo que la extensión no toca: inicialización de esquema y semillado

`Program.cs:336-389` es código **imperativo de arranque**, no registro de servicios: crea el directorio de SQLite, ejecuta `MigrateAsync` en PostgreSQL o `EnsureCreatedAsync` más `SqliteSchemaMigrator` en SQLite (`:365-376`), y siembra el administrador y los datos de demostración (`:379-388`). La extracción **no lo mueve** y `Ludeka.Jobs` **no lo replica**. Dos razones:

1. Cuatro trabajos concurrentes ejecutando `MigrateAsync` competirían por la tabla de historial de migraciones.
2. La propiedad del esquema debe tener un único dueño: el host web.

En su lugar, `Ludeka.Jobs` hace una **comprobación de solo lectura** al arrancar: si `Database.GetPendingMigrationsAsync()` devuelve algo en PostgreSQL, sale con **código 3** y un mensaje que dice que el esquema está atrasado. Un trabajo que corre contra un esquema viejo es peor que un trabajo que no corre.

### 4.6. Prueba de humo de composición — la red de seguridad de R2

Prueba obligatoria en R2, y la que ata todos los cabos de §4:

```
Dado un IServiceCollection al que solo se le aplica AddLudekaApplicationCore(configuration)
  más los registros mínimos de un host sin web (ILogger, DenyAllSessionPermissionGuard)
Cuando se construye el IServiceProvider con validateScopes: true y validateOnBuild: true
Entonces se resuelven sin excepción, dentro de un ámbito:
  INightlyCatalogingService, IPriceRadarService, ISocialCollectorService,
  ICommunityNotificationService, ICommunityNotificationQueue, INotificationOutboxRepository,
  IJobExecutionCoordinator
Y IEnumerable<ISocialChannelCollector> devuelve 4 elementos en el orden
  YouTube, Telegram, RSS, Instagram
Y IEnumerable<IStoreStockClient> devuelve 2 elementos, Simulation antes de HtmlSchema
Y el conjunto de IHostedService resueltos está vacío
```

`validateOnBuild: true` convierte cada dependencia ausente en un fallo de construcción con el nombre del servicio, que es exactamente el diagnóstico que hace falta para el riesgo 2 y para C3.

---

## 5. Decisión D2 — Esquema del outbox (rebanada R3)

**Elección:** tabla nueva `NotificationOutboxMessages` para el mensaje lógico; la tabla existente `NotificationLogs` pasa a ser la tabla de **sub-entregas por canal**, con tres columnas nuevas y `MessageId` anulable.
**Alternativas consideradas:** (a) `CommunityNotificationLog` se convierte en la tabla de mensajes y se crea una tabla nueva de sub-entregas; (b) dos tablas enteramente nuevas y `CommunityNotificationLog` se conserva como bitácora histórica congelada.
**Razón:** `CommunityNotificationLog` **ya es por canal** —tiene columna `Channel` y hoy se crea una fila por canal, en `CommunityNotificationService.cs:108` (Discord) y `:157` (Telegram)—, ya tiene `Status`, `ErrorDetails`, `CreatedAt` y `SentAt`, y ya tiene los tres índices que la sub-entrega necesita (`LudekaDbContext.cs:283-285`). Encajarla como tabla de mensajes (a) exigiría mover o reinterpretar el valor de `Channel` de cada fila de producción. Congelarla (b) partiría el historial en dos fuentes y dejaría el panel mostrando media película. La opción elegida **no mueve ni un dato** y deja el camino de lectura del panel funcionando sobre la misma tabla.

### 5.1. Destino explícito de `CommunityNotificationLog`

| Aspecto | Resolución |
|---|---|
| **Rol nuevo** | Tabla de **sub-entregas por canal** del outbox. Tabla física: sigue siendo `NotificationLogs` (`LudekaDbContext.cs:281`) |
| **Nombre de entidad y tabla** | **No se renombran.** El nombre queda descriptivamente corto, y se declara así en lugar de pagar un renombrado de entidad, tabla, `DbSet`, migración y ~4 ficheros de prueba por cero valor funcional |
| **Datos de producción** | **Intactos, sin retrodocumentación (*backfill*).** Las filas históricas quedan con `MessageId = NULL`, `Attempts = 0`, `NextAttemptAt = NULL` |
| **Reclamabilidad de lo histórico** | Ninguna. El predicado de reclamación opera sobre `NotificationOutboxMessages`, no sobre `NotificationLogs`; una fila con `MessageId = NULL` no pertenece a ningún mensaje y nunca se despacha |
| **Camino de lectura del panel** | `SqliteCommunityNotificationRepository.GetRecentLogsAsync` (`:22-34`) → `ICommunityNotificationService.GetHistoryAsync` (`:200-217`) → `CommunityNotificationLogDto` → `AdminNotifications.razor`. **Sin cambios.** Lo histórico y las sub-entregas nuevas aparecen en la misma lista ordenada por `CreatedAt`, que es lo que un operador quiere ver |
| `.ToListAsync()` + orden en memoria | **Se conserva tal cual.** La propuesta §2.2 lo declara fuera de alcance y la reclamación ya no pasa por este método, de modo que su patrón ineficiente no contamina el camino crítico. Se registra como deuda conocida, no se arregla aquí |
| `RetryFailedNotificationAsync(Guid logId)` | **Sigue funcionando** sobre la fila de sub-entrega, sin cambios de firma. Nota honesta: ya hoy pierde `Fields` (C1) y **este diseño no lo arregla**; con `MessageId` poblado la mejora natural es releer el mensaje y reprogramar la sub-entrega, y queda apuntada como trabajo posterior |
| Escrituras directas (panel, *ping* de prueba, reintento manual) | Siguen creando su fila con `MessageId = NULL`, exactamente como hoy. Una tabla, dos procedencias, ambas visibles |

### 5.2. Entidades

```csharp
// src/Ludeka.Core/Enums/OutboxMessageStatus.cs (NUEVO)
public enum OutboxMessageStatus { Pending = 0, Completed = 1, Dead = 2 }
```

Enum nuevo en lugar de reutilizar `NotificationStatus` (`Queued`/`Sent`/`Failed`/`DryRun`): ese enum describe el resultado de **una entrega por un canal** y sus valores están persistidos en filas de producción. El estado de un **mensaje** es otra cosa. La sub-entrega sigue usando `NotificationStatus` sin cambios.

```csharp
// src/Ludeka.Core/Entities/NotificationOutboxMessage.cs (NUEVO)
public class NotificationOutboxMessage
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public NotificationEventType EventType { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public string? TargetUrl { get; private set; }
    public string? ImageUrl { get; private set; }

    /// <summary>Campos del mensaje serializados. Sin esta columna, cada notificación que
    /// sobreviva a un reinicio llegaría sin los <c>fields</c> del embed de Discord
    /// (DiscordWebhookClient.cs:64-68) ni las líneas de Telegram (TelegramBotClient.cs:107-113). Ver C1.</summary>
    public string FieldsJson { get; private set; } = "{}";

    /// <summary>Canal exclusivo solicitado al encolar, o <c>null</c> para difusión.
    /// NUNCA se resuelve el fan-out aquí: lo decide el despachador releyendo las opciones (decisión 4).</summary>
    public NotificationChannel? TargetChannel { get; private set; }

    public OutboxMessageStatus Status { get; private set; } = OutboxMessageStatus.Pending;
    public int Attempts { get; private set; }

    /// <summary>Momento a partir del cual el mensaje es reclamable. La reclamación lo desplaza
    /// al futuro (concesión temporal): es lo que hace invisible el mensaje a otros despachadores
    /// durante el envío y lo que devuelve a la cola una ejecución que muriese a mitad.</summary>
    public DateTimeOffset NextAttemptAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; private set; }

    // Observabilidad de la concesión. NO forman parte del predicado de reclamación.
    public DateTimeOffset? ClaimedAt { get; private set; }
    public string? ClaimedBy { get; private set; }

    public string? LastError { get; private set; }
}
```

```csharp
// src/Ludeka.Core/Entities/CommunityNotificationLog.cs (MODIFICADO — 3 columnas nuevas)
public Guid? MessageId { get; private set; }            // NULL en lo histórico y en envíos directos
public int Attempts { get; private set; }               // intentos de entrega de ESTE canal
public DateTimeOffset? NextAttemptAt { get; private set; }
```

Y tres métodos de dominio nuevos, en la línea de los tres existentes (`MarkAsSent:53`, `MarkAsFailed:60`, `MarkAsDryRun:69`):

```csharp
public static CommunityNotificationLog ForDelivery(Guid messageId, NotificationEventType eventType,
    NotificationChannel channel, string title, string summary, string? targetUrl, string? imageUrl);
public void RegisterFailedAttempt(string error, DateTimeOffset nextAttemptAt);   // Attempts++, Status=Queued
public void MarkAsPermanentlyFailed(string error);                              // Status=Failed, terminal
```

### 5.3. Configuración EF Core e índices

```csharp
// src/Ludeka.Infrastructure/Data/LudekaDbContext.cs
public DbSet<NotificationOutboxMessage> NotificationOutboxMessages => Set<NotificationOutboxMessage>();

var outbox = modelBuilder.Entity<NotificationOutboxMessage>();
outbox.ToTable("NotificationOutboxMessages");
outbox.HasKey(m => m.Id);
outbox.Property(m => m.Title).IsRequired().HasMaxLength(250);
outbox.Property(m => m.Summary).IsRequired();
outbox.Property(m => m.FieldsJson).IsRequired();
outbox.Property(m => m.ClaimedBy).HasMaxLength(128);

// Índice de reclamación: cubre el filtro y la ordenación completos de la sentencia de §6.3
// (WHERE Status = 0 AND NextAttemptAt <= now ORDER BY NextAttemptAt, CreatedAt).
outbox.HasIndex(m => new { m.Status, m.NextAttemptAt, m.CreatedAt });

// Índice del chequeo de salud: COUNT(*) y MIN(CreatedAt) sobre Status = Pending (§11).
outbox.HasIndex(m => new { m.Status, m.CreatedAt });

// NotificationLogs: los tres índices existentes de :283-285 NO se tocan.
// La reclamación ya no escanea esta tabla, así que Status, Channel y CreatedAt vuelven a servir
// exactamente lo que servían: filtrado del panel y lectura por antigüedad.
notificationLog.HasIndex(n => new { n.MessageId, n.Channel }).IsUnique();
```

El `UNIQUE (MessageId, Channel)` es el mecanismo de idempotencia de la creación de sub-entregas: un mensaje reclamado por segunda vez vuelve a derivar su conjunto de canales y no puede duplicar una sub-entrega existente.

**Supuesto a fijar con prueba:** en PostgreSQL y en SQLite los `NULL` se consideran distintos en un índice único, de modo que todas las filas históricas con `MessageId = NULL` conviven sin violar la restricción. Es comportamiento estándar en ambos motores, pero **no se ha ejecutado**: lo demuestra el escenario «Reconciliación de una base SQLite ya existente en disco» sembrando varias filas históricas antes de aplicar la restricción.

### 5.4. Opciones — sección `Outbox`

Sin colisión (verificado: `"Outbox"` no aparece en `src/Ludeka.Web/appsettings.json`). `Enabled` como primera propiedad, siguiendo el patrón del almacén (`appsettings.json:118-119`, `CommunityNotificationOptions.cs:7`).

```json
"Outbox": {
  "Enabled": true,
  "BatchSize": 20,
  "MaxDeliveryAttempts": 5,
  "MaxClaimAttempts": 10,
  "LeaseSeconds": 300,
  "RetryBackoffSeconds": 60,
  "RetryBackoffMultiplier": 4,
  "HealthPendingDepthDegraded": 100,
  "HealthOldestPendingDegradedMinutes": 30,
  "HealthQueryTimeoutSeconds": 2
}
```

`OutboxOptions.SectionName = "Outbox"`, en `src/Ludeka.Application/Options/OutboxOptions.cs`.

Retroceso exponencial acotado: `NextAttemptAt = ahora + RetryBackoffSeconds × RetryBackoffMultiplier^(Attempts-1)`, con techo en `LeaseSeconds × 12`.

---

## 6. Decisión D3 — Contrato de reclamación frente a `ICommunityNotificationQueue`

### 6.1. Forma exacta de los contratos

**Elección:** `ICommunityNotificationQueue` se reduce a `EnqueueAsync`; `ReadAllAsync` **desaparece de la interfaz** y sobrevive como método público de la clase concreta; la reclamación vive en un contrato nuevo y separado.
**Alternativas consideradas:** (a) conservar `ReadAllAsync` en la interfaz y lanzar `NotSupportedException` en la implementación de outbox; (b) conservarla y que la implementación de outbox devuelva un `IAsyncEnumerable` que sondee la base de datos.
**Razón:** (a) viola la segregación de interfaces y deja una bomba de relojería en tiempo de ejecución; (b) reintroduce precisamente el flujo infinito que el incremento existe para eliminar —`CommunityNotificationDispatcherHostedService.cs:43` consume `ReadAllAsync` como `IAsyncEnumerable` infinito— y es incompatible con un proceso de vida corta.

```csharp
// src/Ludeka.Application/Contracts/ICommunityNotificationQueue.cs (MODIFICADO)
public interface ICommunityNotificationQueue
{
    // Firma IDÉNTICA a la actual (:10). Los dos productores no se tocan:
    // FoundingVerdictService.cs:217 y RuleQAService.cs:199.
    ValueTask EnqueueAsync(CommunityNotificationMessage message, CancellationToken ct = default);
    // ReadAllAsync (:11) se retira de la interfaz.
}
```

```csharp
// src/Ludeka.Application/Contracts/INotificationOutboxRepository.cs (NUEVO)
public interface INotificationOutboxRepository
{
    Task EnqueueAsync(NotificationOutboxMessage message, CancellationToken ct = default);

    /// <summary>Reclama hasta <paramref name="batchSize"/> mensajes de forma exclusiva y atómica.
    /// En PostgreSQL con FOR UPDATE SKIP LOCKED; en SQLite en modo degradado (§6.4).
    /// La sentencia desplaza NextAttemptAt al futuro: el lote devuelto es invisible para
    /// cualquier otro despachador durante la concesión.</summary>
    Task<IReadOnlyList<OutboxClaim>> ClaimPendingAsync(int batchSize, string claimedBy,
        TimeSpan lease, CancellationToken ct = default);

    Task<IReadOnlyList<CommunityNotificationLog>> GetDeliveriesAsync(Guid messageId, CancellationToken ct = default);
    Task<CommunityNotificationLog> EnsureDeliveryAsync(Guid messageId, NotificationChannel channel,
        OutboxClaim claim, CancellationToken ct = default);   // idempotente vía UNIQUE(MessageId, Channel)

    Task CompleteMessageAsync(Guid messageId, CancellationToken ct = default);
    Task ReleaseMessageAsync(Guid messageId, DateTimeOffset nextAttemptAt, string? lastError,
        CancellationToken ct = default);
    Task MarkMessageDeadAsync(Guid messageId, string reason, CancellationToken ct = default);

    Task<OutboxHealthSnapshot> GetHealthSnapshotAsync(CancellationToken ct = default);
}

// src/Ludeka.Application/DTOs — proyección de la reclamación y muestra de salud
public sealed record OutboxClaim(Guid Id, NotificationEventType EventType, string Title, string Summary,
    string? TargetUrl, string? ImageUrl, string FieldsJson, NotificationChannel? TargetChannel,
    int Attempts, DateTimeOffset CreatedAt);

public sealed record OutboxHealthSnapshot(int PendingCount, DateTimeOffset? OldestPendingAt,
    int DeadCount, string Provider);
```

```csharp
// src/Ludeka.Application/Contracts/INotificationOutboxDispatcher.cs (NUEVO)
public interface INotificationOutboxDispatcher
{
    /// <summary>Un ciclo acotado: reclama un lote, deriva los canales de CADA mensaje releyendo
    /// las opciones, asegura la sub-entrega por canal e intenta el envío. No entra en ningún bucle.</summary>
    Task<OutboxDispatchResultDto> DispatchPendingAsync(CancellationToken ct = default);
}
```

**Destino de `ReadAllAsync` e `InMemoryCommunityNotificationQueue`:** la clase se conserva íntegra para desarrollo local y pruebas, con `EnqueueAsync` implementando la interfaz y `ReadAllAsync` como método público sin contrato. Detalle verificado que lo hace gratis: la prueba existente `tests/Ludeka.UnitTests/Infrastructure/CommunityNotificationQueueTests.cs:15` tipa la variable como la **clase concreta** (`new InMemoryCommunityNotificationQueue(capacity: 10)`) y llama a `queue.ReadAllAsync` en `:28`, de modo que **compila y pasa sin ningún cambio** tras retirar el método de la interfaz.

### 6.2. Implementación de `ICommunityNotificationQueue` para el outbox

```csharp
// src/Ludeka.Infrastructure/Notifications/OutboxCommunityNotificationQueue.cs (NUEVO)
public sealed class OutboxCommunityNotificationQueue : ICommunityNotificationQueue
{
    // Ámbito (Scoped), no Singleton: consume el LudekaDbContext con ámbito a través del repositorio.
    // Program.cs:241 pasa de AddSingleton a AddScoped. Un singleton que capture un DbContext con
    // ámbito es la dependencia cautiva clásica, y validateScopes de §4.6 la detecta.
    public async ValueTask EnqueueAsync(CommunityNotificationMessage message, CancellationToken ct = default)
    {
        try { /* … mapear Fields → FieldsJson, TargetChannel, e insertar … */ }
        catch (Exception ex)
        {
            // Respuesta a C2: los dos productores tragan la excepción en su catch vacío
            // (FoundingVerdictService.cs:219-222, RuleQAService.cs:201-204). Aquí queda el rastro.
            _logger.LogError(ex, "Fallo al persistir la notificación en el outbox: {Title}", message.Title);
            throw;
        }
    }
}
```

`Program.cs:241` pasa a `AddScoped<ICommunityNotificationQueue, OutboxCommunityNotificationQueue>()`; `InMemoryCommunityNotificationQueue` queda registrada solo cuando la configuración lo pide para desarrollo local. Compatibilidad de tiempos de vida: los dos productores son `AddScoped` (`Program.cs:149`, `:223`), así que ámbito consumiendo ámbito es correcto.

### 6.3. SQL de reclamación (PostgreSQL)

```sql
WITH reclamados AS (
    SELECT "Id"
      FROM "NotificationOutboxMessages"
     WHERE "Status" = 0                      -- Pending
       AND "NextAttemptAt" <= @ahora
     ORDER BY "NextAttemptAt", "CreatedAt"
     LIMIT @tamanoLote
       FOR UPDATE SKIP LOCKED
)
UPDATE "NotificationOutboxMessages" AS m
   SET "Attempts"      = m."Attempts" + 1,
       "NextAttemptAt" = @ahora + @concesion,
       "ClaimedAt"     = @ahora,
       "ClaimedBy"     = @reclamadoPor
  FROM reclamados r
 WHERE m."Id" = r."Id"
RETURNING m."Id", m."EventType", m."Title", m."Summary", m."TargetUrl",
          m."ImageUrl", m."FieldsJson", m."TargetChannel", m."Attempts", m."CreatedAt";
```

Cuatro propiedades que hacen que esta sentencia sea correcta, y cada una es una decisión:

1. **Es una sola sentencia.** El bloqueo de fila que `FOR UPDATE SKIP LOCKED` adquiere solo vive dentro de una transacción; al ser una única sentencia, su transacción implícita basta y **no hace falta abrir ninguna transacción explícita**. Esto es lo que permite que la llamada HTTP a Discord o Telegram ocurra después, sin ningún lock abierto sobre la base de datos. Reclamar en una transacción y enviar dentro de ella sería el error clásico: un *timeout* de webhook mantendría filas bloqueadas durante minutos.
2. **`SKIP LOCKED` salta las filas que otro reclamante está bloqueando en ese instante**, en lugar de esperarlas. Es lo que hace que la intersección entre dos lotes concurrentes sea vacía.
3. **La propiedad durante el envío no la da el lock, la da la concesión.** El `UPDATE` desplaza `NextAttemptAt` a `ahora + concesion`, de modo que el mensaje queda invisible para el predicado de reclamación de cualquier otro despachador. **Esto resuelve la fila reclamada y huérfana sin ningún proceso segador:** si la ejecución muere a mitad de envío, la concesión caduca por sí sola y el mensaje vuelve a ser reclamable.
4. **No hay estado `Claimed`.** Se consideró y se descartó: exigiría un segundo campo en el predicado, un segador que devolviese `Claimed` caducado a `Pending`, y una reconciliación ante desajuste de relojes. `ClaimedAt` y `ClaimedBy` se conservan **solo como observabilidad** y no participan del predicado.

Consecuencia declarada de (3): el outbox es **al-menos-una-vez**, no exactamente-una-vez. Si el envío a Discord tiene éxito y el proceso muere antes de marcar la sub-entrega, un despachador posterior reintentará ese canal y la comunidad verá el mensaje dos veces. Es el mismo compromiso que ya asumía el documento de incremento en su §5 («El outbox puede reenviar un mensaje si falla entre envío y marcado»), y ningún escenario de la especificación exige exactamente-una-vez. La mitigación barata que sí incorpora el diseño: marcar la sub-entrega **inmediatamente después** de la respuesta del webhook, sin trabajo intermedio, reduciendo la ventana a una sola escritura.

### 6.4. Cómo se ejecuta desde EF Core

EF Core no traduce `FOR UPDATE SKIP LOCKED` desde LINQ y `UPDATE … RETURNING` no es un `SELECT` componible, así que ni `FromSql*` ni `SqlQuery*` son la herramienta: las dos canalizaciones envuelven la consulta para poder componerla.

**Elección:** comando ADO.NET crudo y **parametrizado** sobre la conexión del propio `LudekaDbContext`.
**Alternativas consideradas:** (a) `DbSet.FromSqlRaw`; (b) `Database.SqlQueryRaw<T>`; (c) `Database.ExecuteSqlRaw` (no devuelve filas).
**Razón:** además de la incompatibilidad de composición, este repositorio **ya tiene precedente probado** de ADO.NET crudo sobre la conexión del contexto: `SqliteSchemaMigrator.cs:19-40` y `:887-898`. Se usa el patrón que el proyecto ya usa, no un mecanismo nuevo.

```csharp
// src/Ludeka.Infrastructure/Repositories/NotificationOutboxRepository.cs (NUEVO)
// Reclamación: comando parametrizado sobre db.Database.GetDbConnection(), mismo patrón que
// SqliteSchemaMigrator.cs:19-40. Nunca interpolación de cadenas: solo DbParameter.
// Todo lo demás (asegurar sub-entregas, marcar estados, completar) usa el rastreador de cambios
// de EF Core con la API normal: es LINQ traducible y no necesita SQL crudo.
```

**Rama por proveedor**, siguiendo el patrón dual ya establecido en `Program.cs:85-101` y `:365-376`:

| Proveedor | Reclamación | Garantía |
|---|---|---|
| `Database.IsNpgsql()` | La sentencia de §6.3 completa | Exclusión real entre procesos concurrentes |
| `Database.IsSqlite()` | `UPDATE … WHERE "Id" IN (SELECT "Id" … ORDER BY … LIMIT n) RETURNING …`, sin cláusula de bloqueo | **Modo degradado.** SQLite serializa a los escritores, de modo que es correcto para el uso de un solo escritor en desarrollo, y **no es** la garantía de concurrencia |

Eso es exactamente por qué el criterio de reclamación exclusiva se verifica **solo** contra PostgreSQL real, tal y como la especificación ya establece (`specs/notification-outbox/spec.md:48`).

### 6.5. El fan-out por canal se resuelve al despachar — mecanismo

Este es el corazón de la decisión 4 y merece detallarse, porque es donde un diseño descuidado reintroduce la pérdida silenciosa.

```
Para cada mensaje del lote reclamado:
  1. Derivar el conjunto de canales AHORA, releyendo CommunityNotificationOptions:
       si !Enabled                      → conjunto vacío   (CommunityNotificationService.cs:66)
       si TargetChannel = Discord  y DiscordEnabled   → { Discord }    (:77)
       si TargetChannel = Telegram y TelegramEnabled  → { Telegram }   (:81)
       si TargetChannel = null     → { Discord si DiscordEnabled }     (:89)
                                   ∪ { Telegram si TelegramEnabled }   (:94)
  2. Conjunto vacío → liberar el mensaje con retroceso exponencial, sin marcarlo Completed.
     Al agotar MaxClaimAttempts → Dead, con motivo. Ver más abajo.
  3. Para cada canal del conjunto: EnsureDeliveryAsync(MessageId, canal, claim)
       — idempotente por UNIQUE(MessageId, Channel): si la sub-entrega ya existe se reutiliza.
  4. Para cada sub-entrega NO terminal: enviar, y registrar el resultado de inmediato.
  5. Completed cuando ninguna sub-entrega del conjunto derivado queda en estado no terminal.
```

**La propiedad que esto preserva, y que el maintainer identificó como decisiva:** un mensaje encolado con Telegram deshabilitado **no** queda con Telegram cerrado para siempre. En la reclamación siguiente se vuelve a derivar el conjunto; si Telegram ya está habilitado, no existe sub-entrega de Telegram para ese mensaje, `EnsureDeliveryAsync` la crea y se entrega. Fijar los canales en `EnqueueAsync` habría cerrado esa puerta en silencio, que es literalmente lo que la decisión 4 descartó.

Consecuencia declarada, y compromiso consciente: la ventana de «se habilita después» está acotada por `MaxClaimAttempts × retroceso`, no es infinita. Al agotarla el mensaje pasa a `Dead` con su motivo. **Sigue siendo estrictamente mejor que hoy**, donde `BroadcastAsync` con `Enabled = false` devuelve `[]` y descarta el mensaje al instante y sin rastro (`CommunityNotificationService.cs:66-70`).

### 6.6. Refactorización de `CommunityNotificationService`: separar enviar de registrar

Hoy la creación de la fila y el envío están fundidos en `SendToDiscordAsync` (`:108-148`) y `SendToTelegramAsync` (`:157-197`). El despachador necesita enviar **sobre una sub-entrega que ya existe**, sin crear una segunda fila. La refactorización mínima:

```csharp
// PÚBLICO, SIN CAMBIOS de firma ni de comportamiento: crea su fila (MessageId = NULL),
// envía y registra. Lo usan el panel, el ping de prueba y BroadcastAsync.
Task<NotificationDispatchResult> SendToDiscordAsync(CommunityNotificationMessage m, CancellationToken ct);
Task<NotificationDispatchResult> SendToTelegramAsync(CommunityNotificationMessage m, CancellationToken ct);

// NUEVO, exclusivo del despachador de outbox: envía por delivery.Channel y actualiza
// ESA fila (Attempts, Status, NextAttemptAt). No crea ninguna fila.
Task<NotificationDispatchResult> DeliverAsync(CommunityNotificationLog delivery,
    CommunityNotificationMessage message, CancellationToken ct = default);

// PRIVADO: la mecánica pura de envío, sin ninguna persistencia. Los tres de arriba delegan aquí.
private Task<NotificationDispatchResult> SendOnChannelAsync(NotificationChannel channel,
    CommunityNotificationMessage message, CancellationToken ct);
```

Que los métodos públicos existentes conserven firma y comportamiento es deliberado: las pruebas de `tests/Ludeka.UnitTests/Application/CommunityNotificationServiceTests.cs` siguen siendo válidas y el panel no cambia. La duplicación queda contenida en `SendOnChannelAsync`.

---

## 7. Decisión D4 — Clave de ventana e idempotencia (rebanada R5)

**Elección:** tabla genérica nueva `JobExecutionLeases` con `UNIQUE (JobName, WindowKey)`, compartida por los cuatro trabajos, y adquisición por **`INSERT` primero**.
**Alternativas consideradas:** (a) añadir `WindowKey` + `UNIQUE` a `NightlyCatalogingExecutionLog` y replicar el patrón en tablas por trabajo; (b) una tabla de bloqueos con `SELECT` previo y `INSERT` después.
**Razón:** (a) es lo que la propuesta insinuaba, pero solo el trabajo nocturno tiene hoy tabla de bitácora: los otros tres no tienen ninguna, así que exigiría tres tablas nuevas y cuatro mecanismos distintos, en contra del requisito de definir el mecanismo para los cuatro. (b) es el antipatrón exacto que la especificación prohíbe: entre el `SELECT` y el `INSERT` hay una condición de carrera, y la decisión la tomaría un `if`, no la base de datos.

### 7.1. Entidad y restricción

```csharp
// src/Ludeka.Core/Entities/JobExecutionLease.cs (NUEVO)
public class JobExecutionLease
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string JobName { get; private set; } = string.Empty;   // máx. 64
    public string WindowKey { get; private set; } = string.Empty;  // máx. 32
    public string Status { get; private set; } = "Running";        // Running | Completed | Failed
    public DateTimeOffset StartedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset HeartbeatAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; private set; }
    public int ProcessedCount { get; private set; }
    public int FailedCount { get; private set; }
    public long? DurationMs { get; private set; }
    public string? HostIdentifier { get; private set; }            // máx. 128
    public string? ErrorMessage { get; private set; }
}
```

`Status` es `string`, no enum, por coherencia con `NightlyCatalogingExecutionLog.Status` (`:23`, configurado como `IsRequired().HasMaxLength(50)` en `LudekaDbContext.cs:404`). Se sigue la convención del almacén en lugar de introducir una distinta.

```csharp
var lease = modelBuilder.Entity<JobExecutionLease>();
lease.ToTable("JobExecutionLeases");
lease.HasKey(l => l.Id);
lease.Property(l => l.JobName).IsRequired().HasMaxLength(64);
lease.Property(l => l.WindowKey).IsRequired().HasMaxLength(32);
lease.Property(l => l.Status).IsRequired().HasMaxLength(20);
lease.Property(l => l.HostIdentifier).HasMaxLength(128);

// LA restricción. Es lo que hace que un reintento de Cloud Scheduler choque contra la base
// de datos y no contra un if en memoria.
lease.HasIndex(l => new { l.JobName, l.WindowKey }).IsUnique();

// Lectura operativa: últimas ejecuciones de un trabajo.
lease.HasIndex(l => new { l.JobName, l.StartedAt });
```

**`NightlyCatalogingExecutionLog` no cambia de esquema.** Conserva su papel de bitácora rica y específica del nocturno (`CatalogedTitlesJson`, los cinco contadores), la lee `CatalogQueueAdmin.razor`, y su índice `HasIndex(l => l.StartedAt)` (`LudekaDbContext.cs:403`) **se queda no único, tal cual**. Las métricas mínimas por ejecución que exige la especificación las cubre `JobExecutionLease` de forma **uniforme para los cuatro trabajos** (`StartedAt`, `CompletedAt`, `ProcessedCount`, `FailedCount`, `DurationMs`), no solo para el nocturno. Esto **reduce** la superficie de esquema de R3 respecto a la propuesta §2.1, punto 3; se reporta en §14.

### 7.2. Forma de la clave de ventana

**Huso horario: UTC, sin excepciones.** Evidencia de que es la convención del proyecto: cada marca de tiempo del dominio usa `DateTimeOffset.UtcNow` (`CommunityNotificationLog.cs:17,45`, `NightlyCatalogingExecutionLog.cs:14`, `NightlyCatalogingHostedService.cs:54`) y la opción del nocturno se llama literalmente `ExecutionHourUtc` (`:57`).

Razón técnica, no solo de coherencia: con `Europe/Madrid` una ventana local de 02:00-03:00 **ocurre dos veces** el domingo de octubre y **ninguna** el de marzo. En el primer caso la restricción `UNIQUE` rechazaría una ejecución legítima; en el segundo se perdería una ventana entera. UTC no tiene ese agujero.

| Trabajo | `JobName` | Granularidad | Forma de `WindowKey` | Ejemplo |
|---|---|---|---|---|
| Lote nocturno | `nightly-cataloging` | Diaria | `yyyy-MM-dd` | `2026-09-18` |
| Radar de precios | `price-radar` | Bloque de `PriceRadar:CheckIntervalHours` h | `yyyy-MM-ddTHH` del inicio del bloque | `2026-09-18T12` |
| Recolector social | `social-collector` | Bloque de `SocialCollector:IntervalMinutes` min | `yyyy-MM-ddTHH:mm` del inicio del bloque | `2026-09-18T14:00` |
| Despachador de outbox | `notification-outbox` | Segundo | `yyyy-MM-ddTHH:mm:ss` | `2026-09-18T14:03:07` |

**La granularidad reutiliza las opciones existentes, no crea duplicados.** `PriceRadarOptions.CheckIntervalHours` (por defecto 6, `PriceRadarOptions.cs:18`) y `SocialCollectorOptions.IntervalMinutes` (por defecto 120, `appsettings.json:120`) ya expresan «cada cuánto debe ejecutarse esto», y la cadencia de Cloud Scheduler tiene que coincidir con ellas de todos modos. Una segunda copia del valor en la sección `Workers` solo invitaría a la divergencia.

**Inicio de bloque anclado al epoch Unix**, no al arranque del proceso:
`inicioBloque = UnixEpoch + TimeSpan.FromHours(Math.Floor((ahora - UnixEpoch).TotalHours / N) × N)`.
Así las fronteras de ventana son estables entre ejecuciones y entre instancias, que es la propiedad que la restricción `UNIQUE` necesita para significar algo. Sin el ancla, dos instancias arrancadas con un minuto de diferencia calcularían ventanas distintas para el mismo instante y las dos ejecutarían.

**Por qué el outbox tiene ventana de un segundo y no de cinco minutos.** El outbox no es un trabajo con ventana: es un drenaje. Ejecutarlo dos veces en el mismo minuto **no** es trabajo duplicado —su garantía de concurrencia es la reclamación de §6.3, y dos ejecuciones simultáneas se reparten lotes disjuntos—. Imponerle una ventana de 5 minutos **reduciría** la disponibilidad: un reintento de Scheduler se negaría a drenar sin ningún beneficio. Con granularidad de un segundo, dos disparos en el mismo segundo son un disparo genuinamente duplicado y rechazar uno es correcto, mientras que dos disparos separados por un minuto ambos drenan. Se obtiene así **mecánica uniforme para los cuatro trabajos** —todos escriben su fila de concesión, todos tienen `WindowKey`, la restricción `UNIQUE` siempre aplica— sin ningún caso especial en el código.

Coste declarado: a razón de un drenaje cada 5 minutos, `JobExecutionLeases` crece ~288 filas al día. **No hay política de purga en este alcance**; se registra como deuda operativa con una recomendación de retención (90 días) para la documentación de despliegue.

### 7.3. Adquisición: `INSERT` primero, y qué pasa con la fila huérfana

```
TryAcquireAsync(jobName, windowKey):
  INSERT INTO "JobExecutionLeases" (Id, JobName, WindowKey, Status, StartedAt, HeartbeatAt, HostIdentifier)
  VALUES (@id, @job, @window, 'Running', @ahora, @ahora, @host);

  Éxito                      → Acquired
  Violación de unicidad      → leer la fila existente y decidir:
```

| Estado de la fila existente | Decisión | Código de salida |
|---|---|---|
| `Completed` | `AlreadyCompleted`: no hay nada que hacer | **0** (la especificación lo exige: éxito «incluido el caso en que la ventana ya estaba completada») |
| `Running` con `HeartbeatAt >= ahora − StaleLeaseMinutes` | `HeldByLiveExecution`: otra ejecución la tiene, viva | **0** — un poseedor sano no es un fallo observable |
| `Running` con `HeartbeatAt < ahora − StaleLeaseMinutes` | **Toma de control** por intercambio condicional (abajo) | 0 o 1 según el resultado del trabajo |
| `Failed` | Retomable: el mismo intercambio condicional con `Status = 'Failed'` en el predicado | 0 o 1 según el resultado |

**Toma de control de una fila huérfana** —el caso de «arrancó y murió sin completar»— mediante un intercambio condicional (*compare-and-swap*), sin ningún bloqueo adicional:

```sql
UPDATE "JobExecutionLeases"
   SET "Status" = 'Running', "StartedAt" = @ahora, "HeartbeatAt" = @ahora,
       "HostIdentifier" = @host, "ErrorMessage" = @notaDeTomaDeControl
 WHERE "JobName" = @job AND "WindowKey" = @window
   AND "Status" = 'Running' AND "HeartbeatAt" < @umbralDeCaducidad;
-- filas afectadas = 1 → la concesión es mía. = 0 → otro se adelantó → salir con 0.
```

El número de filas afectadas es el árbitro. Dos instancias que intenten la toma de control a la vez producen un ganador y un perdedor, y el perdedor sale limpiamente con 0.

Retomar una `Failed` es deliberado: la especificación acota «como máximo una ejecución **exitosa** por ventana» (`specs/background-jobs-scheduling/spec.md:65`), de modo que un reintento tras un fallo **debe** poder ejecutar la ventana.

**Latido (*heartbeat*):** `HeartbeatAt` se actualiza en cada frontera de fase de la unidad de trabajo, no con un temporizador en un hilo aparte. En un proceso de vida corta, un hilo de fondo solo añadiría una tarea que hay que apagar ordenadamente; las fronteras de fase dan una resolución más que suficiente frente a un `StaleLeaseMinutes` por defecto de 60.

### 7.4. Coordinador: un solo sitio con esta lógica

```csharp
// src/Ludeka.Application/Contracts/IJobExecutionCoordinator.cs (NUEVO)
public interface IJobExecutionCoordinator
{
    Task<JobLeaseOutcome> ExecuteWithWindowLeaseAsync(string jobName, string windowKey,
        Func<IJobHeartbeat, CancellationToken, Task<JobWorkResult>> work, CancellationToken ct = default);
}

public enum JobLeaseOutcome { Completed, Failed, SkippedAlreadyCompleted, SkippedHeldByOther }
public sealed record JobWorkResult(int Processed, int Failed, string? Message);
public interface IJobHeartbeat { Task BeatAsync(CancellationToken ct = default); }
```

Implementación `JobExecutionCoordinator` en `src/Ludeka.Application/Features/Jobs/`, consumiendo `IJobExecutionLeaseRepository`. Los cuatro *runners* quedan finos: calculan su `WindowKey` e invocan al coordinador.

**Frontera de proveedor, importante:** clasificar una violación de unicidad es específico del proveedor —`PostgresException.SqlState == "23505"` en Npgsql, `SqliteException.SqliteErrorCode == 19` en SQLite—. Esa clasificación vive en **`Ludeka.Infrastructure`**: `IJobExecutionLeaseRepository.TryAcquireAsync` devuelve un `LeaseAcquisition` y **nunca propaga una excepción de proveedor**. `Ludeka.Application` se mantiene agnóstica, como corresponde a la arquitectura del proyecto.

### 7.5. Sección `Workers`

```json
"Workers": {
  "Enabled": true,
  "JobName": "",
  "JobTimeoutMinutes": 30,
  "StaleLeaseMinutes": 60,
  "RequirePostgreSqlInProduction": true
}
```

Sin colisión (verificado: `"Workers"` no aparece en `appsettings.json`), `Enabled` primero por convención. `Workers:Enabled = false` hace que todos los *runners* salgan con 0 sin reclamar ventana: es una parada de emergencia del lado del código, complementaria al paso 1 del plan de reversión (pausar los cuatro Cloud Scheduler).

**El interruptor por trabajo no se duplica.** Cada *runner* sigue consultando el `Enabled` de su propia sección —`NightlyCataloging:Enabled` (`NightlyCatalogingHostedService.cs:52`), `PriceRadar:Enabled` (`:50`), `SocialCollector:Enabled` (`:54`), `CommunityNotifications:Enabled` (`CommunityNotificationService.cs:66`)—, con la semántica intacta.

---

## 8. Decisión D5 — `src/Ludeka.Jobs` (rebanada R6)

### 8.1. Estructura del proyecto

```
src/Ludeka.Jobs/
├── Ludeka.Jobs.csproj          Microsoft.NET.Sdk · OutputType=Exe · net10.0 · Nullable · ImplicitUsings
├── Program.cs                  top-level: host, argumentos, guardas de arranque, código de salida
├── JobNames.cs                 constantes de los 4 nombres, única fuente de verdad
├── JobRunnerServiceCollectionExtensions.cs    AddLudekaJobRunners()
├── IJobRunner.cs
└── Runners/
    ├── NightlyCatalogingJobRunner.cs
    ├── PriceRadarJobRunner.cs
    ├── SocialCollectorJobRunner.cs
    └── NotificationOutboxJobRunner.cs
```

```xml
<!-- Configuración: se ENLAZA el appsettings del host web, no se duplica. Un solo fichero,
     una sola verdad. Sin esto, cada valor por defecto desaparecería en el proceso de trabajo
     (p. ej. CommunityNotifications:DryRun = true, appsettings.json). -->
<ItemGroup>
  <Content Include="..\Ludeka.Web\appsettings.json" Link="appsettings.json"
           CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

### 8.2. Contrato de los *runners*

```csharp
public interface IJobRunner
{
    string Name { get; }
    Task<JobLeaseOutcome> RunAsync(CancellationToken ct);
}
```

Registro `AddScoped` y selección por nombre con `GetServices<IJobRunner>().SingleOrDefault(r => r.Name == nombre)`, el mismo patrón de resolución múltiple que el proyecto ya usa para `ISocialChannelCollector` (`Program.cs:313-316`).

### 8.3. `Program.cs` de los trabajos

```csharp
// src/Ludeka.Jobs/Program.cs (NUEVO) — forma, no implementación
var builder = Host.CreateApplicationBuilder(args);   // host genérico, NO WebApplication

builder.Services.AddLudekaApplicationCore(builder.Configuration);
builder.Services.AddScoped<ISessionPermissionGuard, DenyAllSessionPermissionGuard>();  // §4.2
builder.Services.AddLudekaJobRunners();

using var host = builder.Build();

// Guarda de arranque: un trabajo contra la base de datos equivocada es peor que un trabajo caído. §8.6
if (StartupGuards.Evaluate(host.Services, builder.Configuration) is { } fallo)
{
    Console.Error.WriteLine(fallo);
    return 3;
}

using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(timeoutMinutes));
using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM,
    c => { c.Cancel = true; cts.Cancel(); });

using var scope = host.Services.CreateScope();
var runner = scope.ServiceProvider.GetServices<IJobRunner>()
                  .SingleOrDefault(r => r.Name == jobName);
if (runner is null) { /* listar nombres válidos en stderr */ return 2; }

var outcome = await runner.RunAsync(cts.Token);
return outcome is JobLeaseOutcome.Failed ? 1 : 0;

// NUNCA se llama a host.RunAsync(): es lo que garantiza «una unidad de trabajo por disparo»
// y «el proceso termina por sí mismo» (specs/background-jobs-scheduling/spec.md:142-152).
```

### 8.4. Análisis de argumentos y selección del trabajo

**Sin librería de análisis.** Superficie mínima, contrato explícito:

```
dotnet Ludeka.Jobs.dll <nombre-del-trabajo>
dotnet Ludeka.Jobs.dll --job=<nombre-del-trabajo>
```

Precedencia: argumento posicional → `--job=<nombre>` → `Workers:JobName` de configuración. El tercero honra la variable `Workers__JobName` que el documento de incremento §2.5 proponía, y cuesta dos líneas.

Nombres válidos, únicos y en minúsculas con guiones: `nightly-cataloging`, `price-radar`, `social-collector`, `notification-outbox`. Comparación **sensible a mayúsculas y exacta**: aceptar variantes ortográficas de un nombre de trabajo que decide qué se ejecuta en producción es una comodidad que no compensa el riesgo de ejecutar el trabajo equivocado por una mayúscula.

### 8.5. Contrato de código de salida

| Código | Significado |
|---|---|
| **0** | Unidad de trabajo completada; o ventana ya completada; o ventana en manos de otra ejecución viva |
| **1** | Fallo observable durante la unidad de trabajo: excepción no controlada, `FailedCount > 0` según el contrato del *runner*, o agotamiento del `JobTimeoutMinutes` |
| **2** | Error de invocación: nombre de trabajo ausente, desconocido o ambiguo |
| **3** | Error de arranque: guarda de §8.6 en rojo, esquema atrasado, o fallo de composición **antes** de la unidad de trabajo |

La especificación solo exige «distinto de cero ante fallo observable» y «cero en éxito» (`specs/background-jobs-scheduling/spec.md:124-140`). Distinguir 2 y 3 es un extra de coste nulo y valor operativo alto: un `2` es un error de configuración del Scheduler, un `3` es un error de entorno, y confundirlos en producción cuesta horas.

### 8.6. Guardas de arranque — el agujero silencioso que cierran

`Dockerfile:52` fija en la imagen `ConnectionStrings__DefaultConnection="Data Source=/app/data/ludeka.db"`, y en Cloud Run el secreto lo sobrescribe (`ci-cd.yml:112`). **Un Cloud Run Job al que se olvide dar el secreto `SUPABASE_DB_CONNECTION` heredaría la cadena SQLite de la imagen**, crearía un fichero vacío y efímero por ejecución, y **terminaría con éxito** habiendo procesado exactamente nada. Un resultado silenciosamente incorrecto es peor que una caída.

Dos guardas, las dos comprobables con prueba unitaria:

1. **Coherencia de proveedor.** Si `Workers:RequirePostgreSqlInProduction` está activo y `ASPNETCORE_ENVIRONMENT = Production`, la cadena resuelta **debe** ser PostgreSQL según `DatabaseOptions.IsPostgreSql` —el mismo método que ya decide el proveedor en `Program.cs:83`—. Si no lo es, salida **3** con un mensaje que nombra la variable que falta.
2. **Esquema al día.** Si `Database.GetPendingMigrationsAsync()` devuelve algo en PostgreSQL, salida **3**. El trabajo **no migra nunca** (§4.5).

### 8.7. Cambios exactos en `Dockerfile`

Estado actual verificado: tres etapas, `dotnet restore src/Ludeka.Web/Ludeka.Web.csproj` (`:29`), `dotnet publish Ludeka.Web.csproj -c Release -o /app/publish /p:UseAppHost=false` (`:40`), *runtime* `mcr.microsoft.com/dotnet/aspnet:10.0` (`:45`), usuario `app` sin privilegios (`:66`), `HEALTHCHECK` contra `/healthz` (`:78-79`), `ENTRYPOINT ["dotnet", "Ludeka.Web.dll"]` (`:81`).

| # | Cambio | Ubicación |
|---|---|---|
| 1 | `COPY src/Ludeka.Jobs/Ludeka.Jobs.csproj src/Ludeka.Jobs/` | tras `:26`, con los otros cuatro `COPY` de `.csproj` |
| 2 | `RUN dotnet restore src/Ludeka.Jobs/Ludeka.Jobs.csproj` | tras `:29`. **No** se sustituye por `dotnet restore Ludeka.sln`: la etapa solo copia los `.csproj` de `src/`, y la solución incluye los proyectos de prueba, que faltarían |
| 3 | `RUN dotnet publish /src/src/Ludeka.Jobs/Ludeka.Jobs.csproj -c Release -o /app/publish /p:UseAppHost=false` | tras `:40`. **Ruta absoluta**: `:39` deja el `WORKDIR` en `/src/src/Ludeka.Web` |
| 4 | `ENTRYPOINT` — **sin cambios** | `:81` intacto |

**Que el `ENTRYPOINT` no cambie es la decisión, no un olvido.** Es exactamente el mecanismo de la decisión 5: el servicio web arranca con el `ENTRYPOINT` por defecto y el Cloud Run Job **sobrescribe el comando del contenedor**. Una sola imagen, dos usos, cero lógica de despacho de modo dentro del artefacto. Y satisface literalmente el requisito de `dockerfile-build`, «seleccionado en tiempo de arranque del contenedor —no en tiempo de compilación de la imagen—».

Los dos proyectos publican en el mismo `/app/publish`, de forma secuencial. Los ensamblados compartidos (`Ludeka.Core/Application/Infrastructure.dll`) y el `appsettings.json` enlazado son idénticos en ambas publicaciones, así que la segunda los reescribe con los mismos bytes. **Se declara como detalle de construcción a confirmar en `sdd-apply`**, no como hecho verificado: esta fase no ha ejecutado `docker build`.

Verificación manual local (la especificación ya clasifica esto como manual/mixto):
```
docker run --rm --no-healthcheck --entrypoint dotnet ludeka:ci Ludeka.Jobs.dll nightly-cataloging
```
`--no-healthcheck` es necesario porque la sonda de `:78-79` es una característica de Docker: Cloud Run Jobs no la evalúa en absoluto, y de ahí que el escenario «la sonda `HEALTHCHECK` no aplica a una ejecución en modo trabajo» se sostenga en la plataforma real.

### 8.8. Cambios exactos en `Ludeka.sln`

Estado actual: 5 proyectos, ninguno de consola, dos carpetas de solución (`src` `{827E0CD3-…}`, `tests` `{0AB3BF05-…}`).

| Proyecto | Carpeta de solución | Motivo |
|---|---|---|
| `src/Ludeka.Jobs/Ludeka.Jobs.csproj` | `src` | Se compila con `dotnet build Ludeka.sln` (`ci-cd.yml:41`) |
| `tests/Ludeka.IntegrationTests/Ludeka.IntegrationTests.csproj` | `tests` | **Obligatorio.** `dotnet test Ludeka.sln --no-build` (`:44`) solo recoge lo que la solución construyó en `:41`; un proyecto fuera de la solución no se compilaría ni se ejecutaría, y la garantía de fallo ruidoso se evaporaría |

Cada uno necesita su `Project(...)`/`EndProject`, sus 12 líneas de `ProjectConfigurationPlatforms` (las seis configuraciones × `ActiveCfg`/`Build.0`, siguiendo el patrón de `:30-41`) y su entrada en `NestedProjects` (`:95-99`).

### 8.9. Paso nuevo en `ci-cd.yml`

Estado actual: un único trabajo `deploy-cloudrun` (`:52-118`), un `docker build`/`docker push` (`:92-93`) con la etiqueta `ludeka-web:${{ github.sha }}` (`:90`), y un único `deploy-cloudrun@v2` (`:97-118`).

**Elección:** un paso nuevo tras el despliegue del servicio, con `gcloud run jobs deploy` (el SDK ya está configurado en `:77-79` y el `gcloud auth configure-docker` en `:83`), **cuatro recursos Cloud Run Job**, uno por cadencia, cada uno con su nombre de trabajo en `--args`, **sobre la misma imagen**.
**Alternativa considerada:** un único Job parametrizado cuyo nombre de trabajo llegue como sobrescritura desde cada Cloud Scheduler.
**Razón:** la alternativa mete semántica de aplicación en el cuerpo HTTP de la programación, de modo que una sobrescritura mal escrita ejecuta en silencio el trabajo equivocado. Con cuatro Jobs, cada Scheduler solo tiene que invocar `:run` con cuerpo vacío y el nombre del trabajo está fijado en el recurso, versionado por el pipeline.

```yaml
# Forma del paso; conserva la misma condición has_gcp == 'true' que :86, :96
- name: Publicar la revisión de los Cloud Run Jobs
  if: steps.check-secrets.outputs.has_gcp == 'true'
  run: |
    for JOB in nightly-cataloging price-radar social-collector notification-outbox; do
      gcloud run jobs deploy "ludeka-job-${JOB}" \
        --image "${IMAGE_NAME}" \
        --region "${REGION}" \
        --command dotnet \
        --args "Ludeka.Jobs.dll,${JOB}" \
        ... # variables de entorno y secretos equivalentes a :103-118
    done
```

**Hueco de evidencia declarado:** la ortografía exacta de las banderas de `gcloud run jobs deploy` y la disponibilidad de un modo *job* en `google-github-actions/deploy-cloudrun@v2` **no se han verificado**: no hay acceso autorizado a Google Cloud en este ciclo. Lo que el diseño fija es la forma y el motivo; el maintainer confirma la sintaxis. La propuesta §2.2 y el riesgo 7 ya clasifican la provisión y verificación de GCP como paso manual fuera del alcance automatizado.

**Cadencias recomendadas de Cloud Scheduler** (cron en UTC, configurables y no bloqueantes):

| Trabajo | Cron | Coherencia con la ventana |
|---|---|---|
| `nightly-cataloging` | `0 3 * * *` | Ventana diaria; una sola ventana al día |
| `price-radar` | `0 */6 * * *` | Debe ser ≥ `PriceRadar:CheckIntervalHours` (6) |
| `social-collector` | `0 */2 * * *` | Debe ser ≥ `SocialCollector:IntervalMinutes` (120) |
| `notification-outbox` | `*/5 * * * *` | Ventana de un segundo: cada disparo drena |

**Nota operativa que debe ir a la documentación de despliegue:** si la cadencia del Scheduler es **más frecuente** que el tamaño de la ventana, los disparos sobrantes salen con 0 y sin hacer nada por diseño (`SkippedAlreadyCompleted`). No es un fallo, es idempotencia, y quien mire los registros tiene que saberlo para no perseguir un fantasma.

---

## 9. Decisión D6 — Política de pruebas de integración: resolución del riesgo 8

Este es el punto donde `strict_tdd: true` y la disponibilidad de Docker colisionan de verdad. **Un verde silencioso sin cobertura de concurrencia no es aceptable**, y la política que sigue lo hace estructuralmente imposible.

### 9.1. Proyecto aparte, no *traits* dentro de la suite existente

**Elección:** proyecto nuevo `tests/Ludeka.IntegrationTests`, dado de alta en `Ludeka.sln`.
**Alternativas consideradas:** (a) `[Trait("Category","Integration")]` dentro de `Ludeka.UnitTests`; (b) `[Fact(Skip=…)]` o un `SkippableFact` condicional; (c) activación por variable de entorno (`RUN_PG_TESTS=1`).
**Razón:** (b) y (c) están **prohibidos por la especificación**: los dos producen una ejecución en verde con la prueba de concurrencia omitida, que es literalmente el resultado que `specs/postgres-integration-testing/spec.md:36` proscribe. (a) falla por una razón más sutil y decisiva: **la política de fallo ruidoso tiene que estar acotada al ensamblado**. Dentro de un solo proyecto, hacer que el ensamblado falle sin Docker se llevaría por delante las ~40 pruebas SQLite y volvería el repositorio indesarrollable sin Docker. Con un ensamblado propio el fallo queda contenido: `Ludeka.UnitTests` pasa, `Ludeka.IntegrationTests` falla en rojo, y `dotnet test Ludeka.sln` reporta la ejecución en rojo. Exactamente el comportamiento exigido.

Beneficios adicionales: `Testcontainers.PostgreSql` y `Npgsql` se quedan donde hacen falta, las ~40 pruebas SQLite conservan su conjunto de dependencias y su tiempo de ejecución, y el bucle rápido local sigue disponible con `dotnet test tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj` —el comando que `openspec/config.yaml:31` ya declara para ese proyecto—.

### 9.2. Mecanismo de fallo ruidoso

**Elección:** un `ICollectionFixture` que intenta arrancar el contenedor en su inicializador asíncrono y, si falla, **captura el diagnóstico**; cada prueba de la colección invoca `EnsureAvailable()`, que **lanza** con un mensaje accionable.

Por qué capturar y relanzar en vez de dejar que el inicializador propague: si el inicializador de la colección explota, xUnit reporta un error a nivel de colección cuyo mensaje es difícil de atribuir. Capturándolo, **cada prueba afectada aparece en rojo con el mensaje útil**, y además se puede afirmar el comportamiento del guardián de forma determinista.

```csharp
// tests/Ludeka.IntegrationTests/PostgresFixture.cs (NUEVO) — forma, no implementación
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine").WithCleanUp(true).Build();

    public Exception? StartupFailure { get; private set; }

    public async Task InitializeAsync()
    {
        try { await _container.StartAsync(); /* … */ }
        catch (Exception ex) { StartupFailure = ex; }   // se captura, NO se traga: §9.3 lo relanza
    }
}

[CollectionDefinition(PostgresCollection.Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres-real";
}
```

### 9.3. Qué hace `dotnet test Ludeka.sln` en cada entorno

| Entorno | Comportamiento |
|---|---|
| CI (`ubuntu-latest`, Docker garantizado por el *runner*) | Las 7 pruebas de integración se ejecutan y reportan su resultado real. **Cero cambios necesarios en el pipeline**: `:44` ya invoca `dotnet test Ludeka.sln` y `:47` ya evidencia demonio Docker operativo en el **mismo trabajo** |
| Máquina de desarrollo con Docker | Idéntico a CI |
| Máquina de desarrollo **sin** Docker | `Ludeka.UnitTests` pasa. **Cada** prueba de `Ludeka.IntegrationTests` aparece en **rojo** con el mensaje de §9.4. La ejecución global es **roja**. Nunca verde, nunca omitida |

Mensaje de la excepción, redactado para que el desarrollador entienda por qué el diseño le está fallando a propósito:

> Las pruebas de integración de INC-47 requieren un motor de contenedores operativo (Docker o Podman) para levantar PostgreSQL real. Sin él **no se puede verificar** la reclamación exclusiva con `FOR UPDATE SKIP LOCKED`, que es el criterio que estas pruebas demuestran. La suite falla de forma deliberada en lugar de omitirse en silencio. Arranca el motor de contenedores y repite `dotnet test Ludeka.sln`.

En CI (variable `CI` presente) el mensaje cambia para decir que el *runner* garantiza el demonio y que, por tanto, esto es un fallo de infraestructura de CI y no una limitación de la máquina. **Mismo resultado rojo**, solo mejor diagnóstico.

### 9.4. Prueba unitaria del guardián — el escenario que la especificación pide

`specs/postgres-integration-testing/spec.md:29` exige que la lógica de detección de capacidad sea comprobable «sin depender de que la máquina de pruebas realmente carezca de Docker». La decisión de fallo se extrae a una función pura:

```csharp
public static class ContainerCapabilityGuard
{
    /// <summary>Traduce el fallo de arranque del contenedor en la excepción que las pruebas
    /// deben lanzar. Devuelve null solo si no hubo fallo. NUNCA devuelve una omisión.</summary>
    public static Exception? Evaluate(Exception? startupFailure, bool runningOnCi);
}
```

Su prueba vive **dentro de `Ludeka.IntegrationTests` pero fuera de la colección de PostgreSQL**, de modo que se ejecuta y pasa con o sin Docker. Alimentada con un fallo ficticio, afirma que se produce una excepción descriptiva —nunca `null`, nunca una omisión— y que el mensaje cambia con `runningOnCi`. Evita un cuarto proyecto solo para compartir un tipo.

### 9.5. Aislamiento y coste

- **Un contenedor por ensamblado de pruebas** vía el `ICollectionFixture`, no uno por prueba. Es la palanca de coste principal.
- **Una base de datos por clase de prueba** dentro de ese contenedor, con las migraciones aplicadas en el *fixture* de clase. Se descartó envolver cada prueba en una transacción con retroceso: **rompería la prueba de concurrencia**, que necesita dos conexiones reales y confirmadas contra la misma base. Si el tiempo de ejecución se vuelve un problema, la optimización disponible es `CREATE DATABASE … TEMPLATE`, y se declara como tal.
- **El escenario de migración necesita migrar a un punto intermedio.** Mecanismo concreto: `db.GetService<IMigrator>().MigrateAsync("20260917112154_AddProviderEmailVerifiedAtToExternalLogins")` —la última migración real, verificada en `src/Ludeka.Infrastructure/Migrations/`—, sembrar filas, y después `MigrateAsync()` hasta la última para afirmar que nada se perdió.

---

## 10. Decisión D7 — *Health check* del outbox

Estado actual verificado: `NotificationQueueHealthCheck` inyecta `ICommunityNotificationQueue`, construye `{ queue_type, operational: true }` y devuelve `Healthy` siempre que el servicio se resuelva (`src/Ludeka.Web/Health/NotificationQueueHealthCheck.cs:24-30`). Registrado en `Program.cs:325` con `tags: ["ready"]`.

**Elección:** inyectar `INotificationOutboxRepository` más `IOptionsMonitor<OutboxOptions>` y reportar tres números reales con **una sola consulta**.

```sql
SELECT COUNT(*) AS pendientes, MIN("CreatedAt") AS masAntiguo,
       COUNT(*) FILTER (WHERE "Status" = 2) AS muertos
  FROM "NotificationOutboxMessages";
```

Carga útil de `data`, sustituyendo `queue_type`/`operational`: `pending_count`, `oldest_pending_age_seconds`, `dead_count`, `provider`.

### 10.1. Umbrales y — decisión importante — el techo en `Degraded`

| Condición | Estado reportado |
|---|---|
| Por debajo de todos los umbrales | `Healthy` |
| `pending_count > HealthPendingDepthDegraded` (100) | `Degraded` |
| `oldest_pending_age > HealthOldestPendingDegradedMinutes` (30) | `Degraded` |
| `dead_count > 0` | `Degraded` |
| Consulta agota `HealthQueryTimeoutSeconds` (2 s) | `Degraded`, con «no se pudo medir» en la descripción |
| La consulta **lanza** (outbox ilegible) | `Unhealthy` |

**La profundidad y la antigüedad nunca producen `Unhealthy`, y esa es una decisión de diseño con consecuencia operativa concreta.** `/ready` filtra por la etiqueta `ready` (`Program.cs:431`) y un chequeo `Unhealthy` hace que el endpoint responda 503 (`specs/health-checks/spec.md:26-28`). Si un atasco del outbox pusiera `notification_queue` en `Unhealthy`, **Cloud Run sacaría de rotación el servicio web** por un problema que, después de R7, ni siquiera es suyo: el que drena el outbox es un Cloud Run Job. Se evitaría un incidente de notificaciones provocando un incidente de disponibilidad.

`Unhealthy` se reserva para el outbox **ilegible**, que sí es un fallo de dependencia de base de datos y es coherente con lo que `sqlite_db` reporta en la misma situación.

Esto satisface el escenario modificado de la especificación —«si esos valores superan un umbral configurado de degradación, el componente `notification_queue` se reporta como **no saludable** aunque el resto de componentes estén sanos»— porque `Degraded` es precisamente el valor «no saludable» del enum `HealthStatus`, y el desglose JSON reporta la profundidad y la antigüedad reales tal como el escenario exige.

### 10.2. Impacto conocido en pruebas

`tests/Ludeka.UnitTests/Health/HealthChecksTests.cs:130-140` construye hoy un contenedor que registra `ICommunityNotificationQueue` para este chequeo. Al cambiar el constructor, esa prueba **deja de compilar y hay que actualizarla** en R4. Se señala aquí para que `sdd-tasks` lo programe como trabajo explícito y no como sorpresa; bajo `strict_tdd` la prueba nueva se escribe en rojo antes del cambio de producción.

---

## 11. Flujo de datos

### 11.1. Notificación: desde el productor hasta los dos canales

```
FoundingVerdictService:217          RuleQAService:199
  (o el panel de administración)
            │                              │
            └──────────────┬───────────────┘
                           ▼
        ICommunityNotificationQueue.EnqueueAsync   ← firma intacta, productores sin tocar
                           ▼
        OutboxCommunityNotificationQueue  (Scoped)
                           ▼
        INSERT NotificationOutboxMessages
          Status=Pending, Attempts=0, NextAttemptAt=ahora,
          FieldsJson y TargetChannel persistidos  ← C1
                           │
      ······················· LA INSTANCIA PUEDE MORIR AQUÍ ·······································
      ······················· la fila sobrevive; no hay dependencia del despachador ················
                           │
        Cloud Scheduler  ──► Cloud Run Job «notification-outbox»  (cada 5 min)
                           ▼
        NotificationOutboxJobRunner → INotificationOutboxDispatcher.DispatchPendingAsync
                           ▼
        ClaimPendingAsync:  UPDATE … FROM (SELECT … FOR UPDATE SKIP LOCKED) … RETURNING
          Attempts++, NextAttemptAt = ahora + concesión   ← invisible a otros despachadores
          UNA sola sentencia: ningún lock queda abierto durante el envío
                           ▼
        Derivar canales AHORA releyendo CommunityNotificationOptions   ← decisión 4, §6.5
                           ▼
        EnsureDeliveryAsync(MessageId, canal)  →  NotificationLogs
          idempotente por UNIQUE(MessageId, Channel)
                           ▼
        DeliverAsync → DiscordWebhookClient / TelegramBotClient   ← sin transacción abierta
                           ▼
        Sub-entrega: Sent | Failed(Attempts++, NextAttemptAt) | Failed terminal
                           ▼
        Ninguna sub-entrega no terminal  →  mensaje Completed
        Conjunto de canales vacío        →  liberar con retroceso; Dead al agotar intentos
```

### 11.2. Trabajo con ventana: desde el disparo hasta el código de salida

```
Cloud Scheduler  ──►  Cloud Run Job «nightly-cataloging»
                         ▼
        Ludeka.Jobs: argumentos → JobRunner; guardas de arranque (§8.6) o salida 3
                         ▼
        NightlyCatalogingJobRunner: WindowKey = «2026-09-18» (UTC, §7.2)
                         ▼
        IJobExecutionCoordinator.ExecuteWithWindowLeaseAsync
                         ▼
        INSERT JobExecutionLeases  ← UNIQUE(JobName, WindowKey) decide, no un if
           ├── éxito                            → ejecutar la unidad de trabajo
           ├── violación + Completed            → salida 0, nada que hacer
           ├── violación + Running y con latido  → salida 0, otra ejecución la tiene
           ├── violación + Running sin latido    → intercambio condicional: toma de control
           └── violación + Failed                → intercambio condicional: retomar
                         ▼
        INightlyCatalogingService.RunScheduledCatalogingAsync   ← ruta de sistema, sin sesión
          (crea además su bitácora rica en NightlyCatalogingExecutionLogs:92-93, sin cambios)
                         ▼
        Concesión: Completed | Failed, con ProcessedCount, FailedCount, DurationMs
                         ▼
        Código de salida 0 o 1  →  Cloud Scheduler y las alertas lo ven
```

---

## 12. Cambios de ficheros

| Fichero | Acción | Descripción | Rebanada |
|---|---|---|---|
| `tests/Ludeka.IntegrationTests/Ludeka.IntegrationTests.csproj` | Crear | Proyecto de integración: `Testcontainers.PostgreSql`, `Npgsql.EntityFrameworkCore.PostgreSQL`, xUnit | R1 |
| `tests/Ludeka.IntegrationTests/PostgresFixture.cs` | Crear | *Collection fixture*: contenedor, captura del fallo de arranque, `EnsureAvailable` | R1 |
| `tests/Ludeka.IntegrationTests/ContainerCapabilityGuard.cs` | Crear | Función pura de decisión de fallo ruidoso, unitariamente comprobable | R1 |
| `Ludeka.sln` | Modificar | Alta de `Ludeka.IntegrationTests` (carpeta `tests`) y `Ludeka.Jobs` (carpeta `src`) | R1 / R6 |
| `src/Ludeka.Infrastructure/DependencyInjection/LudekaServiceCollectionExtensions.cs` | Crear | La frontera: 4 métodos públicos de composición | R2 |
| `src/Ludeka.Infrastructure/Ludeka.Infrastructure.csproj` | Modificar | `Microsoft.Extensions.Http`, `Microsoft.Extensions.Options.ConfigurationExtensions` (§4.4) | R2 |
| `src/Ludeka.Web/Program.cs` | Modificar | R2: sustituir ~230 líneas por `AddLudekaApplicationCore`. R7: retirar los 4 `AddHostedService` (`:166,215,244,319`) | R2 / R7 |
| `src/Ludeka.Application/Contracts/DenyAllSessionPermissionGuard.cs` | Crear | Guarda que deniega siempre, para hosts sin sesión (§4.2) | R2 |
| `src/Ludeka.Core/Entities/NotificationOutboxMessage.cs` | Crear | Mensaje lógico del outbox, con `FieldsJson` y `TargetChannel` | R3 |
| `src/Ludeka.Core/Enums/OutboxMessageStatus.cs` | Crear | `Pending`/`Completed`/`Dead` | R3 |
| `src/Ludeka.Core/Entities/CommunityNotificationLog.cs` | Modificar | `MessageId?`, `Attempts`, `NextAttemptAt?` y tres métodos de dominio | R3 |
| `src/Ludeka.Core/Entities/JobExecutionLease.cs` | Crear | Concesión de ventana genérica para los cuatro trabajos | R3 |
| `src/Ludeka.Infrastructure/Data/LudekaDbContext.cs` | Modificar | Dos `DbSet` nuevos, configuración e índices. **No se toca `:403`** (índice no único de `NightlyCatalogingExecutionLog`) ni `:283-285` | R3 |
| `src/Ludeka.Infrastructure/Migrations/` | Crear | Migración Npgsql: dos tablas, tres columnas, cuatro índices (paso 1 de propuesta §4) | R3 |
| `src/Ludeka.Infrastructure/Data/SqliteSchemaMigrator.cs` | Modificar | Reconciliación incremental con `PRAGMA table_info` + `ALTER TABLE`. **No replicar C5** | R3 |
| `src/Ludeka.Application/Options/OutboxOptions.cs` | Crear | `SectionName = "Outbox"` | R3 |
| `src/Ludeka.Application/Contracts/ICommunityNotificationQueue.cs` | Modificar | `EnqueueAsync` intacto (`:10`); retirar `ReadAllAsync` (`:11`) | R4 |
| `src/Ludeka.Application/Contracts/INotificationOutboxRepository.cs` | Crear | Reclamación, sub-entregas, estados terminales, muestra de salud | R4 |
| `src/Ludeka.Application/Contracts/INotificationOutboxDispatcher.cs` | Crear | Un ciclo acotado, sin bucle | R4 |
| `src/Ludeka.Infrastructure/Repositories/NotificationOutboxRepository.cs` | Crear | Reclamación en ADO.NET parametrizado con rama por proveedor; resto con EF Core | R4 |
| `src/Ludeka.Infrastructure/Notifications/OutboxCommunityNotificationQueue.cs` | Crear | `EnqueueAsync` persistente, `Scoped`, con registro del fallo (C2) | R4 |
| `src/Ludeka.Application/Features/Community/NotificationOutboxDispatcher.cs` | Crear | Reclamar → derivar canales → asegurar sub-entrega → entregar → completar | R4 |
| `src/Ludeka.Application/Features/Community/CommunityNotificationService.cs` | Modificar | Separar `SendOnChannelAsync` (sin persistencia) y añadir `DeliverAsync`; los públicos existentes sin cambios | R4 |
| `src/Ludeka.Infrastructure/Notifications/InMemoryCommunityNotificationQueue.cs` | Conservar | `ReadAllAsync` pasa a método concreto sin contrato; desarrollo local y pruebas | R4 |
| `src/Ludeka.Web/Health/NotificationQueueHealthCheck.cs` | Modificar | De `queue_type` a profundidad, antigüedad y muertos, con techo en `Degraded` (§10) | R4 |
| `tests/Ludeka.UnitTests/Health/HealthChecksTests.cs` | Modificar | Impacto conocido del cambio de constructor (`:130-140`) | R4 |
| `src/Ludeka.Application/Contracts/IJobExecutionCoordinator.cs` | Crear | Envoltura de concesión de ventana, común a los cuatro trabajos | R5 |
| `src/Ludeka.Application/Features/Jobs/JobExecutionCoordinator.cs` | Crear | `INSERT` primero, toma de control por intercambio condicional, métricas | R5 |
| `src/Ludeka.Application/Contracts/IJobExecutionLeaseRepository.cs` | Crear | `TryAcquireAsync` devuelve `LeaseAcquisition`, nunca una excepción de proveedor | R5 |
| `src/Ludeka.Infrastructure/Repositories/JobExecutionLeaseRepository.cs` | Crear | Clasificación de violación de unicidad por proveedor (`23505` / `19`) | R5 |
| `src/Ludeka.Infrastructure/Background/NightlyCatalogingHostedService.cs` | Modificar | A unidad de trabajo de un disparo; desaparecen `_lastExecutionDate` (`:22,68`) y el retardo de 15 min (`:79-80`) | R5 |
| `src/Ludeka.Infrastructure/Background/PriceRadarHostedService.cs` | Modificar | Desaparece el bucle de `CheckIntervalHours` (`:69-72`) | R5 |
| `src/Ludeka.Infrastructure/Background/SocialCollectorHostedService.cs` | Modificar | Desaparece el bucle de `IntervalMinutes` (`:81-85`) | R5 |
| `src/Ludeka.Infrastructure/Notifications/CommunityNotificationDispatcherHostedService.cs` | Modificar | Reescritura: fuera el `ReadAllAsync` infinito (`:43`), `_lastFridayBulletinDispatched` (`:16,92`) y el retardo de 60 min (`:103`) | R4 / R5 |
| `src/Ludeka.Jobs/Ludeka.Jobs.csproj` | Crear | Consola `net10.0`, `Microsoft.Extensions.Hosting`, `appsettings.json` enlazado | R6 |
| `src/Ludeka.Jobs/Program.cs` | Crear | Host genérico, argumentos, guardas, código de salida; **nunca** `host.RunAsync()` | R6 |
| `src/Ludeka.Jobs/IJobRunner.cs`, `JobNames.cs`, `Runners/*.cs` (4) | Crear | Un *runner* fino por trabajo | R6 |
| `Dockerfile` | Modificar | Los tres cambios de §8.7. **`ENTRYPOINT:81` NO se toca** | R6 |
| `.github/workflows/ci-cd.yml` | Modificar | Paso nuevo de publicación de los cuatro Cloud Run Jobs (§8.9) | R6 |
| `src/Ludeka.Web/appsettings.json` | Modificar | Secciones `Workers` y `Outbox`, `Enabled` primero | R7 |
| `docs/deployment/DEPLOYMENT_GUIDE.md`, `docs/deployment/google-cloud-run.md` | Modificar | Jobs, los 4 Scheduler con sus crones, `roles/run.invoker`, retención de `JobExecutionLeases`, nota de cadencia frente a ventana | R7 |
| `openspec/config.yaml` | Modificar | Alta de `src/Ludeka.Jobs` y `tests/Ludeka.IntegrationTests` en `projects` | R7 |

---

## 13. Estrategia de pruebas

`strict_tdd: true`: cada fila se escribe **en rojo** antes del cambio de producción que la satisface. Ejecutor contractual `dotnet test Ludeka.sln`.

| Capa | Qué se prueba | Cómo | Rebanada |
|---|---|---|---|
| Integración (PostgreSQL) | Dos despachadores concurrentes nunca reclaman el mismo mensaje | Dos conexiones reales, `ClaimPendingAsync` simultáneo, intersección vacía | R1 → R4 |
| Integración (PostgreSQL) | `FOR UPDATE SKIP LOCKED` sobre tabla real, dentro del ejecutor contractual | *Fixture* de contenedor, tabla real | R1 |
| Unitaria (guardián) | Sin capacidad de contenedor se produce excepción descriptiva, nunca omisión | `ContainerCapabilityGuard.Evaluate` con fallo ficticio; fuera de la colección PostgreSQL | R1 |
| Unitaria (composición) | `AddLudekaApplicationCore` resuelve los 4 servicios programados; orden de los dos `IEnumerable<T>`; cero `IHostedService` | `validateScopes: true`, `validateOnBuild: true` (§4.6) | R2 |
| Integración (PostgreSQL) | La migración Npgsql no pierde filas de notificación ni de bitácora | `MigrateAsync("20260917112154_…")` → sembrar → `MigrateAsync()` | R3 |
| Unitaria (SQLite) | Base en disco con esquema anterior: el reconciliador añade columnas y tablas sin pérdida | Sembrar fichero SQLite, ejecutar `SqliteSchemaMigrator` | R3 |
| Unitaria (SQLite) | `EnqueueAsync` persiste la fila **sin ejecutar nunca el despachador** | Encolar y leer el estado persistido directamente | R4 |
| Unitaria (SQLite) | Un mensaje `Queued` sobrevive a un reinicio simulado y acaba enviándose | Destruir y reconstruir el despachador contra el mismo almacén | R4 |
| Unitaria (SQLite) | `FieldsJson` y `TargetChannel` sobreviven al ciclo completo (C1) | Encolar con `Fields`, despachar, afirmar sobre lo que recibe el cliente | R4 |
| Unitaria (SQLite) | Fallo de entrega: `Attempts++` y reprogramación en estado no terminal | Cliente doble que falla | R4 |
| Unitaria (SQLite) | Agotados los intentos: `Failed` terminal y no vuelve a reclamarse | `MaxDeliveryAttempts` bajo | R4 |
| Unitaria (SQLite) | Canal habilitado **después** del encolado: se entrega y no se pierde (decisión 4) | Encolar con Telegram deshabilitado, habilitar, reclamar de nuevo | R4 |
| Unitaria (SQLite) | Sub-entrega idempotente: reclamar dos veces no duplica filas por canal | `UNIQUE(MessageId, Channel)` | R4 |
| Unitaria (SQLite) | Chequeo de salud: profundidad y antigüedad reales; umbral → `Degraded`; outbox ilegible → `Unhealthy` | Sembrar outbox y variar `OutboxOptions` | R4 |
| Integración (PostgreSQL) | Cinco instancias concurrentes disputan la misma ventana: solo una completa | Cinco tareas contra `TryAcquireAsync`, `UNIQUE` real | R1 → R5 |
| Integración (PostgreSQL) | Reintento de Scheduler sobre ventana completada: choca con `UNIQUE`, no con un `if` | Sembrar la concesión `Completed` y repetir | R1 → R5 |
| Unitaria (SQLite) | Instancia recién iniciada sin estado: no repite ventana completada; `_lastExecutionDate` ya no existe | Sembrar concesión, ejecutar *runner* | R5 |
| Unitaria (SQLite) | Boletín semanal: no se re-despacha; `_lastFridayBulletinDispatched` ya no existe | Sembrar concesión de la semana | R5 |
| Unitaria (SQLite) | Concesión huérfana: `Running` con latido caducado se toma; con latido vivo no | Manipular `HeartbeatAt` | R5 |
| Unitaria (SQLite) | Clave de ventana: fronteras ancladas al epoch, UTC, estables entre instancias | Tabla de casos por trabajo | R5 |
| Unitaria (SQLite) | Métricas mínimas persistidas con fallos parciales | Trabajo con elementos que fallan | R5 |
| Unitaria | Códigos de salida 0/1/2/3 y el contrato de vida corta (retorna acotado, sin cancelación) | Invocar el punto de entrada con dobles | R6 |
| Unitaria | Guardas de arranque: cadena SQLite en `Production` → 3; migraciones pendientes → 3 | Configuración sintética | R6 |
| Unitaria | Análisis de argumentos: posicional, `--job=`, configuración, desconocido → 2 | Tabla de casos | R6 |
| Unitaria | El host web no registra **ningún** `IHostedService` de negocio | Resolver `IEnumerable<IHostedService>` de la composición web real | R7 |
| Estructural | `AddHostedService` no devuelve resultados para los 4 tipos de negocio | Búsqueda sobre `src/` | R7 |
| Manual (maintainer) | Cloud Scheduler dispara, IAM autoriza, el Job arranca; reinicio real de contenedor sin pérdida; arranque del contenedor en modo trabajo; documentación de despliegue | Sin acceso autorizado a GCP en este ciclo | R6 / R7 |

---

## 14. Matriz de amenazas

La matriz de `references/threat-matrix.md` cubre fronteras de automatización de Git, PR y *shell*. Este cambio no toca ninguna de ellas:

| Frontera | Aplicabilidad | Razón |
|---|---|---|
| Rutas con aspecto de documentación | **N/A** | No se introduce ninguna clasificación ni ejecución de ficheros por su nombre o extensión. Los ficheros nuevos son `.cs`, `.csproj`, YAML y Markdown, todos inertes |
| Selección de repositorio Git | **N/A** | Ni el diseño ni el artefacto ejecutan Git. El flujo de *worktree* es del proceso SDD, no del código entregado |
| Estado del índice de commits | **N/A** | Cero automatización de commits |
| Estado de *push* | **N/A** | Cero automatización de *push* |
| Comandos de PR | **N/A** | Cero automatización de PR |

**La frontera real que este cambio sí introduce está fuera de las cinco filas de la matriz** y se define aquí, con su comportamiento seguro y su comportamiento ante fallo, para que `sdd-tasks` la lleve a pruebas en rojo:

| Frontera | Caso adversario | Comportamiento seguro exigido | Prueba en rojo |
|---|---|---|---|
| Selección de trabajo por argumento | Nombre desconocido, vacío, con espacios o con otra capitalización | Salida **2**, con la lista de nombres válidos en `stderr`; **ningún** trabajo se ejecuta | Tabla de casos sobre el análisis de argumentos (R6) |
| Selección de trabajo por argumento | Dos fuentes en conflicto (posicional y `--job=`) | Precedencia determinista: posicional gana; jamás se ejecutan dos trabajos | Caso explícito en la misma tabla (R6) |
| Procedencia de la base de datos | El Job arranca sin el secreto de conexión y hereda la cadena SQLite de `Dockerfile:52` | Salida **3** antes de la unidad de trabajo, nombrando la variable ausente; **nunca** un éxito con cero trabajo procesado (§8.6) | Guarda de coherencia de proveedor (R6) |
| Propiedad del esquema | Un Job intenta migrar o sembrar | El Job **no** migra ni siembra; con migraciones pendientes en PostgreSQL, salida **3** (§4.5) | Guarda de esquema al día (R6) |
| Sobrescritura del comando del contenedor | El Job se despliega con `--args` mal escritos | El nombre del trabajo está fijado en el recurso Cloud Run Job y versionado por el pipeline, no en el cuerpo HTTP del Scheduler (§8.9) | Verificación manual del maintainer (sin acceso a GCP) |
| Inyección SQL en la reclamación | Valores de control (`batchSize`, `claimedBy`, `lease`) en el SQL crudo | **Solo `DbParameter`**, nunca interpolación de cadenas; el SQL es un literal fijo | Revisión y prueba de reclamación con `claimedBy` conteniendo comillas (R4) |

---

## 15. Migración y despliegue

### 15.1. Los tres pasos obligatorios de la propuesta §4

| Paso | Acción concreta de INC-47 |
|---|---|
| 1 | `dotnet ef migrations add AddNotificationOutboxAndJobLeases` → tablas `NotificationOutboxMessages` y `JobExecutionLeases`; columnas `MessageId`, `Attempts`, `NextAttemptAt` en `NotificationLogs`; los cuatro índices de §5.3 y §7.1 |
| 2 | `SqliteSchemaMigrator.cs` **a mano**: `CREATE TABLE IF NOT EXISTS` para las dos tablas nuevas y `PRAGMA table_info` + `ALTER TABLE` para las tres columnas. Patrón incremental de `:49-83` y `:887-898`, **no** el `CREATE TABLE` de una sola oportunidad que causó C5 |
| 3 | Nada adicional: las pruebas recrean el esquema con `EnsureCreatedAsync()` (`Program.cs:371`), que recoge automáticamente lo nuevo |

### 15.2. Orden de despliegue y compatibilidad

Todas las rebanadas anteriores a R7 son **compatibles hacia atrás en producción**: las columnas y tablas nuevas existen sin usarse hasta que el código que las lee esté desplegado, y los cuatro `AddHostedService` siguen registrados hasta R7. La retirada va última por el riesgo 5 de la propuesta —un estado intermedio mergeado que retire los trabajos antes de que los Jobs existan y disparen deja producción sin ningún ejecutor—.

El plan de reversión de la propuesta §10 se mantiene íntegro y este diseño no introduce ningún punto sin retorno. Dos precisiones que el diseño añade:

- **R4/R5 revertidas:** las columnas y tablas nuevas quedan en la base sin usarse, lo cual es inocuo. Pero **los mensajes ya encolados en `NotificationOutboxMessages` quedan sin ejecutor**, porque el código revertido vuelve a la cola en memoria. No se pierden —siguen en la tabla— pero no se entregan hasta que se vuelva a desplegar. **Debe figurar en la documentación de reversión.**
- **R3 revertida:** se aplica el `Down` de la migración Npgsql **y** se revierte a mano `SqliteSchemaMigrator.cs`. Los dos pasos, o las bases SQLite en disco quedan divergentes.

### 15.3. Provisión de Google Cloud: alcance manual del maintainer

Fuera del alcance automatizado, sin acceso autorizado a GCP: cuatro recursos Cloud Run Job, cuatro Cloud Scheduler, una cuenta de servicio con `roles/run.invoker`, y la verificación de que el disparo llega y la invocación se autoriza. El incremento entrega pipeline y documentación; el maintainer verifica.

---

## 16. Encaje con la secuenciación y efecto sobre el tamaño de las rebanadas

Este diseño es **compatible con el corte de siete rebanadas** de la propuesta §9 y respeta sus dos dependencias duras. Donde una decisión de diseño cambia el tamaño previsto, se dice:

| Rebanada | Estimación de la propuesta | Efecto del diseño | Motivo |
|---|---|---|---|
| **R1** | ~150-250 | **Sube** → ~200-320 | Proyecto de pruebas propio (§9.1) con *fixture* de colección, aislamiento por base de datos, guardián puro y su prueba, y alta en `Ludeka.sln` |
| **R2** | ~400-460 · **Alto** | **Sube algo** → ~430-500 | Igual de traslado puro, más dos `PackageReference` (§4.4), `DenyAllSessionPermissionGuard` y la prueba de humo de composición (§4.6). La presión de presupuesto **no baja**; la costura de cuatro métodos existe si `sdd-tasks` la necesita |
| **R3** | ~200-300 | **Sube** → ~320-420 | Dos entidades y dos tablas nuevas en lugar de columnas sobre tablas existentes, más tres columnas en `CommunityNotificationLog`, cuatro índices, la migración Npgsql y la reconciliación SQLite de todo ello. **Compensación parcial:** `NightlyCatalogingExecutionLog` no cambia de esquema y `LudekaDbContext.cs:403` no se toca (§7.1) |
| **R4** | ~300-400 | **Sube** → ~380-470 | Se añaden la refactorización `SendOnChannelAsync`/`DeliverAsync` (§6.6), la rama por proveedor de la reclamación (§6.4) y la actualización de `HealthChecksTests.cs` (§10.2) |
| **R5** | ~250-350 | **Se mantiene** | El coordinador genérico es trabajo nuevo, pero deja los cuatro *runners* finos y evita cuatro implementaciones de idempotencia |
| **R6** | ~250-350 | **Sube algo** → ~280-380 | Se añaden las guardas de arranque (§8.6), las 12 líneas × 2 de `Ludeka.sln` (§8.8) y el bucle de cuatro Jobs en el pipeline (§8.9) |
| **R7** | ~150-250 | **Se mantiene** | Retirada, configuración y documentación. Se añaden dos notas operativas (retención de concesiones, cadencia frente a ventana) y el alta en `openspec/config.yaml` |

**Tres rebanadas quedan en riesgo claro de presupuesto: R2, R3 y R4.** Este diseño **no decide** si se parten o si piden `size:exception`: eso lo aterriza `sdd-tasks` midiendo el diff real, como la propia propuesta indica. Lo que el diseño aporta son costuras limpias por donde partir si hace falta:

- **R2:** cuatro métodos de extensión por responsabilidad técnica (§4) → persistencia y dominio en una sub-rebanada, integraciones y clientes HTTP en otra.
- **R3:** las dos tablas nuevas son independientes entre sí → outbox (`NotificationOutboxMessages` más las tres columnas de `NotificationLogs`) en una, concesiones de ventana (`JobExecutionLeases`) en otra. Dos migraciones en lugar de una, que la propuesta §4 admite sin problema porque el historial es uno y secuencial.
- **R4:** el *health check* (§10) es separable del mecanismo de reclamación y no lo bloquea.

---

## 17. Preguntas abiertas y huecos de evidencia

Ninguna bloquea `sdd-tasks`. Todas son verificaciones de tiempo de implementación o de plataforma que esta fase no puede ejecutar.

- [ ] **Conjunto exacto de `PackageReference` de la extracción de DI** (§4.4). Verificado: `Microsoft.Extensions.Http` no se referencia en ningún `.csproj` de `src/` y `SocialIngestionService.cs:43` exige un `HttpClient` del contenedor. No verificado: la lista completa, que solo cierra el compilador. Detector: la prueba de humo de §4.6.
- [ ] **El contenedor de DI y los parámetros opcionales no registrados.** El diseño **evita** depender de ese comportamiento registrando `DenyAllSessionPermissionGuard` (§4.2), así que la duda no bloquea; queda anotada porque los seis servicios del proyecto usan ese patrón.
- [ ] **`NULL` distintos en índice único** (§5.3), en PostgreSQL y SQLite. Comportamiento estándar en ambos, no ejecutado. Lo demuestra el escenario de reconciliación SQLite con varias filas históricas.
- [ ] **Doble `dotnet publish` en el mismo `/app/publish`** (§8.7). Se espera reescritura con bytes idénticos; no se ha ejecutado `docker build`.
- [ ] **Ortografía exacta de las banderas de `gcloud run jobs deploy`** y modo *job* de `deploy-cloudrun@v2` (§8.9). Sin acceso autorizado a GCP.
- [ ] **Semántica exacta de `FOR UPDATE SKIP LOCKED` en la sentencia de §6.3.** El diseño la fija; **quien la verifica es la prueba de concurrencia de R1**, no la afirmación de esta fase. Es la consecuencia deliberada de haber puesto el habilitador de pruebas primero.
- [ ] **Divergencia de redacción con la especificación, para `sdd-verify`.** El requisito «Migración del esquema de idempotencia por ventana sin pérdida de datos» (`specs/background-jobs-scheduling/spec.md:166-177`) presupone en su Escenario 1 una **columna añadida** sobre la bitácora existente. Este diseño la pone en una **tabla nueva** (§7.1, con razones). La **intención** del requisito —cero pérdida de datos en el esquema de seguimiento de ejecuciones— se cumple y es comprobable; la **letra** del escenario («quedan disponibles con la columna nueva en su valor por defecto») debe leerse contra la intención. No es una renuncia a un requisito: es un ajuste de redacción que `sdd-tasks` debe reflejar en la prueba y `sdd-archive` al fusionar. El requisito equivalente del outbox ya anticipa explícitamente esta libertad (`specs/notification-outbox/spec.md:85`).
- [ ] **`RetryFailedNotificationAsync` sigue perdiendo `Fields`** (C1). Con `MessageId` poblado la mejora natural es releer el mensaje y reprogramar la sub-entrega. **Fuera del alcance de INC-47**; registrado para no perderlo.
- [ ] **Atomicidad transaccional entre cambio de dominio y encolado** (C4). No alcanzable dentro del alcance aprobado. Candidato a incremento propio.
- [ ] **Sin purga de `JobExecutionLeases`** (§7.2): ~288 filas/día por el drenaje del outbox. Se recomienda documentar una retención de 90 días; la política efectiva es trabajo posterior.
- [ ] **No se ha ejecutado `dotnet test Ludeka.sln`** en esta fase, igual que en exploración, propuesta y especificación. El recuento de pruebas en verde sobre `56c02b9` sigue sin confirmar; es responsabilidad de `sdd-verify`.
