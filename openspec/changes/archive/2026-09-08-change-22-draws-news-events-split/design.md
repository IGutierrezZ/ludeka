# Diseño Técnico y Arquitectura: change-22-draws-news-events-split

## 1. Diagrama de Arquitectura y Componentes

```mermaid
graph TD
    Nav[MainLayout.razor (Navbar + Footer)] --> SorteosUI[Draws.razor (/sorteos, /radar)]
    Nav --> NewsUI[News.razor (/novedades)]
    Nav --> EventsUI[Events.razor (/eventos)]
    Nav --> EventsAdminUI[EventsManagement.razor (/admin/eventos)]

    SorteosUI --> GiveawaySvc[IGiveawayService (SetPromoted, Ordenación)]
    NewsUI --> ReleaseSvc[IWeeklyReleaseService (CreateRelease)]
    EventsUI --> EventSvc[IBoardGameEventService (GetUpcoming, GetPast)]
    EventsAdminUI --> EventSvc
    EventsAdminUI --> ImageSvc[IImageStorageService (SaveEventPoster)]
    NewsUI --> ImageSvc

    EventSvc --> EventRepo[IBoardGameEventRepository]
    EventRepo --> DB[(SQLite: BoardGameEvents)]
    GiveawaySvc --> DB
    ReleaseSvc --> DB
```

---

## 2. Capa de Aplicación (`Ludeka.Application`)

### 2.1 Contrato y Servicio de Eventos Lúdicos
```csharp
namespace Ludeka.Application.Contracts;

public interface IBoardGameEventService
{
    Task<IReadOnlyList<BoardGameEventDto>> GetUpcomingEventsAsync(int limit = 50, CancellationToken ct = default);
    Task<IReadOnlyList<BoardGameEventDto>> GetPastEventsAsync(int limit = 50, CancellationToken ct = default);
    Task<BoardGameEventDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<BoardGameEventDto> CreateEventAsync(CreateBoardGameEventRequest request, CancellationToken ct = default);
    Task<BoardGameEventDto> UpdateEventAsync(Guid id, UpdateBoardGameEventRequest request, CancellationToken ct = default);
    Task DeleteEventAsync(Guid id, CancellationToken ct = default);
}
```

### 2.2 DTOs de Eventos (`BoardGameEventDtos.cs`)
```csharp
namespace Ludeka.Application.DTOs;

public record CreateBoardGameEventRequest(
    string Title,
    string Description,
    string ImageUrl,
    DateOnly StartDate,
    DateOnly EndDate,
    string Location,
    string? WebsiteUrl = null,
    string Organizer = "",
    bool IsOfficial = true
);

public record UpdateBoardGameEventRequest(
    string Title,
    string Description,
    string ImageUrl,
    DateOnly StartDate,
    DateOnly EndDate,
    string Location,
    string? WebsiteUrl = null,
    string Organizer = "",
    bool IsOfficial = true
);
```

### 2.3 Ampliación de `IGiveawayService`
```csharp
namespace Ludeka.Application.Contracts;

public interface IGiveawayService
{
    Task<IReadOnlyList<GiveawayDto>> GetGiveawaysAsync(bool includeExpired = false, CancellationToken ct = default);
    Task<GiveawayDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<GiveawayDto> CreateOrMergeGiveawayAsync(CreateGiveawayRequest request, CancellationToken ct = default);
    Task SetPromotedAsync(Guid id, bool isPromoted, CancellationToken ct = default);
}
```

### 2.4 Ampliación de `IWeeklyReleaseService`
```csharp
namespace Ludeka.Application.Contracts;

public interface IWeeklyReleaseService
{
    Task<IReadOnlyList<WeeklyReleaseDto>> GetReleasesAsync(DateOnly? weekOf = null, CancellationToken ct = default);
    Task<WeeklyReleaseDto> CreateReleaseAsync(CreateWeeklyReleaseRequest request, CancellationToken ct = default);
}

public record CreateWeeklyReleaseRequest(
    string Title,
    string Publisher,
    DateOnly ReleaseDate,
    Guid? GameId = null,
    string? CoverImageUrl = null,
    decimal? EstimatedPvp = null,
    bool IsReprint = false,
    string? Notes = null
);
```

### 2.5 Ampliación de `IImageStorageService`
```csharp
namespace Ludeka.Application.Contracts;

public interface IImageStorageService
{
    Task<GameImageUploadResult> SaveGameCoverAsync(
        string slug,
        Stream contentStream,
        string originalFileName,
        string contentType,
        CancellationToken ct = default);

    Task<GameImageUploadResult> SaveEventPosterAsync(
        string eventSlugOrId,
        Stream contentStream,
        string originalFileName,
        string contentType,
        CancellationToken ct = default);

    Task<GameImageUploadResult> SaveCommunityImageAsync(
        string subfolder,
        string identifier,
        Stream contentStream,
        string originalFileName,
        string contentType,
        CancellationToken ct = default);

    Task<GameImageUploadResult> ValidateCoverUrlAsync(
        string imageUrl,
        CancellationToken ct = default);
}
```

---

## 3. Capa de Infraestructura (`Ludeka.Infrastructure`)

### 3.1 `PhysicalFileImageStorageService`
- Soporte para subdirectorios:
  - `wwwroot/images/events/`
  - `wwwroot/images/giveaways/`
  - `wwwroot/images/releases/`
- Validaciones estándar de extensión (`.jpg`, `.jpeg`, `.png`, `.webp`) y límite de 5 MB.

### 3.2 Implementación `BoardGameEventService`
- Inyecta `IBoardGameEventRepository`.
- `GetUpcomingEventsAsync`: mapea a `BoardGameEventDto` usando `DateOnly.FromDateTime(DateTime.UtcNow)` para calcular `DaysUntilStart` y `IsUpcoming`.
- `GetPastEventsAsync`: filtra de `GetAllEventsAsync` los que cumplen `EndDate < hoy` ordenados por `EndDate` descendente.
- `CreateEventAsync`, `UpdateEventAsync`, `DeleteEventAsync` con comprobaciones de nulidad y validación de reglas de negocio.

### 3.3 Registro de Dependencias en `DependencyInjection.cs`
- `services.AddScoped<IBoardGameEventService, BoardGameEventService>();`

---

## 4. Capa Web Blazor (`Ludeka.Web`)

### 4.1 Navegación en `MainLayout.razor`
- Retirar enlace `/radar`.
- Incorporar:
  - `Sorteos` (`/sorteos`)
  - `Novedades` (`/novedades`)
  - `Eventos` (`/eventos`)
- En el pie de página, reflejar los tres enlaces segregados.

### 4.2 Página de Sorteos (`Radar.razor` / `Draws.razor`)
- Rutas: `@page "/sorteos"`, `@page "/radar"`.
- Lista ordenada por `IsPromoted DESC`, `DeadlineAt ASC`.
- Tarjeta de sorteo con badge "⭐ Promocionado" y botón de conmutación de estado para usuarios con roles autorizados.
- Modal de propuesta/alta con casilla `Promocionado` para moderadores.

### 4.3 Página de Novedades (`News.razor`)
- Ruta: `@page "/novedades"`.
- Grid editorial de novedades con portadas, filtros y botón `[ ➕ Añadir Novedad ]` para moderadores con modal interactivo.

### 4.4 Página de Eventos (`Events.razor`)
- Ruta: `@page "/eventos"`.
- Selector de pestañas: "Próximas Citas" e "Histórico".
- Tarjetas con cartel promocional, badge temporal ("En 18 días" o "En curso"), fechas, ubicación y botón saliente.
- Botón de acceso a administración si el usuario es moderador/fundador.

### 4.5 Panel de Administración de Eventos (`EventsManagement.razor`)
- Ruta: `@page "/admin/eventos"`.
- Tabla de eventos existentes con botones de editar y eliminar.
- Modal para crear o actualizar con subida de cartel (`InputFile`).

---

## 5. Estrategia de Pruebas Unitarias
1. **`BoardGameEventServiceTests`:** Validar segregación de futuros/pasados, creación con datos válidos, validación de excepciones (fechas inconsistentes) y borrado.
2. **`GiveawayPromotionTests`:** Validar conmutación `SetPromotedAsync`, persistencia de `IsPromoted` y orden prioritario de sorteos.
3. **`WeeklyReleaseCreationTests`:** Validar alta manual de novedades y listado cronológico.
4. **`PhysicalFileImageStorageExtendedTests`:** Validar almacenamiento de carteles y rechazo de archivos inválidos o vacíos.
5. **Verificación Global:** Ejecución de `dotnet test` asegurando que los 453 tests anteriores más los nuevos pasen al 100% en verde.
