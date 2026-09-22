# Tareas de Implementación — INC-54: Padrón Exhaustivo y Mecanismo de Carga del Directorio Lúdico

- [x] 1. **Dataset Canónico y Embebido**
  - [x] 1.1 Crear `src/Ludeka.Infrastructure/Seeding/seed-directory.json` con más de 45 editoriales, más de 35 tiendas y más de 35 creadores de contenido con datos exhaustivos (logos, webs, redes).
  - [x] 1.2 Configurar `Ludeka.Infrastructure.csproj` con `<EmbeddedResource Include="Seeding\seed-directory.json" />`.

- [x] 2. **Actualización de DirectorySeeder**
  - [x] 2.1 Modelos de deserialización DTO (`SeedDirectoryModel`, `SeedPublisherModel`, `SeedStoreModel`, `SeedCreatorModel`).
  - [x] 2.2 Lectura de recurso embebido con fallback a disco (`ReadDirectorySeedJson()`).
  - [x] 2.3 Lógica de reconciliación e inserción idempotente preservando purga de diseñadores de INC-31.
  - [x] 2.4 Generación de `DirectorySeedResult`.

- [x] 3. **Mecanismo de Ejecución CLI (Ludeka.Jobs)**
  - [x] 3.1 Registrar `JobNames.SeedDirectory = "seed-directory"` en `JobNames.cs`.
  - [x] 3.2 Implementar `SeedDirectoryJobRunner.cs` coordinado con `IDirectorySeederService` e `IJobExecutionCoordinator`.
  - [x] 3.3 Registrar el runner en `JobRunnerServiceCollectionExtensions.cs`.

- [x] 4. **Mecanismo de Ejecución Web (Administración y Directorio)**
  - [x] 4.1 Añadir acción y botón de siembra en `CatalogQueueAdmin.razor` con feedback interactivo y revalidación de permisos.
  - [x] 4.2 Añadir botón de sincronización rápida en `PublishersDirectory.razor`, `StoresDirectory.razor` y `CreatorsDirectory.razor` para moderadores y fundadores.

- [x] 5. **Pruebas y Verificación**
  - [x] 5.1 Actualizar `DirectorySeederTests.cs` (verificar >40 editoriales, >30 tiendas, >30 creadores, idempotencia y purga).
  - [x] 5.2 Actualizar `LudekaJobsCompositionTests.cs` para el nuevo recuento de 6 runners.
  - [x] 5.3 Crear `SeedDirectoryJobRunnerTests.cs`.
  - [x] 5.4 Ejecutar suite completa `dotnet test tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj` (1.619 tests superados, 0 fallos).
  - [x] 5.5 Ejecutar dos veces consecutivas `dotnet run --project src/Ludeka.Jobs -- seed-directory` verificando creación inicial e idempotencia aditiva posterior (0 creadas, 0 duplicados).

- [x] 6. **Documentación SDD y Roadmap**
  - [x] 6.1 Crear `docs/increments/inc-54-directorio-exhaustivo.md`.
  - [x] 6.2 Actualizar `docs/increments/ROADMAP.md` (conducido a verificación y listo para PR).
