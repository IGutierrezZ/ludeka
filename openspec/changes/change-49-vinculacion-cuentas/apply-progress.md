# Progreso de aplicación — INC-49, PR #1 (Cimientos de datos y contrato)

> **Cambio:** `change-49-vinculacion-cuentas` · **Fase:** `sdd-apply` · **Fecha:** 2026-09-17
> **Alcance de este lote:** únicamente las 13 tareas del PR #1 (Unidades A + B + H), sección «PR #1 — Cimientos de datos y contrato» de `tasks.md`.
> **Modo:** Strict TDD activo. Runner contractual: `dotnet test Ludeka.sln`.
> **Lote:** primer lote — no existía progreso previo en Engram ni en este fichero.
> **Worktree / rama:** `C:\repos\ludeka-wt\vinculacion-cuentas` — `inc/vinculacion-cuentas`.

---

## Tareas completadas (13/13)

- [x] 1.1 **[RED]** `ExternalLoginTests.cs` — 3 casos nuevos del invariante `ProviderEmailVerifiedAt` (verificado+correo, verificado sin correo, no verificado). RED por error de compilación (`providerEmailVerified` y `ProviderEmailVerifiedAt` no existían).
- [x] 1.2 **[GREEN]** `ExternalLogin.cs` — propiedad `ProviderEmailVerifiedAt` y 6.º parámetro opcional `providerEmailVerified = false` al final del constructor.
- [x] 1.3 **[RED]** `ExternalLoginPersistenceTests.cs` — 4 casos nuevos (round-trip de la marca, `ListByUserIdAsync`, `RemoveAsync`, índice único tras ciclo borrar+crear). RED por error de compilación (`ListByUserIdAsync`/`RemoveAsync` no existían en el contrato).
- [x] 1.4 **[GREEN]** `IExternalLoginRepository.cs` — añadidos `ListByUserIdAsync` y `RemoveAsync`. Sin método de actualización, por diseño (§3.1).
- [x] 1.5 **[GREEN]** `ExternalLoginRepository.cs` (implementación) + `LudekaDbContext.cs` (mapeo Fluent `ProviderEmailVerifiedAt`, tras la línea de `ProviderEmail`).
- [x] 1.6 **Migración EF Core** `20260917112154_AddProviderEmailVerifiedAtToExternalLogins` generada con `Database__Provider=PostgreSql dotnet ef migrations add ... --project src/Ludeka.Infrastructure --startup-project src/Ludeka.Web`. `AddColumn<DateTimeOffset>(..., type: "timestamp with time zone", nullable: true)` en `Up`; `DropColumn` en `Down`. `.Designer.cs` (2339 líneas) y `LudekaDbContextModelSnapshot.cs` (+3) son artefactos generados, declarados aquí explícitamente y excluidos del presupuesto de 400 líneas.
- [x] 1.7 **[RED]** `SqliteSchemaMigratorTests.cs` — caso de reconciliación sobre una tabla `ExternalLogins` manual con las 6 columnas de INC-46. RED por aserción en tiempo de ejecución (`PRAGMA table_info` no incluía la columna todavía).
- [x] 1.8 **[GREEN]** `SqliteSchemaMigrator.cs` — columna añadida al `CREATE TABLE` del bloque 22 (bases creadas desde cero).
- [x] 1.9 **[GREEN]** `SqliteSchemaMigrator.cs` — bloque 23 nuevo, `ALTER TABLE ... ADD COLUMN` protegido por `PRAGMA table_info` (bases preexistentes). Bloque 22 (creación) y bloque 23 (reconciliación) quedan como dos cambios de código distintos, tal como exige la tarea; se implementaron y verificaron juntos, en el mismo commit de trabajo, sin fusionar su lógica.
- [x] 1.10 **[RED]** `UserManagementDomainTests.cs` — aserción de ordinales explícitos (8 y 9) y no colisión. RED por error de compilación (`AuditAction.LinkedProvider`/`UnlinkedProvider` no existían).
- [x] 1.11 **[GREEN]** `AuditAction.cs` — `LinkedProvider` (8) y `UnlinkedProvider` (9) añadidos al final.
- [x] 1.12 **[RED]** `UserManagementAndAuditServiceTests.cs` — 2 `[Fact]` nuevos para `AuditService.GetActionDisplayName`. RED por aserción en tiempo de ejecución (caían al `_ => "Operación"` por defecto).
- [x] 1.13 **[GREEN]** `AuditService.cs` — dos ramas nuevas en `GetActionDisplayName`.

**No regresión confirmada:** las 6 pruebas de `ExternalLoginServiceTests.cs:51-146` y `ExternalLoginTests.cs:20` no se tocaron y siguen en verde (confirmado en cada ejecución de la suite completa).

---

## Evidencia del ciclo TDD

| Tarea | Fichero de prueba | Capa | Red de seguridad | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 1.1/1.2 | `Domain/ExternalLoginTests.cs` | Unidad (dominio puro) | ✅ 14/14 (baseline del fichero) | ✅ Escrito (CS1739/CS1061) | ✅ Pasa (17/17) | ✅ 3 casos (verificado+correo, verificado sin correo, no verificado) | ➖ No hizo falta — invariante de una sola línea |
| 1.3/1.4/1.5 | `Infrastructure/ExternalLoginPersistenceTests.cs` | Unidad (SQLite `:memory:`) | ✅ 7/7 (baseline del fichero) | ✅ Escrito (CS1061 ×3) | ✅ Pasa (34/34 tras 1.5) | ✅ 4 casos (round-trip, listado, borrado, índice tras ciclo) | ➖ No hizo falta |
| 1.7/1.8/1.9 | `Infrastructure/SqliteSchemaMigratorTests.cs` | Unidad (SQLite `:memory:`, reconciliación) | ✅ 1/1 (baseline del fichero) | ✅ Escrito, fallo real en ejecución (`Assert.Contains` no encontraba la columna) | ✅ Pasa (2/2 tras 1.8+1.9) | ➖ Único escenario de reconciliación exigido por la tarea | ➖ No hizo falta |
| 1.10/1.11 | `Domain/UserManagementDomainTests.cs` | Unidad (enum puro) | ✅ 9/9 (baseline del fichero) | ✅ Escrito (CS0117 ×4) | ✅ Pasa (10/10) | ➖ Un solo escenario (ordinales + no colisión) | ➖ No hizo falta |
| 1.12/1.13 | `Application/UserManagementAndAuditServiceTests.cs` | Unidad (método estático puro) | ✅ 8/8 (baseline del fichero) | ✅ Escrito, fallo real en ejecución (`"Operación"` en vez del texto esperado) | ✅ Pasa (10/10) | ✅ 2 casos (LinkedProvider, UnlinkedProvider, cruzados por distinción) | ➖ No hizo falta |

### Resumen de pruebas

- **Pruebas nuevas escritas:** 11 (3 + 4 + 1 + 1 + 2).
- **Pruebas totales pasando:** 1356/1356 (baseline 1345 + 11 nuevas), 0 fallos, 0 omitidas.
- **Capas usadas:** Unidad (11), Integración (0), E2E (0) — todo el PR #1 es dominio/aplicación/infraestructura sin superficie HTTP.
- **Pruebas de aprobación (refactor):** Ninguna — no hubo tareas de refactorización de conducta existente en este PR.
- **Funciones puras creadas:** 1 (el cálculo de `ProviderEmailVerifiedAt` en el constructor de `ExternalLogin`, determinista y sin efectos secundarios).

### Hallazgo transitorio durante el ciclo (documentado, no es una desviación)

Entre la tarea 1.5 (mapeo Fluent) y la tarea 1.8 (columna en el `CREATE TABLE` del bloque 22), la prueba preexistente `ExternalLoginPersistenceTests.SqliteSchemaMigrator_ShouldCreateExternalLoginsTable_OnLegacyDatabase` falló transitoriamente (`SqliteException: no such column: e.ProviderEmailVerifiedAt`) porque el modelo EF ya conocía la columna nueva pero el SQL crudo del bloque 22 todavía no la creaba. Es la consecuencia esperada del orden secuencial que el propio `tasks.md` impone (dominio/Fluent antes que el SQL crudo); la tarea 1.8 la resolvió y la prueba volvió a verde de inmediato, confirmado por ejecución.

---

## Evidencia de unidad de trabajo

| Evidencia | Valor |
|---|---|
| Comando de prueba enfocado y resultado exacto | `dotnet test Ludeka.sln --filter "FullyQualifiedName~ExternalLoginTests\|FullyQualifiedName~ExternalLoginPersistenceTests\|FullyQualifiedName~SqliteSchemaMigratorTests\|FullyQualifiedName~UserManagementDomainTests\|FullyQualifiedName~UserManagementAndAuditServiceTests"` → **Con error: 0, Superado: 50, Omitido: 0, Total: 50** |
| Arnés de runtime / escenario e resultado exacto | **N/A** — el propio work unit de `tasks.md` lo declara así: sin superficie de usuario, solo esquema/dominio/repositorio. No hay endpoint, página ni flujo HTTP en el alcance de PR #1 |
| Frontera de reversión | Revertir los 8 ficheros de producción (`ExternalLogin.cs`, `IExternalLoginRepository.cs`, `ExternalLoginRepository.cs`, `LudekaDbContext.cs`, `SqliteSchemaMigrator.cs`, `AuditAction.cs`, `AuditService.cs`) + los 2 ficheros de migración + el snapshot; `Down()` de la migración elimina la columna en PostgreSQL, SQLite ignora la columna sobrante si solo se revierte el código |

---

## Ficheros modificados

| Fichero | Acción | Qué cambia |
|---|---|---|
| `src/Ludeka.Core/Entities/ExternalLogin.cs` | Modificado | `ProviderEmailVerifiedAt` + 6.º parámetro opcional |
| `src/Ludeka.Core/Enums/AuditAction.cs` | Modificado | `LinkedProvider` (8), `UnlinkedProvider` (9) |
| `src/Ludeka.Application/Contracts/IExternalLoginRepository.cs` | Modificado | `ListByUserIdAsync`, `RemoveAsync` |
| `src/Ludeka.Application/Features/Admin/AuditService.cs` | Modificado | 2 ramas nuevas en `GetActionDisplayName` |
| `src/Ludeka.Infrastructure/Data/ExternalLoginRepository.cs` | Modificado | Implementación de los 2 métodos nuevos |
| `src/Ludeka.Infrastructure/Data/LudekaDbContext.cs` | Modificado | 1 línea de mapeo Fluent |
| `src/Ludeka.Infrastructure/Data/SqliteSchemaMigrator.cs` | Modificado | Columna en bloque 22 + bloque 23 nuevo |
| `src/Ludeka.Infrastructure/Migrations/20260917112154_AddProviderEmailVerifiedAtToExternalLogins.cs` | Creado | Migración aditiva (cuenta para el presupuesto) |
| `src/Ludeka.Infrastructure/Migrations/20260917112154_AddProviderEmailVerifiedAtToExternalLogins.Designer.cs` | Creado (generado) | 2339 líneas — **excluido** del presupuesto de 400 líneas |
| `src/Ludeka.Infrastructure/Migrations/LudekaDbContextModelSnapshot.cs` | Modificado (generado) | +3 líneas — **excluido** del presupuesto de 400 líneas |
| `tests/Ludeka.UnitTests/Domain/ExternalLoginTests.cs` | Modificado | +3 pruebas |
| `tests/Ludeka.UnitTests/Infrastructure/ExternalLoginPersistenceTests.cs` | Modificado | +4 pruebas |
| `tests/Ludeka.UnitTests/Infrastructure/SqliteSchemaMigratorTests.cs` | Modificado | +1 prueba |
| `tests/Ludeka.UnitTests/Domain/UserManagementDomainTests.cs` | Modificado | +1 prueba |
| `tests/Ludeka.UnitTests/Application/UserManagementAndAuditServiceTests.cs` | Modificado | +2 pruebas |
| `openspec/changes/change-49-vinculacion-cuentas/tasks.md` | Modificado | 13 casillas marcadas `[x]` |

---

## Recuento de líneas autoradas (`git diff a6e720c..HEAD`)

| Alcance | Inserciones | Eliminaciones | Total |
|---|---|---|---|
| Producción + pruebas (sin `tasks.md`, sin generados) | 348 | 2 | **350** |
| + `tasks.md` (bookkeeping de tareas) | 361 | 15 | **376** |
| Generados excluidos (`.Designer.cs` + `ModelSnapshot.cs`) | — | — | 2342 (no cuentan) |

**Contraste con la estimación de `tasks.md` (240-385):** 350 (o 376 con `tasks.md`) cae dentro de la banda estimada. Por categoría: producción real ≈99 líneas (estimado 103-166, ligeramente por debajo); pruebas reales ≈251 líneas (estimado 138-220, **por encima** del extremo alto estimado en +31 líneas, principalmente por el armazón de `SqliteSchemaMigratorTests` — crear tabla `Games` + `AppUsers` + `ExternalLogins` a mano cuesta más líneas que las otras unidades). El total combinado sigue dentro de presupuesto.

---

## Commits creados (7, ninguno pusheado)

1. `db52bc7` `feat(core): registrar la verificacion del correo del proveedor en ExternalLogin`
2. `85020f7` `feat(infrastructure): anadir ListByUserIdAsync y RemoveAsync al repositorio de identidades externas`
3. `d6a0547` `feat(infrastructure): generar la migracion EF AddProviderEmailVerifiedAtToExternalLogins`
4. `3d23373` `feat(infrastructure): reconciliar ProviderEmailVerifiedAt en el esquema SQLite`
5. `c709743` `feat(core): anadir AuditAction.LinkedProvider y UnlinkedProvider`
6. `856fbf1` `feat(application): resolver el nombre visible de LinkedProvider y UnlinkedProvider`
7. `5aa089b` `docs(sdd): completar las tareas 1.1-1.13 del PR #1 de INC-49`

Ninguno lleva atribución de IA (regla explícita de `AGENTS.md` §4). El worktree queda limpio (`git status` sin cambios pendientes) sobre `inc/vinculacion-cuentas`, sin push ni PR: eso lo decide el orquestador con `scripts/sdd-worktree.ps1 pr vinculacion-cuentas`.

---

## Desviaciones respecto al diseño / tareas

Ninguna. Las 13 tareas se implementaron tal como están escritas, en el orden RED→GREEN especificado, sin reabrir ninguna de las decisiones cerradas de `proposal.md` §3 ni las resoluciones P2-P5 de `design.md`.

## Tropiezos operativos

Ninguno más allá del hallazgo transitorio ya documentado arriba (esperado por el propio orden de tareas, no un tropiezo real). No hubo bloqueo por `MSB3027` (no había ningún proceso `Ludeka.Web` en ejecución) y `dotnet-ef` ya estaba instalado globalmente (10.0.12).

---

## Estado

**13/13 tareas completas.** `dotnet test Ludeka.sln`: **1356/1356 en verde, 0 fallos, 0 omitidas.** Listo para que el orquestador cierre PR #1 (`scripts/sdd-worktree.ps1 pr vinculacion-cuentas`) y decida si continúa con PR #2 (`sdd-apply` sobre la Unidad C, en una rama nueva `inc/vinculacion-cuentas-02-application` partiendo de esta).

---

# Progreso de aplicación — INC-49, PR #2 (Vinculación y desvinculación en Application)

> **Cambio:** `change-49-vinculacion-cuentas` · **Fase:** `sdd-apply` · **Fecha:** 2026-09-17
> **Alcance de este lote:** únicamente las 14 tareas del PR #2 (Unidad C + resultados/excepciones/mensajes), sección «PR #2 — Vinculación y desvinculación en Application» de `tasks.md`.
> **Modo:** Strict TDD activo. Runner contractual: `dotnet test Ludeka.sln`.
> **Lote:** segundo lote — progreso previo del PR #1 (observación 370) leído y fusionado arriba, sin pisarlo.
> **Worktree / rama:** `C:\repos\ludeka-wt\vinculacion-cuentas` — `inc/vinculacion-cuentas-02-application` (parte de `inc/vinculacion-cuentas` en `f54c35a`).

---

## Tareas completadas (14/14)

- [x] 2.1 **[RED]** `ExternalLoginLinkingTests.cs` (nuevo) — caso `LinkAsync` sin vínculo previo. RED por `CS1061`/`CS0103` (`LinkAsync`/`ExternalLoginLinkOutcome` no existían).
- [x] 2.2 **[GREEN]** `AccountConnectionDtos.cs` (nuevo) + `LinkAsync` en `IExternalLoginService.cs`/`ExternalLoginService.cs`, camino feliz mínimo.
- [x] 2.3 **[RED]** Casos `AlreadyLinkedToThisAccount` y `RejectedOwnedByAnotherAccount`. RED en tiempo de ejecución: `DbUpdateException` sin capturar al insertar el par repetido (todavía sin comprobación previa).
- [x] 2.4 **[GREEN]** Comprobación previa (`GetByProviderKeyAsync`) antes de cualquier escritura.
- [x] 2.5 **[RED]** Simulación de carrera con un doble de prueba (`RaceSimulatingRepository`) que fuerza que la comprobación previa no vea la fila competidora. RED en tiempo de ejecución: `DbUpdateException` sin capturar.
- [x] 2.6 **[GREEN]** `catch (DbUpdateException)` alrededor del `AddAsync`, mismo resultado de rechazo.
- [x] 2.7 **[RED]** `AccountConnectionMessagesTests.cs` (nuevo) — auditoría de honestidad del mensaje de rechazo. RED por `CS0103` (`AccountConnectionMessages` no existía).
- [x] 2.8 **[GREEN]** `AccountConnectionMessages.cs` (nuevo) con el texto exacto de `design.md` §3.1 y la guarda del último método.
- [x] 2.9 **[RED]** `UnlinkAsync` elimina la fila indicada cuando quedan 2+ vínculos (+ caso idempotente añadido en el mismo lote RED, exigido por el propio texto de 2.10). RED por `CS1061` (`UnlinkAsync` no existía).
- [x] 2.10 **[GREEN]** `UnlinkAsync(userId, provider, ct)` en `IExternalLoginService.cs`/`ExternalLoginService.cs`, sin la guarda todavía.
- [x] 2.11 **[RED]** Desvincular el único vínculo se deniega llamando **directamente** a `UnlinkAsync` desde la prueba. RED por `CS0246` (`LastAccessMethodException` no existía).
- [x] 2.12 **[GREEN]** `AccountConnectionExceptions.cs` (nuevo) con `LastAccessMethodException` y `ExternalLoginCollisionException` (sin consumidor hasta PR #5). Guarda `if (links.Count <= 1) throw ...` en `UnlinkAsync`.
- [x] 2.13 **[RED]** 4 casos de auditoría (`LinkAsync`/`UnlinkAsync` con éxito auditan; rechazado/denegado no auditan), con un `FakeAuditService` local (mismo idioma que `InstagramPublisherServiceTests.cs:151`). RED por `CS1729` (el constructor no aceptaba un 3.er argumento).
- [x] 2.14 **[GREEN]** `IAuditService? audit = null` opcional en el constructor; `RecordAuditAsync` privado invocado solo en el único camino de éxito de `LinkAsync`/`UnlinkAsync`. Verificado: `IAuditService` ya estaba registrado en `Program.cs:283` — no hizo falta registro nuevo.

**No regresión de este PR:** las 6 pruebas de `ExternalLoginServiceTests.cs:51-146` no se tocaron y siguen en verde (confirmado en cada ejecución de la suite completa).

---

## Hallazgo y corrección fuera de las 14 tareas (bug de PR #1, no una tarea nueva)

**`ExternalLoginRepository.RemoveAsync` (creado en la tarea 1.5 del PR #1) fallaba con `InvalidOperationException` del `ChangeTracker`** cuando la fila a borrar ya estaba bajo seguimiento por otra instancia en el mismo `DbContext` — exactamente el caso real de `UnlinkAsync` (`ListByUserIdAsync` con `AsNoTracking` + `RemoveAsync` sobre el resultado), tras que alguna fila de esa cuenta hubiera sido creada antes con `AddAsync` en el mismo ámbito. La única prueba de PR #1 para `RemoveAsync` (`RemoveAsync_ShouldDeleteTheRow`, `ExternalLoginPersistenceTests.cs:189`) pasaba la **misma instancia** de vuelta, por lo que el conflicto quedó latente y sin detectar hasta este PR.

**Corrección:** `RemoveAsync` ahora busca si ya hay una entrada rastreada con el mismo `Id` en el `ChangeTracker` y elimina esa instancia en vez de adjuntar una segunda; si no hay ninguna, se comporta exactamente como antes. No cambia la firma ni el contrato del método (sigue siendo `Task RemoveAsync(ExternalLogin, CancellationToken)`), y las 2 pruebas existentes de PR #1 que lo ejercitan (`RemoveAsync_ShouldDeleteTheRow`, `UniqueIndex_ShouldStillRejectRepeatedProviderPair_AfterRemoveThenAdd`) siguen en verde sin tocarlas. Se reporta aquí con transparencia porque toca un fichero que PR #1 ya había cerrado — no es una desviación de las 14 tareas del PR #2, es un defecto preexistente que esas mismas 14 tareas dejaron de poder ignorar.

**También se añadió** la referencia al paquete `Microsoft.EntityFrameworkCore` (solo el núcleo, sin proveedor) en `Ludeka.Application.csproj`: la tarea 2.6 exige capturar `DbUpdateException` directamente en `ExternalLoginService` (`Ludeka.Application`), y ese tipo vive en el paquete núcleo de EF Core, que el proyecto no referenciaba (correctamente, por Clean Architecture). Es un prerrequisito técnico no declarado por `design.md`/`tasks.md`, análogo al prerrequisito operativo de migración que el propio PR #1 ya documentó. No se añade ningún proveedor concreto (SQLite/Npgsql): solo las abstracciones y excepciones agnósticas de proveedor que la propia sección 3.1 del diseño exige capturar.

---

## Evidencia del ciclo TDD

| Tarea | Fichero de prueba | Capa | Red de seguridad | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 2.1/2.2 | `Application/ExternalLoginLinkingTests.cs` | Unidad (SQLite `:memory:`) | ✅ 6/6 (`ExternalLoginServiceTests`, baseline) | ✅ Escrito (CS1061/CS0103) | ✅ Pasa (1/1) | ➖ Caso único de creación; triangula con 2.3 | ➖ No aplica todavía |
| 2.3/2.4 | idem | idem | ✅ 1/1 (test anterior) | ✅ Escrito, fallo real en ejecución (`DbUpdateException` sin capturar) | ✅ Pasa (3/3) | ✅ 2 casos (`AlreadyLinked`, `Rejected`) | ➖ No hizo falta |
| 2.5/2.6 | idem | idem (doble de prueba `RaceSimulatingRepository`) | ✅ 3/3 | ✅ Escrito, fallo real en ejecución (`DbUpdateException` propagada) | ✅ Pasa (4/4) | ➖ Escenario único de carrera exigido por la tarea | ➖ No hizo falta |
| 2.7/2.8 | `Application/AccountConnectionMessagesTests.cs` (nuevo) | Unidad (función pura) | ➖ N/A (fichero nuevo) | ✅ Escrito (CS0103) | ✅ Pasa (2/2) | ✅ 2 casos (palabras prohibidas, dirección a desvincular) | ➖ No hizo falta |
| 2.9/2.10 | `Application/ExternalLoginLinkingTests.cs` | idem | ✅ 4/4 | ✅ Escrito (CS1061) | ✅ Pasa (7/7) tras corregir `RemoveAsync` | ✅ 2 casos (2+ vínculos, idempotente sin vínculo) | ➖ No hizo falta |
| 2.11/2.12 | idem | idem | ✅ 7/7 | ✅ Escrito (CS0246) | ✅ Pasa (7/7) | ➖ Caso único; triangula con 2.9/2.10 (guarda vs. permitido) | ➖ No hizo falta |
| 2.13/2.14 | idem (+ `FakeAuditService` local) | idem | ✅ 7/7 | ✅ Escrito (CS1729 ×4) | ✅ Pasa (11/11) | ✅ 4 casos (link éxito, unlink éxito, link rechazado, unlink denegado) | ✅ Extraído `RequireActiveUserAsync` compartido por `LinkAsync`/`UnlinkAsync`; `user.Id` (canónico) en vez del `userId` solo recortado, para alinear con el precedente de `ResolveAsync` |

### Resumen de pruebas

- **Pruebas nuevas escritas:** 13 (11 en `ExternalLoginLinkingTests.cs` + 2 en `AccountConnectionMessagesTests.cs`).
- **Pruebas totales pasando:** 1369/1369 (baseline 1356 + 13 nuevas), 0 fallos, 0 omitidas.
- **Capas usadas:** Unidad (13; Application con SQLite `:memory:` + una función pura), Integración (0), E2E (0) — todo el PR #2 es lógica de Application, sin transporte todavía (eso es PR #3).
- **Pruebas de aprobación (refactor):** Ninguna dedicada — el refactor de `RequireActiveUserAsync` se protegió reejecutando toda la suite de esta clase tras extraerlo, no con un test de aprobación distinto.
- **Funciones puras creadas:** 0 en producción nueva de este PR (los mensajes de `AccountConnectionMessages` son funciones puras, pero envuelven interpolación de texto, no lógica de negocio).

---

## Correspondencia con la especificación (los 13 escenarios que este PR cierra)

| Escenario de `account-provider-connections` | Prueba(s) |
|---|---|
| Vincular un proveedor no vinculado previamente | `LinkAsync_WhenNoExistingLink_ShouldCreateRowWithoutProvisioningNewUser` |
| Rechazo cuando el proveedor ya pertenece a otra cuenta | `LinkAsync_WhenPairOwnedByAnotherAccount_ShouldRejectWithoutMovingTheExistingRow` |
| El mensaje de rechazo no promete una resolución inexistente | `RejectedOwnedByAnotherAccountMessage_ShouldNotPromiseAnAutomaticResolution`, `...ShouldDirectToUnlinkFromTheOtherAccount` |
| La fila en conflicto nunca cambia de propietario | `LinkAsync_WhenAddAsyncRacesAgainstAConcurrentInsert_ShouldRejectAndKeepOriginalOwner` |
| Intento de desvincular el único proveedor es denegado / Una petición que evita la interfaz también se deniega | `UnlinkAsync_WhenAccountHasOnlyOneLink_ShouldDenyEvenWhenCalledDirectlyBypassingTheInterface` (cierra ambos escenarios a la vez, por construcción) |
| Desvincular procede cuando queda al menos otro método | `UnlinkAsync_WhenAccountHasTwoOrMoreLinks_ShouldRemoveOnlyTheIndicatedOne` |
| Vincular registra una entrada de auditoría | `LinkAsync_WhenSuccessful_ShouldRecordAuditEntryForTheSessionUser` |
| Desvincular registra una entrada de auditoría | `UnlinkAsync_WhenSuccessful_ShouldRecordAuditEntryForTheSessionUser` |
| Un intento denegado o rechazado no registra una auditoría de éxito | `LinkAsync_WhenRejected_ShouldNotRecordAnyAuditEntry`, `UnlinkAsync_WhenDenied_ShouldNotRecordAnyAuditEntry` |

*(El escenario "La identidad vinculada es siempre la de la sesión, no la del cliente" pertenece a `ExternalLoginIntentTests`/`ExternalLoginEventsLinkBranchTests` del PR #3 — no hay transporte HTTP en este PR; la parte de Application ya la cierra `LinkAsync` recibiendo `userId` como parámetro explícito de la sesión del servidor, nunca del cliente.)*

---

## Evidencia de unidad de trabajo

| Evidencia | Valor |
|---|---|
| Comando de prueba enfocado y resultado exacto | `dotnet test Ludeka.sln --filter "FullyQualifiedName~ExternalLoginLinkingTests\|FullyQualifiedName~AccountConnectionMessagesTests"` → **Con error: 0, Superado: 15, Omitido: 0, Total: 15** |
| Arnés de runtime / escenario y resultado exacto | **N/A** — el propio work unit de `tasks.md` lo declara así: lógica de Application, sin transporte todavía (el endpoint es PR #3) |
| Frontera de reversión | Revertir `ExternalLoginService.cs`/`IExternalLoginService.cs` a su estado de PR #1, borrar los 3 ficheros nuevos de `Features/Identity/` y los 2 ficheros de prueba nuevos, y revertir el paquete EF Core de `Ludeka.Application.csproj`; nada los consume aún (el endpoint es PR #3), así que la reversión es limpia. La corrección de `RemoveAsync` es la única pieza que sí toca PR #1: revertirla dejaría el bug latente, no rompería nada nuevo |

---

## Ficheros modificados

| Fichero | Acción | Qué cambia |
|---|---|---|
| `src/Ludeka.Application/Features/Identity/AccountConnectionDtos.cs` | Creado | `ExternalLoginLinkOutcome`, `ExternalLoginLinkResult`, `AccountConnectionDto`, `AccountConnectionsView` |
| `src/Ludeka.Application/Features/Identity/AccountConnectionMessages.cs` | Creado | Mensaje de rechazo (titular + detalle) y guarda del último método |
| `src/Ludeka.Application/Features/Identity/AccountConnectionExceptions.cs` | Creado | `LastAccessMethodException`, `ExternalLoginCollisionException` (sin consumidor hasta PR #5) |
| `src/Ludeka.Application/Features/Identity/IExternalLoginService.cs` | Modificado | `LinkAsync`, `UnlinkAsync` |
| `src/Ludeka.Application/Features/Identity/ExternalLoginService.cs` | Modificado | `LinkAsync`, `UnlinkAsync`, `RequireActiveUserAsync`, `RecordAuditAsync`, `IAuditService?` opcional |
| `src/Ludeka.Application/Ludeka.Application.csproj` | Modificado | Referencia al núcleo de `Microsoft.EntityFrameworkCore` (prerrequisito de 2.6, ver hallazgo arriba) |
| `src/Ludeka.Infrastructure/Data/ExternalLoginRepository.cs` | Modificado | Corrección de `RemoveAsync` (bug de PR #1, ver hallazgo arriba) |
| `tests/Ludeka.UnitTests/Application/ExternalLoginLinkingTests.cs` | Creado | 11 pruebas (`LinkAsync` ×4, `UnlinkAsync` ×3, auditoría ×4) |
| `tests/Ludeka.UnitTests/Application/AccountConnectionMessagesTests.cs` | Creado | 2 pruebas de honestidad del mensaje de rechazo |
| `openspec/changes/change-49-vinculacion-cuentas/tasks.md` | Modificado | 14 casillas marcadas `[x]` |

---

## Recuento de líneas autoradas (`git diff f54c35a..HEAD --stat`)

| Alcance | Inserciones | Eliminaciones | Total |
|---|---|---|---|
| Producción (7 ficheros, sin pruebas ni `tasks.md`) | 291 | 2 | **293** |
| Pruebas (2 ficheros nuevos) | 337 | 0 | **337** |
| Producción + pruebas (sin `tasks.md`) | 628 | 2 | **630** |
| + `tasks.md` (bookkeeping de tareas) | 642 | 16 | **658** |

**Contraste con la estimación de `tasks.md` (≈305-465, "banda alta con riesgo"):** el real (630, o 658 con `tasks.md`) **supera el extremo alto de la estimación en 165 líneas** (193 con `tasks.md`) y **supera el presupuesto de 400 líneas en 230 líneas** (258 con `tasks.md`). El propio `tasks.md` ya advertía de este riesgo antes de empezar («PR #2 y PR #3 llevan el riesgo real») y esta fase lo confirma con medición real, no estimación.

**Por qué se disparó por encima de la estimación:** `ExternalLoginLinkingTests.cs` solo (294 líneas) ya iguala casi el extremo alto completo de pruebas estimado para todo el PR (162-238). El armazón `IAsyncLifetime`/SQLite se repite igual que en `ExternalLoginServiceTests.cs`, pero el PR #2 necesita además dos dobles de prueba locales (`RaceSimulatingRepository`, `FakeAuditService`) que la estimación de `tasks.md` no desglosaba línea a línea.

**Decisión NO tomada por esta fase (correcto, según instrucción explícita):** no se ha troceado el PR, no se ha omitido ninguna prueba y no se ha comprimido código para encajar en 400. Las 14 tareas están completas y correctas; la cifra real queda reportada para que el orquestador y el maintainer decidan entre partir en dos PRs o registrar `size:exception`.

---

## Commits creados (5, ninguno pusheado)

1. `f348d95` `feat(application): vincular un proveedor de identidad desde sesion activa`
2. `2bc6d1e` `feat(application): redactar el mensaje de rechazo sin prometer fusion automatica`
3. `fdda6e7` `feat(application): desvincular un proveedor con la guarda del ultimo metodo` (incluye `fix(infrastructure): permitir eliminar una fila ya rastreada por otra instancia`, mismo commit por ser la misma unidad de trabajo indivisible sin staging interactivo)
4. `5eb732c` `feat(application): auditar cada vinculacion y desvinculacion completada con exito`
5. `a5ebd72` `docs(sdd): completar las tareas 2.1-2.14 del PR #2 de INC-49`

Ninguno lleva atribución de IA (regla explícita de `AGENTS.md` §4). El worktree queda limpio (`git status` sin cambios pendientes) sobre `inc/vinculacion-cuentas-02-application`, sin push ni PR: eso lo decide el orquestador.

---

## Desviaciones respecto al diseño / tareas

1. **Ninguna de las 14 tareas se reinventó.** Se implementaron tal como están escritas, en el orden RED→GREEN especificado.
2. **Corrección de un bug de PR #1** (`ExternalLoginRepository.RemoveAsync`) — documentada en detalle arriba, necesaria para que 2.9/2.10 funcionen según el propio patrón que el diseño exige (`ListByUserIdAsync` + `RemoveAsync`).
3. **Añadida la referencia al paquete núcleo de EF Core en `Ludeka.Application.csproj`** — documentada arriba, prerrequisito técnico no declarado por el diseño para poder cumplir literalmente la tarea 2.6.
4. **Refactor (limpieza, sin cambio de comportamiento):** se extrajo `RequireActiveUserAsync` como método privado compartido entre `LinkAsync` y `UnlinkAsync`, y se cambió el uso de la variable `id` (recortada pero no canonicalizada) por `user.Id` (canónico, en minúsculas) al construir filas `ExternalLogin` y comparar propietarios — alineado con el precedente ya establecido en `ResolveAsync`. Verificado con la suite completa tras el cambio.

## Tropiezos operativos

Ninguno más allá de los dos hallazgos ya documentados (el bug de `RemoveAsync` y la referencia a EF Core faltante), ambos resueltos y verificados por ejecución. No hubo bloqueo por `MSB3027`.

---

## Estado

**14/14 tareas completas.** `dotnet test Ludeka.sln`: **1369/1369 en verde, 0 fallos, 0 omitidas.** Líneas autoradas reales: **630 (658 con `tasks.md`)**, por encima del presupuesto de 400 y del extremo alto de la estimación (465) — **decisión de partición/excepción pendiente del orquestador y el maintainer**, no tomada por esta fase. Worktree limpio, 5 commits sin pushear, sin PR abierto: eso lo gestiona el orquestador.

---

# Progreso de aplicación — INC-49, PR #3 (Transporte OAuth y contrato de lectura)

> **Cambio:** `change-49-vinculacion-cuentas` · **Fase:** `sdd-apply` · **Fecha:** 2026-09-17
> **Alcance de este lote:** únicamente las 11 tareas del PR #3 (Unidad D + `ExternalLoginIntent` + G1 + `AccountConnectionRoutes`), sección «PR #3 — Transporte OAuth y contrato de lectura» de `tasks.md`.
> **Modo:** Strict TDD activo. Runner contractual: `dotnet test Ludeka.sln`.
> **Lote:** tercer lote — progreso previo de PR #1 y PR #2 (observación 370) leído íntegramente y fusionado arriba, sin pisarlo.
> **Worktree / rama:** `C:\repos\ludeka-wt\vinculacion-cuentas` — `inc/vinculacion-cuentas-03-transporte` (parte de `inc/vinculacion-cuentas-02-application` en `f9aa11f`).

---

## Tareas completadas (11/11)

- [x] 3.1 **[RED]** `ExternalLoginIntentTests.cs` (nuevo) — ida/vuelta de `MarkLink`/`TryReadLink` sobre `Items`, ausencia, valor distinto, `null`, no lectura de `Parameters` ni de ningún otro canal, y contrato de firma por reflexión. RED por `CS0103`/`CS0246` (`ExternalLoginIntent` no existía).
- [x] 3.2 **[GREEN]** `ExternalLoginIntent.cs` (nuevo) — clase estática y pura con `IntentKey`, `LinkValue`, `UserIdKey`, `MarkLink`, `TryReadLink`. Hace pasar 3.1.
- [x] 3.3 **[GREEN, mecánica]** `AccountConnectionRoutes.cs` (nuevo) — `Page`, `LoginWithLinkWithoutSession`, `PageWithSessionChanged`, `PageWithResult(outcome)`. Sin RED dedicado, según lo previsto por la propia tarea; se ejercita indirectamente por 3.4-3.7.
- [x] 3.4 **[RED]** `ExternalLoginEventsLinkBranchTests.cs` (nuevo) — caso "sesión coincide con la intención", construyendo un `TicketReceivedContext` real (`AuthenticationScheme` con `CookieAuthenticationHandler`, `RemoteAuthenticationOptions`, `AuthenticationTicket`). RED por `CS0117` (`HandleTicketReceivedAsync` era `private`).
- [x] 3.5 **[GREEN]** `ExternalLoginEvents.cs` — método pasado a `public static`; bifurcación `if (ExternalLoginIntent.TryReadLink(...))` antes de `ResolveAsync`, camino feliz (sin la reconfirmación de sesión todavía). Hace pasar 3.4.
- [x] 3.6 **[RED]** Ampliado `ExternalLoginEventsLinkBranchTests.cs` — casos "sin sesión al volver" y "sesión de otro usuario". RED en ejecución real (`NullReferenceException`: sin la reconfirmación, el código llamaba a `LinkAsync` incondicionalmente y el doble de prueba no tenía `LinkResult` configurado para esos casos).
- [x] 3.7 **[GREEN]** Completada la bifurcación con la reconfirmación `intendedUserId == sessionUserId`; las dos ramas de fallo llaman a `HandleResponse()` + `Response.Redirect(...)` **sin** invocar `LinkAsync`. Hace pasar 3.6.
- [x] 3.8 **[RED]** `AccountConnectionsServiceTests.cs` (nuevo, fixture SQLite `:memory:`) — `GetConnectionsAsync` sin/con sesión, `HasVerifiedProviderEmailAsync` en sus tres variantes. RED por `CS0246` (`AccountConnectionsService` no existía).
- [x] 3.9 **[GREEN]** `IAccountConnectionsService.cs` + `AccountConnectionsService.cs` (nuevos) sobre `IExternalLoginRepository` + `ICurrentUserService` + `IOptions<AuthenticationOptions>`, con caché de ámbito por instancia. `ICurrentUserService` no se tocó. Hace pasar 3.8.
- [x] 3.10 **[RED]** `AuthorizationPipelineContractTests.cs` — `[Fact]` hermano de `PublicEndpoint_ShouldDeclareAllowAnonymous` para el bloque de `/cuenta/conexiones/vincular`. RED: el endpoint no existía.
- [x] 3.11 **[GREEN]** `Program.cs` — `POST /cuenta/conexiones/vincular` mapeado tras el bloque `/logout`, con antiforgery manual, `userId` leído exclusivamente de `httpContext.User`, `ExternalLoginIntent.MarkLink` + `Results.Challenge`, `.RequireAuthorization()`; registro de `IAccountConnectionsService` en el contenedor. Hace pasar 3.10.

**No regresión confirmada:** las 6 pruebas de `ExternalLoginServiceTests.cs:51-146` no se tocaron y siguen en verde; `PublicEndpoint_ShouldDeclareAllowAnonymous` (4 casos) sigue en verde sin tocarse — el endpoint nuevo se mapea después de `/logout`, dejando ese bloque delimitado igual. Sin `ludeka:intent` en `Items`, el camino de acceso queda textualmente idéntico salvo por la reubicación (sin cambio de comportamiento) de la línea que resuelve `IExternalLoginService`; no existía ni se pidió un arnés de prueba end-to-end del camino de acceso a través del evento antes de este PR, así que esta garantía se apoya en inspección directa del diff, no en una ejecución nueva de ese camino completo.

## Tarea añadida fuera de la numeración 3.1-3.11 (necesaria para completar honestamente 3.5/3.7)

Al completar 3.5 con el camino feliz literal ("si `Outcome == Linked`, reconstruir el principal..."), quedaba sin cubrir el caso `Outcome != Linked` con sesión coincidente (p. ej. `RejectedOwnedByAnotherAccount`), que el propio código de diseño §D1 exige manejar con `HandleResponse()` + redirección, sin firmar sesión. Se añadió un `[Fact]` adicional (`HandleTicketReceivedAsync_WhenLinkAsyncDoesNotLink_ShouldHandleResponseAndRedirectWithoutRebuildingThePrincipal`) verificado RED (con la rama retirada temporalmente) → GREEN (con la rama añadida), en vez de dejarlo sin probar. No es una tarea nueva del alcance, es la finalización honesta de 3.5/3.7 exigida por el propio snippet de diseño.

## Hallazgo relevante: contrato real de `HandleResponse()` (tarea 3.6, ver informe del orquestador)

Antes de escribir la prueba, se construyó un `TicketReceivedContext` real en un programa de consola de un solo uso (`net10.0`, `FrameworkReference` a `Microsoft.AspNetCore.App`, fuera del repositorio) para verificar por ejecución, no por documentación, el contrato de `HandleResponse()`. Resultado observado: `context.HandleResponse()` fija `context.Result` a un `HandleRequestResult` con `Handled = true` y `Skipped = false`; `context.Response.Redirect(url)` fija `StatusCode = 302` y la cabecera `Location`; nada firma `Set-Cookie`. Esto confirma el contrato documentado de la clase base (`HandleRequestContext<TOptions>.HandleResponse()`: "Discontinue all processing for this request and return to the client"). **Lo que esta prueba NO ejercita** es el bucle completo de `RemoteAuthenticationHandler<TOptions>` (código de framework, no de este repositorio) que consulta ese `Result.Handled` para decidir si omite su propio `SignInAsync` posterior — esa garantía de extremo a extremo la sigue reservando el smoke test manual de la propuesta §11 (tarea 4.5, fuera de este PR), tal y como el propio diseño lo declara.

Se descubrió también, por reflexión sobre el ensamblado real: `TicketReceivedContext` se construye con `(HttpContext, AuthenticationScheme, RemoteAuthenticationOptions, AuthenticationTicket)` —no `(..., ClaimsPrincipal, AuthenticationProperties)` como el snippet de `design.md` podría sugerir a primera lectura—; `AuthenticationTicket` empaqueta ambos. `RemoteAuthenticationContext<TOptions>.Principal` y `.Properties` sí tienen setter público (`get=true set=true`), confirmando que el código de diseño (`context.Principal = ...`, `context.Properties.IsPersistent = true`) compila tal cual está escrito.

---

## Evidencia del ciclo TDD

| Tarea | Fichero de prueba | Capa | Red de seguridad | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 3.1/3.2 | `Web/ExternalLoginIntentTests.cs` | Unidad (pura, sin HTTP) | ➖ N/A (fichero nuevo) | ✅ Escrito (CS0103/CS0246) | ✅ Pasa (6/6) | ✅ 5 casos (ida/vuelta, ausente, valor distinto, `null`, `Parameters`) + 1 de contrato de firma | ➖ No hizo falta |
| 3.4/3.5 | `Web/ExternalLoginEventsLinkBranchTests.cs` | Unidad (`TicketReceivedContext` real) | ➖ N/A (fichero nuevo) | ✅ Escrito (CS0117, método `private`) | ✅ Pasa (1/1) | ➖ Caso único del camino feliz | ➖ No hizo falta |
| 3.6/3.7 | idem | idem | ✅ 1/1 (test anterior) | ✅ Escrito, fallo real en ejecución (`NullReferenceException` por ausencia de reconfirmación) | ✅ Pasa (3/3) | ✅ 2 casos (sin sesión, sesión distinta) | ➖ No hizo falta |
| *(añadida)* | idem | idem | ✅ 3/3 | ✅ Escrito, `Assert.NotNull` fallaba con la rama `Outcome != Linked` retirada | ✅ Pasa (4/4) | ➖ Caso único, finalización honesta de 3.5/3.7 | ➖ No hizo falta |
| 3.8/3.9 | `Application/AccountConnectionsServiceTests.cs` | Unidad (SQLite `:memory:`) | ➖ N/A (fichero nuevo) | ✅ Escrito (CS0246) | ✅ Pasa (5/5) | ✅ 5 casos (vacía sin sesión, listado con 2 habilitados, 3 variantes de `HasVerifiedProviderEmailAsync`) | ➖ No hizo falta |
| 3.10/3.11 | `Web/AuthorizationPipelineContractTests.cs` | Contrato de fuente | ✅ 10/10 (baseline del fichero) | ✅ Escrito, fallo real en ejecución (`"No se encontró el endpoint..."`) | ✅ Pasa (11/11) | ➖ Caso único, espejo de `PublicEndpoint_ShouldDeclareAllowAnonymous` | ➖ No hizo falta |

### Resumen de pruebas

- **Pruebas nuevas escritas:** 16 (6 + 4 + 5 + 1).
- **Pruebas totales pasando:** 1385/1385 (baseline 1369 + 16 nuevas), 0 fallos, 0 omitidas.
- **Capas usadas:** Unidad (16: 6 puras, 5 con `TicketReceivedContext` real, 5 con SQLite `:memory:`), Contrato de fuente (1, sobre el fichero ya existente), Integración (0), E2E (0) — el smoke test manual (tarea 4.5) queda para el PR #4.
- **Pruebas de aprobación (refactor):** Ninguna — sin refactorización de conducta existente en este PR.
- **Funciones puras creadas:** 2 (`ExternalLoginIntent.MarkLink`/`TryReadLink`, sin dependencias de HTTP; `AccountConnectionRoutes.PageWithResult`).

---

## Evidencia de unidad de trabajo

| Evidencia | Valor |
|---|---|
| Comando de prueba enfocado y resultado exacto | `dotnet test Ludeka.sln --filter "FullyQualifiedName~ExternalLoginIntentTests\|FullyQualifiedName~ExternalLoginEventsLinkBranchTests\|FullyQualifiedName~AccountConnectionsServiceTests\|FullyQualifiedName~AuthorizationPipelineContractTests"` → **Con error: 0, Superado: 26, Omitido: 0, Total: 26** (6+4+5+11, incluyendo las 10 pruebas preexistentes de `AuthorizationPipelineContractTests`) |
| Arnés de runtime / escenario y resultado exacto | **N/A parcial, según lo previsto por el propio work unit de `tasks.md`**: el endpoint es alcanzable por HTTP pero sin botón en interfaz (PR #4); las pruebas de `ExternalLoginEventsLinkBranchTests` ya ejercitan el contrato real de `TicketReceivedContext`, que es el arnés más cercano a runtime disponible sin levantar un `WebApplicationFactory` completo con proveedores OAuth reales |
| Frontera de reversión | Revertir el endpoint (`Program.cs`), la bifurcación (`ExternalLoginEvents.cs`) y los 4 ficheros nuevos de `Ludeka.Web/Authentication` + `Ludeka.Application/Features/Identity`; sin `Items`, el camino de login queda igual; el `[Fact]` añadido a `AuthorizationPipelineContractTests.cs` se revierte con el endpoint |

---

## Ficheros modificados

| Fichero | Acción | Qué cambia |
|---|---|---|
| `src/Ludeka.Web/Authentication/ExternalLoginIntent.cs` | Creado | Clase pura de intención de vinculación sobre `Items` |
| `src/Ludeka.Web/Authentication/AccountConnectionRoutes.cs` | Creado | Rutas cerradas de resultado |
| `src/Ludeka.Web/Authentication/ExternalLoginEvents.cs` | Modificado | `HandleTicketReceivedAsync` público + bifurcación de vinculación completa |
| `src/Ludeka.Application/Features/Identity/IAccountConnectionsService.cs` | Creado | Contrato de lectura de conexiones |
| `src/Ludeka.Application/Features/Identity/AccountConnectionsService.cs` | Creado | Implementación con caché de ámbito |
| `src/Ludeka.Web/Program.cs` | Modificado | Endpoint `POST /cuenta/conexiones/vincular` + registro de DI |
| `tests/Ludeka.UnitTests/Web/ExternalLoginIntentTests.cs` | Creado | 6 pruebas |
| `tests/Ludeka.UnitTests/Web/ExternalLoginEventsLinkBranchTests.cs` | Creado | 4 pruebas |
| `tests/Ludeka.UnitTests/Application/AccountConnectionsServiceTests.cs` | Creado | 5 pruebas |
| `tests/Ludeka.UnitTests/Web/AuthorizationPipelineContractTests.cs` | Modificado | +1 prueba |
| `openspec/changes/change-49-vinculacion-cuentas/tasks.md` | Modificado | 11 casillas marcadas `[x]` |

---

## Recuento de líneas autoradas (`git diff f9aa11f..HEAD --stat`)

| Alcance | Inserciones | Eliminaciones | Total |
|---|---|---|---|
| Producción (6 ficheros) | 266 | 1 | **267** |
| Pruebas (4 ficheros) | 427 | 0 | **427** |
| Producción + pruebas (sin `tasks.md`) | 692 | 1 | **693** |
| + `tasks.md` (bookkeeping de tareas) | 703 | 12 | **715** |

Sin artefactos generados en este PR (no hay migración EF Core ni `.Designer.cs`).

**Contraste con la estimación de `tasks.md` (≈365-570, "el de mayor riesgo de la cadena"):** el real (693, o 715 con `tasks.md`) **supera el extremo alto de la estimación en 123 líneas** (145 con `tasks.md`) y **supera el presupuesto de 400 líneas en 293 líneas** (315 con `tasks.md`). `tasks.md` ya anticipaba este riesgo antes de empezar («PR #3 escribe primero una prueba que construye un `TicketReceivedContext` real... la prueba más cara de todo el incremento») y esta fase lo confirma con medición real. No se ha troceado el PR, no se ha omitido ninguna prueba y no se ha comprimido código para encajar en 400: las 11 tareas están completas y correctas (más el `[Fact]` añadido para completar honestamente 3.5/3.7); la cifra real queda reportada para que el orquestador y el maintainer decidan entre partir o registrar `size:exception`, tal y como exige el propio aviso de entrega de `tasks.md`.

---

## Commits creados (5, ninguno pusheado)

1. `21546ac` `feat(web): transportar la intencion de vinculacion en AuthenticationProperties.Items`
2. `ef2a033` `feat(web): bifurcar HandleTicketReceivedAsync hacia la vinculacion de cuentas`
3. `09c5ffa` `feat(application): exponer la lectura de conexiones de la propia cuenta` — **el cuerpo de este commit tiene un error tipográfico propio** ("ampliaICurrentUserService" sin espacio, debía decir "amplía `ICurrentUserService`"). No se corrigió con `--amend` por prohibición explícita de esta fase de recurrir a esa operación sin petición expresa del usuario; queda anotado aquí con transparencia.
4. `1cf49f4` `feat(web): anadir el endpoint de vinculacion con autorizacion exigida`
5. `c1cebd4` `docs(sdd): completar las tareas 3.1-3.11 del PR #3 de INC-49`

Ninguno lleva atribución de IA. Worktree limpio (`git status` sin cambios pendientes) sobre `inc/vinculacion-cuentas-03-transporte`, sin push ni PR: eso lo decide el orquestador.

---

## Desviaciones respecto al diseño / tareas

1. **Ninguna de las 11 tareas se reinventó.** Se implementaron tal como están escritas, en el orden RED→GREEN especificado.
2. **`[Fact]` añadido fuera de la numeración 3.1-3.11** (documentado arriba): cubre `Outcome != Linked` con sesión coincidente, exigido por el propio código de `design.md` §D1 pero sin RED explícito en `tasks.md`. Verificado RED→GREEN igualmente antes de dejarlo en el código.
3. **`AccountConnectionsService` usa `IOptions<AuthenticationOptions>` (Application) para resolver "proveedores habilitados"**, en vez de depender de `Ludeka.Web.ExternalAuthenticationSchemes.GetEnabledProviders` (que vive en Web y expondría `ClientId`/`ClientSecret` a Application). Es una repetición deliberada y mínima de la condición `Enabled && HasCredentials` ya expuesta por `ExternalProviderOptions.IsUsable`, no una violación de Clean Architecture: `AuthenticationOptions`/`ExternalProviderNames` ya viven en `Ludeka.Application.Features.Identity`, verificado antes de escribir código. No se añadió ningún paquete a `Ludeka.Application.csproj` (regla de obligado cumplimiento del encargo: verificado que `Microsoft.Extensions.Options` ya era dependencia transitiva de `Microsoft.Extensions.Caching.Memory` y ya se usaba en otros servicios de Application).
4. **Error tipográfico en el mensaje del commit 3** (documentado en la sección de commits), no corregido por prohibición de `amend` sin petición expresa.

## Tropiezos operativos

1. **Reflexión sobre ensamblados de referencia (`ref/net10.0`) falla en tiempo de ejecución** (`ReflectionOnlyLoad`/`BadImageFormatException` en PowerShell 5.1, que es .NET Framework): se resolvió creando un proyecto de consola `net10.0` de un solo uso en el scratchpad, con `FrameworkReference` al `Microsoft.AspNetCore.App` compartido, para reflexionar y ejecutar en vivo contra el ensamblado real y así fijar el contrato de `TicketReceivedContext`/`HandleResponse()` con evidencia, no con memoria del entrenamiento. No se compiló ni ejecutó nada dentro del repositorio del incremento para esta comprobación.
2. Ningún bloqueo por `MSB3027` (no había ningún proceso `Ludeka.Web` en ejecución).

---

## Estado

**11/11 tareas completas.** `dotnet test Ludeka.sln`: **1385/1385 en verde, 0 fallos, 0 omitidas.** Líneas autoradas reales: **693 (715 con `tasks.md`)**, muy por encima del presupuesto de 400 y del extremo alto de la propia estimación de riesgo de `tasks.md` (570) — **decisión de partición/excepción pendiente del orquestador y el maintainer**, no tomada por esta fase. Worktree limpio, 5 commits sin pushear, sin PR abierto: eso lo gestiona el orquestador.

---

# Progreso de aplicación — INC-49, PR #4 (Pantalla `/cuenta/conexiones`)

> **Cambio:** `change-49-vinculacion-cuentas` · **Fase:** `sdd-apply` · **Fecha:** 2026-09-17
> **Alcance de este lote:** únicamente las tareas 4.1-4.5 (Unidad E), sección «PR #4 — Pantalla `/cuenta/conexiones`» de `tasks.md`. 4.5 queda **sin marcar** (verificación manual, no ejecutable en este entorno).
> **Modo:** Strict TDD activo. Runner contractual: `dotnet test Ludeka.sln`.
> **Lote:** cuarto lote — progreso previo de PR #1/#2/#3 (observación 370) leído íntegramente y fusionado arriba, sin pisarlo.
> **Worktree / rama:** `C:\repos\ludeka-wt\vinculacion-cuentas` — `inc/vinculacion-cuentas-04-pantalla` (parte de `inc/vinculacion-cuentas-03-transporte` en `22678ad`).

---

## Tareas completadas (4/5 — 4.5 queda sin marcar)

- [x] 4.1 **[RED]** `AccountConnectionsPageContractTests.cs` (nuevo) — 2 `[Fact]`: (a) la página declara `@page "/cuenta/conexiones"` y `@attribute [Authorize]`, y no declara `[Authorize(Policy`; (b) el formulario de vinculación declara `method="post"`, `action="/cuenta/conexiones/vincular"`, `data-enhance="false"` y `<AntiforgeryToken />`. RED confirmado por ejecución: `No se encontró el archivo fuente: src/Ludeka.Web/Components/Pages/AccountConnections.razor` (2 fallos, 0 superados) — el fichero no existía todavía.
- [x] 4.2 **[GREEN]** Creado `src/Ludeka.Web/Components/Pages/AccountConnections.razor`: `@page "/cuenta/conexiones"`, `@attribute [Authorize]` (sin `Policy=`), listado de proveedores habilitados vía `ExternalAuthenticationSchemes.GetEnabledProviders`/`DisplayNameFor`, estado de vinculación obtenido de `IAccountConnectionsService.GetConnectionsAsync()`.
- [x] 4.3 **[GREEN]** Formulario de vinculación por proveedor no vinculado (`<form method="post" action="/cuenta/conexiones/vincular" data-enhance="false">` + `<AntiforgeryToken />` + `<input type="hidden" name="provider">`, patrón de `Login.razor:35`) y botón `@onclick` de desvinculación por proveedor vinculado, capturando `LastAccessMethodException` para el mensaje de guarda.
- [x] 4.4 **[GREEN]** `[SupplyParameterFromQuery(Name = "resultado")]` traducido a un mensaje fijo según la tabla cerrada de D4 (`vinculado`, `ya-vinculado`, `en-uso`, `sesion-cambiada` → constantes nuevas de `AccountConnectionMessages`; ausente/desconocido → sin mensaje) y bloque de aviso de correo no verificado (`if (!await Connections.HasVerifiedProviderEmailAsync())`, reutilizando `AccountConnectionMessages.UnverifiedProviderEmailNotice`).
- [ ] 4.5 **Verificación manual** — sin marcar. No es automatizable sin credenciales OAuth reales en el repositorio; ver la sección "Pasos del smoke test manual" más abajo.

**No regresión de este PR:** las 6 pruebas de `ExternalLoginServiceTests.cs:51-146` no se tocaron y siguen en verde; `AuthorizationPipelineContractTests` (11 hechos, incluidos `PublicEndpoint_ShouldDeclareAllowAnonymous` y la `TheoryData ProtectedPages`) y `CurrentUserContractTests` siguen en verde sin tocarse — confirmado con filtro dedicado. La página nueva no entra en `ProtectedPages` (exige `Policy=`); se cubre con los `[Fact]` propios de 4.1. `ICurrentUserService` no se tocó.

---

## Hallazgo de esta fase: caché de ámbito de `IAccountConnectionsService` y desvinculación en el mismo circuito (no es un bug de un PR anterior — es una interacción a resolver en éste)

`AccountConnectionsService.GetConnectionsAsync()` (creado en PR #3) cachea el resultado en `_cachedView` durante la vida de la instancia `Scoped` (diseño §D6, "Caché de ámbito y su rebaba conocida"). En Blazor Server esa instancia vive todo el circuito, no solo una petición. La tarea 4.3 exige "refresco del listado en el circuito" tras desvincular (mismo circuito, sin recarga), pero volver a llamar a `Connections.GetConnectionsAsync()` después de `UnlinkAsync` devolvería la **misma** vista cacheada, sin la fila recién borrada.

**No se ha modificado `AccountConnectionsService.cs` ni su contrato** (ningún fichero de PR #3 se ha tocado): en vez de invalidar la caché del servicio, `AccountConnections.razor` mantiene su propia copia en memoria (`_view`) y, tras una desvinculación confirmada por el servidor (sin excepción), la actualiza localmente (`ApplyLocalUnlink`) reconstruyendo el `AccountConnectionDto`/`AccountConnectionsView` inmutables con la fila desvinculada. Es una solución contenida enteramente en el fichero nuevo de este PR, no una corrección retroactiva de PR #3. Se documenta aquí con transparencia porque es una interacción no evidente entre una decisión ya cerrada (la caché de §D6) y un requisito de esta fase; el diseño anticipa la conducta deseada ("la página `/cuenta/conexiones` lo muestra de inmediato porque recarga su propio listado") pero no detalla el mecanismo, y este PR lo resuelve sin reabrir PR #3.

---

## Evidencia del ciclo TDD

| Tarea | Fichero de prueba | Capa | Red de seguridad | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 4.1/4.2/4.3 | `Web/AccountConnectionsPageContractTests.cs` (nuevo) | Contrato de fuente | ➖ N/A (fichero nuevo) | ✅ Escrito, fallo real en ejecución (`No se encontró el archivo fuente`, 2/2 fallos) | ✅ Pasa (2/2) tras crear la página completa (4.2+4.3 en el mismo `Write`, sin RED intermedio propio: `tasks.md` no define uno para 4.2/4.3) | ➖ 2 casos (ruta+autorización sin política; formulario clásico con antiforgery) | ➖ No hizo falta |
| 4.4 | — (sin RED propio en `tasks.md`; el escenario de especificación "Aviso visible en la pantalla de conexiones" no es verificable sin `bUnit`, ver desviación 2 abajo) | — | — | ➖ No aplica | ✅ Verificado por compilación + suite completa en verde | ➖ No aplica | ➖ No hizo falta |

### Resumen de pruebas

- **Pruebas nuevas escritas:** 2 (`AccountConnectionsPageContractTests`).
- **Pruebas totales pasando:** 1387/1387 (baseline 1385 + 2 nuevas), 0 fallos, 0 omitidas.
- **Capas usadas:** Contrato de fuente (2), Integración (0), E2E (0) — el smoke test manual (tarea 4.5) es el único nivel que ejercita PR #2+#3+#4 juntos con un navegador real.
- **Pruebas de aprobación (refactor):** Ninguna — sin refactorización de conducta existente en este PR.
- **Funciones puras creadas:** 0 en producción nueva propia de este PR más allá de los literales de `AccountConnectionMessages` (funciones de interpolación ya existentes en ese patrón).

---

## Evidencia de unidad de trabajo

| Evidencia | Valor |
|---|---|
| Comando de prueba enfocado y resultado exacto | `dotnet test Ludeka.sln --filter "FullyQualifiedName~AccountConnectionsPageContractTests"` → **Con error: 0, Superado: 2, Omitido: 0, Total: 2** |
| Arnés de runtime / escenario y resultado exacto | **N/A — manual**, tal como el propio work unit de `tasks.md` lo declara: smoke test de navegador real (vincular → desvincular → intentar desvincular el último). Es el único punto donde PR #2 (guarda), PR #3 (transporte) y PR #4 (interfaz) se comprueban juntos, y exige credenciales OAuth reales que no están en este entorno. Pasos exactos en la sección siguiente |
| Frontera de reversión | Revertir `AccountConnections.razor` (fichero nuevo, autónomo) y las 4 constantes nuevas añadidas a `AccountConnectionMessages.cs`; el endpoint (`Program.cs`) y `IExternalLoginService`/`IAccountConnectionsService` (PR #2/#3) siguen operativos sin interfaz, exactamente como ya preveía `tasks.md` |

---

## Pasos del smoke test manual (tarea 4.5 — para que el maintainer los ejecute a mano)

No se ejecuta en esta fase: exige credenciales OAuth reales de al menos un proveedor social, que no están disponibles en este entorno de aplicación. Pasos exactos:

1. Configurar credenciales reales de al menos dos proveedores habilitados (p. ej. Google y Discord) en `appsettings.Development.json` o variables de entorno (`Authentication__Providers__Google__ClientId`/`ClientSecret`, ídem Discord), y arrancar la aplicación (`dotnet run --project src/Ludeka.Web`).
2. Iniciar sesión en `/login` con una cuenta de prueba usando el proveedor A. Confirmar que la sesión queda activa.
3. Navegar a `/cuenta/conexiones`. Comprobar: el proveedor A aparece con la píldora "Vinculado" (verde/activa); el resto de proveedores habilitados aparecen "Sin vincular"; si la cuenta no tiene ningún correo verificado, aparece el aviso ámbar de correo no verificado.
4. Pulsar "Vincular" sobre un segundo proveedor habilitado (B). Debe dispararse el desafío OAuth de B (redirección al proveedor, no un error ni un `@onclick` fallido).
5. Completar el consentimiento con una cuenta de B que entregue correo verificado. Al volver, comprobar la URL `?resultado=vinculado`, el aviso verde de éxito, que B pasa a "Vinculado · correo verificado", y que el aviso ámbar desaparece si antes era el único correo verificado.
6. Pulsar "Desvincular" sobre B. Comprobar que la fila pasa a "Sin vincular" **sin recargar la página** (verificar en las herramientas de red del navegador que no hay una navegación HTTP nueva) y que no aparece ningún aviso de error.
7. Pulsar "Desvincular" sobre A (ahora el único método restante). Comprobar que aparece el aviso rojo de denegación con el texto de `AccountConnectionMessages.LastAccessMethodDenied` ("No puedes desvincular tu único método de acceso…") y que A sigue "Vinculado".
8. **Accesibilidad:** repetir los pasos 6 y 7 navegando solo con teclado (Tab/Enter), confirmando que el anillo de foco es visible en cada botón y enlace; y con un lector de pantalla activo (NVDA o VoiceOver), confirmar que el aviso de denegación del paso 7 se anuncia automáticamente en cuanto aparece, sin necesitar mover el foco manualmente.
9. **Caso de colisión (opcional, requiere una segunda cuenta de prueba):** vincular el mismo proveedor B desde una cuenta distinta a la usada en el paso 5 mientras B sigue vinculado a la primera cuenta; comprobar la redirección a `?resultado=en-uso` con el aviso rojo genérico y que ninguna fila cambia de propietario.

---

## Ficheros modificados

| Fichero | Acción | Qué cambia |
|---|---|---|
| `tests/Ludeka.UnitTests/Web/AccountConnectionsPageContractTests.cs` | Creado | 2 pruebas de contrato de fuente (ruta+autorización, formulario clásico) |
| `src/Ludeka.Web/Components/Pages/AccountConnections.razor` | Creado | Página `/cuenta/conexiones`: listado, vincular (formulario clásico), desvincular (`@onclick` + guarda), aviso de correo no verificado, traducción de `?resultado=` |
| `src/Ludeka.Application/Features/Identity/AccountConnectionMessages.cs` | Modificado | 4 constantes nuevas (`LinkedSuccessfully`, `AlreadyLinkedToThisAccountNotice`, `RejectedOwnedByAnotherAccountGenericNotice`, `SessionChangedDuringLink`) + `UnverifiedProviderEmailNotice`, sin tocar los miembros existentes de PR #2 |
| `openspec/changes/change-49-vinculacion-cuentas/tasks.md` | Modificado | 4 casillas marcadas `[x]` (4.1-4.4); 4.5 queda sin marcar |

---

## Recuento de líneas autoradas (`git diff 22678ad..HEAD --stat`)

| Alcance | Inserciones | Eliminaciones | Total |
|---|---|---|---|
| Producción (`AccountConnections.razor` + `AccountConnectionMessages.cs`) | 262 | 0 | **262** |
| Pruebas (`AccountConnectionsPageContractTests.cs`) | 61 | 0 | **61** |
| Producción + pruebas | 323 | 0 | **323** |

Sin artefactos generados en este PR (no hay migración EF Core).

**Contraste con la estimación de `tasks.md` (≈160-255, la más holgada de la cadena):** el real (323) **supera el extremo alto de la estimación en 68 líneas**, pero queda **77 líneas por debajo del presupuesto de 400** — el único PR de la cadena, junto con #1, #5, #6 y #7, que no necesita partición ni `size:exception`. El exceso sobre la estimación se concentra en la página (221 líneas): el diseño reutiliza patrones ya existentes (`badge-pill`, tokens `--state-*`, `Login.razor:35`) pero la pantalla cubre listado + 2 mecanismos de acción (formulario clásico y `@onclick`) + 4 tipos de aviso + comentarios de intención en español, más que un "listado mínimo". Las pruebas (61 líneas) están dentro de la banda estimada (~40-65).

---

## Desviaciones respecto al diseño / tareas

1. **Ninguna de las tareas 4.1-4.4 se reinventó.** 4.1 se implementó tal como está escrita, con RED observado por ejecución. 4.2/4.3/4.4 no tienen un `[RED]` propio en `tasks.md` (solo 4.1 lo tiene): se implementaron como `[GREEN]` directo, verificadas por compilación, por los 2 `[Fact]` de 4.1 y por la suite completa en verde — tal como el propio documento las clasifica.
2. **El escenario de especificación "Aviso visible en la pantalla de conexiones" (4.4) no tiene una prueba automática dedicada.** Sin `bUnit` en el repositorio (verificado en PR #3), no hay manera de simular el renderizado condicional de un `@if` sobre un valor async sin convertirlo en otra prueba de contrato de fuente que solo comprobaría la presencia del texto en el fichero, no que se muestre condicionalmente. Se cubre por: (a) el nivel de servicio ya probado en PR #3 (`AccountConnectionsServiceTests`), y (b) el smoke test manual de la tarea 4.5.
3. **Interacción entre la caché de ámbito de PR #3 y el refresco en el circuito de PR #4** (documentada en detalle arriba): resuelta dentro de `AccountConnections.razor` sin tocar `AccountConnectionsService.cs`.
4. **Añadidas 4 constantes nuevas a `AccountConnectionMessages.cs`** (más `UnverifiedProviderEmailNotice`) para los mensajes fijos de `?resultado=` y el aviso de correo no verificado — instrucción explícita del encargo: "si necesitas un texto nuevo que no está ahí, añádelo a esa clase en vez de embeberlo en el Razor". Los mensajes son genéricos (no interpolan `{Proveedor}` como sugiere la tabla ilustrativa de `design.md` §D4) porque `AccountConnectionRoutes.PageWithResult` (PR #3, no modificado) solo transporta el código de resultado, no el nombre del proveedor.
5. **Añadida una captura defensiva de `UnauthorizedAccessException` → `Navigation.TryRedirectToLogin(ex)`** en `UnlinkAsync`, más allá de la única excepción que la tarea 4.3 menciona explícitamente (`LastAccessMethodException`). Sigue el mismo idioma que **todos** los demás escritores con identidad del proyecto (`SessionDenialUiContractTests.EveryIdentityWriter_TranslatesTheSessionDenialIntoALoginRedirect`, 12 ficheros). **No se ha añadido `AccountConnections.razor` a esa `[Theory]`** (fichero fuera del alcance de las tareas 4.1-4.5): queda anotado para que el maintainer decida si lo incorpora.
6. **No se ha usado el componente compartido `PageHeaderEditorial.razor`** para la cabecera de la página. Su propio comentario de intención lo reserva a "las 4 páginas de listado" (Catálogo, Eventos, Sorteos, Novedades); `/cuenta/conexiones` es una página de ajustes de cuenta, la misma categoría que `MyLibrary.razor`/`UserManagement.razor`, que tampoco lo usan. Se reutilizó en su lugar la clase `.badge-pill` directamente (la misma que usa `PageHeaderEditorial` por debajo) para la píldora de identidad, y la estructura de cabecera manual de `MyLibrary.razor`/`UserManagement.razor`.

## Tropiezos operativos

Ninguno. No hubo bloqueo por `MSB3027` (no había ningún proceso `Ludeka.Web` en ejecución antes de compilar ni ejecutar pruebas).

---

## Estado

**4/5 tareas completas (4.1-4.4); 4.5 sin marcar por ser verificación manual no ejecutable en este entorno.** `dotnet test Ludeka.sln`: **1387/1387 en verde, 0 fallos, 0 omitidas.** Líneas autoradas reales: **323**, por encima de la estimación de 160-255 pero 77 líneas por debajo del presupuesto de 400 — sin necesidad de partición ni `size:exception`. Commits de este lote sin pushear, sin PR abierto: eso lo gestiona el orquestador.

---

# Progreso de aplicación — INC-49, PR #5 (Flujo de colisión: partición 2a/2b de `ResolveAsync`)

> **Cambio:** `change-49-vinculacion-cuentas` · **Fase:** `sdd-apply` · **Fecha:** 2026-09-17
> **Alcance de este lote:** únicamente las 6 tareas del PR #5 (Unidad F), sección «PR #5 — Flujo de colisión: partición 2a/2b de `ResolveAsync`» de `tasks.md`.
> **Modo:** Strict TDD activo. Runner contractual: `dotnet test Ludeka.sln`.
> **Lote:** quinto lote — progreso previo de PR #1/#2/#3 (observación 370) y PR #4 (sección local de este mismo fichero) leído íntegramente y fusionado arriba, sin pisarlo.
> **Worktree / rama:** `C:\repos\ludeka-wt\vinculacion-cuentas` — `inc/vinculacion-cuentas-05-colision` (parte de `inc/vinculacion-cuentas-04-pantalla` en `1a98be2`).

---

## Criterio de aceptación innegociable — verificado

Las **6 pruebas existentes** de `tests/Ludeka.UnitTests/Application/ExternalLoginServiceTests.cs:51-146` **no se han tocado ni una sola línea** y siguen en verde (confirmado por ejecución, no por inspección). `git diff 1a98be2..HEAD --name-only` no incluye ese fichero (ver sección de líneas autoradas). No hizo falta reconsiderar el enfoque en ningún momento: el trazado manual de `sdd-design` (sección 7) se confirmó exactamente por ejecución.

---

## Tareas completadas (6/6)

- [x] 5.1 **[RED]** `ExternalLoginCascadeRegressionTests.cs` (nuevo) — las 6 pruebas de regresión obligatorias, con los nombres exactos y en el orden de `tasks.md`. RED real confirmado por ejecución: **1/6, 5/6 y 6/6 ya pasaban** (fijaciones de conducta existente); **2/6, 3/6 y 4/6 fallaban** — exactamente la nota de TDD de la propia tarea, no un error de la prueba.
- [x] 5.2 **[GREEN]** `ExternalLoginService.cs` — rama 2 de `ResolveAsync` partida en 2a (`ListByUserIdAsync(match.Id)` vacío ⇒ vincula automáticamente, conducta idéntica a INC-46) y 2b (`Count > 0` ⇒ `throw new ExternalLoginCollisionException(...)`, cero escrituras). `providerEmailVerified: emailVerified` añadido a la creación de la fila en las ramas 2a **y** 3 (esta última no lo tenía y tampoco estaba pedido explícitamente por ningún test de INC-46, pero sí por el texto literal de la tarea). Hace pasar 5.1: **12/12** (6 regresión + 6 protegidas).
- [x] 5.3 **[RED]** `AccountConnectionMessagesTests.cs` — 2 `[Fact]` nuevos para el aviso de colisión de login. RED por `CS0117` (`AccountConnectionMessages.LoginCollisionNotice` no existía).
- [x] 5.4 **[GREEN]** `AccountConnectionMessages.cs` — constante `LoginCollisionNotice` con el texto exacto de la propuesta §4.2. Hace pasar 5.3: **4/4**.
- [x] 5.5 **[RED]** `LoginRedirectTests.cs` — 2 pruebas nuevas (`[Fact]` + `[Theory]` con 4 casos) para `LoginRedirect.ResolveAccountCollisionNotice`. RED por `CS0117` (el método no existía).
- [x] 5.6 **[GREEN]** `LoginRedirect.cs` (método nuevo `ResolveAccountCollisionNotice`), `AccountConnectionRoutes.cs` (constante `LoginWithAccountCollision`), `ExternalLoginEvents.cs` (el camino **normal** de `HandleTicketReceivedAsync` envuelve `ResolveAsync` en `try/catch (ExternalLoginCollisionException)`, redirige con `HandleResponse()`), `Login.razor` (`[SupplyParameterFromQuery] string? Aviso` + bloque de renderizado). Hace pasar 5.5: **27/27** (comando enfocado completo del work unit F).

**No regresión confirmada:** las 6 pruebas de `ExternalLoginServiceTests.cs:51-146` no se tocaron y siguen en verde; suite completa **1400/1400** sin fallos.

---

## Decisiones tomadas por esta fase (no fijadas literalmente por `design.md`/`tasks.md`)

1. **Mensaje de la excepción desacoplado de `AccountConnectionMessages.LoginCollisionNotice`.** La tarea 5.2 antecede a la 5.4 en el orden RED→GREEN, así que `ExternalLoginCollisionException` no podía referenciar todavía una constante que no existiría hasta dos tareas después. Se usó un texto diagnóstico interno propio (interpolando el proveedor) en vez de forzar una referencia adelantada. Es coherente con el propio flujo de datos: nadie lee `.Message` de esta excepción en el camino de usuario — `ExternalLoginEvents.cs` la traduce exclusivamente a través del código cerrado `?aviso=cuenta-existente`, nunca del texto de la excepción (mismo principio que el diseño fija en §3.1 para el rechazo de vinculación).
2. **`ResolveAccountCollisionNotice` vive en `LoginRedirect.cs` (`Ludeka.Web.Services`), no inline en `Login.razor`.** La tarea 5.5 nombra explícitamente `LoginRedirectTests.cs` como fichero a ampliar; sin `bUnit` en el repositorio (confirmado en PR #3), la única forma de que ese fichero contenga una prueba de comportamiento real (no de contrato de fuente) es extraer la función de decisión a una clase estática pura. Amplía ligeramente el propósito original de `LoginRedirect` (de "navegar hacia el login" a "también resolver qué aviso se pinta en él"), pero evita duplicar lógica entre `Login.razor` y un fichero de pruebas que ya existía con ese nombre desde INC-46.
3. **Código cerrado elegido: `cuenta-existente`.** Ni la propuesta ni el diseño fijan el literal exacto de `?aviso=` para esta colisión (sí fijan `vinculacion-sin-sesion` para el caso de vinculación sin sesión, PR #3). Se eligió siguiendo el mismo idioma kebab-case en español ya usado por `sesion-cambiada`/`ya-vinculado`/`en-uso`.
4. **Literal duplicado, no compartido, entre `AccountConnectionRoutes.LoginWithAccountCollision` y `LoginRedirect.ResolveAccountCollisionNotice`.** Sigue el precedente ya establecido en el repositorio (`AccountConnectionRoutes.PageWithResult` y `AccountConnections.razor.BuildResultBanner` ya duplican literalmente "vinculado"/"en-uso"/etc. en vez de compartir una constante); no se introduce una constante compartida nueva para no romper ese estilo ya asentado.
5. **Sin prueba dedicada para el `try/catch` de `ExternalLoginEvents.cs` en el camino normal.** `ExternalLoginEventsLinkBranchTests.cs` construye siempre un contexto **con** intención de vinculación (`ExternalLoginIntent.MarkLink`); no existe ningún arnés previo para el camino normal (`ResolveAsync`) a este nivel, y el propio `tasks.md` declara el arnés de runtime de la Unidad F como **N/A, no exigido por la propuesta §11** (exigiría dos proveedores OAuth reales entregando el mismo correo verificado). Se decidió no inventar un fichero de prueba nuevo fuera de las 6 tareas asignadas — mismo criterio que ya aplicó PR #4 para su tarea 4.4 (aviso sin prueba dedicada por falta de `bUnit`). La cobertura de esta pieza es: compilación + suite completa en verde + inspección directa del diff. **Riesgo residual explícito:** si `ExternalLoginEvents.cs` alguna vez deja de traducir la excepción al código cerrado, ningún test automático lo detectaría hoy; queda anotado para que el maintainer decida si lo amplía.

---

## Evidencia del ciclo TDD

| Tarea | Fichero de prueba | Capa | Red de seguridad | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 5.1/5.2 | `Application/ExternalLoginCascadeRegressionTests.cs` (nuevo) | Unidad (SQLite `:memory:`) | ➖ N/A (fichero nuevo); las 6 de `ExternalLoginServiceTests.cs` no se tocan | ✅ Escrito, RED real en ejecución: 3/6 con error (pruebas 2, 3, 4) y 3/6 ya en verde (1, 5, 6) — exactamente la nota de TDD de la tarea 5.1 | ✅ Pasa 12/12 (6 regresión + 6 protegidas) tras partir la rama 2 en 2a/2b y escribir `providerEmailVerified` en 2a y 3 | ✅ 6 casos (rama 1, 2a, 2b, no reasignación bajo colisión repetida, rama 3 no verificado, rama 3 sin correo) | ➖ No hizo falta |
| 5.3/5.4 | `Application/AccountConnectionMessagesTests.cs` | Unidad (función pura) | ✅ 2/2 (baseline del fichero, PR #2) | ✅ Escrito (`CS0117`: `LoginCollisionNotice` no existía) | ✅ Pasa (4/4) | ✅ 2 casos (palabras prohibidas, dirección a Ajustes → Conexiones) | ➖ No hizo falta |
| 5.5/5.6 | `Web/LoginRedirectTests.cs` | Unidad (función pura) | ✅ 6/6 (baseline del fichero, INC-46) | ✅ Escrito (`CS0117`: `ResolveAccountCollisionNotice` no existía) | ✅ Pasa (27/27 del comando enfocado completo de la Unidad F) | ✅ 5 casos (código cerrado; `null`; cadena vacía; código de otra funcionalidad, `vinculacion-sin-sesion`; código desconocido) | ➖ No hizo falta |

### Resumen de pruebas

- **Pruebas nuevas escritas:** 13 (6 de regresión + 2 de mensajes + 1 `[Fact]` + 4 casos de `[Theory]` en `LoginRedirectTests.cs`).
- **Pruebas totales pasando:** 1400/1400 (baseline 1387 + 13 nuevas), 0 fallos, 0 omitidas.
- **Capas usadas:** Unidad (13: 6 con SQLite `:memory:`, 7 funciones puras), Integración (0), E2E (0) — el arnés de runtime de esta unidad es N/A por declaración explícita de `tasks.md`.
- **Pruebas de aprobación (refactor):** Ninguna — sin refactorización de conducta existente en este PR.
- **Funciones puras creadas:** 1 (`LoginRedirect.ResolveAccountCollisionNotice`, determinista y sin efectos secundarios).

---

## Evidencia de unidad de trabajo

| Evidencia | Valor |
|---|---|
| Comando de prueba enfocado y resultado exacto | `dotnet test Ludeka.sln --filter "FullyQualifiedName~ExternalLoginCascadeRegressionTests\|FullyQualifiedName~ExternalLoginServiceTests\|FullyQualifiedName~AccountConnectionMessagesTests\|FullyQualifiedName~LoginRedirectTests"` → **Con error: 0, Superado: 27, Omitido: 0, Total: 27** |
| Arnés de runtime / escenario y resultado exacto | **N/A** — el propio work unit de `tasks.md` lo declara así: exigiría dos proveedores OAuth reales entregando el mismo correo verificado; no exigido por la propuesta §11 |
| Frontera de reversión | Revertir la partición 2a/2b de `ResolveAsync` (vuelve a la rama 2 única de INC-46), el mensaje de colisión de login, `LoginRedirect.ResolveAccountCollisionNotice`, la constante de ruta, el `try/catch` de `ExternalLoginEvents.cs` y el bloque nuevo de `Login.razor`; independiente de PR #2/#3/#4, tal como preveía `tasks.md` |

---

## Ficheros modificados

| Fichero | Acción | Qué cambia |
|---|---|---|
| `tests/Ludeka.UnitTests/Application/ExternalLoginCascadeRegressionTests.cs` | Creado | 6 pruebas de regresión de las 3 ramas de `ResolveAsync` |
| `src/Ludeka.Application/Features/Identity/ExternalLoginService.cs` | Modificado | Partición 2a/2b de la rama 2; `providerEmailVerified` en 2a y 3 |
| `tests/Ludeka.UnitTests/Application/AccountConnectionMessagesTests.cs` | Modificado | +2 pruebas de honestidad del aviso de colisión de login |
| `src/Ludeka.Application/Features/Identity/AccountConnectionMessages.cs` | Modificado | Constante `LoginCollisionNotice` |
| `tests/Ludeka.UnitTests/Web/LoginRedirectTests.cs` | Modificado | +2 pruebas (`[Fact]` + `[Theory]` ×4) de `LoginRedirect.ResolveAccountCollisionNotice` |
| `src/Ludeka.Web/Services/LoginRedirect.cs` | Modificado | Método nuevo `ResolveAccountCollisionNotice` |
| `src/Ludeka.Web/Authentication/AccountConnectionRoutes.cs` | Modificado | Constante `LoginWithAccountCollision` |
| `src/Ludeka.Web/Authentication/ExternalLoginEvents.cs` | Modificado | `try/catch (ExternalLoginCollisionException)` en el camino normal de `HandleTicketReceivedAsync` |
| `src/Ludeka.Web/Components/Pages/Login.razor` | Modificado | `[SupplyParameterFromQuery] string? Aviso` + bloque de renderizado del aviso |
| `openspec/changes/change-49-vinculacion-cuentas/tasks.md` | Modificado | 6 casillas marcadas `[x]` (5.1-5.6) |

---

## Recuento de líneas autoradas (`git diff 1a98be2..HEAD --stat` / `--numstat`)

| Alcance | Inserciones | Eliminaciones | Total |
|---|---|---|---|
| Producción (6 ficheros: `ExternalLoginService.cs`, `AccountConnectionMessages.cs`, `LoginRedirect.cs`, `AccountConnectionRoutes.cs`, `ExternalLoginEvents.cs`, `Login.razor`) | 93 | 15 | **108** |
| Pruebas (3 ficheros: 1 nuevo + 2 ampliados) | 199 | 0 | **199** |
| Producción + pruebas (sin `tasks.md`) | 292 | 15 | **307** |
| + `tasks.md` (bookkeeping de tareas) | 298 | 21 | **319** |

Sin artefactos generados en este PR (no hay migración EF Core).

**Contraste con la estimación de `tasks.md` (≈250-380):** el real (307, o 319 con `tasks.md`) cae **dentro** de la banda estimada — producción (108) dentro de 93-155; pruebas (199) dentro de 160-228. Es, junto con PR #1 y PR #4, el tercer PR de la cadena que no necesita partición ni `size:exception`: muy por debajo del presupuesto de 400.

---

## Commits creados (3 de implementación, ninguno pusheado; más el commit de documentación que cierra este lote)

1. `6534300` `feat(application): partir la rama 2 de resolveasync en 2a/2b para no fusionar en silencio`
2. `3895acb` `feat(application): anadir el aviso de colision de login sin prometer fusion automatica`
3. `dfdb631` `feat(web): redirigir el login a un aviso cuando el correo verificado colisiona`
4. (este mismo commit de documentación) `docs(sdd): completar las tareas 5.1-5.6 del PR #5 de INC-49`

Ninguno lleva atribución de IA (regla explícita de `AGENTS.md` §4). Worktree sobre `inc/vinculacion-cuentas-05-colision`, sin push ni PR: eso lo decide el orquestador.

---

## Desviaciones respecto al diseño / tareas

Ninguna de las 6 tareas se reinventó: se implementaron tal como están escritas, en el orden RED→GREEN especificado, sin reabrir ninguna decisión cerrada de `proposal.md` §3 ni las resoluciones P2-P5 de `design.md`. Las 5 decisiones de implementación no fijadas literalmente por el diseño/tareas (elección de literal, ubicación de la función pura, desacople del mensaje de la excepción, ausencia de prueba dedicada del `try/catch`) están documentadas en la sección dedicada arriba, con su razonamiento.

## Tropiezos operativos

Ninguno. No hubo bloqueo por `MSB3027` (no había ningún proceso `Ludeka.Web` en ejecución antes de compilar ni ejecutar pruebas).

---

## Estado

**6/6 tareas completas (5.1-5.6).** `dotnet test Ludeka.sln`: **1400/1400 en verde, 0 fallos, 0 omitidas.** Las 6 pruebas de `ExternalLoginServiceTests.cs:51-146` no se tocaron. Líneas autoradas reales: **307** (319 con `tasks.md`), dentro de la estimación de 250-380 y muy por debajo del presupuesto de 400 — sin necesidad de partición ni `size:exception`. Worktree sobre `inc/vinculacion-cuentas-05-colision`, 3 commits de implementación sin pushear, sin PR abierto: eso lo gestiona el orquestador. Próximo paso sugerido: `sdd-archive` sobre PR #5 (verificación es opcional), o continuar con `sdd-apply` para PR #6 (`inc/vinculacion-cuentas-06-aviso-cabecera`, depende de PR #3 y PR #4, ambos ya completos) o PR #7 (depende de PR #1 y PR #2, también completos).

---

# Progreso de aplicación — INC-49, PR #6 (Aviso de correo no verificado en cabecera + arreglo de caché obsoleta)

> **Cambio:** `change-49-vinculacion-cuentas` · **Fase:** `sdd-apply` · **Fecha:** 2026-09-17
> **Alcance de este lote:** las 3 tareas del PR #6 (Unidad G2), sección «PR #6 — Aviso de correo no verificado en cabecera» de `tasks.md`, **más un arreglo de caché obsoleta añadido explícitamente por el orquestador** (no estaba en `tasks.md` ni en `design.md` §3.2 tal como se cerró en su momento).
> **Modo:** Strict TDD activo. Runner contractual: `dotnet test Ludeka.sln`.
> **Lote:** sexto lote — progreso previo de PR #1-#3 (observación 370) y PR #4/#5 (secciones locales de este mismo fichero) leído íntegramente y fusionado arriba, sin pisarlo.
> **Worktree / rama:** `C:\repos\ludeka-wt\vinculacion-cuentas` — `inc/vinculacion-cuentas-06-aviso-cabecera` (parte de `inc/vinculacion-cuentas-05-colision` en `59e5260`).

---

## Tareas completadas (3/3)

- [x] 6.1 **[RED]** `AuthorizationPipelineContractTests.cs` — dos `[Fact]` del encargo original: (a) `MainLayout.razor` monta `<AccountEmailNotice />` (RED por ausencia del componente); (b) `PublicProfile.razor` no referencia `AccountEmailNotice`/`IAccountConnectionsService`/`HasVerifiedProviderEmailAsync` (confirmado en verde **ya antes** de tocar código — pin de regresión hacia adelante, exactamente como anticipaba el propio `tasks.md`, nota 5 del encargo). **Añadido por el arreglo de caché:** un tercer `[Fact]`, `AccountEmailNotice_ShouldSubscribeToConnectionsInvalidatedAndDisposeCleanly`, contrato de fuente hermano de `SessionGuard_ShouldForceAFullReload…` que exige `@implements IDisposable`, `Connections.Invalidated +=`/`-=` y `RendererInfo.IsInteractive`. RED confirmado por ejecución: 3 fallos (fichero `AccountEmailNotice.razor` inexistente para (a) y el tercer `[Fact]`; (b) ya en verde).
- [x] 6.2 **[GREEN]** Creado `src/Ludeka.Web/Components/Shared/AccountEmailNotice.razor`: inyecta `ICurrentUserService`/`IAccountConnectionsService`; `OnInitializedAsync` sale de inmediato sin sesión; con sesión, `_needsNotice = !await Connections.HasVerifiedProviderEmailAsync()`; renderiza `AccountConnectionMessages.UnverifiedProviderEmailNotice` (mismo literal que 4.4, sin duplicar) con enlace a `/cuenta/conexiones` y botón de descarte `@onclick="Dismiss"` (campo privado `_dismissed`, sin persistencia — mecanismo de descarte del diseño §3.2, **sin tocar**). **Ampliado por el arreglo de caché:** `@implements IDisposable`; `OnAfterRender(firstRender)` se suscribe a `Connections.Invalidated` bajo el mismo guard que `SessionGuard.razor` (`RendererInfo.IsInteractive`, para no suscribir la instancia descartada del prerenderizado); el handler llama a `InvokeAsync` para refrescar `_needsNotice` y `StateHasChanged()`; `Dispose()` se da de baja. Hace pasar 6.1(a) parcialmente (el componente existe, pero `MainLayout` aún no lo monta) y por completo el tercer `[Fact]` añadido.
- [x] 6.3 **[GREEN]** Modificado `src/Ludeka.Web/Components/Layout/MainLayout.razor`: montado `<AccountEmailNotice />`. Hace pasar 6.1(a) por completo.
  **Desviación de ubicación, documentada y razonada:** se monta inmediatamente después de `</header>` y antes de `<main>`, **no** como hermano literal de `<SessionGuard />` al final del documento (`:302`, junto a `<LocationSelectorModal>` y `#blazor-error-ui`). Motivo verificado en el propio fichero: `SessionGuard` no renderiza ningún nodo (componente puro de suscripción), así que su posición en el árbol es indiferente; `AccountEmailNotice` sí renderiza un `<div role="status">` visible, y colocarlo junto a `SessionGuard` lo dejaría **después del `<footer>`**, fuera de la vista sin desplazarse hasta el final de la página — contradiciendo literalmente el escenario de especificación «Aviso visible y descartable en la cabecera» y el propio nombre de la tarea. La prueba 6.1(a) (`MainLayout_ShouldMountTheAccountEmailNotice`) solo exige `Assert.Contains("<AccountEmailNotice />", ...)`, sin comprobar la posición exacta — igual que su hermana `SessionGuard_ShouldForceAFullReload…` tampoco la comprueba —, así que esta reubicación no rompe ningún contrato ya cerrado ni reabre ninguna decisión de diseño sobre el *mecanismo* del aviso (solo su ubicación visual en el DOM).

**No regresión de este PR:** las 6 pruebas de `ExternalLoginServiceTests.cs:51-146` no se tocaron; `CurrentUserContractTests` no se tocó (`ICurrentUserService` sigue exactamente igual); las 25 pruebas de `AuthorizationPipelineContractTests` preexistentes (incluida `SessionGuard_ShouldForceAFullReloadWhenTheSessionIsInvalidated`, que solo afirma la presencia de `<SessionGuard />` y sigue en verde sin tocarse) y las 2 de `AccountConnectionsPageContractTests` preexistentes siguen en verde — confirmado por ejecución completa, no por inspección.

---

## Arreglo de caché obsoleta (encargo explícito del orquestador, fuera de las 3 tareas de `tasks.md`)

**El problema, tal como lo verificó el orquestador antes de encargarlo:** `AccountConnectionsService.GetConnectionsAsync()` (creado en PR #3) cachea el resultado en el campo `_cachedView` durante la vida de la instancia. El servicio está registrado `AddScoped` (`Program.cs:75`) y, en Blazor Server con `App.razor:39` montando `<Routes @rendermode="InteractiveServer" />` (interactivo **global**, no por islas), un ámbito `Scoped` vive **todo el circuito**, no una petición HTTP. `design.md` §D6 ("Caché de ámbito y su rebaba conocida") había aceptado un "desfase de una sola navegación" como limitación menor — esa premisa era la equivocada: sin invalidación explícita, **ningún** lector posterior del mismo circuito (ni la propia página `/cuenta/conexiones` en una recarga interna del componente, ni menos aún un componente de cabecera que vive todo el circuito) vería el cambio hasta una **recarga completa de página**, no hasta "la siguiente navegación". PR #4 ya lo esquivaba con `ApplyLocalUnlink` (parche en memoria local a la página), pero el aviso de cabecera de este PR **no tenía ninguna copia local que parchear**, y la especificación exige que una cuenta sin correo verificado vea el aviso — incluida justo después de desvincular su único proveedor verificado.

**Mecanismo elegido: invalidación explícita + evento de notificación, exactamente el patrón que pedía el encargo.**

1. `IAccountConnectionsService.InvalidateCache()` (método nuevo) — pone `_cachedView = null` y dispara `Invalidated`.
2. `IAccountConnectionsService.Invalidated` (evento nuevo, `EventHandler?`) — se dispara en cada invalidación, no solo la primera (verificado por prueba: `InvalidateCache_ShouldRaiseInvalidatedEachTimeItIsCalled`, dos invocaciones consecutivas incrementan el contador dos veces).
3. `AccountConnections.razor.UnlinkAsync` invoca `Connections.InvalidateCache()` tras un `UnlinkAsync` exitoso, y vuelve a pedir la vista (`RefreshViewAsync()`, extraído en el paso REFACTOR para no duplicar las dos llamadas `await Connections.GetConnectionsAsync()`/`HasVerifiedProviderEmailAsync()` que ya existían en `OnInitializedAsync`). Al estar la caché invalidada, esa relectura golpea de verdad `IExternalLoginRepository.ListByUserIdAsync`, no la copia obsoleta.
4. `AccountEmailNotice.razor` se suscribe a `Connections.Invalidated` en `OnAfterRender(firstRender)` (mismo guard `RendererInfo.IsInteractive` que `SessionGuard.razor`, para no suscribir la instancia que se descarta al terminar el prerenderizado) y se da de baja en `Dispose()`. Cuando la página de conexiones invalida la caché en el mismo circuito, el aviso de cabecera se refresca y repinta sin recarga completa — el requisito exacto del encargo.

**Por qué esta forma y no otra.** El encargo proponía explícitamente "un método de invalidación en `IAccountConnectionsService` más una notificación a la que el componente de aviso se suscribe (implementando `IDisposable` para darse de baja)". Es el patrón de contenedor de estado habitual en Blazor Server para un servicio `Scoped` compartido por varios componentes del mismo circuito, y es la extensión más pequeña posible sobre el contrato ya cerrado en PR #3: no se toca ningún miembro existente de `IAccountConnectionsService`, no se añade DI nueva (el propio servicio ya es la pieza compartida), y no reabre el mecanismo de *descarte* del aviso (`_dismissed`, diseño §3.2, sección "Alternativas descartadas" — esa decisión sigue intacta: aquí no se descarta nada, se **refresca el dato subyacente** que decide si el aviso debe existir).

**Por qué se retira `ApplyLocalUnlink` (y no se deja en paralelo).** Con la caché ya invalidada, una relectura real de `IExternalLoginRepository` produce el mismo resultado visible que el parche local calculaba a mano — con una diferencia a favor de la relectura real: `ApplyLocalUnlink` calculaba `CanUnlink` contando solo las filas de `_view.Connections` (proveedores actualmente habilitados en configuración), mientras que `BuildViewAsync` (la fuente canónica, usada tanto en la carga inicial como ahora tras invalidar) cuenta `links.Count` sobre **todas** las filas de la cuenta. Ambos cálculos coinciden en el caso normal — todo proveedor vinculado sigue habilitado — pero divergirían si un proveedor se deshabilitara en configuración mientras una sesión sigue abierta; ningún escenario de la especificación ejercita ese borde, así que no cambia el comportamiento observable en ningún caso probado, y de paso elimina un cálculo paralelo que podía divergir del canónico. Verificado con la suite completa tras el cambio: comportamiento idéntico de cara al usuario en los 5 escenarios de PR #4 (listado, vincular, desvincular con 2+, guarda del último método, traducción de `?resultado=`).

**Cómo se probó (prueba antes que código, también para esta parte):**

| Prueba | Fichero | Qué prueba | RED confirmado | GREEN confirmado |
|---|---|---|---|---|
| `GetConnectionsAsync_AfterInvalidateCache_ShouldReturnFreshDataFromTheRepository` | `AccountConnectionsServiceTests.cs` | Tras borrar una fila por fuera del servicio e invalidar, la siguiente lectura ya no es la misma instancia cacheada y refleja la fila borrada (`Assert.NotSame` + aserción de contenido real) | Error de compilación (`InvalidateCache` no existía) | Verde tras implementar `InvalidateCache`/campo `_cachedView = null` |
| `InvalidateCache_ShouldRaiseInvalidatedEachTimeItIsCalled` | `AccountConnectionsServiceTests.cs` | El evento se dispara en cada llamada (contador 1, luego 2), no solo la primera | Error de compilación (`Invalidated` no existía) | Verde tras implementar el evento |
| `UnlinkAsync_ShouldInvalidateTheSharedConnectionsCacheInsteadOfPatchingLocalState` | `AccountConnectionsPageContractTests.cs` | Contrato de fuente: `AccountConnections.razor` llama a `Connections.InvalidateCache()` y ya no contiene `ApplyLocalUnlink` | Sub-cadena `Connections.InvalidateCache()` ausente; `ApplyLocalUnlink` presente | Verde tras la edición de la página |
| `AccountEmailNotice_ShouldSubscribeToConnectionsInvalidatedAndDisposeCleanly` | `AuthorizationPipelineContractTests.cs` | Contrato de fuente: el componente implementa `IDisposable`, se suscribe y se da de baja, y respeta el guard de interactividad | Fichero inexistente | Verde tras crear el componente |

No hay `bUnit` en el repositorio (verificado en PR #3): la suscripción/baja del componente se prueba por contrato de fuente, como el resto de componentes de este incremento; la lógica real de invalidación (la parte con comportamiento no trivial) se prueba con pruebas de comportamiento reales sobre `AccountConnectionsService`, tal como autorizaba el encargo.

---

## Evidencia del ciclo TDD

| Tarea / pieza | Fichero de prueba | Capa | Red de seguridad | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 6.1(a)/6.1(b) | `Web/AuthorizationPipelineContractTests.cs` | Contrato de fuente | ✅ 22/22 (baseline del fichero) | ✅ Escrito; (a) fallo real (`Assert.Contains` sin coincidencia); (b) verde ya antes de tocar código | ✅ Pasa (25/25 tras 6.2+6.3) | ➖ Casos únicos (presencia; ausencia) | ➖ No hizo falta |
| Arreglo de caché — servicio | `Application/AccountConnectionsServiceTests.cs` | Unidad (SQLite `:memory:` + evento síncrono) | ✅ 5/5 (baseline del fichero) | ✅ Escrito, error de compilación (`CS1061` ×4) | ✅ Pasa (7/7) | ✅ 2 casos por prueba (cacheado vs. invalidado; disparo único vs. repetido) | ➖ No hizo falta — métodos ya mínimos |
| 6.1 (tercer `[Fact]`) / 6.2 | `Web/AuthorizationPipelineContractTests.cs` (contrato) | Contrato de fuente | (mismo fichero, ver arriba) | ✅ Escrito, fichero inexistente | ✅ Pasa tras crear `AccountEmailNotice.razor` | ➖ Caso único (contrato de suscripción/baja) | ➖ No hizo falta |
| Arreglo de caché — página | `Web/AccountConnectionsPageContractTests.cs` | Contrato de fuente | ✅ 2/2 (baseline del fichero) | ✅ Escrito, fallo real (sub-cadena ausente/presente invertidas) | ✅ Pasa (3/3) | ➖ Caso único (invalidar presente, parche local ausente) | ✅ Extraído `RefreshViewAsync()` para no duplicar la relectura entre `OnInitializedAsync` y `UnlinkAsync`; suite completa re-ejecutada tras el cambio, sigue en verde |

### Resumen de pruebas

- **Pruebas nuevas escritas:** 6 (2 de `AccountConnectionsServiceTests` + 1 de `AccountConnectionsPageContractTests` + 3 de `AuthorizationPipelineContractTests`).
- **Pruebas totales pasando:** 1408/1408 (baseline 1402 + 6 nuevas), 0 fallos, 0 omitidas.
- **Capas usadas:** Unidad (2, SQLite `:memory:` + evento), Contrato de fuente (4), Integración (0), E2E (0) — ni la funcionalidad del aviso ni el arreglo de caché tienen superficie HTTP propia nueva.
- **Pruebas de aprobación (refactor):** Ninguna dedicada — el refactor de `RefreshViewAsync()` se protegió reejecutando la suite completa tras extraerlo, no con un test de aprobación distinto.
- **Funciones puras creadas:** 0 — `InvalidateCache()`/`Invalidated` mutan estado de instancia por diseño (son, precisamente, el mecanismo de invalidación de una caché con estado); `AccountEmailNotice`/`AccountConnections` son componentes con estado por naturaleza.

---

## Evidencia de unidad de trabajo

| Evidencia | Valor |
|---|---|
| Comando de prueba enfocado y resultado exacto (declarado por `tasks.md`) | `dotnet test Ludeka.sln --filter "FullyQualifiedName~AuthorizationPipelineContractTests"` → **Con error: 0, Superado: 25, Omitido: 0, Total: 25** |
| Comando de prueba enfocado ampliado (cubre también el arreglo de caché añadido por el orquestador) | `dotnet test Ludeka.sln --filter "FullyQualifiedName~AuthorizationPipelineContractTests\|FullyQualifiedName~AccountConnectionsServiceTests\|FullyQualifiedName~AccountConnectionsPageContractTests"` → **Con error: 0, Superado: 35, Omitido: 0, Total: 35** |
| Arnés de runtime / escenario y resultado exacto | **N/A para 6.1-6.3, tal como declara el propio work unit de `tasks.md`:** solo visual, sin comportamiento de servidor nuevo. **Para el arreglo de caché, N/A también automatizado:** el escenario real (dos pestañas del mismo circuito, o navegación interna sin recarga, viendo aparecer el aviso tras una desvinculación) exige un navegador real con SignalR activo; no hay `bUnit` ni un cliente de circuito en memoria en este repositorio. Cubierto en su lugar por la prueba de comportamiento real sobre el servicio (que prueba la pieza no trivial: la propia invalidación) y por los contratos de fuente (que prueban el cableado: quién invoca, quién se suscribe) |
| Frontera de reversión | Revertir `AccountEmailNotice.razor` (fichero nuevo, autónomo) y su montaje en `MainLayout.razor`: el resto de la aplicación queda exactamente igual. Revertir el arreglo de caché por separado (commit `fix` independiente): `IAccountConnectionsService.InvalidateCache()`/`Invalidated`, la llamada en `AccountConnections.razor` y `RefreshViewAsync()` — si se revierte solo esto, `AccountEmailNotice.razor` dejaría de compilar (depende de `Connections.Invalidated`), así que ambos commits son reversibles pero **no independientes entre sí**: revertir el `fix` exige revertir también el `feat` que lo consume |

---

## Ficheros modificados

| Fichero | Acción | Qué cambia |
|---|---|---|
| `src/Ludeka.Application/Features/Identity/IAccountConnectionsService.cs` | Modificado | `InvalidateCache()` + evento `Invalidated` (arreglo de caché) |
| `src/Ludeka.Application/Features/Identity/AccountConnectionsService.cs` | Modificado | Implementación de `InvalidateCache()`/`Invalidated` (arreglo de caché) |
| `src/Ludeka.Web/Components/Pages/AccountConnections.razor` | Modificado | `UnlinkAsync` invoca `InvalidateCache()` + `RefreshViewAsync()` (extraído); retirado `ApplyLocalUnlink` (arreglo de caché) |
| `src/Ludeka.Web/Components/Shared/AccountEmailNotice.razor` | Creado | Aviso descartable de cabecera (tarea 6.2) + suscripción a `Invalidated`/`IDisposable` (arreglo de caché) |
| `src/Ludeka.Web/Components/Layout/MainLayout.razor` | Modificado | Montaje de `<AccountEmailNotice />` tras `</header>` (tarea 6.3) |
| `tests/Ludeka.UnitTests/Web/AuthorizationPipelineContractTests.cs` | Modificado | +3 pruebas (mount, pin de perfil público, contrato de suscripción/baja) |
| `tests/Ludeka.UnitTests/Application/AccountConnectionsServiceTests.cs` | Modificado | +2 pruebas (invalidación refleja datos frescos; evento se dispara cada vez) |
| `tests/Ludeka.UnitTests/Web/AccountConnectionsPageContractTests.cs` | Modificado | +1 prueba (contrato de invalidación en la página) |
| `openspec/changes/change-49-vinculacion-cuentas/tasks.md` | Modificado | 3 casillas marcadas `[x]` (6.1-6.3) + anotaciones del arreglo de caché y de la desviación de ubicación |

---

## Recuento de líneas autoradas (`git diff 59e5260 --stat` / `--numstat`, worktree vs. base de esta rama)

| Alcance | Inserciones | Eliminaciones | Total |
|---|---|---|---|
| Producción (5 ficheros: `IAccountConnectionsService.cs`, `AccountConnectionsService.cs`, `AccountConnections.razor`, `AccountEmailNotice.razor`, `MainLayout.razor`) | 141 | 28 | **169** |
| Pruebas (3 ficheros: `AuthorizationPipelineContractTests.cs`, `AccountConnectionsServiceTests.cs`, `AccountConnectionsPageContractTests.cs`) | 98 | 0 | **98** |
| Producción + pruebas (sin `tasks.md`) | 239 | 28 | **267** |

Sin artefactos generados en este PR (no hay migración EF Core). `tasks.md` no se ha vuelto a contar por separado en esta tabla (su edición es bookkeeping de tareas, igual criterio que los PR anteriores).

**Contraste con la estimación de `tasks.md` (≈60-100, solo para las tareas 6.1-6.3):** el real (267) la supera con holgura, pero **por un motivo explícito y autorizado**: la estimación de `tasks.md` no incluía el arreglo de caché, que es un encargo añadido por el orquestador después de que se escribiera `tasks.md`. Descontando las 3 tareas originales (aproximadamente el componente + su montaje + las 2 pruebas de 6.1(a)/6.1(b): `AccountEmailNotice.razor` 86 líneas + `MainLayout.razor` 6 líneas + ~20 líneas de las 2 pruebas originales de `AuthorizationPipelineContractTests.cs` ≈ 112 líneas), el aviso de cabecera por sí solo queda razonablemente cerca de la banda estimada; el resto (≈155 líneas: la interfaz, el servicio, la página, el tercer `[Fact]` y las pruebas de `AccountConnectionsServiceTests`/`AccountConnectionsPageContractTests`) es íntegramente el arreglo de caché. **267 líneas totales quedan cómodamente por debajo del presupuesto de revisión de 400**, así que no hace falta partición ni `size:exception`. No se ha comprimido código ni omitido ninguna prueba para encajar en ningún número.

---

## Commits creados (2 de implementación, ninguno pusheado; más el commit de documentación que cierra este lote)

1. `0a1422a` `fix(application): invalidar la cache de conexiones tras desvincular en el mismo circuito` (el arreglo de caché: interfaz, servicio, página, y las 3 pruebas correspondientes — el primer intento de este commit quedó con el cuerpo del mensaje mal formado por un problema de escape de shell al invocarlo; se corrigió con un único `amend` **antes de que nada más dependiera de él**, sin tocar el árbol/diff, solo el texto del mensaje — no hubo ningún fallo de pre-commit hook de por medio)
2. `98aec7d` `feat(web): mostrar el aviso descartable de correo no verificado en la cabecera` (el componente, su montaje, y las 3 pruebas de `AuthorizationPipelineContractTests.cs`)
3. `1daa9c1` `docs(sdd): completar las tareas 6.1-6.3 del PR #6 de INC-49`
4. `5e2f479` `docs(sdd): corregir errata de idioma en apply-progress.md del PR #6`
5. `0b1a616` `fix(web): asegurar el tamano minimo de objetivo de 24px en el boton de descarte` — **hallazgo propio al redactar la auditoría WCAG de este informe** (no señalado por ninguna prueba automática: no hay comprobación de tamaño de objetivo en la suite). El botón de descarte medía 22×22px (icono de 14px + relleno de 4px), por debajo del mínimo de 24×24px CSS que exige WCAG 2.2 SC 2.5.8 (Target Size Minimum, nivel AA). Corregido ampliando el relleno a 6px y fijando `min-w-[24px] min-h-[24px]` explícitos. Verificado con la suite completa tras el cambio (1408/1408, sin regresión: ninguna prueba afirma clases CSS, per las reglas de este mismo repositorio).

Ninguno lleva atribución de IA (regla explícita de `AGENTS.md` §4). Worktree sobre `inc/vinculacion-cuentas-06-aviso-cabecera`, sin push ni PR: eso lo decide el orquestador. Los commits 1 y 2 son reversibles pero no independientes entre sí (ver "Frontera de reversión" arriba): el `fix` de caché debe revertirse junto con el `feat` que lo consume, no antes. El commit 5 es independiente de los demás (solo toca una clase CSS del botón de descarte).

---

## Desviaciones respecto al diseño / tareas

1. **Las 3 tareas 6.1-6.3 no se reinventaron.** Se implementaron tal como están escritas, con la única adición explícitamente autorizada por el orquestador (el arreglo de caché) documentada en su propia sección arriba, no mezclada en silencio con el resto.
2. **Ubicación de `<AccountEmailNotice />` en `MainLayout.razor`** (documentada en detalle en la tarea 6.3 arriba): hermano funcional de `<SessionGuard />`, pero no vecino literal en el DOM, por ser el único de los dos que renderiza contenido visible.
3. **`design.md` §D6 queda corregido por los hechos, no reabierto por decisión.** La sección "Caché de ámbito y su rebaba conocida" asumía que el peor caso era "un desfase de una sola navegación... en la dirección conservadora". El orquestador verificó que la premisa sobre la vida del ámbito era incorrecta (es todo el circuito, no una petición) y encargó explícitamente el arreglo; esta fase lo ejecuta como una corrección de una limitación mal caracterizada, no como una reapertura de ninguna decisión P2-P5 cerrada en la sección 3 del diseño.
4. **`ApplyLocalUnlink` retirado de `AccountConnections.razor`**, con la nota de paridad de comportamiento (incluida la pequeña divergencia de borde en `CanUnlink` que la relectura real corrige) documentada en la sección dedicada al arreglo de caché arriba.

## Tropiezos operativos

1. **Mensaje del primer commit (`fix`) mal formado por un problema de escape de shell** al pasar saltos de línea a través de `powershell.exe -Command` desde una invocación intermedia: el cuerpo del mensaje perdió sus saltos de línea. Corregido con un único `git commit --amend` (mismo árbol, solo el texto del mensaje) antes de crear el siguiente commit — no hubo pérdida de trabajo ni fallo de hook. A partir de ahí, los mensajes de commit se pasaron por heredoc directamente en Git Bash, como exige el protocolo de commits.
2. Ningún bloqueo por `MSB3027` (no había ningún proceso `Ludeka.Web` en ejecución antes de compilar ni ejecutar pruebas, verificado explícitamente antes de empezar).

---

## Estado

**3/3 tareas completas (6.1-6.3), más el arreglo de caché encargado por el orquestador, también completo y probado.** `dotnet test Ludeka.sln`: **1408/1408 en verde, 0 fallos, 0 omitidas.** Las 6 pruebas de `ExternalLoginServiceTests.cs:51-146` no se tocaron; `ICurrentUserService`/`CurrentUserContractTests` no se tocaron. Líneas autoradas reales: **267**, muy por debajo del presupuesto de 400 (la estimación original de `tasks.md`, 60-100, no incluía el arreglo de caché añadido en esta fase) — sin necesidad de partición ni `size:exception`. Worktree sobre `inc/vinculacion-cuentas-06-aviso-cabecera`, 5 commits sin pushear (2 `fix` — caché y tamaño de objetivo WCAG —, 1 `feat`, 2 `docs`), sin PR abierto: eso lo gestiona el orquestador. Próximo paso sugerido: `sdd-archive` sobre PR #6 (verificación es opcional), o continuar con `sdd-apply` para PR #7 (`inc/vinculacion-cuentas-07-reemplazo-correo`, depende de PR #1 y PR #2, ambos ya completos) — **recordatorio explícito del encargo: el PR #7 queda fuera del alcance de este lote y no se ha tocado.**
