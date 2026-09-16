# Apply Progress: change-46-autenticacion-real (INC-46) — Fases F0 y F1

> Fase SDD `sdd-apply`. Store: openspec (este archivo + `tasks.md`).
> Worktree: `C:\repos\ludeka-wt\autenticacion-real`.
> · Slice **PR-1 / Fase F0**: rama `inc/autenticacion-real`, base `e3b9951` (encima de `04a89fe`), mergeado a `main` en `3ec23d5`.
> · Slice **PR-2 / Fase F1**: rama `inc/autenticacion-real-f1`, base `3ec23d5` (= `origin/main`).
> Modo: **TDD estricto**. Runner contractual: `dotnet test Ludeka.sln`. Estrategia de cadena: `stacked-to-main` (PR 2 de 8).

## Estado F1: COMPLETADO ✅

| Tarea | Estado | Ciclo TDD | Commit |
|---|---|---|---|
| 1.1 RED de `ExternalLoginTests` (constructor rechaza `UserId`/`Provider`/`ProviderKey` vacíos; expone `LinkedAt`) | ✅ | ROJO por compilación (CS0246) | `b4ab43a` |
| 1.2 GREEN de `ExternalLogin` (`Id` Guid, `UserId`, `Provider`, `ProviderKey`, `ProviderEmail?`, `LinkedAt`) | ✅ | VERDE 14/14 focal | `b4ab43a` |
| 1.3 RED de `ExternalLoginPersistenceTests` (índice único, FK `Cascade`, repositorio y reconciliador SQLite) | ✅ | ROJO por compilación (CS0246 de `IExternalLoginRepository`/`ExternalLoginRepository`) | `21eb78a` |
| 1.4 GREEN de `LudekaDbContext` (`DbSet<ExternalLogin>`, índice único y FK `Cascade` a `AppUsers`) | ✅ | VERDE 7/7 focal | `21eb78a` |
| 1.5 GREEN de `SqliteSchemaMigrator` (tabla `ExternalLogins` + índice único + índice de FK) | ✅ | VERDE 7/7 focal (caso `OnLegacyDatabase`) | `21eb78a` |
| 1.6 GREEN de `IExternalLoginRepository` y `ExternalLoginRepository` | ✅ | VERDE 7/7 focal | `21eb78a` |
| 1.7 Migración `AddExternalLogins` con `Database__Provider=PostgreSql` y tipos Npgsql en el snapshot | ✅ | Estructural (artefacto generado por EF; verificado con `migrations script`) | `36cb30f` |
| 1.8 `dotnet test Ludeka.sln` verde | ✅ | VERDE 1041/1041 en Release | (docs) |

### Commits del slice F1 (rama `inc/autenticacion-real-f1`, base `3ec23d5`)

| Sha | Mensaje |
|---|---|
| `b4ab43a` | `feat(core): añadir la entidad ExternalLogin con validación de identidad externa` |
| `21eb78a` | `feat(infrastructure): persistir la identidad externa con índice único y FK en cascada` |
| `36cb30f` | `feat(infrastructure): generar la migración EF AddExternalLogins para PostgreSQL` |
| (docs) | `docs(sdd): registrar el progreso de apply de la fase F1 de INC-46` |

### Commits del slice F0 (rama histórica `inc/autenticacion-real`)

| Sha | Mensaje |
|---|---|
| `ff6eaee` | `fix(sdd): normalizar encabezados de specs canónicas y fijar Markdown a LF` |
| `c18f652` | `feat(core): añadir CanManageUsers y CanViewAuditLog a ModeratorPermission` |
| `ee582cb` | `feat(core): añadir AuditAction.LinkedFounderIdentity` |

## TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 0.1–0.3 (F0) | — (sin producción de código) | Artefacto | N/A | N/A | `git check-attr` → `lf`; 0 archivos `*.md` con `w/crlf` | ➖ Estructural: conteo exacto 3+5 y 4+6 | ➖ No needed |
| 0.4–0.5 (F0) | `Application/GranularPermissionsTests.cs` | Unit | ✅ 7/7 | ✅ `Expected: 1023 / Actual: 255` + CS0117 | ✅ 21/21 focal | ✅ Teoría de 10 banderas y disjunción | ✅ Docs XML |
| 0.6 (F0) | `Domain/UserManagementDomainTests.cs` | Unit | ✅ 8/8 | ✅ CS0117 `LinkedFounderIdentity` | ✅ 30/30 focal | ✅ Ordinales ancla del enum | ➖ No needed |
| 1.1 + 1.2 | `Domain/ExternalLoginTests.cs` | Unit | N/A (entidad nueva) | ✅ CS0246: `ExternalLogin` no existe | ✅ 14/14 focal (`--filter FullyQualifiedName~ExternalLoginTests`) | ✅ 3 teorías de validación (null/vacío/espacios en cada campo), `LinkedAt` explícito y por defecto, `ProviderEmail` nulo, normalización por trim e Ids distintos | ➖ No needed: entidad inmutable y sin ramas ocultas |
| 1.3 + 1.4 + 1.5 + 1.6 | `Infrastructure/ExternalLoginPersistenceTests.cs` | Integration (SQLite `:memory:`) | N/A (archivos existentes sin tests propios; suite base 1020/1020 ejecutada antes) | ✅ CS0246: `IExternalLoginRepository`, `ExternalLoginRepository` y `ExternalLogins` no existen | ✅ 7/7 focal (`--filter FullyQualifiedName~ExternalLoginPersistenceTests`) | ✅ Modelo (índice único + FK `Cascade`), lectura existente/desconocida, duplicado rechazado por `DbUpdateException`, borrado en cascada real y tabla creada por el reconciliador con 2 índices | ✅ `AsNoTracking` en la relectura del repositorio; validación en un solo punto del dominio |

## Work Unit Evidence

| Unidad / commit | Prueba focal y resultado exacto | Arnés de ejecución y resultado exacto | Límite de rollback |
|---|---|---|---|
| U1-Dominio / `b4ab43a` | `dotnet test Ludeka.sln --filter FullyQualifiedName~ExternalLoginTests` → **14/14** | N/A: entidad de dominio sin frontera de ejecución propia | Revertir el commit: la entidad y su prueba desaparecen; nada más la referencia todavía |
| U1-Persistencia / `21eb78a` | `dotnet test Ludeka.sln --filter FullyQualifiedName~ExternalLoginPersistenceTests` → **7/7** | SQLite `:memory:` real: `EnsureCreatedAsync` + `SqliteSchemaMigrator.EnsureSchemaUpToDateAsync` sobre esquema legado; arranque local = `EnsureCreatedAsync` + reconciliador (`Program.cs:328-338`), cubierto por el caso `OnLegacyDatabase` | Revertir el commit: `DbSet`, config, repositorio y sección 22 del reconciliador vuelven atrás |
| U1-Migración / `36cb30f` | `dotnet ef migrations script … -o tmp` → SQL PostgreSQL correcto | `dotnet ef migrations list` con `Database__Provider=PostgreSql` lista las 3 migraciones | Revertir el commit: se eliminan migración y snapshot; la tabla nueva no toca datos existentes |

## Verificación observada (registro)

| Comando / comprobación | Resultado observado |
|---|---|
| `dotnet test Ludeka.sln --configuration Release` | **1041 correctas, 0 con error, 0 omitidas** (línea base en `3ec23d5`: 1020 → +21 casos nuevos; avisos preexistentes CS8629/CS8604/CS8625/xUnit2013 sin cambios) |
| `dotnet ef migrations list --project src/Ludeka.Infrastructure --startup-project src/Ludeka.Web` (con `Database__Provider=PostgreSql`) | Lista las **3** migraciones: `20260914001323_InitialSupabasePostgres`, `20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages`, `20260915164921_AddExternalLogins`. Sin PostgreSQL accesible avisa «Pending status not shown» y continúa (no se requiere base de datos) |
| `dotnet ef migrations script 20260915100112_… 20260915164921_… --no-build -o %TEMP%\ludeka-inc46-f1.sql` | SQL PostgreSQL: `"Id" uuid NOT NULL`, `"UserId" character varying(100) NOT NULL`, `"ProviderKey" character varying(255) NOT NULL`, `"LinkedAt" timestamp with time zone NOT NULL`, `FOREIGN KEY … ON DELETE CASCADE`, `CREATE UNIQUE INDEX "IX_ExternalLogins_Provider_ProviderKey"`. Sin tipos SQLite |
| Índice único `(Provider, ProviderKey)` | Presente en `LudekaDbContext.OnModelCreating` (`IsUnique()`), en la migración (`unique: true`), en el reconciliador SQLite y verificado por metadata del modelo + rechazo real de duplicados en SQLite |
| FK `Cascade` a `AppUsers` | Presente en el modelo (`DeleteBehavior.Cascade`), en la migración (`ReferentialAction.Cascade`) y en el reconciliador; el borrado del principal elimina la fila dependiente en SQLite `:memory:` |
| `SqliteSchemaMigrator` y arranque SQLite | Caso `SqliteSchemaMigrator_ShouldCreateExternalLoginsTable_OnLegacyDatabase` en verde: crea `ExternalLogins` y sus 2 índices sobre esquema legado. Filtro de esquema (`SqliteSchemaMigratorTests` + `ExternalLogin*` + `DatabaseProviderTests`) → **40/40**; suite completa verde con el arranque SQLite (`EnsureCreatedAsync` + reconciliador) |
| `Up()` de la migración | Solo `CreateTable` + 2 `CreateIndex`; **sin `DROP`** ni alteraciones destructivas. `Down()` elimina la tabla nueva (reversión limpia) |

## Desviaciones y hallazgos

1. **Nombre del repositorio**: se usa `ExternalLoginRepository` (tarea 1.6) en `src/Ludeka.Infrastructure/Data/`, aunque el resto de repositorios del directorio llevan prefijo `Sqlite`. Se sigue el nombre literal de la tarea; el prefijo ya resulta engañoso porque el mismo repositorio sirve a PostgreSQL y SQLite.
2. **Sin registro en DI**: `IExternalLoginRepository` no se registra en `Program.cs` en este slice; el registro llega con la composición de servicios de F2 (tarea 2.5/2.9). Ningún consumidor lo requiere todavía y el prompt prohíbe tocar `Program.cs` en F1.
3. **`AsNoTracking()` en la lectura del repositorio**: añadido por coherencia con la decisión de revocación del diseño (§2, `AsNoTracking` en lecturas de identidad) y para que la prueba de ida y vuelta materialice la fila desde la base de datos.
4. **`Dotnet ef migrations list` sin base de datos**: la CLI intenta conectar y, al no haber PostgreSQL, informa del fallo y sigue listando; el listado de las 3 migraciones es correcto. No se creó ninguna base de datos real.
5. **Generado vs. autorado**: la migración, su `.Designer.cs` (2336 líneas) y `LudekaDbContextModelSnapshot.cs` (47) son artefactos generados por EF y quedan fuera del conteo de riesgo según `tasks.md`.
6. **Fuera de alcance (intacto)**: F2 (esquemas externos, cookie, `PermissionAuthorizationHandler`, políticas, `[Authorize]`, `Program.cs`), F3 (retirada de `DefaultCurrentUserService` y conmutadores), F4 (anonimia y smoke) y F5 (docs y archive). No se ejecutó `sdd-archive`, no se hizo `git push` ni se abrió PR.

## Presupuesto y frontera de PR

- **Líneas autoradas del slice F1** (sin `.Designer.cs` ni snapshot, generados por EF): **404** → dentro del presupuesto de 450.
  - Producción: 141 (entidad 43, contrato 28, repositorio 33, `DbContext` +16, reconciliador +21).
  - Pruebas: 318 (`ExternalLoginTests` 108, `ExternalLoginPersistenceTests` 210).
  - Contando además el `.cs` de la migración (55, también generado por EF): 459.
  - `git diff --shortstat 3ec23d5..HEAD`: 2897 inserciones totales, de las que 2383 son generadas por EF.
- **Modo**: chained/stacked PR slice (`stacked-to-main`), PR 2 de 8. Frontera: de `3ec23d5` a la tabla `ExternalLogins` con migración PostgreSQL, persistencia SQLite reconciliada y repositorio listo para que F2 lo consuma.

## Estado acumulado

- **F0: 6/6 tareas completadas** (mergeadas en `3ec23d5`).
- **F1: 8/8 tareas completadas**; F2–F5 sin tocar.
- Listo para la verificación independiente de `sdd-verify` sobre el slice F1.
