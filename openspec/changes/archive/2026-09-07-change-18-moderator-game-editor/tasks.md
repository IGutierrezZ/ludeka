# Tareas de Implementación: change-18-moderator-game-editor (Incremento 18)

## Tareas

- [x] **1. Modelo de Dominio e Invariantes (`Ludeka.Core`)**
  - [x] 1.1 Enriquecer `src/Ludeka.Core/Entities/Game.cs` con el método `UpdateCatalogInformation(...)` para actualizar títulos, autores, editorial, año, sinopsis, ADN lúdico, edades, duración y rango de jugadores con ajuste de `Scalability`.
  - [x] 1.2 Enriquecer `UpdateImages(...)` en `Game.cs` para actualizar carátula y miniatura con normalización de rutas.
  - [x] 1.3 Crear entidad de auditoría `src/Ludeka.Core/Entities/GameEditLog.cs` con invariantes y constructor validado.
  - [x] 1.4 Crear pruebas unitarias de dominio `tests/Ludeka.UnitTests/Domain/GameEditorDomainTests.cs` (validaciones de invariantes de juego, rangos de comensales y log de auditoría).

- [x] **2. Contratos, DTOs y Servicio de Aplicación (`Ludeka.Application`)**
  - [x] 2.1 Crear DTOs y comandos `src/Ludeka.Application/DTOs/GameEditorDtos.cs` (`UpdateGameDetailsCommand`, `GameImageUploadResult`, `GameEditLogDto`).
  - [x] 2.2 Crear contratos `src/Ludeka.Application/Contracts/IGameEditorService.cs` e `src/Ludeka.Application/Contracts/IImageStorageService.cs`.
  - [x] 2.3 Implementar servicio `src/Ludeka.Application/Features/Catalog/GameEditorService.cs` con control de acceso (`Moderator` / `FoundingTeam`), invalidación de caché, registro en `GameEditLog` y resolución en cascada de reportes de INC-17.
  - [x] 2.4 Crear pruebas unitarias de aplicación `tests/Ludeka.UnitTests/Application/GameEditorServiceTests.cs` (autorización, validaciones y resolución de reporte).

- [x] **3. Persistencia y Almacenamiento en Infraestructura (`Ludeka.Infrastructure`)**
  - [x] 3.1 Implementar `src/Ludeka.Infrastructure/Services/PhysicalFileImageStorageService.cs` con validación MIME, tope de 5 MB, guardado físico en `wwwroot/images/games/` y nombre canónico higienizado.
  - [x] 3.2 Actualizar `src/Ludeka.Infrastructure/Data/SqliteGameRepository.cs` para persistir completamente las propiedades modificadas de la entidad en `UpdateAsync`.
  - [x] 3.3 Registrar `DbSet<GameEditLog>` en `src/Ludeka.Infrastructure/Data/LudekaDbContext.cs` y reconciliación de tabla e índices en `src/Ludeka.Infrastructure/Data/SqliteSchemaMigrator.cs`.
  - [x] 3.4 Crear pruebas de infraestructura `tests/Ludeka.UnitTests/Infrastructure/PhysicalFileImageStorageServiceTests.cs`.

- [x] **4. Componentes Web y Experiencia de Usuario (`Ludeka.Web`)**
  - [x] 4.1 Crear componente modal `src/Ludeka.Web/Components/Shared/GameEditorModal.razor` (diseño editorial en 4 pestañas, subida reactiva con `InputFile`, previsualización en vivo, banner de reporte asociado y cumplimiento WCAG 2.2 AA).
  - [x] 4.2 Integrar botón `[ ✏️ Editar Ficha ]` en `src/Ludeka.Web/Components/Pages/GameDetail.razor` exclusivo para moderadores con actualización en caliente Zero-FOUC.
  - [x] 4.3 Integrar botón `[ ✏️ Corregir Ficha y Resolver ]` en `src/Ludeka.Web/Components/Pages/GameReportsModeration.razor` con refresco automático de la bandeja.
  - [x] 4.4 Registrar dependencias en `src/Ludeka.Web/Program.cs`.
  - [x] 4.5 Crear pruebas de integración web `tests/Ludeka.UnitTests/Web/GameEditorWebIntegrationTests.cs`.

- [x] **5. Verificación Integral y Cierre (`sdd-verify` / `sdd-archive`)**
  - [x] 5.1 Ejecutar suite completa de pruebas unitarias (`dotnet test Ludeka.sln`) y verificar el 100% de tests en verde (395 de 395 tests superados).
  - [x] 5.2 Generar reporte de verificación `verify-report.md`.
  - [x] 5.3 Actualizar catálogo y archivar incremento en `docs/increments/archive/` y `.openspec/changes/archive/`.
