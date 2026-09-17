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
