# Apply Progress: change-46-autenticacion-real (INC-46) — Fase F0 (PR 1)

> Fase SDD `sdd-apply`, slice **PR-1 / Fase F0** (tareas 0.1–0.6). Store: openspec (este archivo + `tasks.md`).
> Worktree: `C:\repos\ludeka-wt\autenticacion-real`, rama `inc/autenticacion-real`. Base: `e3b9951` (encima de `04a89fe`).
> Modo: **TDD estricto**. Runner contractual: `dotnet test Ludeka.sln`. Estrategia de cadena: `stacked-to-main` (PR 1 de 8).

## Estado F0: COMPLETADO ✅

| Tarea | Estado | Ciclo TDD | Commit |
|---|---|---|---|
| 0.1 Normalizar encabezados de `editorial-role-management` (3 + 5) | ✅ | Estructural (solo encabezados, sin lógica) | `ff6eaee` |
| 0.2 Normalizar encabezados de `media-moderation-panel` (4 + 6) | ✅ | Estructural (solo encabezados, sin lógica) | `ff6eaee` |
| 0.3 `.gitattributes` `*.md text eol=lf` + re-materialización LF | ✅ | Estructural (atributos y checkout) | `ff6eaee` |
| 0.4 RED de `GranularPermissionsTests` (banderas nuevas en `All` y granularidad intacta) | ✅ | ROJO ejecutado (255 ≠ 1023) + ROJO por compilación (CS0117) | `c18f652` |
| 0.5 GREEN de `ModeratorPermission` (`1 << 8`, `1 << 9`, `All` = 1023) | ✅ | VERDE 21/21 focal | `c18f652` |
| 0.6 `AuditAction.LinkedFounderIdentity` (apéndice sin renumerar) | ✅ | ROJO por compilación (CS0117) → VERDE 30/30 focal | `ee582cb` |

### Commits (rama `inc/autenticacion-real`)

| Sha | Mensaje |
|---|---|
| `ff6eaee` | `fix(sdd): normalizar encabezados de specs canónicas y fijar Markdown a LF` |
| `c18f652` | `feat(core): añadir CanManageUsers y CanViewAuditLog a ModeratorPermission` |
| `ee582cb` | `feat(core): añadir AuditAction.LinkedFounderIdentity` |
| (docs) | `docs(sdd): registrar el progreso de apply de la fase F0 de INC-46` |

## TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 0.1–0.3 | — (sin producción de código) | Artefacto | N/A | N/A | `git check-attr` → `lf`; 0 archivos `*.md` con `w/crlf` | ➖ Estructural: conteo exacto 3+5 y 4+6 y ausencia de encabezados antiguos | ➖ No needed |
| 0.4 + 0.5 | `tests/Ludeka.UnitTests/Application/GranularPermissionsTests.cs` | Unit | ✅ 7/7 pre-cambio | ✅ Ejecutado: `Expected: 1023 / Actual: 255`; además CS0117 de `CanManageUsers`/`CanViewAuditLog` | ✅ 21/21 focal | ✅ Teoría sobre las 10 banderas, disjunción de los dos flags nuevos, denegación granular en `PublisherService` con solo los flags nuevos | ✅ Documentación XML de los dos flags y comentario de `All` actualizado a 1023 |
| 0.6 | `tests/Ludeka.UnitTests/Domain/UserManagementDomainTests.cs` | Unit | ✅ 8/8 pre-cambio | ✅ CS0117: `AuditAction` no contiene `LinkedFounderIdentity` | ✅ 30/30 focal (dominio + permisos) | ✅ Ordinales ancla `Created=0`, `Published=6`, `LinkedFounderIdentity=7` (contrato persistido) | ➖ No needed (enum puro con ordinal apéndice) |

## Work Unit Evidence

| Unidad / commit | Prueba focal y resultado exacto | Arnés de ejecución y resultado exacto | Límite de rollback |
|---|---|---|---|
| U0-Encabezados / `ff6eaee` | `gentle-ai sdd-archive-compose --canonical … --delta … --output -` → **exit 0** en ambas specs | N/A: solo specs canónicas y `.gitattributes`; sin código de aplicación | Revertir el commit: encabezados y atributos vuelven al estado de `e3b9951` |
| U0-Banderas / `c18f652` | `dotnet test Ludeka.sln --filter FullyQualifiedName~GranularPermissionsTests` → **21/21** (7 base + 14 nuevas) | N/A: enum de dominio sin frontera de ejecución propia; la denegación granular se ejerce vía `PublisherService` en la suite | Revertir el commit: `ModeratorPermission` vuelve a 8 banderas/255 y los tests nuevos desaparecen con él |
| U0-Auditoría / `ee582cb` | `dotnet test Ludeka.sln --filter "FullyQualifiedName~UserManagementDomainTests\|FullyQualifiedName~GranularPermissionsTests"` → **30/30** | N/A: valor de enum; la emisión real de la acción llega en F1/F2 | Revertir el commit: el enum vuelve a terminar en `Published` |

## Verificación observada (registro)

| Comando / comprobación | Resultado observado |
|---|---|
| `dotnet test Ludeka.sln --configuration Release` | **1020 correctas, 0 con error, 0 omitidas** (línea base `04a89fe`: 1005 → +15 casos nuevos; avisos preexistentes CS8629/CS8604/CS8625/xUnit2013 sin cambios) |
| `git check-attr text eol -- openspec/specs/editorial-role-management/spec.md` | `text: set`, `eol: lf` |
| `git check-attr text eol -- openspec/specs/media-moderation-panel/spec.md` | `text: set`, `eol: lf` |
| `git ls-files --eol -- "*.md"` | **0** archivos con `w/crlf` (562 Markdown en LF en árbol e índice) |
| Conteo de encabezados en `editorial-role-management/spec.md` | `### Requirement:` = **3**, `#### Scenario:` = **5**, `Requerimiento:` = 0, `Escenario:` = 0 (coincide con el conteo previo) |
| Conteo de encabezados en `media-moderation-panel/spec.md` | `### Requirement:` = **4**, `#### Scenario:` = **6**, `Requerimiento:` = 0, `Escenario:` = 0 (coincide con el conteo previo) |
| `gentle-ai sdd-archive-compose --canonical openspec/specs/editorial-role-management/spec.md --delta openspec/changes/change-46-autenticacion-real/specs/editorial-role-management/spec.md --output -` | **exit 0**; compone la canónica completa en stdout sin tocar el árbol (`git status`: solo `tasks.md`) |
| `gentle-ai sdd-archive-compose --canonical openspec/specs/media-moderation-panel/spec.md --delta openspec/changes/change-46-autenticacion-real/specs/media-moderation-panel/spec.md --output -` | **exit 0**; ídem |

## Desviaciones y hallazgos

1. **`design.md` §8 vs. decisión del desglose (CRLF)**: el diseño pedía «preservando los CRLF actuales», pero la decisión aprobada en `tasks.md` (línea 22) fija `.gitattributes` con `*.md text eol=lf` y árbol en LF, porque el compositor falla con CRLF. Se implementó la decisión del desglose; el diseño queda desactualizado en ese paréntesis.
2. **`git checkout-index -f -a` no re-materializa**: git considera los archivos «sin cambios» por estadísticas y omite la escritura, de modo que el árbol siguió en CRLF aunque el atributo ya aplicaba. Se re-materializó eliminando los 562 Markdown y restaurándolos con `git checkout -- "*.md"` (los archivos ausentes se escriben siempre, aplicando `eol=lf`). Objetivo de la tarea 0.3 cumplido y verificado.
3. **Aviso cosmético de `.gitattributes`**: `git add` avisa que `.gitattributes` pasará a CRLF (no está cubierto por su propia regla). Se verificó empíricamente que git tolera CRLF al parsear los atributos (`git check-attr` devuelve `text: set / eol: lf` con el archivo en CRLF), por lo que no se amplió el contenido indicado.
4. **Etiqueta de auditoría pendiente**: `AuditService.GetActionDisplayName` mantiene el texto genérico «Operación» para `LinkedFounderIdentity`; la tarea 0.6 solo pedía el valor del enum y la acción aún no se emite (llega en F1/F2). Sin impacto en la suite.
5. **Sin efectos de datos**: no se ejecutaron migraciones EF, no se tocó base de datos ni configuración de despliegue; el slice es reversible con `git revert` de sus tres commits.

## Presupuesto y frontera de PR

- **Líneas cambiadas del slice** (`git diff --shortstat e3b9951..HEAD`, sin contar este artefacto): **104 inserciones + 20 eliminaciones = 124**, dentro del presupuesto de 400.
- **Modo**: chained/stacked PR slice (`stacked-to-main`), PR 1 de 8. Frontera: de `04a89fe`/`e3b9951` a las banderas nuevas y los encabezados normalizados.
- **Fuera de alcance (intacto)**: F1 (entidad `ExternalLogin`), F2 (esquemas, handler, `[Authorize]`, `Program.cs`), F3 (retirada de `DefaultCurrentUserService` y conmutadores), F4 (anonimia y smoke), F5 (docs y archive). No se ejecutó `sdd-archive` ni `git push`.

## Estado acumulado

- **6/6 tareas de F0 completadas**; F1–F5 sin tocar.
- Listo para la verificación independiente de `sdd-verify` sobre el slice F0 (o para el siguiente apply de F1, según decisión del orquestador).
