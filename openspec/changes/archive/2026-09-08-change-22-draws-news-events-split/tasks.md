# Checklist de Tareas Técnicas: change-22-draws-news-events-split

## Fase 1: Capa de Aplicación (`Ludeka.Application`)
- [x] **1.1** Actualizar `src/Ludeka.Application/Contracts/IImageStorageService.cs` con los métodos `SaveEventPosterAsync` y `SaveCommunityImageAsync`.
- [x] **1.2** Crear `src/Ludeka.Application/DTOs/BoardGameEventDtos.cs` con `CreateBoardGameEventRequest` y `UpdateBoardGameEventRequest`.
- [x] **1.3** Crear el contrato `src/Ludeka.Application/Contracts/IBoardGameEventService.cs`.
- [x] **1.4** Actualizar `src/Ludeka.Application/Contracts/IGiveawayService.cs` añadiendo `SetPromotedAsync(Guid id, bool isPromoted, CancellationToken ct = default)`.
- [x] **1.5** Actualizar `src/Ludeka.Application/Contracts/IWeeklyReleaseService.cs` y DTOs añadiendo `CreateReleaseAsync(CreateWeeklyReleaseRequest request, CancellationToken ct = default)`.
- [x] **1.6** Implementar `src/Ludeka.Application/Features/Events/BoardGameEventService.cs`.
- [x] **1.7** Actualizar `src/Ludeka.Application/Features/Community/GiveawayService.cs` implementando `SetPromotedAsync` y ordenación con `IsPromoted`.
- [x] **1.8** Actualizar `src/Ludeka.Application/Features/Community/WeeklyReleaseService.cs` implementando `CreateReleaseAsync`.

## Fase 2: Capa de Infraestructura (`Ludeka.Infrastructure`)
- [x] **2.1** Actualizar `src/Ludeka.Infrastructure/Services/PhysicalFileImageStorageService.cs` implementando el guardado seguro en carpetas físicas (`events`, `giveaways`, `releases`).
- [x] **2.2** Registrar `IBoardGameEventService` en `src/Ludeka.Infrastructure/DependencyInjection.cs` y `Program.cs`.

## Fase 3: Capa de Presentación Web Blazor (`Ludeka.Web`)
- [x] **3.1** Actualizar `src/Ludeka.Web/Components/Layout/MainLayout.razor` reemplazando `/radar` por `/sorteos`, `/novedades` y `/eventos` en Navbar y Footer.
- [x] **3.2** Actualizar `src/Ludeka.Web/Components/Pages/Radar.razor` especializándolo en Sorteos (`/sorteos` y `@page "/radar"`), incorporando botón de conmutación de `IsPromoted` en 1 clic y casilla en modal para moderadores.
- [x] **3.3** Crear `src/Ludeka.Web/Components/Pages/News.razor` en `@page "/novedades"` con vista editorial de lanzamientos, filtros y modal de creación manual.
- [x] **3.4** Crear `src/Ludeka.Web/Components/Pages/Events.razor` en `@page "/eventos"` con pestañas de próximos/pasados, tarjetas de ferias y botón a administración.
- [x] **3.5** Crear `src/Ludeka.Web/Components/Pages/EventsManagement.razor` en `@page "/admin/eventos"` con panel CRUD de ferias y festivales para moderadores.

## Fase 4: Pruebas y Verificación (`Ludeka.UnitTests`)
- [x] **4.1** Crear `tests/Ludeka.UnitTests/Application/BoardGameEventServiceTests.cs`.
- [x] **4.2** Crear `tests/Ludeka.UnitTests/Application/GiveawayPromotionTests.cs`.
- [x] **4.3** Crear `tests/Ludeka.UnitTests/Application/WeeklyReleaseCreationTests.cs`.
- [x] **4.4** Crear `tests/Ludeka.UnitTests/Infrastructure/PhysicalFileImageStorageExtendedTests.cs`.
- [x] **4.5** Ejecutar `dotnet test` y comprobar que el 100% de las pruebas pasen en verde sin regresiones (472 pruebas superadas).
