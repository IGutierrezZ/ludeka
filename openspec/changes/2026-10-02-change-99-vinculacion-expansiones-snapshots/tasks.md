# Tareas de Implementación: Reclasificación y Vinculación Masiva de Expansiones (INC-99)

## Fase 1: Dominio y Contratos
- [x] 1.1 Añadir `BggExpansionReconciliationResultDto` en `Ludeka.Application.DTOs`.
- [x] 1.2 Añadir `GetSnapshotsAfterBggIdAsync` en `IBggRawSnapshotRepository` y actualizar `SqliteBggRawSnapshotRepository`.
- [x] 1.3 Añadir `ReconcileAndLinkExpansionsFromSnapshotsAsync` y `RunScheduledReconcileAndLinkExpansionsFromSnapshotsAsync` en `IBggRawSnapshotSyncService`.
- [x] 1.4 Añadir campo `UnpromotedFailedCount` en `BggStagingMetricsDto` y actualizar cálculo en `SqliteBggCatalogStagingRepository.GetMetricsAsync`.

## Fase 2: Implementación de Lógica y Reconciliador
- [x] 2.1 Implementar `IsExpansionTypeFromJson` en `BggRawSnapshotSyncService` (vía `BggRawSnapshotParser`).
- [x] 2.2 Implementar `ReconcileAndLinkExpansionsFromSnapshotsAsync` en `BggRawSnapshotSyncService` con procesamiento cursor-based por lotes, reclasificación de `GameType.Expansion` y vinculación bidireccional de `BaseGameId`.
- [x] 2.3 Modificar `BggMassIngestionService.PromoteReadyToCatalogBatchAsync` para consultar el snapshot satélite y asignar `type: GameType.Expansion` y `baseGameId` en promociones futuras.

## Fase 3: CLI Desatendida en Ludeka.Jobs
- [x] 3.1 Registrar `JobNames.BggReconcileExpansions` en `JobNames.cs`.
- [x] 3.2 Implementar `BggReconcileExpansionsJobRunner` en `Ludeka.Jobs.Runners` con arrendamiento seguro de ventana temporal.
- [x] 3.3 Cablear el runner en `src/Ludeka.Jobs/Program.cs` y registrar en `JobRunnerServiceCollectionExtensions.cs`.

## Fase 4: Interfaz de Administración en Blazor Web App
- [x] 4.1 Añadir botón interactivo «Reconciliar Expansiones desde Snapshots» en `/admin/cola-catalogacion` (`CatalogQueueAdmin.razor`) con control reactivo de estado y telemetría de reclasificación y vinculación.
- [x] 4.2 Actualizar la tarjeta de Staging en `CatalogQueueAdmin.razor` para desglosar «Fallidos sin promover» de los fallos accesorios en juegos promovidos.

## Fase 5: Pruebas Unitarias y Verificación Integral
- [x] 5.1 Crear suite `BggExpansionReconciliationTests.cs` en `tests/Ludeka.UnitTests/Application/`.
- [x] 5.2 Añadir pruebas de promoción con tipo de expansión en `BggMassIngestionSweepTests.cs`.
- [x] 5.3 Añadir pruebas de métricas de fallidos en `SqliteBggCatalogStagingRepositoryTests.cs`.
- [x] 5.4 Ejecutar toda la suite de pruebas automáticas del repositorio garantizando el 100% de tests en verde (2.281 pruebas superadas).
