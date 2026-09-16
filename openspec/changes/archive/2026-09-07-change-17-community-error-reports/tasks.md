# Tareas de Implementación: change-17-community-error-reports (Incremento 17)

## Tareas

- [x] **1. Modelo de Dominio e Invariantes (`Ludeka.Core`)**
  - [x] 1.1 Crear enumerado `src/Ludeka.Core/Enums/GameIssueType.cs` con las 8 tipologías de incidencia tipificadas.
  - [x] 1.2 Crear enumerado `src/Ludeka.Core/Enums/GameReportStatus.cs` con los 4 estados de ciclo de vida (`Pending`, `InReview`, `Resolved`, `Dismissed`).
  - [x] 1.3 Crear entidad rica `src/Ludeka.Core/Entities/GameIssueReport.cs` con constructor validado, métodos de mutación (`MarkAsInReview`, `Resolve`, `Dismiss`, `Reopen`) e invariantes de negocio.
  - [x] 1.4 Crear pruebas unitarias de dominio `tests/Ludeka.UnitTests/Domain/GameIssueReportTests.cs` (invariantes, validaciones y transiciones de estado).

- [x] **2. Contratos, DTOs y Servicio de Aplicación (`Ludeka.Application`)**
  - [x] 2.1 Crear contrato de repositorio `src/Ludeka.Application/Contracts/IGameIssueReportRepository.cs`.
  - [x] 2.2 Crear DTOs y comandos `src/Ludeka.Application/DTOs/GameIssueReportDtos.cs` (`CreateGameReportCommand`, `UpdateGameReportStatusCommand`, `GameIssueReportDto`, `GameReportFilter`, `GameIssueReportSummaryDto`).
  - [x] 2.3 Crear interfaz de servicio `src/Ludeka.Application/Contracts/IGameIssueReportService.cs`.
  - [x] 2.4 Implementar servicio `src/Ludeka.Application/Features/Reports/GameIssueReportService.cs` con validaciones, sanitización de datos y orquestación.
  - [x] 2.5 Crear pruebas unitarias de servicio `tests/Ludeka.UnitTests/Application/GameIssueReportServiceTests.cs`.

- [x] **3. Persistencia y Repositorio SQLite/EF Core (`Ludeka.Infrastructure`)**
  - [x] 3.1 Actualizar `src/Ludeka.Infrastructure/Data/LudekaDbContext.cs` incorporando `DbSet<GameIssueReport> IssueReports` y configuración en `OnModelCreating` (índices y longitudes).
  - [x] 3.2 Implementar repositorio `src/Ludeka.Infrastructure/Repositories/SqliteGameIssueReportRepository.cs` con consultas asíncronas optimizadas y conteos de resumen.
  - [x] 3.3 Crear pruebas de repositorio `tests/Ludeka.UnitTests/Infrastructure/SqliteGameIssueReportRepositoryTests.cs` con SQLite InMemory.

- [x] **4. Componentes Web y UX de Reporte (`Ludeka.Web`)**
  - [x] 4.1 Crear componente accesible `src/Ludeka.Web/Components/Shared/GameReportModal.razor` (selector de 8 motivos, formulario en 2 clics, estados de carga y agradecimiento lúdico).
  - [x] 4.2 Integrar botón `[ 🚩 Reportar problema ]` en `src/Ludeka.Web/Components/Pages/GameDetail.razor` y enlazar con `GameReportModal`.
  - [x] 4.3 Crear página de moderación centralizada `src/Ludeka.Web/Components/Pages/GameReportsModeration.razor` (`/moderacion/reportes` y `/admin/reportes`) con métricas, filtros por estado/tipo y modales de acción (revisar, descartar, resolver con notas).
  - [x] 4.4 Añadir pestaña de navegación hacia reportes en `src/Ludeka.Web/Components/Pages/MediaModeration.razor`.
  - [x] 4.5 Añadir enlace a moderación de reportes en `src/Ludeka.Web/Components/Layout/MainLayout.razor` para moderadores y Mesa Fundadora.
  - [x] 4.6 Registrar servicios y repositorios en `src/Ludeka.Web/Program.cs`.

- [x] **5. Verificación, Validación Suite Completa y Cierre (`sdd-verify` / `sdd-archive`)**
  - [x] 5.1 Ejecutar suite completa `dotnet test Ludeka.sln` y verificar que el 100% de los tests pasan en verde (378 tests en verde).
  - [x] 5.2 Generar reporte de verificación `verify-report.md`.
  - [x] 5.3 Actualizar catálogo y archivar incremento en `docs/increments/archive/` y `.openspec/changes/archive/`.
