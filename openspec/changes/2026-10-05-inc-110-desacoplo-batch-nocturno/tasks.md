# Tareas de Trabajo: INC-110

- [x] **Unidad 1: Auto-recuperación de bloqueos y saneamiento de bitácora**
  - [x] 1.1: Añadir `RecoverStaleProcessingToPendingAsync` en `IPendingBggImportRepository` y `SqlitePendingBggImportRepository`.
  - [x] 1.2: Añadir `FailStaleRunningLogsAsync` en `INightlyCatalogingLogRepository` y `SqliteNightlyCatalogingLogRepository`.
  - [x] 1.3: Incorporar en `NightlyCatalogingService.RunScheduledCatalogingAsync` las llamadas pre-vuelo de recuperación.
  - [x] 1.4: Tests unitarios TDD para verificar la recuperación de títulos en `Processing` y la marcación de logs fallidos por timeout.

- [x] **Unidad 2: Desacoplo a segundo plano y control de cupo en la UI**
  - [x] 2.1: Refactorizar `ExecuteNightlyBatchAsync` en `CatalogQueueAdmin.razor` para lanzar la ejecución en `Task.Run` con `ScopeFactory` y `CancellationTokenSource`.
  - [x] 2.2: Añadir feedback reactivo en el panel con mensaje de progreso, estado activo y botón de detención segura.
  - [x] 2.3: Añadir selector de cupo personalizado (ej. 40, 50, 100, 200, 400 títulos) junto al botón de disparo.

- [x] **Unidad 3: Verificación, suite completa y cierre SDD**
  - [x] 3.1: Ejecutar la suite completa de pruebas unitarias (`dotnet test tests/Ludeka.UnitTests`).
  - [x] 3.2: Actualizar `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md`.
  - [ ] 3.3: Abrir PR mediante `scripts/sdd-worktree.ps1 pr desacoplo-batch-nocturno`.
