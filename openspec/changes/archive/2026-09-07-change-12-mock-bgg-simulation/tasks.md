# Tareas: change-12-mock-bgg-simulation (Incremento 12)

## Fase 1: Configuración y Conmutador (`Ludeka.Infrastructure`)
- [x] 1.1 Ampliar `BggOptions.cs` con `SimulateApi` y `ShouldSimulate`.
- [x] 1.2 Actualizar `appsettings.json` en `Ludeka.Web` con `"SimulateApi": true`.
- [x] 1.3 Crear pruebas unitarias para `BggOptions` en `tests/Ludeka.UnitTests/Infrastructure/BggOptionsTests.cs`.

## Fase 2: Dataset de Simulación y Cliente Mock (`Ludeka.Infrastructure.Bgg`)
- [x] 2.1 Crear `BggSimulationDataset.cs` con los 40 títulos reales (30 juegos base + 10 expansiones), metadatos completos, colecciones de usuario y búsqueda con normalización de caracteres.
- [x] 2.2 Crear `SimulatedBggClient.cs` implementando `IBggClient`.
- [x] 2.3 Crear pruebas unitarias para `SimulatedBggClient` en `tests/Ludeka.UnitTests/Infrastructure/SimulatedBggClientTests.cs`.

## Fase 3: Cableado e Inyección de Dependencias (`Ludeka.Web`)
- [x] 3.1 Actualizar registro de `IBggClient` en `src/Ludeka.Web/Program.cs` para conmutar transparentemente entre `SimulatedBggClient` y `BggXmlApiClient`.
- [x] 3.2 Crear pruebas de integración en `tests/Ludeka.UnitTests/Application/BggSimulationIntegrationTests.cs` verificando `BggSearchAssistedService`, `BggImportService` y `BggCatalogQueueService` con `SimulatedBggClient`.

## Fase 4: Semillado Completo de Catálogo (`Ludeka.Infrastructure.Seeding`)
- [x] 4.1 Ampliar `src/Ludeka.Infrastructure/Seeding/seed-games.json` con los 30 juegos base canónicos con metadatos reales, escalabilidad, fundas y enlaces de compra.
- [x] 4.2 Actualizar `CatalogSeeder.cs` para semillar las 10 expansiones oficiales (añadiendo *7 Wonders Duel: Pantheon*, *Dune: Imperium - El Auge de Ix* y *Everdell: Bellfaire*) junto con sus sinergias par-a-par y recetas de mesa.

## Fase 5: Verificación Integral y Cierre
- [x] 5.1 Ejecutar suite completa de pruebas unitarias (`dotnet test`).
- [x] 5.2 Generar reporte de verificación en `.openspec/changes/change-12-mock-bgg-simulation/verify-report.md`.
- [x] 5.3 Actualizar catálogo de incrementos y registrar progreso en memoria Engram.
