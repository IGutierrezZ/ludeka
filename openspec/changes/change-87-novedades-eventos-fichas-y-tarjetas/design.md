# Diseño Técnico: INC-87 — Fichas Inteligentes de Novedades y Eventos, Rediseño Compacto a Proporción de Catálogo y Edición Editorial

## 1. Arquitectura de Dominio y Datos

### Entidad `WeeklyRelease` (`Ludeka.Core.Entities`)
```csharp
public class WeeklyRelease
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Title { get; private set; } = string.Empty;
    public string Publisher { get; private set; } = string.Empty;
    public DateOnly ReleaseDate { get; private set; }
    public Guid? GameId { get; private set; }
    public string? CoverImageUrl { get; private set; }
    public decimal? EstimatedPvp { get; private set; }
    public bool IsReprint { get; private set; }
    public string? Notes { get; private set; }
    public string? SourceUrl { get; private set; }
    public string? InstagramMediaId { get; private set; }
    public string? InstagramPermalink { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; private set; }

    public virtual Game? Game { get; private set; }

    public void Update(
        string title,
        string publisher,
        DateOnly releaseDate,
        Guid? gameId,
        string? coverImageUrl,
        decimal? estimatedPvp,
        bool isReprint,
        string? notes,
        string? sourceUrl = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("El título del lanzamiento no puede estar vacío.", nameof(title));
        if (string.IsNullOrWhiteSpace(publisher))
            throw new ArgumentException("La editorial del lanzamiento no puede estar vacía.", nameof(publisher));

        Title = title.Trim();
        Publisher = publisher.Trim();
        ReleaseDate = releaseDate;
        GameId = gameId;
        CoverImageUrl = string.IsNullOrWhiteSpace(coverImageUrl) ? null : coverImageUrl.Trim();
        EstimatedPvp = estimatedPvp;
        IsReprint = isReprint;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        SourceUrl = string.IsNullOrWhiteSpace(sourceUrl) ? null : sourceUrl.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
```

### Contratos de Aplicación (`Ludeka.Application`)
- **DTOs (`CommunityDtos.cs`):**
  - `WeeklyReleaseDto`: se añade `string? SourceUrl = null`.
  - `CreateWeeklyReleaseRequest`: se añade `string? SourceUrl = null`.
  - `UpdateWeeklyReleaseRequest`: nuevo DTO con `Guid Id`, `string Title`, `string Publisher`, `DateOnly ReleaseDate`, `Guid? GameId`, `string? CoverImageUrl`, `decimal? EstimatedPvp`, `bool IsReprint`, `string? Notes`, `string? SourceUrl`.
- **Servicio `IWeeklyReleaseService`:**
  - `Task<WeeklyReleaseDto?> GetByIdAsync(Guid id, CancellationToken ct = default);`
  - `Task<WeeklyReleaseDto> UpdateReleaseAsync(Guid id, UpdateWeeklyReleaseRequest request, CancellationToken ct = default);`
  - `Task DeleteReleaseAsync(Guid id, CancellationToken ct = default);`

---

## 2. Componentes de Interfaz de Usuario (`Ludeka.Web`)

### `ReleaseDetail.razor`
- Rutas: `@page "/novedades/{Id:guid}"`, `@page "/novedad/{Id:guid}"`.
- Carga `WeeklyReleaseDto` mediante `ReleaseService.GetByIdAsync(Id)`.
- Si no existe: estado visual amigable de no encontrado con enlace al radar de novedades.
- Si existe:
  - Botón CTA: «Ir a la noticia oficial &rarr;» (`target="_blank" rel="noopener noreferrer"`).
  - Bloque para moderadores (`CanApproveMedia` o Mesa Fundadora) con botón «Editar Novedad» y «Eliminar».
  - Modal editorial de edición con carga de carátula WebP en R2 (`releases/{id:N}/cover.webp`).

### `EventDetail.razor`
- Rutas: `@page "/eventos/{Id:guid}"`, `@page "/evento/{Id:guid}"`.
- Carga `BoardGameEventDto` mediante `EventService.GetByIdAsync(Id)`.
- Si no existe: estado visual amigable de no encontrado con enlace al calendario de eventos.
- Si existe:
  - Botón CTA: «Visitar web oficial del evento &rarr;» (`target="_blank" rel="noopener noreferrer"`).
  - Bloque para moderadores (`PermisoGestionarEventos` o Mesa Fundadora) con botón «Editar Evento» y «Eliminar».
  - Modal editorial de edición con carga de cartel WebP en R2 (`events/{id:N}/cover.webp`).

### Tarjetas de Catálogo y Portada
- **`News.razor`:**
  - Cuadrícula compacta: `grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-3 sm:gap-4`.
  - Tarjetas compactas con `rail-cover--square` (240x240) y enlace a `/novedades/{id}`.
- **`Events.razor`:**
  - Cuadrícula compacta: `grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-3 sm:gap-4`.
  - Tarjetas compactas con `rail-cover--square` (240x240) y enlace a `/eventos/{id}`.
- **`HomeReleaseCard.razor` y `HomeEventCard.razor`:**
  - `w-[28vw] min-w-[105px] max-w-[125px] sm:w-44 md:w-48` con `rail-cover--square` (192x192).
  - Enlaces directos a `/novedades/{id}` y `/eventos/{id}`.
