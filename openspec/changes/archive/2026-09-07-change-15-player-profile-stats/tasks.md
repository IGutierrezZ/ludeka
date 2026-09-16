# Tareas de Implementación: change-15-player-profile-stats (Incremento 15)

## Tareas

- [x] **1. Contratos y DTOs (`Ludeka.Application`)**
  - [x] 1.1 Crear DTOs de estadísticas (`ShelfTimeStatsDto`, `StylePercentageDto`, `PlayerDnaDistributionDto`, `ScalabilityCountDto`, `ScalabilitySweetSpotDto`, `TopEntityStatDto`, `SleeveFormatStatDto`, `SleeveProtectionRadarDto`, `PlayerBadgeDto`, `UserLibraryStatsDto`, `PublicUserProfileDto`) en `src/Ludeka.Application/DTOs/UserLibraryStatsDtos.cs`.
  - [x] 1.2 Crear interfaz de servicio `IUserLibraryStatsService` en `src/Ludeka.Application/Contracts/IUserLibraryStatsService.cs`.

- [x] **2. Lógica de Aplicación (`Ludeka.Application`)**
  - [x] 2.1 Implementar servicio `UserLibraryStatsService` en `src/Ludeka.Application/Features/Library/UserLibraryStatsService.cs`.
  - [x] 2.2 Implementar cálculo de horas de estantería (`TotalMinMinutes`, `TotalMaxMinutes`, `TotalMinHours`, `TotalMaxHours`).
  - [x] 2.3 Implementar desglose de ADN lúdico (`GameStyle`, porcentaje cooperativo, porcentaje solitario, estilo dominante).
  - [x] 2.4 Implementar extractor y normalizador de diseñadores (con soporte para autores múltiples) y editoriales (Top 5).
  - [x] 2.5 Implementar detector de rango dulce de comensales (Sweet Spot de escalabilidad 1 a 7+).
  - [x] 2.6 Implementar radar de fundas de cartas y estimación de paquetes de 50 unidades requeridos.
  - [x] 2.7 Implementar asignación dinámica de insignias de nivel (0 a 4) y rasgos lúdicos según umbrales de ADN.
  - [x] 2.8 Implementar consulta de perfil público `GetPublicProfileAsync(userId)`.

- [x] **3. Registro e Inyección de Dependencias (`Ludeka.Web`)**
  - [x] 3.1 Registrar `IUserLibraryStatsService` y `UserLibraryStatsService` en `src/Ludeka.Web/Program.cs`.

- [x] **4. Componentes y Vistas Blazor (`Ludeka.Web`)**
  - [x] 4.1 Crear componente visual `LibraryStatsDashboard.razor` en `src/Ludeka.Web/Components/Features/Library/LibraryStatsDashboard.razor` (insignia de perfil, píldoras métricas, espectro de ADN, gráfico de comensales, radar de fundas y podio de autores).
  - [x] 4.2 Integrar la nueva pestaña `🧬 ADN y Estadísticas` en `src/Ludeka.Web/Components/Pages/MyLibrary.razor`.
  - [x] 4.3 Crear página de perfil público `PublicProfile.razor` en `src/Ludeka.Web/Components/Pages/PublicProfile.razor` con rutas `@page "/u/{UserId}"` y `@page "/perfil/{UserId}"`, botón para copiar enlace propio y vitrina pública de títulos.

- [x] **5. Pruebas Automatizadas y Verificación (`Ludeka.UnitTests`)**
  - [x] 5.1 Crear suite de pruebas unitarias `UserLibraryStatsServiceTests.cs` en `tests/Ludeka.UnitTests/Application/UserLibraryStatsServiceTests.cs`.
  - [x] 5.2 Validar colecciones vacías, cálculos de horas, distribución de estilos,Sweet Spot de comensales, parsing de múltiples diseñadores, radar de fundas y asignación de insignias/rasgos.
  - [x] 5.3 Validar resolución de perfil público (existente e inexistente).
  - [x] 5.4 Ejecutar suite completa `dotnet test` y garantizar 100% de tests en verde sin regresiones.
  - [x] 5.5 Generar informe de verificación `verify-report.md`.
