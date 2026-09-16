# Checklist de Tareas Técnicas: change-21-home-dashboard

## Fase 1: Dominio (`Ludeka.Core`)
- [x] **1.1** Actualizar `src/Ludeka.Core/Entities/Giveaway.cs` para incluir `public bool IsPromoted { get; private set; }` y `SetPromoted(bool isPromoted)`.
- [x] **1.2** Crear `src/Ludeka.Core/Entities/BoardGameEvent.cs` con validaciones de fechas, descripción y propiedades del evento.

## Fase 2: Aplicación (`Ludeka.Application`)
- [x] **2.1** Actualizar `src/Ludeka.Application/DTOs/CommunityDtos.cs` incorporando `bool IsPromoted = false` en `GiveawayDto` y `CreateGiveawayRequest`.
- [x] **2.2** Crear `src/Ludeka.Application/DTOs/HomeDashboardDtos.cs` con `BoardGameEventDto` y `HomeDashboardDto`.
- [x] **2.3** Crear `src/Ludeka.Application/Contracts/IBoardGameEventRepository.cs`.
- [x] **2.4** Crear `src/Ludeka.Application/Contracts/IHomeDashboardService.cs`.
- [x] **2.5** Implementar `src/Ludeka.Application/Features/Home/HomeDashboardService.cs` orquestando los 4 carriles con sus respectivas ordenaciones.
- [x] **2.6** Implementar `src/Ludeka.Application/Features/Home/CachedHomeDashboardService.cs` con `IMemoryCache` (TTL de 10 minutos).

## Fase 3: Infraestructura (`Ludeka.Infrastructure`)
- [x] **3.1** Añadir `DbSet<BoardGameEvent> BoardGameEvents` a `src/Ludeka.Infrastructure/Data/LudekaDbContext.cs`.
- [x] **3.2** Actualizar `src/Ludeka.Infrastructure/Data/SqliteSchemaMigrator.cs` con la columna `IsPromoted` en `Giveaways` y la creación de la tabla `BoardGameEvents`.
- [x] **3.3** Implementar `src/Ludeka.Infrastructure/Data/SqliteBoardGameEventRepository.cs`.
- [x] **3.4** Crear `src/Ludeka.Infrastructure/Seeding/BoardGameEventSeeder.cs` con las ferias y festivales de referencia.
- [x] **3.5** Registrar `IBoardGameEventRepository`, `IHomeDashboardService` y el seeder en `src/Ludeka.Infrastructure/DependencyInjection.cs` y `src/Ludeka.Web/Program.cs`.

## Fase 4: Presentación Web Blazor (`Ludeka.Web`)
- [x] **4.1** Actualizar `src/Ludeka.Web/Components/Pages/Home.razor` para que responda exclusivamente a `@page "/catalogo"`.
- [x] **4.2** Crear `src/Ludeka.Web/Components/Pages/HomeDashboard.razor` en `@page "/"` con diseño editorial y 4 carriles de desplazamiento táctil (*mobile-first snap*).
- [x] **4.3** Modificar `src/Ludeka.Web/Components/Layout/MainLayout.razor` para retirar el selector de temas del Navbar superior y actualizar el enlace de catálogo a `/catalogo`.
- [x] **4.4** Actualizar `src/Ludeka.Web/Components/Pages/GameDetail.razor` con el enlace canónico directo `[ 🌐 Ver en BoardGameGeek ]` y enlaces de vuelta a `/catalogo`.

## Fase 5: Pruebas y Verificación (`Ludeka.UnitTests`)
- [x] **5.1** Crear `tests/Ludeka.UnitTests/Domain/BoardGameEventTests.cs` validando la entidad de eventos.
- [x] **5.2** Crear `tests/Ludeka.UnitTests/Application/HomeDashboardServiceTests.cs` validando orquestación, orden de sorteos promocionados, orden cronológico de eventos y funcionamiento de la caché.
- [x] **5.3** Ejecutar `dotnet test` y verificar que el 100% de las pruebas pasen en verde (453 pruebas superadas).
