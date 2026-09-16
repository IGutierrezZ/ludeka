# Exploración: Vinculación de Cuentas entre Proveedores, Recuperación de Acceso y Política de Correo Ausente

> **Cambio:** `change-49-vinculacion-cuentas` (INC-49)
> **Fase:** `sdd-explore`
> **Fecha:** 2026-09-17
> **Rama / worktree:** `inc/vinculacion-cuentas` — `C:\repos\ludeka-wt\vinculacion-cuentas` (base `origin/main` = `01b4b73`)
> **Espejo en Engram:** `sdd/change-49-vinculacion-cuentas/explore` (observación 347)
> **Dependencia:** INC-46 (Autenticación Real, archivado)

---

## Resumen ejecutivo

La cascada de vinculación descrita en INC-49 §1 está **confirmada por el código**, con una imprecisión de formato en el ejemplo de correo sintético.

El hallazgo más importante para el diseño: `ExternalLogin.ProviderEmail` existe, pero **no hay ninguna marca de verificación por fila** en el esquema, y el código actual (`ExternalLoginService.cs:77`) persiste ese correo aunque **no** esté verificado en la rama de alta de cuenta nueva. Derivar "correo verificado" de "`ProviderEmail` no nulo" tal como está hoy produciría falsos positivos.

No existe ninguna página bajo `/cuenta`, ni infraestructura de vinculación-desde-sesión, ni métodos de repositorio para listar o borrar vínculos por usuario: hay que construirlos desde cero, aunque INC-46 ya dejó la costura prevista (`design.md:14`: "la pantalla futura es UI más `LinkAsync(...)`").

---

## Estado actual (verificado en código)

### 1. Identidad externa y cascada de vinculación

- Entidad `ExternalLogin` (`src/Ludeka.Core/Entities/ExternalLogin.cs:9-43`): `Id` (Guid), `UserId` (string), `Provider` (string), `ProviderKey` (string), `ProviderEmail` (`string?`), `LinkedAt`. Constructor validante (líneas 21-42), sin más campos.
- Migración `20260915164921_AddExternalLogins.cs:14-46`: tabla `ExternalLogins` con exactamente esas 6 columnas, PK en `Id`, índice único `(Provider, ProviderKey)` (líneas 36-40), índice simple en `UserId` (líneas 42-45), FK `Cascade` a `AppUsers` (líneas 28-33). Confirmado también en `LudekaDbContext.cs:481-494` (Fluent API, sin propiedades sombra adicionales).
- Cascada real en `ExternalLoginService.ResolveAsync` (`src/Ludeka.Application/Features/Identity/ExternalLoginService.cs:28-80`):
  1. **Líneas 43-52** — por `(Provider, ProviderKey)`: si existe el vínculo y el `AppUser` sigue vivo, se devuelve esa cuenta. Sin filas nuevas.
  2. **Líneas 54-64** — si `emailVerified && normalizedEmail is not null` y ese correo coincide con un `AppUser.Email` existente (`IUserRepository.GetByEmailAsync`), se crea la fila `ExternalLogin` (líneas 60-61, con el correo **verificado**) y se devuelve esa cuenta con sus roles y permisos intactos.
  3. **Líneas 66-79** — si nada de lo anterior aplica: alta de `AppUser` nuevo con `CommunityUser`/`None` (constructor en `AppUser.cs:24-58`, forzado por el `switch` de las líneas 52-57 de esa clase — ningún rol distinto de `FoundingTeam` recibe permisos por defecto).
- `PlaceholderEmailDomain = "ludeka.invalid"` está en `ExternalLoginService.cs:16`. `BuildPlaceholderEmail` (líneas 88-89) construye `"{providerKey}@{provider}.ludeka.invalid"` en minúsculas.
- **Invariante de correo obligatorio confirmado exactamente donde dice el documento**: `AppUser.cs:40-41` lanza `ArgumentException` si el correo está vacío.
- **Corrección menor al documento**: INC-49 §1 (línea 28) da como ejemplo `discord_12345@ludeka.invalid`. El código real construye `12345@discord.ludeka.invalid`. La especificación viva `docs/specs/sistema/32-autenticacion-y-autorizacion.md:62` sí usa el formato correcto (`<clave>@<proveedor>.ludeka.invalid`). Solo el ejemplo ilustrativo de INC-49 es impreciso; no afecta al alcance.

### 2. ¿Existe `ProviderEmail` y alguna marca de verificación? — HALLAZGO CRÍTICO PARA §2.5

**Sí existe `ProviderEmail`** (`ExternalLogin.cs:15`, `string?`, `HasMaxLength(200)` en `LudekaDbContext.cs:489`). **No existe ninguna columna ni bandera de verificación** — ni en la migración (`20260915164921_AddExternalLogins.cs`, solo 6 columnas) ni en la configuración Fluent (`LudekaDbContext.cs:481-494`, sin propiedad sombra).

El problema no es solo la ausencia de columna: **el dato que ya se persiste no permite derivar la verificación**. En `ExternalLoginService.ResolveAsync`, rama 3 (alta de cuenta nueva, líneas 66-79):

```csharp
// (3) Alta de una cuenta comunitaria nueva: nunca hereda roles ni permisos.
// El correo sin verificar no se persiste en AppUser (el índice único de Email es la
// identidad verificada de la cuenta); queda únicamente como pista en ExternalLogin.ProviderEmail.
var verifiedEmail = emailVerified ? normalizedEmail : null;
var newUser = new AppUser(..., verifiedEmail ?? BuildPlaceholderEmail(...));

await _users.AddAsync(newUser, cancellationToken);
await _externalLogins.AddAsync(
    new ExternalLogin(newUser.Id, providerName, key, normalizedEmail), cancellationToken); // líneas 76-77
```

`AppUser.Email` usa `verifiedEmail` (correctamente nulo si no está verificado). Pero la fila `ExternalLogin` se crea con `normalizedEmail` — el correo **crudo**, sin condicionarlo a `emailVerified`.

**Este comportamiento es deliberado, no un descuido.** El comentario de las líneas 67-68 lo declara explícitamente: el correo sin verificar "queda únicamente como pista en `ExternalLogin.ProviderEmail`". INC-46 decidió conservar esa pista a propósito. Esto tiene una consecuencia directa sobre la decisión 2 del maintainer: la Ruta A (condicionar la escritura a `emailVerified`) no es una corrección de un error, sino la **reversión de una decisión de diseño previa**, y destruye información que INC-46 quiso conservar.

Lo corrobora el test `UnverifiedEmail_ShouldNotFuseAccountsAndShouldProvisionANewOne` (`tests/Ludeka.UnitTests/Application/ExternalLoginServiceTests.cs:106-123`): llama con `emailVerified=false` y un correo que coincide con otra cuenta (`carlos@ludeka.es`); aunque el test no lo asegura por `assert`, la fila resultante tendría `ProviderEmail = "carlos@ludeka.es"` sin verificar. En cambio, en la rama 2 (correo verificado, líneas 60-61) el test `VerifiedEmailMatchingExistingAccount_...` (líneas 65-86) sí fija por `assert` que `link.ProviderEmail` es exactamente el correo verificado (línea 85).

**Conclusión verificable**: "una cuenta tiene correo verificado si tiene alguna fila con `ProviderEmail` no nulo" es **falso hoy** para cuentas creadas en la rama 3 con un correo no verificado. La opción "preferida sin migración" de INC-49 §2.5 es viable, pero **no como derivación pasiva de los datos existentes**.

### 3. Infraestructura OAuth para vincular desde sesión activa

- Único endpoint de desafío: `POST /login/external` en `Program.cs:455-482`. Valida antiforgery manualmente (líneas 460-467, 400 si falta token), lee el proveedor del formulario y llama `Results.Challenge(new AuthenticationProperties { RedirectUri = "/" }, [registration.Scheme])` (línea 481). **No recibe `returnUrl` ni ningún parámetro de intención** — el `RedirectUri` está fijo a `"/"`.
- El resultado del desafío siempre llega a `ExternalLoginEvents.HandleTicketReceivedAsync` (`src/Ludeka.Web/Authentication/ExternalLoginEvents.cs:37-72`), suscrito vía `options.Events.OnTicketReceived` en `ConfigureExternalLogin` (líneas 30-35) para los tres proveedores (`ExternalAuthenticationSchemes.cs:166,180,195`). Ese manejador **siempre** llama `IExternalLoginService.ResolveAsync` (línea 60): no hay ninguna rama que distinga "vincular desde sesión activa" de "iniciar sesión".
- **No existe ningún punto de extensión ya construido** para un callback de vinculación distinto. Haría falta: (a) marcar la intención en `AuthenticationProperties.Items` al invocar el `Challenge` (por ejemplo, desde un endpoint nuevo `POST /cuenta/conexiones/vincular` que capture el `UserId` de la sesión activa antes del desafío), y (b) leer ese ítem en `HandleTicketReceivedAsync` para bifurcar hacia un método nuevo (`LinkAsync(currentUserId, provider, providerKey, email, emailVerified)`) en vez de `ResolveAsync`. Coincide exactamente con lo que anticipó y aplazó el diseño de INC-46 (`design.md:14`).
- El formulario de `Login.razor:35` usa `<form method="post" action="/login/external" data-enhance="false">` — una petición HTTP clásica, **no** un `@onclick` de Blazor Interactive Server. Es deliberado: `Results.Challenge` necesita escribir cabeceras HTTP de redirección, algo que un circuito interactivo por SignalR no puede hacer directamente. Cualquier botón de "vincular" en `/cuenta/conexiones` deberá reproducir este patrón de formulario clásico hacia un endpoint minimal API, no un manejador de evento Blazor.
- Registro de esquemas y nombres visibles: `ExternalAuthenticationSchemes.cs` — `GetEnabledProviders` (líneas 78-99), `DisplayNameFor` (144-150), `SchemeFor`/`CallbackPathFor` (126-141). Reutilizable tal cual para listar "proveedores disponibles para vincular".

### 4. Superficie de UI de cuenta

**No existe ninguna página bajo `/cuenta` ni de ajustes de perfil** (búsqueda sin resultados en todo `src/Ludeka.Web`). Tampoco existe ningún patrón "página para cualquier usuario autenticado, sin permiso granular": las 11 páginas con `[Authorize(Policy = ...)]` (`AuthorizationPolicies.cs:14-24`, todas atadas a un `ModeratorPermission`) son de administración o moderación.

La página más parecida por audiencia es `MyLibrary.razor` (`/mi-ludoteca`, `@rendermode InteractiveServer`), pero **no** lleva `[Authorize]`: es pública, inyecta `ICurrentUserService` (línea 8) y muestra un aviso de "estás navegando sin sesión" cuando `!HasSession` (líneas 60-70), degradando en vez de redirigir.

`/cuenta/conexiones`, tal como la describe INC-49 §2.1, necesita el patrón de redirección real (`[Authorize]` simple, sin `Policy=`), que no tiene precedente exacto hoy pero está soportado por el mismo mecanismo: `Routes.razor:1-10` usa `AuthorizeRouteView` con `NotAuthorized → RedirectToLogin`, y `RedirectToLogin.razor:10-17` fuerza navegación a `/login` solo en render interactivo.

`MainLayout.razor` ya inyecta `ICurrentUserService` (línea 6), renderiza bloques condicionales por rol y permiso en la cabecera (líneas 100-198) y monta `<SessionGuard />` (línea 302). Es el punto de extensión natural para la decisión 3, pero **hoy `ICurrentUserService` no expone "¿tiene correo verificado?"**: habría que añadirlo al contrato o resolverlo con un servicio nuevo. No es gratis solo porque el fichero ya exista.

### 5. Auditoría

- `AuditAction` (`src/Ludeka.Core/Enums/AuditAction.cs:6-47`): 8 valores, incluido `LinkedFounderIdentity` (línea 46) — **definido pero sin ningún consumidor**: no aparece en ningún servicio, y `AuditService.GetActionDisplayName` (`AuditService.cs:114-124`) no tiene `case` para él (cae al `_ => "Operación"`). Confirma la nota de archivo de INC-46 (`design.md:83`): la página del fundador no se construyó.
- Patrón exacto para registrar una auditoría: `IAuditService.RecordChangeAsync(RecordAuditCommand)` (`IAuditService.cs:11-15`) → `AuditService.cs:27-55`, que exige `SessionIdentity.Require(command.UserId)` (línea 33, denegación controlada sin sesión) y construye un `AuditLogEntry` (`AuditLogEntry.cs:29-61`) vía `IAuditLogRepository.AddAsync`. Añadir `LinkedProvider`/`UnlinkedProvider` (valores de enum nuevos) y su rama en `GetActionDisplayName` es mecánico y sigue este patrón.

### 6. Sesión y permisos en servicios de escritura

- `ICurrentUserService` (`ICurrentUserService.cs:11-34`): `UserId`, `UserName`, `Roles`, `IsFoundingTeam`, `IsInRole`, `HasPermission`. Implementado por `AuthenticatedCurrentUserService` (`src/Ludeka.Web/Services/AuthenticatedCurrentUserService.cs:20-165`), que resuelve primero el snapshot del circuito y, si no, proyecta los claims de la cookie (líneas 73-101).
- `SessionIdentity.Require(...)` (`src/Ludeka.Application/Contracts/SessionIdentity.cs:23-40`, dos sobrecargas) es el punto único para "dame el `UserId` de sesión o lanza `UnauthorizedAccessException` controlada". Lo usan tanto `AuditService.cs:33` como `SessionPermissionGuard.cs:33`.
- `ISessionPermissionGuard.RequireAsync(permission, denialMessage, ct)` (`ISessionPermissionGuard.cs:14-26`, implementado en `SessionPermissionGuard.cs:15-47`) relee el `AppUser` **sin rastreo** (`AsNoTracking`, vía `IUserRepository.GetByIdAsync`) antes de cada escritura administrativa, y deniega si la cuenta ya no existe, está suspendida o no tiene el permiso exacto.
- **Matiz importante para el diseño**: vincular o desvincular un proveedor propio **no es una acción administrativa con `ModeratorPermission`**; es autoservicio de cualquier usuario autenticado sobre su propia cuenta. El patrón directamente reutilizable no es `ISessionPermissionGuard` (que exige una bandera de permiso concreta), sino `SessionIdentity.Require(_currentUser)` para obtener el `UserId` de forma fiable, más una relectura `AsNoTracking` del propio `AppUser` (mismo mecanismo, sin la comprobación de `ModeratorPermission`).

---

## Hallazgos que confirman o contradicen el documento de incremento

| # | Afirmación de INC-49 | Veredicto | Evidencia |
|---|---|---|---|
| 1 | Cascada `(Provider,ProviderKey)` → correo verificado → alta nueva | **Confirmado** | `ExternalLoginService.cs:43-79` |
| 2 | `AppUser.Email` obligatorio, excepción si vacío | **Confirmado**, cita de línea exacta | `AppUser.cs:40-41` |
| 3 | Ejemplo `discord_12345@ludeka.invalid` | **Impreciso** (formato real: `12345@discord.ludeka.invalid`) | `ExternalLoginService.cs:88-89` frente a `docs/specs/sistema/32-autenticacion-y-autorizacion.md:62`, que sí es correcto |
| 4 | "Derivar de `ExternalLogin`… no requiere migración destructiva y es la preferida" (§2.5) | **Parcialmente refutado**: no requiere migración destructiva, pero derivarlo de los datos que el código persiste hoy es incorrecto sin antes cambiar la rama 3 de `ResolveAsync` o añadir una columna aditiva. Además, esa escritura del correo crudo es una decisión deliberada de INC-46 (comentario de `ExternalLoginService.cs:67-68`), no un descuido | `ExternalLoginService.cs:67-68,76-77` + `ExternalLoginServiceTests.cs:106-123` |
| 5 | "Excepción segura y ya implementada" de vinculación automática por correo verificado sin proveedores previos (§2.4) | **Confirmado** — es exactamente la rama 2 de `ResolveAsync` | `ExternalLoginService.cs:54-64` |
| 6 | (No mencionado por el documento) | **Gap**: `IExternalLoginRepository` solo tiene `GetByProviderKeyAsync` y `AddAsync` — no hay forma de listar ni borrar los vínculos de un usuario. Toda la pantalla `/cuenta/conexiones` depende de un contrato que aún no existe | `IExternalLoginRepository.cs` completo |

---

## Áreas afectadas

- `src/Ludeka.Application/Features/Identity/ExternalLoginService.cs` e `IExternalLoginService.cs` — métodos nuevos `LinkAsync`/`UnlinkAsync` y posible ajuste de la rama 3 de `ResolveAsync` (decisión 2).
- `src/Ludeka.Application/Contracts/IExternalLoginRepository.cs` + `src/Ludeka.Infrastructure/Data/ExternalLoginRepository.cs` — añadir `ListByUserIdAsync` y `RemoveAsync`.
- `src/Ludeka.Web/Program.cs` (endpoints nuevos de vinculación) y `src/Ludeka.Web/Authentication/ExternalLoginEvents.cs` (bifurcación login frente a vinculación).
- `src/Ludeka.Web/Components/Pages/` — página nueva `/cuenta/conexiones` (no existe hoy; patrón más cercano: `MyLibrary.razor`).
- `src/Ludeka.Web/Components/Layout/MainLayout.razor` — si la decisión 3 incluye la cabecera.
- `src/Ludeka.Core/Enums/AuditAction.cs` + `src/Ludeka.Application/Features/Admin/AuditService.cs` — acciones de auditoría nuevas.
- `src/Ludeka.Application/Contracts/ICurrentUserService.cs` (o servicio nuevo) — exponer "¿correo verificado?" si se necesita en cabecera o perfil.
- Posible migración aditiva en `src/Ludeka.Infrastructure/Migrations/` + `SqliteSchemaMigrator.cs` si se opta por la Ruta B de la decisión 2.
- `tests/Ludeka.UnitTests/Application/ExternalLoginServiceTests.cs`, `Domain/ExternalLoginTests.cs`, `Infrastructure/ExternalLoginPersistenceTests.cs` y ficheros de test nuevos para vinculación, desvinculación y colisión.

---

## Evidencia para las decisiones pendientes del maintainer (§3 del incremento)

### Decisión 1 — Fusión asistida de cuentas duplicadas

Inventario verificado de entidades que cuelgan de `AppUser` (vía `LudekaDbContext.cs`, búsqueda exhaustiva de `UserId`/`AuthorUserId` y campos equivalentes):

| Entidad (dominio) | Campo | Restricción relevante | ¿FK real a `AppUsers`? |
|---|---|---|---|
| `UserCollectionItem` (ludoteca) | `UserId` | `(UserId,GameId)`, `(UserId,BggId)`, `(UserId,Status)`, `(UserId,IsPlayed)` — ninguno único | No |
| `GamePlayLog` (diario de partidas) | `UserId` | `(UserId,PlayDate)` | No |
| `GameLoan` (préstamos) | `UserId` | `(UserId,IsReturned)`, `(UserId,GameId)` | No |
| `UserGameReview` (reseñas) | `UserId` | `(UserId,GameId)` **ÚNICO** — colisión real si ambas cuentas reseñaron el mismo juego | No |
| `RuleQuestion` / `RuleAnswer` | `UserId` | Sin índice explícito | No |
| `RuleVote` (votos) | `UserId` | `(UserId,QuestionId)`, `(UserId,AnswerId)` | No |
| `UserPreference` (preferencias) | `UserId` | `UserId` **es la PK** (1:1 estricto) — exige decidir qué preferencias ganan | No |
| `FoundingVerdict` (veredictos) | `AuthorUserId` | Sin índice explícito | No |
| `GameIssueReport` (reportes) | `ReportedByUserId`, `ResolvedByUserId` | Sin índice sobre estos campos | No |
| `GameEditLog` (historial de edición) | `EditorUserId` | Sin índice explícito | No |
| `InstagramPostDraft` (borradores) | `CreatedByUserId` | Sin índice explícito | No |
| `AuditLogEntry` (bitácora) | `UserId` | Indexado | No |
| `ExternalLogin` (identidad externa) | `UserId` | `(Provider,ProviderKey)` único | **Sí — `Cascade`** |

**Lectura clave**: de 13 entidades con referencia a `AppUser`, **solo `ExternalLogin` tiene FK real** (`LudekaDbContext.cs:491-494`). El resto son columnas de texto sin integridad referencial forzada por EF Core: ni ventaja (no hay que romper una FK para reasignar), ni red de seguridad (nada impide una fila huérfana si se borra mal una cuenta).

Una herramienta de fusión sería, en esencia, un `UPDATE` masivo de `UserId` en 12 tablas más el borrado de la cuenta perdedora, **con dos puntos de conflicto real**: `UserGameReview` (índice único, puede exigir quedarse con una reseña y descartar la otra) y `UserPreference` (clave primaria 1:1, exige elegir qué preferencias prevalecen). Los permisos no son tabla aparte: viajan con la fila `AppUser` que se conserve como ganadora. No se localizó una entidad de medios con propietario individual más allá de `InstagramPostDraft.CreatedByUserId`; las colas de moderación (`SocialInboxItem`, `MonitoredSocialAccount`) no llevan `UserId` propio.

### Decisión 2 — Verificación por fila y reemplazo del correo sintético

| Enfoque | ¿Migración? | Cambio de código | Riesgo | Esfuerzo |
|---|---|---|---|---|
| **A. Corregir la rama 3 de `ResolveAsync`** — persistir `ProviderEmail` solo si `emailVerified` (si no, `null`) | Ninguna | `ExternalLoginService.cs:76-77` (un condicional) + tests que fijen el invariante nuevo | Bajo en lo técnico, pero **revierte una decisión deliberada de INC-46** (la "pista" de las líneas 67-68) y descansa en una convención que el esquema no refuerza: un cambio futuro podría romperla sin que la base de datos lo impida | Bajo |
| **B. Columna explícita** (`ProviderEmailVerifiedAt DateTimeOffset?` o `IsProviderEmailVerified bool`) en `ExternalLogins` | Sí, **aditiva y no destructiva** (migración EF Core nueva + actualizar `SqliteSchemaMigrator.cs`) | Igual que A, más el mapeo Fluent en `LudekaDbContext.cs` | Bajo. Auto-documentado, verificable por esquema, y **conserva la pista** que INC-46 quiso guardar | Medio |

Sobre el reemplazo del correo sintético en sí: es de bajo riesgo técnico una vez resuelto lo anterior. `AppUser.UpdateProfile(userName, email)` (`AppUser.cs:101-112`) ya es el mutador correcto e `IUserRepository.UpdateAsync` (`IUserRepository.cs:18`) ya existe. El único cuidado real es el índice único de `AppUsers.Email` (`LudekaDbContext.cs:363`): antes de reemplazar hay que comprobar que ningún otro `AppUser` tenga ya ese correo verificado (mismo criterio que aplica `GetByEmailAsync` en la cascada); si no, la actualización viola la restricción única. Es coherente con que el propio flujo de colisión (§2.4) ya exija ese chequeo antes de vincular.

Dónde se lee `AppUser.Email` fuera de la cascada: en `SqliteUserRepository.cs` (`GetByEmailAsync` línea 36 y filtro de búsqueda línea 58), en `UserManagementService.cs` (mapeo de comandos y DTO, líneas 63 y 184) con su UI exclusiva de administración `UserManagement.razor` (líneas 150 y 256, protegida por `PermisoGestionarUsuarios`), y en `AdminUserSeeder.cs` (semilla fija del fundador, no afectada). **`PublicProfile.razor` no muestra el correo en ningún punto**: reemplazar el correo sintético no rompe ninguna vista pública, solo el panel de administración y el índice único ya mencionado.

### Decisión 3 — Visibilidad del aviso

No se encontró ningún componente de "aviso de estado de cuenta" reutilizable ya construido. `MainLayout.razor` sí es un punto de extensión real y ya usado para condicionales por rol (líneas 100-198), así que colocar el aviso en cabecera es técnicamente viable, pero `ICurrentUserService` no expone hoy "¿tiene correo verificado?": habría que añadirlo al contrato (con impacto en todas las implementaciones, incluida `StubCurrentUserService.cs`) o resolverlo con un servicio nuevo inyectado aparte. Es mayormente una decisión de producto y de alcance de UI; no hay evidencia de código que la condicione más allá de ese coste de plomería.

---

## Pruebas existentes de autenticación (INC-46)

Suite total confirmada en los artefactos archivados: **1345/1345 pruebas, 0 errores, 0 omitidas** (`openspec/changes/archive/2026-09-16-change-46-autenticacion-real/archive-report.md:11`, coincidente con `tasks.md:164,170-171` y `apply-progress.md:47,49,122`).

Ficheros directamente relacionados con `ExternalLogin`:

- `tests/Ludeka.UnitTests/Domain/ExternalLoginTests.cs` (invariantes de la entidad)
- `tests/Ludeka.UnitTests/Application/ExternalLoginServiceTests.cs` (cascada completa: par reincidente, correo verificado, alta comunitaria, correo no verificado, `FoundingTeam` nunca autoconcedido, `ArgumentException` en claves vacías — líneas 51-146)
- `tests/Ludeka.UnitTests/Infrastructure/ExternalLoginPersistenceTests.cs` (persistencia EF)

46 ficheros adicionales tocan sesión, permisos y autorización (`SessionPermissionGuardTests.cs`, `AdministrativeWriteGuardContractTests.cs`, `AdministrativeWriteGuardTests.cs`, `AnonymityPolicyTests.cs`, `AnonymityPolicyPrivilegedServicesTests.cs`, `AnonymityPolicyTestBase.cs`, `CurrentUserContractTests.cs`, `SessionIdentityTests.cs`, `PolicyAuthorizationTests.cs`, `AuthorizationPipelineContractTests.cs`, `AuthenticatedCurrentUserServiceTests.cs`, `WebAuthenticationRegistrationTests.cs`, `LoginRedirectTests.cs`, `SessionDenialUiContractTests.cs`, `UserSessionInvalidatorTests.cs`, `UserCircuitHandlerTests.cs`, entre otros).

Convenciones confirmadas: xUnit `[Fact]` sin atributos de clase, `IAsyncLifetime` para montaje y desmontaje con SQLite `Filename=:memory:` (`ExternalLoginServiceTests.cs:17-42`), nombres `MetodoOEscenario_DebeComportamiento` en inglés (identificadores de código) y comentarios de intención en español. Doble de sesión reutilizable: `StubCurrentUserService` (`tests/Ludeka.UnitTests/Application/StubCurrentUserService.cs`) con factorías `Anonymous()`, `WithSession(userId, userName)` (líneas 34-40) y `PrivilegedWithoutSession()`, directamente reutilizable para las pruebas nuevas de vinculación desde sesión activa.

---

## Enfoques comparados

| Enfoque | Ventajas | Inconvenientes | Esfuerzo |
|---|---|---|---|
| **1A — Herramienta de fusión asistida (admin + auditoría)** | Resuelve el caso ya duplicado sin intervención manual repetida; auditable; mejor experiencia para el usuario afectado | Trabajo real: transacción multitabla sobre 12 entidades, dos puntos de conflicto genuino (`UserGameReview` único, `UserPreference` 1:1), UI de administración nueva, riesgo de error humano si la reasignación es parcial | Alto |
| **1B — Procedimiento manual documentado** | Cero código nuevo; el maintainer decide caso por caso con datos reales | No escala si el problema se repite; el mismo inventario de 13 entidades hay que tocarlo a mano por SQL cada vez, sin las garantías de una transacción probada | Bajo, pero recurrente |
| **2A — Corregir la semántica de `ResolveAsync` (sin migración)** | Cero migración; cambio mínimo y localizado | Invariante implícito no reforzado por el esquema; revierte una decisión deliberada de INC-46; exige tests que lo fijen para siempre | Bajo |
| **2B — Columna explícita de verificación (migración aditiva)** | Auto-documentado; verificable por esquema; no depende de disciplina de código futura; conserva la pista de INC-46 | Requiere migración EF y actualizar el reconciliador SQLite; más superficie de cambio | Medio |

---

## Estimación de volumen y entrega

Con el alcance de INC-49 §2.1-§2.8 y las decisiones más económicas de cada par (2A sin migración; 1B procedimiento manual):

| Área | Líneas estimadas (aprox.) |
|---|---|
| Página `/cuenta/conexiones` (Razor + lógica) | 150-250 |
| `IExternalLoginRepository`/`ExternalLoginRepository` (listar, borrar) + tests | 60-100 |
| `ExternalLoginService`/`IExternalLoginService` (`LinkAsync`, `UnlinkAsync`, guarda del último método, ajuste rama 3) + tests | 150-250 |
| Endpoints de vinculación en `Program.cs` / `ExternalLoginEvents.cs` (bifurcación de intención) | 80-120 |
| Flujo de colisión en el login existente (mensaje, sin fusión) + tests | 100-180 |
| Aviso de correo no verificado (servicio + UI en conexiones, y cabecera si aplica la decisión 3) | 60-120 |
| Auditoría (`LinkedProvider`/`UnlinkedProvider`) | 20-40 |
| Pruebas nuevas dedicadas | 250-400 |

**Total aproximado: 870-1460 líneas modificadas.** Supera holgadamente el presupuesto de 400 líneas por PR. Con la estrategia de entrega fijada en `auto-chain`, se recomienda partir en al menos 3 PRs encadenados desde el mismo worktree:

1. Contrato de repositorio + `LinkAsync`/`UnlinkAsync` + ajuste de verificación, con sus tests.
2. Endpoints de vinculación + página `/cuenta/conexiones` + guarda del último método.
3. Flujo de colisión en el login existente + avisos de correo no verificado + auditoría.

Si el maintainer opta por la herramienta de fusión (1A) o la columna explícita (2B), cada una añade al menos un PR adicional.

---

## Recomendación

Explorar sin decidir. La evidencia apunta a que la ruta de menor riesgo técnico es **2B** (columna aditiva de verificación), pese a ser algo más cara que 2A: conserva la pista que INC-46 guardó deliberadamente y deja el invariante reforzado por el esquema, no por disciplina de código. Combinada con **1B** (procedimiento manual documentado), deja la herramienta de fusión para cuando el problema se repita en volumen. Son decisiones de producto y de riesgo que corresponden al maintainer, no a esta fase.

---

## Riesgos

- El flujo de colisión (§2.4) exige tocar `ExternalLoginService.ResolveAsync`, hoy cubierto por pruebas verdes de INC-46: cualquier cambio necesita pruebas de regresión explícitas para no romper la cascada existente.
- Sin resolver la decisión 2, cualquier lectura de "correo verificado" construida sobre el dato actual sería incorrecta desde el primer commit.
- La ausencia de FK real en 12 de las 13 entidades relacionadas con `AppUser` significa que ni una fusión ni un borrado de cuenta tienen red de seguridad de integridad referencial en la base de datos: todo el cuidado recae en el código de aplicación.
- `/cuenta/conexiones` como página "solo para sesión iniciada" no tiene precedente exacto (las 11 páginas `[Authorize]` existentes exigen permiso granular, no solo autenticación); el diseño debe fijar explícitamente ese patrón nuevo.

---

## Listo para propuesta

Sí, con las tres decisiones de §3 del incremento explícitamente pendientes de confirmación del maintainer.
