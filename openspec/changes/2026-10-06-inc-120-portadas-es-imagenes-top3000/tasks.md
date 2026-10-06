# Tareas de Implementación: INC-120 Extracción de Portadas en Español y Sincronización Top 3.000

## Fase 1: Dominio y Parser Analítico de Versiones (BGG Snapshots)
- [x] **Tarea 1.1:** Actualizar `BggSpanishVersionInfoDto` en `src/Ludeka.Application/DTOs/BggVersionDtos.cs` con `CoverImageUrl` y `ThumbnailUrl`.
- [x] **Tarea 1.2:** Modificar `BggRawSnapshotParser.cs` para extraer `image` y `thumbnail` en `ParseVersionInfo`, normalizar URLs (`//` -> `https:`) y soportar fusión de candidatas de portada en español.
- [x] **Tarea 1.3:** Agregar pruebas unitarias en `tests/Ludeka.UnitTests/Bgg/BggRawSnapshotParserVersionsTests.cs` validando la extracción y normalización de portadas de versiones en español.

## Fase 2: Promoción de Portada en Español en el Barrido de Catálogo
- [x] **Tarea 2.1:** Modificar `BggRawSnapshotSyncService.SweepCatalogFromVersionsCoreAsync` para actualizar `game.CoverImageUrl` y `ThumbnailUrl` con la portada en español cuando esté disponible o cuando la actual sea simulada/rota.
- [x] **Tarea 2.2:** Agregar pruebas unitarias en `tests/Ludeka.UnitTests/Infrastructure/BggRawSnapshotSyncServiceTests.cs` (o test suite adecuada) verificando la promoción de carátula en español en el barrido de catálogo.

## Fase 3: Contratos y Servicio de Sincronización de Medios Top 3.000
- [x] **Tarea 3.1:** Crear `BggImagesSyncDtos.cs` y el contrato `IBggImagesSyncService.cs` en `src/Ludeka.Application/`.
- [x] **Tarea 3.2:** Implementar `BggImagesSyncService.cs` en `src/Ludeka.Infrastructure/Bgg/`, gestionando la paginación por `BggRank <= 3000`, la consulta a `GeekDoImagesClient`, la priorización de portada española y la estrategia de persistencia (R2 vs CDN directo seguro anti-404).
- [x] **Tarea 3.3:** Registrar `IBggImagesSyncService` en `LudekaServiceCollectionExtensions.cs`.
- [x] **Tarea 3.4:** Crear pruebas unitarias para `BggImagesSyncService` cubriendo la asignación de portada, trasera, mesa y el modo Zero-Cloud sin R2.

## Fase 4: Runner Autónomo en `Ludeka.Jobs`
- [x] **Tarea 4.1:** Registrar el nombre del trabajo `JobNames.BggImagesTop3000 = "bgg-images-top3000"` en `src/Ludeka.Jobs/JobNames.cs`.
- [x] **Tarea 4.2:** Implementar `BggImagesTop3000JobRunner.cs` en `src/Ludeka.Jobs/Runners/` y registrar en `JobRunnerServiceCollectionExtensions.cs`.
- [x] **Tarea 4.3:** Crear prueba unitaria para `BggImagesTop3000JobRunnerTests.cs` validando el ciclo de ejecución y leases de ventana.

## Fase 5: Verificación Integral y Suite de Pruebas
- [ ] **Tarea 5.1:** Ejecutar la suite completa de pruebas unitarias (`dotnet test`) asegurando 100% verde y cero regresiones.
- [ ] **Tarea 5.2:** Generar informe de verificación `verification-report.md` y preparar apertura de Pull Request vía `scripts/sdd-worktree.ps1 pr bgg-imagenes-top3000`.
