# Tareas — INC-53: Ingesta Masiva Autónoma de Catálogo BGG (~8.000 Juegos) sin Manipulación Manual

> **Fase:** `sdd-tasks` · **Fecha:** 2026-09-21  
> **Worktree:** `C:\repos\ludeka-wt\ingesta-masiva-autonoma-bgg`, rama `inc/ingesta-masiva-autonoma-bgg`, base `main` en `6d3b510`  
> **Entradas:** `design.md`, `specs/autonomous-bgg-catalog-ingestion/spec.md`, `proposal.md`, `explore.md`

---

## Review Workload Forecast

| Campo | Valor |
|---|---|
| Líneas estimadas | ~600–750 (código + pruebas + interfaz + jobs) |
| Riesgo de presupuesto de 400 líneas | **Medio** (se divide en 3 PRs para garantizar cumplimiento estricto < 400 líneas por PR) |
| PRs encadenados recomendados | Sí |
| Partición sugerida | PR #1 (Aplicación, Streaming y Pruebas) ➔ PR #2 (UI Blazor y Cloud Run Jobs) ➔ PR #3 (Verificación, Especificación Viva y Archivado) |
| Estrategia de entrega | stacked-to-main |

```text
Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: Medium (mitigado por partición en 3 PRs)
```

### Suggested Work Units

| Unit (PR) | Objetivo | Comando de prueba enfocado (iteración local) | Límite de reversión |
|---|---|---|---|
| **#1** | Opciones, robustez de streaming en `BggDumpParser`, descarga autónoma con fallback en `BggMassIngestionService` y suite de pruebas unitarias | `dotnet test tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj --filter "FullyQualifiedName~BggMassIngestionAutonomousDownloadTests|FullyQualifiedName~BggDumpParserTests"` | Revertir PR 1: código contenido en `Ludeka.Application` y pruebas; cero impacto en UI o jobs |
| **#2** | Botón interactivo en `CatalogQueueAdmin.razor`, runner `seed-staging` en `Ludeka.Jobs` y auto-siembra en `NightlyCatalogingService` | `dotnet test tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj --filter "FullyQualifiedName~NightlyCatalogingServiceTests|FullyQualifiedName~SeedStagingJobRunnerTests"` | Revertir PR 2: elimina los disparadores interactivos y el nuevo subcomando |
| **#3** | Verificación integral, volcado a especificación viva (`docs/specs/sistema/`) y archivado SDD | `dotnet test Ludeka.sln` | Revertir PR 3: solo documentación y metadatos de roadmap |

---

## PR #1: Aplicación, Robustez de Streaming y Pruebas Unitarias

- [x] **1.1** Actualizar `src/Ludeka.Application/DTOs/BggMassIngestionDtos.cs`:
  - Añadir en `BggMassIngestionOptions` las propiedades `RanksDumpUrlPattern` (plantilla URL a Fastly CDN / GitHub Raw) y `MaxFallbackDays` (valor por defecto 5).
- [x] **1.2** Actualizar `src/Ludeka.Application/Contracts/IBggMassIngestionService.cs`:
  - Incorporar la firma interactiva `Task<int> DownloadAndIngestLatestRanksAsync(int? minUsersRated = null, CancellationToken ct = default)`.
  - Incorporar la firma de sistema `Task<int> RunScheduledDownloadAndIngestLatestRanksAsync(int? minUsersRated = null, CancellationToken ct = default)`.
- [x] **1.3** Modificar `src/Ludeka.Application/Features/Bgg/BggDumpParser.cs`:
  - Ajustar la comprobación de cabecera para streams continuos de red: si `inputStream.CanSeek` es falso, no intentar comprobaciones de longitud ni búsqueda, tratando el contenido como UTF-8 estándar.
- [x] **1.4** Modificar `src/Ludeka.Application/Features/Bgg/BggMassIngestionService.cs`:
  - Implementar la resolución resiliente de URL con bucle de reintento de fechas desde `DateTime.UtcNow.Date` hacia atrás hasta `MaxFallbackDays`.
  - Implementar streaming con `HttpClient.SendAsync(..., HttpCompletionOption.ResponseHeadersRead, ct)`.
  - Implementar procesamiento de volcado conectando con `BggDumpParser.ParseRanksDumpAsync` e inserción en lotes de 100 con `_stagingRepo.UpsertBatchAsync`.
  - Soportar `Simulate = true` para ejecución con datos sintéticos deterministas.
  - Implementar la guarda de permisos `RequirePermissionAsync` en `DownloadAndIngestLatestRanksAsync`.
- [x] **1.5** Crear `tests/Ludeka.UnitTests/Application/BggMassIngestionAutonomousDownloadTests.cs`:
  - Prueba de descarga exitosa con fecha de hoy.
  - Prueba de fallback resiliente ante 404 inicial.
  - Prueba de agotamiento de fallback lanzando `InvalidOperationException`.
  - Prueba de streaming con `Stream` no buscable (`CanSeek == false`).
  - Prueba de filtrado de votos `usersrated >= 30`.
  - Prueba de exigencia de permisos de moderador en la llamada interactiva.
  - Prueba de ejecución en modo simulación (`Simulate = true`).
- [x] **1.6** Verificar suite completa con `dotnet test Ludeka.sln`.

---

## PR #2: UI Blazor y Cloud Run Jobs (`Ludeka.Jobs`)

- [x] **2.1** Modificar `src/Ludeka.Web/Components/Pages/CatalogQueueAdmin.razor`:
  - En la sección "Ingesta Masiva BGG & Staging (~8.000 títulos)", añadir el botón de acción: «Descargar y Poblar Catálogo BGG (~8.000 títulos)».
  - Añadir estados reactivos (`_isSeedingStaging`, mensaje de feedback tras finalización).
  - Gestionar deshabilitación mutua durante drenaje o sembrado.
- [x] **2.2** Actualizar `src/Ludeka.Jobs/JobNames.cs`:
  - Añadir `public const string SeedStaging = "seed-staging";` y agregar a la lista `All`.
- [x] **2.3** Crear `src/Ludeka.Jobs/Runners/SeedStagingJobRunner.cs`:
  - Implementar `IJobRunner` para `JobNames.SeedStaging`.
  - Invocar `RunScheduledDownloadAndIngestLatestRanksAsync` coordinado con `IJobExecutionCoordinator`.
- [x] **2.4** Modificar `src/Ludeka.Jobs/JobRunnerServiceCollectionExtensions.cs` y `JobSelectionResolver.cs`:
  - Registrar `SeedStagingJobRunner` en el contenedor de dependencias y resolución de comandos.
- [x] **2.5** Modificar `src/Ludeka.Application/Features/Bgg/NightlyCatalogingService.cs`:
  - En la Fase 3, verificar si `BggCatalogStaging` tiene 0 elementos (`metrics.TotalInStaging == 0`); en caso afirmativo, disparar la auto-siembra inicial autónoma antes de ejecutar el drenaje.
- [x] **2.6** Añadir pruebas unitarias en `tests/Ludeka.UnitTests`:
  - Prueba unitaria de `SeedStagingJobRunner`.
  - Prueba de auto-siembra condicional en `NightlyCatalogingServiceTests`.
- [x] **2.7** Verificar suite completa con `dotnet test Ludeka.sln`.

---

## PR #3: Verificación Integral, Documentación y Cierre SDD

- [x] **3.1** Ejecutar verificación completa de requerimientos y generar `openspec/changes/2026-09-21-change-53-ingesta-masiva-autonoma-bgg/verify-report.md`.
- [x] **3.2** Actualizar especificación viva del sistema en `docs/specs/sistema/`:
  - Actualizar `27-ingesta-masiva-bgg-galeria-geekdo-ia-lotes.md` documentando la descarga remota en streaming, el botón de administración y la auto-siembra.
  - Actualizar `34-trabajos-en-segundo-plano-cloud-run.md` documentando el nuevo subcomando `seed-staging` y el comportamiento de auto-siembra de `nightly-cataloging`.
  - Actualizar el índice maestro `docs/specs/sistema/README.md` con el nuevo recuento de pruebas y enlaces.
- [x] **3.3** Generar `openspec/changes/2026-09-21-change-53-ingesta-masiva-autonoma-bgg/archive-report.md`.
- [x] **3.4** Trasladar `docs/increments/inc-53-ingesta-masiva-autonoma-bgg.md` a `docs/increments/archive/inc-53-ingesta-masiva-autonoma-bgg.md`.
- [x] **3.5** Actualizar `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md` marcando INC-53 como `✅ Archivado`.
