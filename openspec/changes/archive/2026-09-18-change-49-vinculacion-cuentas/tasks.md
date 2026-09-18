# Tareas: INC-49 — Vinculación de Cuentas entre Proveedores

> **Cambio:** `change-49-vinculacion-cuentas` · **Fase:** `sdd-tasks` · **Fecha:** 2026-09-17
> **Entradas:** [`design.md`](design.md) (APTO CON SALVEDADES) · [`specs/`](specs/) (11 requisitos, 35 escenarios) · [`proposal.md`](proposal.md) · [`exploration.md`](exploration.md)
> **Strict TDD activo.** Runner contractual: `dotnet test Ludeka.sln`. Toda conducta nueva se escribe RED → GREEN, en ese orden de tareas.
> **Entrega:** `auto-chain` · `stacked-to-main` · presupuesto 400 líneas autoradas por PR (`additions + deletions`, artefactos generados excluidos).

## Aviso de entrega (lectura obligatoria antes de `sdd-apply`)

```text
Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High
```

`auto-chain` ya resuelve la decisión: el orquestador procede con la primera rebanada usando `stacked-to-main`, el mismo patrón de INC-46. **PR #2 y PR #3 llevan el riesgo real** (ver la previsión completa al final del documento) — no bloquean el arranque, pero exigen medir el diff real antes de abrir cada uno.

---

## Convenio de ramas de la cadena

Todas las ramas viven en el mismo worktree `C:\repos\ludeka-wt\vinculacion-cuentas`. PR #1 reutiliza la rama ya creada por el script; cada PR posterior parte de la rama del PR anterior (cadena lineal, más simple de revisar que abrir #5 en paralelo, tal y como el diseño §10 permite pero no exige).

| PR | Rama | Parte de | Verbo del script al cerrar |
|---|---|---|---|
| #1 | `inc/vinculacion-cuentas` | (rama del worktree, ya existe) | `scripts/sdd-worktree.ps1 pr vinculacion-cuentas` |
| #2 | `inc/vinculacion-cuentas-02-application` | `inc/vinculacion-cuentas` | `git checkout -b inc/vinculacion-cuentas-02-application` + apertura manual de PR con `gh` (el script `pr` está pensado para el primer PR del incremento; los siguientes abren PR con `gh pr create --base inc/vinculacion-cuentas --head inc/vinculacion-cuentas-02-application`) |
| #3 | `inc/vinculacion-cuentas-03-transporte` | `inc/vinculacion-cuentas-02-application` | ídem, `--base inc/vinculacion-cuentas-02-application` |
| #4 | `inc/vinculacion-cuentas-04-pantalla` | `inc/vinculacion-cuentas-03-transporte` | ídem |
| #5 | `inc/vinculacion-cuentas-05-colision` | `inc/vinculacion-cuentas-04-pantalla` | ídem |
| #6 | `inc/vinculacion-cuentas-06-aviso-cabecera` | `inc/vinculacion-cuentas-05-colision` | ídem |
| #7 | `inc/vinculacion-cuentas-07-reemplazo-correo` | `inc/vinculacion-cuentas-06-aviso-cabecera` | ídem, y este es el último: tras su PR, `scripts/sdd-worktree.ps1 done vinculacion-cuentas` sobre `main` una vez todos mergeados |

**Riesgo operativo anotado (no bloquea, ver sección de riesgos):** `scripts/sdd-worktree.ps1` documenta los verbos `new`/`pr`/`done` a nivel de un incremento con una única rama. Encadenar 7 ramas dentro del mismo worktree es un uso más fino que el script no cubre literalmente; `sdd-apply` debe confirmar en la práctica si `pr` acepta un `--base` explícito o si cada PR de la cadena (#2 en adelante) se abre a mano con `gh pr create --base <rama-anterior> --head <rama-actual>` tras el `git checkout -b` correspondiente, manteniéndose siempre dentro del mismo worktree y sin pushear nunca directamente a `main`.

---

## Paralelismo entre tareas y entre PRs

**Dentro de cada PR: 100% secuencial.** Es la naturaleza de Strict TDD — cada tarea `[RED]` debe fallar antes de escribir su `[GREEN]`, y varias tareas `[GREEN]` habilitan el siguiente `[RED]` (por ejemplo, 2.2 debe existir antes de que 2.3 tenga algo que ampliar). Ninguna tarea dentro de un PR es reordenable sin romper la cadena RED→GREEN.

**Entre PRs: cadena lineal por convención de revisión, con una única relajación real.** Las dependencias declaradas por `design.md` §10 son: PR#2←PR#1, PR#3←PR#2, PR#4←PR#3, PR#5←**solo PR#1**, PR#6←PR#3 y PR#4, PR#7←PR#2. La única dependencia genuinamente relajable es **PR #5**, que no necesita nada de PR#2/#3/#4 y podría desarrollarse en paralelo (rama propia partiendo de `inc/vinculacion-cuentas`) si el maintainer prefiriera paralelismo real sobre simplicidad de revisión. Este documento mantiene el orden lineal 1→7 (branch-off secuencial) porque el propio diseño lo señala como "más simple de revisar", no porque sea la única topología válida. El resto de la cadena (PR#1→#2→#3→#4, y PR#6, PR#7) no admite paralelismo sin reabrir la partición cerrada del diseño.

---

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| A+B+H | Esquema, contrato de repositorio ampliado y valores de auditoría | PR #1 | `dotnet test Ludeka.sln --filter "FullyQualifiedName~ExternalLoginTests\|FullyQualifiedName~ExternalLoginPersistenceTests\|FullyQualifiedName~SqliteSchemaMigratorTests\|FullyQualifiedName~UserManagementDomainTests\|FullyQualifiedName~UserManagementAndAuditServiceTests"` | N/A — sin superficie de usuario, solo esquema/dominio/repositorio | Revertir los 8 ficheros de producción + migración; `Down()` elimina la columna en PostgreSQL, SQLite ignora la columna sobrante |
| C (+DTOs/excepciones/mensajes) | `LinkAsync`/`UnlinkAsync` con la guarda del último método, en Application | PR #2 | `dotnet test Ludeka.sln --filter "FullyQualifiedName~ExternalLoginLinkingTests\|FullyQualifiedName~AccountConnectionMessagesTests"` | N/A — lógica de Application, sin transporte todavía | Revertir `LinkAsync`/`UnlinkAsync` y los 3 ficheros nuevos; nada los consume aún (el endpoint es PR #3) |
| D + `ExternalLoginIntent` + G1 | Endpoint de vinculación, bifurcación OAuth y contrato de lectura de conexiones | PR #3 | `dotnet test Ludeka.sln --filter "FullyQualifiedName~ExternalLoginIntentTests\|FullyQualifiedName~ExternalLoginEventsLinkBranchTests\|FullyQualifiedName~AccountConnectionsServiceTests\|FullyQualifiedName~AuthorizationPipelineContractTests"` | N/A parcial — el endpoint es alcanzable por HTTP pero sin botón en interfaz todavía (PR #4 no existe); una prueba manual con cliente HTTP no añade nada sobre las pruebas de contrato ya planificadas | Revertir el endpoint, la bifurcación y `IAccountConnectionsService`; sin `Items`, el camino de login queda byte a byte igual que hoy |
| E | Página `/cuenta/conexiones` | PR #4 | `dotnet test Ludeka.sln --filter "FullyQualifiedName~AccountConnectionsPageContractTests"` | **Manual**: smoke test de navegador real — vincular con un proveedor de prueba → desvincular → intentar desvincular el último y comprobar la denegación (propuesta §11) | Revertir `AccountConnections.razor`; el endpoint y el servicio siguen operativos sin interfaz |
| F | Partición 2a/2b de `ResolveAsync` + aviso de colisión en login | PR #5 | `dotnet test Ludeka.sln --filter "FullyQualifiedName~ExternalLoginCascadeRegressionTests\|FullyQualifiedName~ExternalLoginServiceTests\|FullyQualifiedName~AccountConnectionMessagesTests\|FullyQualifiedName~LoginRedirectTests"` | N/A — opcional y no exigido por la propuesta §11: exigiría dos proveedores OAuth reales entregando el mismo correo verificado | Revertir la partición devuelve `ResolveAsync` a la rama 2 única de INC-46; independiente de PR #2/#3/#4 |
| G2 | Aviso descartable en cabecera | PR #6 | `dotnet test Ludeka.sln --filter "FullyQualifiedName~AuthorizationPipelineContractTests"` | N/A — solo visual, sin comportamiento de servidor nuevo | Revertir `AccountEmailNotice.razor` y su montaje; ningún otro fichero depende de él |
| I | Reemplazo del correo sintético | PR #7 | `dotnet test Ludeka.sln --filter "FullyQualifiedName~ExternalLoginLinkingTests"` | N/A — exigiría un proveedor OAuth real que pase de no-verificado a verificado entre dos accesos | Revertir el método privado y su llamada; `LinkAsync` sigue funcionando sin el reemplazo |

Gate final de cada PR, además del comando enfocado: `dotnet test Ludeka.sln` completo en verde (1345 + las pruebas nuevas del PR, sin regresión).

---

## PR #1 — Cimientos de datos y contrato (Unidades A + B + H)

**Depende de:** `inc/vinculacion-cuentas` (base). **Líneas recalculadas:** producción 103-166, pruebas 138-220 → **≈240-385**.

- [x] 1.1 **[RED]** Ampliar `tests/Ludeka.UnitTests/Domain/ExternalLoginTests.cs` con 4 casos del invariante nuevo: (a) `providerEmailVerified: true` + `providerEmail` no nulo ⇒ `ProviderEmailVerifiedAt == LinkedAt`; (b) `providerEmailVerified: true` + `providerEmail` nulo ⇒ `ProviderEmailVerifiedAt == null`; (c) `providerEmailVerified: false` ⇒ `ProviderEmailVerifiedAt == null`; (d) el 5.º argumento posicional (`linkedAt`) de `ExternalLoginTests.cs:20` sigue funcionando sin nombrarlo. Deben fallar: ni la propiedad ni el 6.º parámetro existen todavía.
  *Cubre:* social-login-authentication — invariante base de "Verificación registrada..." / "Ausencia de verificación..." (cierre completo en 5.1/5.2).
- [x] 1.2 **[GREEN]** Modificar `src/Ludeka.Core/Entities/ExternalLogin.cs`: añadir la propiedad `ProviderEmailVerifiedAt` (`DateTimeOffset?`) y el 6.º parámetro opcional `providerEmailVerified = false` **al final** del constructor (nunca en la 5.ª posición: rompería `ExternalLoginTests.cs:20`). Calcular `ProviderEmailVerifiedAt = providerEmailVerified && ProviderEmail is not null ? LinkedAt : null`. Hace pasar 1.1.
- [x] 1.3 **[RED]** Ampliar `tests/Ludeka.UnitTests/Infrastructure/ExternalLoginPersistenceTests.cs`: (a) round-trip de `ProviderEmailVerifiedAt` vía `EnsureCreatedAsync` (modelo Fluent nuevo); (b) `ListByUserIdAsync` devuelve solo las filas del usuario indicado; (c) `RemoveAsync` borra la fila; (d) el índice único `(Provider, ProviderKey)` sigue rechazando un duplicado tras `RemoveAsync` + `AddAsync`. Deben fallar: `ListByUserIdAsync`/`RemoveAsync` no existen en el contrato.
  *Cubre:* social-login-authentication, escenario "Columna presente en una base de datos migrada desde cero" (vía el modelo Fluent reflejado por `EnsureCreatedAsync`, el mecanismo que ya usa este proyecto de pruebas en vez de aplicar migraciones reales contra SQLite).
- [x] 1.4 **[GREEN]** Modificar `src/Ludeka.Application/Contracts/IExternalLoginRepository.cs`: añadir `Task<IReadOnlyList<ExternalLogin>> ListByUserIdAsync(string userId, CancellationToken ct = default)` y `Task RemoveAsync(ExternalLogin externalLogin, CancellationToken ct = default)`. **Ningún método de actualización** (invariante de la sección 3.1 del diseño: reasignar una fila entre cuentas debe ser estructuralmente imposible).
- [x] 1.5 **[GREEN]** Implementar los dos métodos en `src/Ludeka.Infrastructure/Data/ExternalLoginRepository.cs` y añadir el mapeo Fluent `externalLogin.Property(l => l.ProviderEmailVerifiedAt);` en `src/Ludeka.Infrastructure/Data/LudekaDbContext.cs` inmediatamente después de la línea 489 (`externalLogin.Property(l => l.ProviderEmail).HasMaxLength(200);`). Hace pasar 1.3.
- [x] 1.6 **Generar la migración EF Core aditiva** (prerrequisito operativo, no verificable por `sdd-design`): `dotnet ef migrations add AddProviderEmailVerifiedAtToExternalLogins --project src/Ludeka.Infrastructure --startup-project src/Ludeka.Web`. Produce `src/Ludeka.Infrastructure/Migrations/<timestamp>_AddProviderEmailVerifiedAtToExternalLogins.cs` con `AddColumn<DateTimeOffset>("ProviderEmailVerifiedAt", "ExternalLogins", nullable: true)` en `Up` y `DropColumn` en `Down`, más `<timestamp>_AddProviderEmailVerifiedAtToExternalLogins.Designer.cs` y la actualización de `LudekaDbContextModelSnapshot.cs`. **Los dos últimos son artefactos generados: quedan fuera del presupuesto de 400 líneas, pero la descripción del PR #1 debe declararlo explícitamente.**
- [x] 1.7 **[RED]** Ampliar `tests/Ludeka.UnitTests/Infrastructure/SqliteSchemaMigratorTests.cs` con el caso de reconciliación: crear a mano la tabla `ExternalLogins` con exactamente las 6 columnas de INC-46 (sin `ProviderEmailVerifiedAt`), insertar una fila, ejecutar `EnsureSchemaUpToDateAsync`, y afirmar (1) `PRAGMA table_info` incluye `ProviderEmailVerifiedAt`, (2) la fila preexistente sigue intacta, (3) `db.ExternalLogins.ToListAsync()` no lanza `no such column`. Debe fallar: el bloque 23 no existe todavía.
  *Cubre:* social-login-authentication, escenario "Columna reconciliada en una base SQLite preexistente".
- [x] 1.8 **[GREEN]** Modificar `src/Ludeka.Infrastructure/Data/SqliteSchemaMigrator.cs`: añadir `"ProviderEmailVerifiedAt" TEXT NULL,` al `CREATE TABLE` del bloque 22 (dentro de `:844-862`, tras la línea `"ProviderEmail" TEXT NULL,`), para que una base SQLite **creada desde cero** ya incluya la columna.
- [x] 1.9 **[GREEN]** Modificar el mismo fichero: añadir el **bloque 23 nuevo**, inmediatamente después del 22, con la reconciliación `ALTER TABLE "ExternalLogins" ADD COLUMN "ProviderEmailVerifiedAt" TEXT NULL;` protegida por `PRAGMA table_info` (patrón idéntico a `Giveaways.IsPromoted` y `Stores.Country`), para una base **ya existente**. Hace pasar 1.7. **Doble frente de esquema: 1.8 (creación) y 1.9 (reconciliación) son dos cambios distintos, no uno solo — no los fusiones en un único commit lógico si el diseño alguna vez sugiere lo contrario.**
- [x] 1.10 **[RED]** Ampliar `tests/Ludeka.UnitTests/Domain/UserManagementDomainTests.cs` con una aserción de que `AuditAction.LinkedProvider` y `AuditAction.UnlinkedProvider` son valores distintos entre sí y de todos los ya definidos (sigue el patrón de aserción de ordinal explícito de la línea 178: `Assert.Equal(7, (int)AuditAction.LinkedFounderIdentity)`). Debe fallar: los valores no existen.
  *Cubre:* user-management-permissions-audit, escenario "Los valores nuevos no colisionan con los existentes".
- [x] 1.11 **[GREEN]** Modificar `src/Ludeka.Core/Enums/AuditAction.cs`: añadir `LinkedProvider` y `UnlinkedProvider` al final (valores 8 y 9), siguiendo el precedente vivo de `LinkedFounderIdentity` (definido sin consumidor todavía). Hace pasar 1.10.
- [x] 1.12 **[RED]** Ampliar `tests/Ludeka.UnitTests/Application/UserManagementAndAuditServiceTests.cs` con dos `[Fact]`: `AuditService.GetActionDisplayName(AuditAction.LinkedProvider)` y `(AuditAction.UnlinkedProvider)` devuelven un texto en español específico, distinto de `"Operación"` y entre sí. **Nota de ubicación:** `GetActionDisplayName` no tiene ninguna prueba dedicada hoy en todo el repositorio (verificado); se añade aquí por ser el fichero de pruebas de Application más cercano a `AuditService`, no por un precedente exacto. Deben fallar: caen al `_ => "Operación"` por defecto.
  *Cubre:* user-management-permissions-audit, escenarios "Nombre visible de LinkedProvider" y "Nombre visible de UnlinkedProvider".
- [x] 1.13 **[GREEN]** Modificar `src/Ludeka.Application/Features/Admin/AuditService.cs`: añadir las dos ramas en `GetActionDisplayName` (`:114-124`): `AuditAction.LinkedProvider => "Vinculación de Proveedor"`, `AuditAction.UnlinkedProvider => "Desvinculación de Proveedor"`. Hace pasar 1.12.

**No regresión de este PR:** el 6.º parámetro de `ExternalLogin` es opcional y va al final, así que `ExternalLoginTests.cs:20` y las 6 pruebas de `ExternalLoginServiceTests.cs:51-146` no se tocan y siguen en verde. Los valores nuevos de `AuditAction` sin consumidor siguen exactamente el precedente vivo de `LinkedFounderIdentity`.

---

## PR #2 — Vinculación y desvinculación en Application (Unidad C + resultados/excepciones/mensajes)

**Depende de:** PR #1. **Líneas recalculadas:** producción 143-229, pruebas 162-238 → **≈305-465** (banda alta con riesgo — ver previsión final).

- [x] 2.1 **[RED]** Crear `tests/Ludeka.UnitTests/Application/ExternalLoginLinkingTests.cs` (armazón `IAsyncLifetime` con SQLite `Filename=:memory:`, siguiendo el patrón de `ExternalLoginServiceTests.cs:17-42`) con el primer caso: `LinkAsync` crea la fila `ExternalLogin` para el `UserId` indicado cuando no hay vínculo previo, y no aprovisiona ningún `AppUser` nuevo. Debe fallar: `LinkAsync` no existe en `IExternalLoginService`.
  *Cubre:* account-provider-connections, escenario "Vincular un proveedor no vinculado previamente"; social-login-authentication, escenario "Vincular un proveedor adicional desde sesión activa" (mismo comportamiento, dos especificaciones).
- [x] 2.2 **[GREEN]** Crear `src/Ludeka.Application/Features/Identity/AccountConnectionDtos.cs` con `ExternalLoginLinkOutcome` (`Linked`, `AlreadyLinkedToThisAccount`, `RejectedOwnedByAnotherAccount`), `ExternalLoginLinkResult(Outcome, Provider, User, AccountEmailReplaced = false)`, `AccountConnectionDto` y `AccountConnectionsView` (estos dos últimos sin consumidor hasta PR #3, mismo patrón que un tipo definido antes de su primer uso). Añadir `LinkAsync(userId, provider, providerKey, email, emailVerified, ct)` a `IExternalLoginService.cs` y su implementación mínima (camino feliz) en `ExternalLoginService.cs`: relee el `AppUser` vía `SessionIdentity.Require` + `AsNoTracking`, crea la fila si no hay vínculo previo. Hace pasar 2.1.
- [x] 2.3 **[RED]** Ampliar `ExternalLoginLinkingTests.cs`: caso `AlreadyLinkedToThisAccount` (mismo `UserId`, mismo par, no crea fila nueva) y caso `RejectedOwnedByAnotherAccount` (comprobación previa: el par pertenece a otro `UserId`, se rechaza, la fila existente conserva su dueño).
  *Cubre:* account-provider-connections, escenario "Rechazo cuando el proveedor ya pertenece a otra cuenta".
- [x] 2.4 **[GREEN]** Completar `LinkAsync` con la comprobación previa (`GetByProviderKeyAsync` antes de cualquier escritura, sección 3.1 del diseño). Hace pasar 2.3.
- [x] 2.5 **[RED]** Ampliar `ExternalLoginLinkingTests.cs`: simular una carrera (`DbUpdateException` en el `AddAsync`, o dos intentos sobre el mismo par) y afirmar que el resultado es el **mismo** `RejectedOwnedByAnotherAccount`, y que la fila original nunca cambia de `UserId`.
  *Cubre:* account-provider-connections, escenario "La fila en conflicto nunca cambia de propietario".
- [x] 2.6 **[GREEN]** Envolver el `AddAsync` de `LinkAsync` en `catch (DbUpdateException)` devolviendo el mismo resultado de rechazo. Hace pasar 2.5.
- [x] 2.7 **[RED]** Crear `tests/Ludeka.UnitTests/Application/AccountConnectionMessagesTests.cs`: el mensaje de rechazo (titular + detalle) no contiene «fusion», «fusión», «transferir», «traspas», «soporte», «contacta»; y dirige a desvincular desde la otra cuenta, nunca a un traspaso automático.
  *Cubre:* account-provider-connections, escenario "El mensaje de rechazo no promete una resolución inexistente".
- [x] 2.8 **[GREEN]** Crear `src/Ludeka.Application/Features/Identity/AccountConnectionMessages.cs` con el texto exacto de la sección 3.1 del diseño (titular y detalle del rechazo) y el texto de la guarda del último método (usado en 2.12). Hace pasar 2.7; conectar 2.4/2.6 para que usen esta constante en vez de un literal embebido.
- [x] 2.9 **[RED]** Ampliar `ExternalLoginLinkingTests.cs`: `UnlinkAsync` elimina la fila cuando la cuenta tiene 2 o más vínculos.
  *Cubre:* account-provider-connections, escenario "Desvincular procede cuando queda al menos otro método".
- [x] 2.10 **[GREEN]** Añadir `UnlinkAsync(userId, provider, ct)` a `IExternalLoginService.cs`/`ExternalLoginService.cs`: `SessionIdentity.Require` + relectura `AsNoTracking`, `ListByUserIdAsync`, búsqueda del proveedor indicado, idempotente si no existía (sin la guarda todavía). Hace pasar 2.9.
- [x] 2.11 **[RED]** Ampliar `ExternalLoginLinkingTests.cs`: desvincular el único vínculo se deniega con `LastAccessMethodException`, y esa denegación se produce llamando **directamente** a `UnlinkAsync` desde la prueba (es, por construcción, la "petición que evita la interfaz": una prueba de `Application` no pasa por ningún botón).
  *Cubre:* account-provider-connections, escenarios "Intento de desvincular el único proveedor es denegado" y "Una petición que evita la interfaz también se deniega".
- [x] 2.12 **[GREEN]** Crear `src/Ludeka.Application/Features/Identity/AccountConnectionExceptions.cs` con `LastAccessMethodException` (usada aquí) y `ExternalLoginCollisionException` (sin consumidor hasta PR #5, mismo patrón de tipo definido antes de su primer uso). Añadir `if (links.Count <= 1) throw new LastAccessMethodException(AccountConnectionMessages.LastAccessMethodDenied);` en `UnlinkAsync`. Hace pasar 2.11.
- [x] 2.13 **[RED]** Ampliar `ExternalLoginLinkingTests.cs`: vincular y desvincular con éxito registran una entrada de auditoría (`LinkedProvider`/`UnlinkedProvider`) asociada al `UserId` de la sesión; un intento denegado (`LastAccessMethodException`) o rechazado (`RejectedOwnedByAnotherAccount`) **no** registra ninguna entrada.
  *Cubre:* account-provider-connections, los 3 escenarios de "Auditoría de vincular y desvincular".
- [x] 2.14 **[GREEN]** Modificar el constructor de `ExternalLoginService` para aceptar `IAuditService? audit = null` (parámetro **opcional**, para que `ExternalLoginServiceTests.cs:35` — `new ExternalLoginService(new ExternalLoginRepository(_context), new SqliteUserRepository(_context))` — siga compilando sin tocarla) y añadir las llamadas a `audit?.RecordChangeAsync(...)` en el único camino de éxito de `LinkAsync` y `UnlinkAsync` (nunca antes de lanzar una excepción ni en un resultado distinto de `Linked`). Verificar si `IAuditService` ya está registrado en el contenedor de `Program.cs` (debería estarlo desde el incremento de auditoría existente) antes de añadir un registro nuevo. Hace pasar 2.13.

**No regresión de este PR:** `IAuditService` entra como parámetro opcional, así que `ExternalLoginServiceTests.cs:35` y las 6 pruebas de esa clase no se tocan y siguen en verde.

---

## PR #3 — Transporte OAuth y contrato de lectura (Unidad D + `ExternalLoginIntent` + G1 + `AccountConnectionRoutes`)

**Depende de:** PR #2. **Líneas recalculadas:** producción 168-265, pruebas 195-305 → **≈365-570** (banda alta con riesgo real — ver previsión final; es el PR con mayor riesgo de la cadena).

> **Gap de diseño cerrado aquí (no estaba en `design.md` §5.1):** el código de la sección D1/D4 del diseño usa `AccountConnectionRoutes.Page`, `.LoginWithLinkWithoutSession`, `.PageWithSessionChanged`, `.PageWithResult(outcome)` como si ya existiera, pero ningún fichero se llama así en el repositorio (confirmado por búsqueda) ni figura en la tabla de "Cambios por fichero" del diseño. La tarea 3.3 lo crea.

- [x] 3.1 **[RED]** Crear `tests/Ludeka.UnitTests/Web/ExternalLoginIntentTests.cs`: `MarkLink`/`TryReadLink` hacen ida y vuelta correctamente sobre `AuthenticationProperties.Items`; `TryReadLink` devuelve `false` si `ludeka:intent` está ausente o vale algo distinto de `link`; **`TryReadLink` solo lee de `Items`, nunca de un formulario ni de la cadena de consulta** (prueba pura, sin HTTP real). Debe fallar: la clase no existe.
  *Cubre:* account-provider-connections, escenario "La identidad vinculada es siempre la de la sesión, no la del cliente" (junto con 3.11, que además nunca lee un `userId` de formulario).
- [x] 3.2 **[GREEN]** Crear `src/Ludeka.Web/Authentication/ExternalLoginIntent.cs`: clase estática y pura, `IntentKey = "ludeka:intent"`, `LinkValue = "link"`, `UserIdKey = "ludeka:link_user_id"`, `MarkLink(properties, userId)`, `TryReadLink(properties, out userId)`. Hace pasar 3.1.
- [x] 3.3 **[GREEN, mecánica]** Crear `src/Ludeka.Web/Authentication/AccountConnectionRoutes.cs`: clase estática con las rutas cerradas que usan las tareas 3.5/3.7/3.11 — `Page = "/cuenta/conexiones"`, `LoginWithLinkWithoutSession = "/login?aviso=vinculacion-sin-sesion"`, `PageWithSessionChanged = "/cuenta/conexiones?resultado=sesion-cambiada"`, `PageWithResult(ExternalLoginLinkOutcome outcome)` que traduce cada valor del enum a un código cerrado (`vinculado`, `ya-vinculado`, `en-uso`). Sin lógica condicional propia que justifique un RED dedicado: se ejercita indirectamente por 3.6/3.7 y por `AccountConnectionsPageContractTests` (PR #4).
- [x] 3.4 **[RED]** Crear `tests/Ludeka.UnitTests/Web/ExternalLoginEventsLinkBranchTests.cs` (construye un `TicketReceivedContext` real, siguiendo la exigencia de la sección 4/D1 del diseño de no asumir el contrato de `HandleResponse()`): caso "sesión coincide con la intención" — `LinkAsync` se invoca, `context.Principal` se reconstruye, `context.Properties.IsPersistent`/`AllowRefresh` quedan en `true`. Debe fallar: no existe bifurcación todavía.
- [x] 3.5 **[GREEN]** Modificar `src/Ludeka.Web/Authentication/ExternalLoginEvents.cs`: cambiar `HandleTicketReceivedAsync` de `private static` a `public static` (para ser invocable desde la prueba, precedente ya sentado por `BuildSessionPrincipal`); insertar la bifurcación `if (ExternalLoginIntent.TryReadLink(context.Properties, out var intendedUserId))` **antes** de la llamada a `ResolveAsync` (`:60`), con el camino feliz: llamar a `loginService.LinkAsync(...)`, y si `Outcome == Linked`, reconstruir el principal y redirigir a `AccountConnectionRoutes.PageWithResult(result.Outcome)`. Hace pasar 3.4.
- [x] 3.6 **[RED]** Ampliar `ExternalLoginEventsLinkBranchTests.cs`: caso "sin sesión al volver" (`context.HandleResponse()` invocado, `Response.StatusCode == 302`, `Location == AccountConnectionRoutes.LoginWithLinkWithoutSession`, no se firma ninguna cookie) y caso "sesión de otro usuario" (`HandleResponse()`, redirección a `AccountConnectionRoutes.PageWithSessionChanged`, tampoco se vincula).
  *Cubre:* la tabla de D1 del diseño ("Qué ocurre si al volver del proveedor la sesión ya no coincide"), threat-matrix fila "Sesión en el retorno".
- [x] 3.7 **[GREEN]** Completar la bifurcación: reconfirmar `intendedUserId` contra `context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)` **antes** de llamar a `LinkAsync`; las dos ramas de fallo (`HandleResponse()` + redirección correspondiente) devuelven **sin** invocar `LinkAsync`. Hace pasar 3.6.
- [x] 3.8 **[RED]** Crear `tests/Ludeka.UnitTests/Application/AccountConnectionsServiceTests.cs` (fixture SQLite): `GetConnectionsAsync` sin sesión devuelve una vista vacía; con sesión, devuelve cada proveedor habilitado con su estado de vinculación; `HasVerifiedProviderEmailAsync` devuelve `false` sin sesión, `false` con sesión pero sin ninguna fila verificada, y `true` con al menos una fila `ProviderEmailVerifiedAt` no nula. Debe fallar: el contrato no existe.
  *Cubre:* account-provider-connections, escenario "Listado de proveedores habilitados y su estado de vinculación" (nivel de servicio) y los escenarios "Aviso ausente cuando existe correo verificado" / base de "Aviso visible..." de la capacidad de aviso (nivel de servicio, antes de la interfaz).
- [x] 3.9 **[GREEN]** Crear `src/Ludeka.Application/Features/Identity/IAccountConnectionsService.cs` (interfaz, `GetConnectionsAsync`, `HasVerifiedProviderEmailAsync`) y `AccountConnectionsService.cs` (implementación sobre `IExternalLoginRepository` + `ICurrentUserService`, con caché de ámbito por petición/circuito, sin tocar `ICurrentUserService`). Hace pasar 3.8.
- [x] 3.10 **[RED]** Ampliar `tests/Ludeka.UnitTests/Web/AuthorizationPipelineContractTests.cs` con un `[Fact]` hermano de `PublicEndpoint_ShouldDeclareAllowAnonymous` (`:73-83`) que afirme que el bloque del endpoint `/cuenta/conexiones/vincular` (desde su ruta hasta el siguiente `app.Map`) declara `.RequireAuthorization()`. Debe fallar: el endpoint no existe todavía.
  *Cubre:* threat-matrix fila "Autorización del endpoint".
- [x] 3.11 **[GREEN]** Modificar `src/Ludeka.Web/Program.cs`: añadir `POST /cuenta/conexiones/vincular` **después** del bloque `/logout` (`:484-488`, para no alterar el delimitador de bloque que usa la prueba `PublicEndpoint_ShouldDeclareAllowAnonymous`), con validación antiforgery manual (igual que `/login/external:460-467`), lectura de `userId` **exclusivamente** de `httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)` (nunca de un campo del formulario), `ExternalLoginIntent.MarkLink(properties, userId)` y `Results.Challenge`, con `.RequireAuthorization()`. Registrar `IAccountConnectionsService` en el contenedor de DI junto a los registros existentes de identidad. Hace pasar 3.10.

**No regresión de este PR:** el endpoint se mapea tras `/logout`, así que `PublicEndpoint_ShouldDeclareAllowAnonymous` sigue delimitando igual los bloques de `/login/external` y `/logout`. Sin `ludeka:intent` en `Items`, el camino de acceso (login) de `HandleTicketReceivedAsync` queda byte a byte igual que hoy.

---

## PR #4 — Pantalla `/cuenta/conexiones` (Unidad E)

**Depende de:** PR #3. **Líneas recalculadas:** producción ~120-190, pruebas ~40-65 → **≈160-255**.

- [x] 4.1 **[RED]** Crear `tests/Ludeka.UnitTests/Web/AccountConnectionsPageContractTests.cs` (idioma de lectura de fuente de `AuthorizationPipelineContractTests.cs`, **no** un caso más en la `TheoryData` de `ProtectedPages` porque esa tabla exige `Policy =` en la aserción): afirma que `AccountConnections.razor` declara `@page "/cuenta/conexiones"`, declara `@attribute [Authorize]` y **no** declara `[Authorize(Policy`; y que el formulario de vinculación declara `data-enhance="false"` y `<AntiforgeryToken />`. Debe fallar: el fichero no existe.
  *Cubre:* account-provider-connections, escenarios "Anónimo en render estático (SSR) no accede al contenido", "Anónimo en render interactivo es redirigido al inicio de sesión" y "Autenticado sin ningún permiso de moderación accede a la página" (a nivel de contrato de fuente; la garantía de ejecución la aportan el pipeline ya probado de `AuthorizeRouteView`/`RedirectToLogin` y el smoke test de 4.5).
- [x] 4.2 **[GREEN]** Crear `src/Ludeka.Web/Components/Pages/AccountConnections.razor`: `@page "/cuenta/conexiones"`, `@attribute [Authorize]` (sin `Policy=`), listado de proveedores habilitados (`ExternalAuthenticationSchemes.GetEnabledProviders`/`DisplayNameFor`) junto con su estado de vinculación, obtenido de `IAccountConnectionsService.GetConnectionsAsync()`. Hace pasar la parte de listado de 4.1 y cierra el escenario "Listado de proveedores habilitados y su estado de vinculación" a nivel de página (el nivel de servicio ya lo cierra 3.8).
- [x] 4.3 **[GREEN]** Ampliar `AccountConnections.razor`: formulario de vinculación por proveedor no vinculado (`<form method="post" action="/cuenta/conexiones/vincular" data-enhance="false">` + `<AntiforgeryToken />` + `<input type="hidden" name="provider">`, patrón de `Login.razor:35`) y botón de desvinculación por proveedor vinculado (`@onclick` → `IExternalLoginService.UnlinkAsync` + refresco del listado en el circuito, capturando `LastAccessMethodException` para mostrar el mensaje de guarda).
- [x] 4.4 **[GREEN]** Ampliar `AccountConnections.razor`: leer `[SupplyParameterFromQuery] string? resultado` y traducirlo a un mensaje fijo según la tabla cerrada de D4 (`vinculado`, `ya-vinculado`, `en-uso` → mensaje de `AccountConnectionMessages` de PR #2, ausente/desconocido → sin mensaje); y el bloque de aviso de correo no verificado (`if (!await Connections.HasVerifiedProviderEmailAsync())`, reutilizando el texto de `AccountConnectionMessages`).
  *Cubre:* account-provider-connections, escenario "Aviso visible en la pantalla de conexiones" (el escenario "ausente cuando existe correo verificado" ya lo prueba 3.8 a nivel de servicio).
- [ ] 4.5 **Verificación manual (no automatizable sin credenciales OAuth reales en el repositorio):** smoke test de navegador real del ciclo completo — vincular un proveedor de prueba → desvincular → intentar desvincular el último método y comprobar la denegación. Es el criterio de aceptación explícito de la propuesta §11 y el único punto donde PR #2 (guarda), PR #3 (transporte) y PR #4 (interfaz) se comprueban juntos.

**No regresión de este PR:** la página nueva no entra en la `TheoryData` de `ProtectedPages` (exige `Policy=`); se cubre con el `[Fact]` propio de 4.1.

---

## PR #5 — Flujo de colisión: partición 2a/2b de `ResolveAsync` (Unidad F)

**Depende de:** PR #1 (encadenado linealmente tras PR #4 por simplicidad de revisión, aunque el diseño confirma que no depende de #2/#3/#4). **Líneas recalculadas:** producción 93-155, pruebas 160-228 → **≈250-380**.

> **Criterio de aceptación explícito y obligatorio de este PR** (exigido por el encargo y verificado por `sdd-design` prueba a prueba): las **6 pruebas existentes** de `tests/Ludeka.UnitTests/Application/ExternalLoginServiceTests.cs:51-146` deben seguir en verde **sin modificar ni una sola línea de ese fichero**. Si `sdd-apply` necesita editar alguna de ellas para que la suite compile o pase, es señal de que la implementación se ha desviado del diseño — no de que la prueba estuviera mal — y debe detenerse a reconsiderar el enfoque antes de continuar.

- [x] 5.1 **[RED]** Crear `tests/Ludeka.UnitTests/Application/ExternalLoginCascadeRegressionTests.cs` con las **6 pruebas de regresión obligatorias** de la sección 7 del diseño, en este orden y con estos nombres exactos:
  1. `RepeatPair_ShouldStillResolveWithoutCreatingRows` — rama 1 intacta.
  2. `VerifiedEmailMatchingAccountWithoutLinks_ShouldStillLinkAutomatically` — rama **2a**, afirma además que `ProviderEmailVerifiedAt` queda establecido.
  3. `VerifiedEmailMatchingAccountWithExistingLinks_ShouldThrowCollisionWithoutCreatingRows` — rama **2b**: `Assert.ThrowsAsync<ExternalLoginCollisionException>` + recuento invariante de `AppUsers` y `ExternalLogins`.
  4. `Collision_ShouldNeverChangeTheExistingLinkOwner` — intentos repetidos, el `UserId` de la fila original no cambia.
  5. `UnverifiedEmail_ShouldStillProvisionANewAccount` — rama 3 intacta.
  6. `NoEmail_ShouldStillUseThePlaceholderDomain` — rama 3 sin correo.

  **Nota de TDD para `sdd-apply`:** las pruebas 1, 5 y 6 deberían pasar **ya hoy** contra el código actual (son fijaciones de regresión de conducta ya existente, no de conducta nueva); las pruebas 2, 3 y 4 sí deben fallar hasta completar 5.2, porque hoy no existe partición 2a/2b ni `ExternalLoginCollisionException`. No es un error si 1/5/6 pasan en el primer `dotnet test` de este PR.
  *Cubre:* social-login-authentication, los 5 escenarios de "Vinculación y aprovisionamiento mediante ExternalLogin" (par reincidente, correo verificado sin vínculos previos, correo verificado con vínculos previos, alta comunitaria, correo no verificado).
- [x] 5.2 **[GREEN]** Modificar `src/Ludeka.Application/Features/Identity/ExternalLoginService.cs`: partir la rama 2 de `ResolveAsync` en **2a** (la cuenta encontrada por correo verificado no tiene ningún `ExternalLogin` — vía `ListByUserIdAsync` — se vincula automáticamente, conducta idéntica a hoy) y **2b** (la cuenta ya tiene al menos un vínculo — lanzar `ExternalLoginCollisionException`, cero escrituras). Escribir `providerEmailVerified` en la creación de la fila de las ramas 2a y 3. Hace pasar 5.1.
- [x] 5.3 **[RED]** Ampliar `tests/Ludeka.UnitTests/Application/AccountConnectionMessagesTests.cs`: el mensaje de colisión **de login** (distinto del mensaje de rechazo de vinculación de PR #2, ver sección 3.1 del diseño "por qué no sirve el mensaje de colisión de §4.2") dirige a iniciar sesión con el método ya usado y a vincular desde Ajustes → Conexiones; tampoco contiene las palabras prohibidas.
- [x] 5.4 **[GREEN]** Ampliar `src/Ludeka.Application/Features/Identity/AccountConnectionMessages.cs` con el texto exacto del aviso de colisión de login (propuesta §4.2: «Ya existe una cuenta con este correo. Inicia sesión con el método que ya usas y vincula este proveedor desde Ajustes → Conexiones.»). Hace pasar 5.3.
- [x] 5.5 **[RED]** Ampliar `tests/Ludeka.UnitTests/Web/LoginRedirectTests.cs` (fichero existente de INC-46): `Login.razor` renderiza el aviso de colisión cuando `?aviso=` trae el código cerrado correspondiente, y no renderiza nada con un código ausente o desconocido.
- [x] 5.6 **[GREEN]** Modificar `src/Ludeka.Web/Authentication/ExternalLoginEvents.cs`: envolver la llamada a `ResolveAsync` del camino **normal** de login (no el de vinculación de PR #3) en `try/catch (ExternalLoginCollisionException)`, redirigiendo a `Login.razor` con el código cerrado de colisión. Modificar `src/Ludeka.Web/Components/Pages/Login.razor`: añadir `[SupplyParameterFromQuery] string? aviso` y el bloque de renderizado del mensaje de 5.4. Hace pasar 5.5.

---

## PR #6 — Aviso de correo no verificado en cabecera (Unidad G2)

**Depende de:** PR #3 (contrato `IAccountConnectionsService`) y PR #4 (destino `/cuenta/conexiones` del enlace del aviso). **Líneas recalculadas:** producción 27-45, pruebas 30-55 → **≈60-100**.

- [x] 6.1 **[RED]** Ampliar `tests/Ludeka.UnitTests/Web/AuthorizationPipelineContractTests.cs` con dos `[Fact]`: (a) `MainLayout.razor` monta `<AccountEmailNotice />`, mismo idioma que la prueba existente `SessionGuard_ShouldForceAFullReload…` que solo afirma la presencia de `<SessionGuard />` (`:99-113` de ese mismo fichero de pruebas); (b) **[cierre de brecha de cobertura detectado en esta fase, el diseño no lo planificaba]** `PublicProfile.razor` **no** referencia `AccountEmailNotice`, `IAccountConnectionsService` ni `HasVerifiedProviderEmailAsync` en ningún punto de su fuente. Deben fallar (a): el componente no existe todavía; (b) puede pasar trivialmente ya hoy y sirve como pin de regresión hacia adelante.
  *Cubre:* account-provider-connections, escenarios "Aviso visible y descartable en la cabecera" (parte a) y "El aviso nunca aparece en el perfil público" (parte b).
  **Añadido en `sdd-apply` (encargo del orquestador, arreglo de caché):** un tercer `[Fact]`, `AccountEmailNotice_ShouldSubscribeToConnectionsInvalidatedAndDisposeCleanly`, contrato de fuente hermano de `SessionGuard_ShouldForceAFullReload…` que exige `@implements IDisposable`, `Connections.Invalidated +=`/`-=` y `RendererInfo.IsInteractive` en `AccountEmailNotice.razor`. RED por fichero inexistente, igual que (a).
- [x] 6.2 **[GREEN]** Crear `src/Ludeka.Web/Components/Shared/AccountEmailNotice.razor`: inyecta `ICurrentUserService` y `IAccountConnectionsService`; `OnInitializedAsync` sale de inmediato si no hay sesión; si la hay, `_needsNotice = !await Connections.HasVerifiedProviderEmailAsync()`; renderiza el texto de `AccountConnectionMessages` (el mismo usado en 4.4, no un literal duplicado) con enlace a `/cuenta/conexiones` y un botón `@onclick="() => _dismissed = true"` que lo descarta sin recargar la página (campo privado de un componente que vive en el circuito interactivo global, sección 3.2 del diseño).
  **Ampliado en `sdd-apply` (encargo del orquestador, arreglo de caché):** además del campo de descarte (sin tocar), el componente implementa `IDisposable` y se suscribe a `IAccountConnectionsService.Invalidated` en `OnAfterRender(firstRender)` bajo el mismo guard que `SessionGuard.razor` (`RendererInfo.IsInteractive`), para refrescar `_needsNotice` sin recarga completa tras una desvinculación en el mismo circuito. Ver sección dedicada en `apply-progress.md`.
- [x] 6.3 **[GREEN]** Modificar `src/Ludeka.Web/Components/Layout/MainLayout.razor`: montar `<AccountEmailNotice />` en la cabecera. Hace pasar 6.1(a).
  **Desviación de ubicación (documentada, no oculta):** se monta inmediatamente después de `</header>` y antes de `<main>`, no como hermano literal de `<SessionGuard />` al final del documento (`:302`). Motivo: `SessionGuard` no renderiza nada, así que su posición es indiferente; `AccountEmailNotice` sí renderiza un aviso visible, y colocarlo tras `<SessionGuard />` lo dejaría después del `<footer>`, fuera de la vista sin desplazarse — contradiciendo literalmente el escenario "Aviso visible... en la cabecera". La prueba 6.1(a) solo exige presencia en el fichero, no la posición exacta, así que esta reubicación no rompe ningún contrato ya cerrado.

---

## PR #7 — Reemplazo del correo sintético por el verificado (Unidad I)

**Depende de:** PR #2 (`LinkAsync`) y PR #1 (columna `ProviderEmailVerifiedAt`, aunque el consumo real es indirecto vía `emailVerified`). **Líneas recalculadas:** producción 48-78, pruebas 60-100 → **≈105-175**.

- [x] 7.1 **[RED]** Ampliar `tests/Ludeka.UnitTests/Application/ExternalLoginLinkingTests.cs` con casos puros de `IsPlaceholderEmail`: reconoce `{clave}@{proveedor}.ludeka.invalid` (formato real de `BuildPlaceholderEmail`) y también `@ludeka.invalid` a secas (forma corta histórica); no reconoce un correo real (`ana@gmail.com`) ni una cadena vacía. Debe fallar: el método no existe.
- [x] 7.2 **[GREEN]** Añadir el método público y estático `IsPlaceholderEmail(string? email)` a `src/Ludeka.Application/Features/Identity/ExternalLoginService.cs`, según el algoritmo exacto de la sección 3.4 del diseño (comparación de host tras el último `@`, `OrdinalIgnoreCase`). Hace pasar 7.1.
- [x] 7.3 **[RED]** Ampliar `ExternalLoginLinkingTests.cs`: (a) una cuenta con correo sintético que vincula un proveedor con correo verificado ve `AppUser.Email` reemplazado por ese correo, y la fila `ExternalLogin` conserva `ProviderEmail`/`ProviderEmailVerifiedAt` como rastro; (b) si otro `AppUser` ya tiene ese correo, el reemplazo **no** se hace, la vinculación **sigue siendo un éxito** (la fila `ExternalLogin` se crea igual) y no se viola el índice único; (c) una cuenta con correo real (no sintético) no se ve afectada por el mismo flujo. Deben fallar: `TryReplacePlaceholderEmailAsync` no existe ni se invoca desde `LinkAsync`.
  *Cubre:* social-login-authentication, los 3 escenarios de "Reemplazo del correo sintético por el correo verificado del proveedor".
- [x] 7.4 **[GREEN, corregido en `sdd-apply`]** Añadido el método privado `TryReplacePlaceholderEmailAsync(AppUser user, string? normalizedEmail, bool emailVerified, CancellationToken ct)` a `ExternalLoginService.cs` (guarda `IsPlaceholderEmail` → comprobación previa `GetByEmailAsync` → `AppUser.UpdateProfile` + `IUserRepository.UpdateAsync` → `catch` no fatal) e invocado desde `LinkAsync` **después** de crear la fila `ExternalLogin` y **solo si** el resultado es `Linked`. `AccountEmailReplaced` propagado en el `ExternalLoginLinkResult` devuelto. Hace pasar 7.3.
  **Corrección arquitectónica obligatoria (encargo del orquestador):** tal y como está redactada, la tarea pide un `catch (DbUpdateException)` directamente en `ExternalLoginService` (Application), lo que reintroduciría la violación de Clean Architecture que el commit `695898c` ya corrigió para el caso análogo de `ExternalLoginRepository.AddAsync`. Se implementó el patrón correcto en su lugar: `DuplicateUserEmailException` (dominio, en `AccountConnectionExceptions.cs`) + traducción de `DbUpdateException` en `SqliteUserRepository.UpdateAsync` (Infrastructure) + `TryReplacePlaceholderEmailAsync` captura la excepción traducida. Ver `apply-progress.md` para el detalle y la evidencia (`grep` de `EntityFrameworkCore` en `Ludeka.Application` sin resultados).
- [x] 7.5 **[GREEN]** Extendida la llamada de auditoría de `LinkAsync` (añadida en 2.14): cuando `TryReplacePlaceholderEmailAsync` devuelve `true`, se añade la tupla `("Email", correoSintéticoAnterior, correoVerificado)` a `Changes` del `RecordAuditCommand`, junto a la tupla `("Provider", null, "{Proveedor}")` ya existente.

---

## Matriz de trazabilidad — 35/35 escenarios cubiertos

### `account-provider-connections` (19 escenarios)

| # | Escenario | Tarea(s) |
|---|---|---|
| 1 | Anónimo en render estático (SSR) no accede al contenido | 4.1 |
| 2 | Anónimo en render interactivo es redirigido al inicio de sesión | 4.1 |
| 3 | Autenticado sin ningún permiso de moderación accede a la página | 4.1 |
| 4 | Listado de proveedores habilitados y su estado de vinculación | 3.8/3.9, 4.2 |
| 5 | Vincular un proveedor no vinculado previamente | 2.1/2.2 |
| 6 | La identidad vinculada es siempre la de la sesión, no la del cliente | 3.1/3.2, reforzado por 3.11 |
| 7 | Rechazo cuando el proveedor ya pertenece a otra cuenta | 2.3/2.4 |
| 8 | El mensaje de rechazo no promete una resolución inexistente | 2.7/2.8 |
| 9 | La fila en conflicto nunca cambia de propietario | 2.5/2.6 |
| 10 | Intento de desvincular el único proveedor es denegado | 2.11/2.12 |
| 11 | Una petición que evita la interfaz también se deniega | 2.11/2.12 |
| 12 | Desvincular procede cuando queda al menos otro método | 2.9/2.10 |
| 13 | Vincular registra una entrada de auditoría | 2.13/2.14 |
| 14 | Desvincular registra una entrada de auditoría | 2.13/2.14 |
| 15 | Un intento denegado o rechazado no registra una auditoría de éxito | 2.13/2.14 |
| 16 | Aviso visible en la pantalla de conexiones | 4.4 (base en 3.8/3.9) |
| 17 | Aviso visible y descartable en la cabecera | 6.1/6.2/6.3 |
| 18 | Aviso ausente cuando existe correo verificado | 3.8/3.9 (nivel servicio); 4.4, 6.2 (consumo) |
| 19 | El aviso nunca aparece en el perfil público | 6.1(b) — tarea añadida en esta fase, el diseño no la planificaba |

### `social-login-authentication` (13 escenarios)

| # | Escenario | Tarea(s) |
|---|---|---|
| 20 | Columna presente en una base de datos migrada desde cero | 1.3, 1.5, 1.6 |
| 21 | Columna reconciliada en una base SQLite preexistente | 1.7/1.9 |
| 22 | Verificación registrada cuando el correo llega verificado | 1.1/1.2 (invariante); cierre en 5.1/5.2 y 2.1/2.2 |
| 23 | Ausencia de verificación cuando el correo no llega verificado | 1.1/1.2 (invariante); cierre en 5.1/5.2 |
| 24 | Reemplazo exitoso del correo sintético | 7.3/7.4 |
| 25 | Reemplazo rechazado por colisión de correo | 7.3/7.4 |
| 26 | Cuenta sin correo sintético no se ve afectada | 7.3/7.4 |
| 27 | Par reincidente resuelve a la misma cuenta | 5.1 (regresión 1) |
| 28 | Correo verificado coincide con cuenta sin identidades previas (no regresión) | 5.1 (regresión 2, rama 2a), 5.2 |
| 29 | Correo verificado coincide con cuenta que ya tiene identidades vinculadas | 5.1 (regresión 3, rama 2b), 5.2 |
| 30 | Alta de usuario comunitario | 5.1 (regresión 5) |
| 31 | Correo no verificado no fusiona cuentas | 5.1 (regresión 5/6) |
| 32 | Vincular un proveedor adicional desde sesión activa | 2.1/2.2 (mismo comportamiento que el escenario 5) |

### `user-management-permissions-audit` (3 escenarios)

| # | Escenario | Tarea(s) |
|---|---|---|
| 33 | Nombre visible de LinkedProvider | 1.12/1.13 |
| 34 | Nombre visible de UnlinkedProvider | 1.12/1.13 |
| 35 | Los valores nuevos no colisionan con los existentes | 1.10/1.11 |

**Ningún escenario queda sin tarea asignada.**

---

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ≈1470-2310 autoradas (recalculado tarea a tarea; excluye `.Designer.cs` y `LudekaDbContextModelSnapshot.cs`) |
| 400-line budget risk | **High** (PR #2 y PR #3 entran en zona de riesgo en banda alta) |
| Chained PRs recommended | Yes |
| Suggested split | PR #1 → PR #2 → PR #3 → PR #4 → PR #5 → PR #6 → PR #7 (cadena lineal, ver tabla de ramas) |
| Delivery strategy | auto-chain |
| Chain strategy | stacked-to-main |

```text
Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High
```

### Recuento recalculado por PR (producción + pruebas, sin depender de la resta de propuesta §13.1)

| PR | Producción | Pruebas | Total recalculado | Total en `design.md` §10 | Lectura |
|---|---|---|---|---|---|
| #1 — Cimientos | 103-166 | 138-220 | **240-385** | 220-365 | Consistente, ligero margen al alza |
| #2 — Application | 143-229 | 162-238 | **305-465** | 210-340 | **Por encima de 400 en banda alta** — el bloque de auditoría + mensajes + excepciones pesa más de lo estimado en la propuesta original |
| #3 — Transporte OAuth + G1 | 168-265 | 195-305 | **365-570** | 220-360 | **El de mayor riesgo de la cadena** — `ExternalLoginEventsLinkBranchTests.cs` exige construir un `TicketReceivedContext` real, la prueba más cara de todo el incremento |
| #4 — Pantalla | ~120-190 | ~40-65 | **160-255** | 190-330 | Por debajo de la estimación de diseño: sin `bUnit` en el repositorio (verificado), la página se prueba por contrato de fuente, no por renderizado simulado |
| #5 — Colisión | 93-155 | 160-228 | **250-380** | 140-250 | Por encima del diseño pero dentro de presupuesto incluso en banda alta |
| #6 — Aviso cabecera | 27-45 | 30-55 | **60-100** | 60-100 | Coincide con el diseño |
| #7 — Reemplazo correo | 48-78 | 60-100 | **105-175** | 80-140 | Ligeramente por encima, dentro de presupuesto |
| **Total** | **702-1128** | **785-1211** | **≈1470-2310** | 1070-1795 (propuesta) | Ver nota |

**Nota de lectura:** el recuento de esta fase es más alto que el de `proposal.md` §13.1 y el de `design.md` §10 porque se ha hecho **tarea a tarea** (incluyendo el coste de armazón `IAsyncLifetime`/SQLite en cada fichero de pruebas nuevo, calibrado contra el tamaño real de `ExternalLoginServiceTests.cs:1-146` del propio repositorio — 50 líneas de armazón + ~16 líneas/prueba de media), no como una resta de bandas por unidad. **Ninguna cifra de esta tabla arrastra la resta de `proposal.md` §13.1**: cada total es la suma explícita de las tareas 1.1-7.5 de este documento.

**Veredicto:** se requieren PRs encadenados (el propio volumen ya lo exigía desde `sdd-explore`). El riesgo de presupuesto es **Alto**, concentrado en PR #2 y PR #3. No hay decisión pendiente antes de empezar `sdd-apply` porque `auto-chain` ya la resuelve — pero `sdd-apply` debe:

1. Medir el diff **real** de PR #2 y PR #3 antes de abrir cada uno (`git diff --stat` contra la rama anterior de la cadena).
2. Si el diff real de PR #3 supera 400, la única opción que no reabre la partición de 7 PRs del diseño es pedir al maintainer un `size:exception` puntual para ese PR — **nunca** dividir G1 de D dentro de la cadena ya cerrada, ni fusionar unidades, sin decisión explícita del maintainer.
3. Si el diff real de cualquier PR queda por debajo de 400, no hace falta ninguna acción adicional.
4. La fusión de #6+#7 o #5+#7 que `proposal.md` §13.2 deja anotada como posible **no se aplica en esta fase** (solo procedería con medición real, nunca por estimación) y no se ha aplicado aquí.

---

## Desajustes encontrados al desglosar (para el informe del orquestador)

1. **Gap de fichero no declarado en `design.md` §5.1:** `AccountConnectionRoutes` se usa en el código de las secciones D1 y D4 del diseño (`.Page`, `.PageWithResult(...)`, etc.) pero ninguna fila de la tabla "Cambios por fichero" lo crea, y una búsqueda confirma que no existe en el repositorio. Cerrado con la tarea 3.3.
2. **Escenario sin prueba planificada en el diseño:** "El aviso nunca aparece en el perfil público" (account-provider-connections) no tenía fichero de prueba asignado en `design.md` §5.2. Cerrado con la tarea 6.1(b), una prueba de contrato de fuente que sigue el idioma ya establecido por el repositorio.
3. **Aritmética de PR #3 recalculada (salvedad pendiente del validador):** el total de PR #3 en `design.md` §10 (220-360) no se sostenía sumando D (130-210, de `proposal.md` §13.1) + G1 (70-120): la suma simple ya da 200-330, y el diseño declaraba 220-360. El recálculo tarea a tarea de esta fase da **365-570**, más alto que ambas cifras anteriores, principalmente por el coste real de construir un `TicketReceivedContext` de prueba.
4. **PR #2 también recalculado al alza:** no señalado por el validador, pero el mismo ejercicio de sumar tarea a tarea muestra que PR #2 (**305-465**) también se acerca o supera 400 en banda alta, por encima de las 210-340 de `design.md`. Se reporta con la misma metodología que el punto 3.
5. **Deriva menor en la suma de G1+G2:** `proposal.md` §13.2 fija el rango original de G en 130-210; `design.md` §10 la parte en G1 (70-120) + G2 (60-100), cuya suma da 130-220 — 10 líneas más en el extremo alto que el original. No se corrige (no reabre una decisión cerrada), solo se deja anotado.
6. **`AuditService.GetActionDisplayName` sin prueba previa:** verificado que no existe ninguna prueba de ese método en todo el repositorio antes de este incremento. Se añadió a `UserManagementAndAuditServiceTests.cs` (el fichero de Application más cercano) por ausencia de un `AuditServiceTests.cs` dedicado, no por un precedente exacto.
7. **Sin `bUnit` en el repositorio:** verificado (búsqueda sin resultados). Confirma que toda prueba relacionada con Blazor en este incremento debe ser de contrato de fuente (leer el `.razor` como texto), igual que ya hace `AuthorizationPipelineContractTests.cs` — no una simulación de renderizado.
8. **Nombres de rama de la cadena:** ni `proposal.md` ni `design.md` fijan los nombres exactos de las 7 ramas (la propuesta delega expresamente esa decisión en `sdd-tasks`, §13.3). Se proponen en la sección "Convenio de ramas" de este documento; `sdd-apply` debe confirmar si `scripts/sdd-worktree.ps1` admite un `--base` explícito para las ramas #2-#7 o si requieren `git checkout -b` + `gh pr create --base` manuales dentro del mismo worktree.
