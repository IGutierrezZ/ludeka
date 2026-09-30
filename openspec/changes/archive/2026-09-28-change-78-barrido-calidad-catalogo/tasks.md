# Checklist de Tareas: INC-78 — Barrido Completo de Calidad de Catálogo (~10.000 Juegos Promovidos)

## Fase 1: Repositorio y Cursor Paginado
- [x] 1.1 Declarar `GetGamesCursorPagedAsync(int afterBggId, int limit, CancellationToken ct)` y `GetTotalCatalogCountAsync(CancellationToken ct)` en `src/Ludeka.Application/Contracts/IGameRepository.cs`.
- [x] 1.2 Implementar `GetGamesCursorPagedAsync` y `GetTotalCatalogCountAsync` en `src/Ludeka.Infrastructure/Data/SqliteGameRepository.cs`.
- [x] 1.3 Crear pruebas unitarias para `GetGamesCursorPagedAsync` en `tests/Ludeka.UnitTests/Infrastructure/SqliteGameRepositoryTests.cs`.

## Fase 2: Lógica de Barrido Integral en `BggMassIngestionService`
- [x] 2.1 Crear `BggQualitySweepBatchResultDto` en `src/Ludeka.Application/Features/Bgg/`.
- [x] 2.2 Declarar `SweepCatalogQualityBatchAsync` y `RunScheduledSweepCatalogQualityBatchAsync` en `src/Ludeka.Application/Contracts/IBggMassIngestionService.cs`.
- [x] 2.3 Extraer y refactorizar la lógica común de enriquecimiento individual (`EnrichSingleGameQualityAsync`) en `src/Ludeka.Application/Features/Bgg/BggMassIngestionService.cs` para dar servicio tanto a `ExecuteBackfillCatalogQualityBatchAsync` como al nuevo barrido.
- [x] 2.4 Implementar `SweepCatalogQualityBatchAsync` y `RunScheduledSweepCatalogQualityBatchAsync` en `BggMassIngestionService.cs`.
- [x] 2.5 Crear pruebas unitarias en `tests/Ludeka.UnitTests/Application/BggMassIngestionSweepTests.cs`.

## Fase 3: Cloud Run Jobs y UI Administrativa
- [x] 3.1 Actualizar `BackfillQualityJobRunner.cs` en `src/Ludeka.Jobs/Runners/` para iterar mediante el cursor hasta completar el catálogo.
- [x] 3.2 Añadir botón y panel de control de barrido continuo en `src/Ludeka.Web/Components/Pages/CatalogQueueAdmin.razor` con telemetría en vivo (evaluados, actualizados, omitidos).
- [x] 3.3 Crear pruebas unitarias para el runner y la integración de jobs.

## Fase 4: Verificación Integral y Archivo
- [x] 4.1 Ejecutar suite completa `dotnet test` y comprobar 100% de tests en verde sin regresiones (2.037 tests: 2.027 unitarios + 10 integración).
- [x] 4.2 Documentar en especificación viva `docs/specs/sistema/` y sincronizar roadmaps.
