# Diseño Técnico y Arquitectura: change-21-home-dashboard

## 1. Diagrama de Arquitectura y Flujo de Datos

```mermaid
graph TD
    UI[HomeDashboard.razor (/)] --> Svc[IHomeDashboardService]
    Svc --> Cache[CachedHomeDashboardService (IMemoryCache)]
    Cache --> CoreSvc[HomeDashboardService]
    CoreSvc --> CatRepo[ICatalogService (Top 20)]
    CoreSvc --> GiveRepo[IGiveawayService (Sorteos con IsPromoted)]
    CoreSvc --> RelRepo[IWeeklyReleaseService (Novedades)]
    CoreSvc --> EvtRepo[IBoardGameEventRepository (Eventos Lúdicos)]
    EvtRepo --> DB[(SQLite: BoardGameEvents)]
    CatRepo --> DB
    GiveRepo --> DB
    RelRepo --> DB
```

---

## 2. Modelo de Dominio (`Ludeka.Core`)

### 2.1 Entidad `BoardGameEvent`
```csharp
namespace Ludeka.Core.Entities;

public class BoardGameEvent
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string ImageUrl { get; private set; } = string.Empty;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public string Location { get; private set; } = string.Empty;
    public string? WebsiteUrl { get; private set; }
    public string Organizer { get; private set; } = string.Empty;
    public bool IsOfficial { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private BoardGameEvent() { }

    public BoardGameEvent(
        string title,
        string description,
        string imageUrl,
        DateOnly startDate,
        DateOnly endDate,
        string location,
        string? websiteUrl = null,
        string organizer = "",
        bool isOfficial = true)
    {
        // Validaciones de invariantes
    }
}
```

### 2.2 Ampliación de `Giveaway`
- Propiedad: `public bool IsPromoted { get; private set; } = false;`
- Método: `public void SetPromoted(bool isPromoted)`

---

## 3. Contratos y DTOs (`Ludeka.Application`)

### 3.1 DTOs
```csharp
namespace Ludeka.Application.DTOs;

public record BoardGameEventDto(
    Guid Id,
    string Title,
    string Description,
    string ImageUrl,
    DateOnly StartDate,
    DateOnly EndDate,
    string Location,
    string? WebsiteUrl,
    string Organizer,
    bool IsOfficial,
    string FormattedDates,
    string RemainingDaysText);

public record HomeDashboardDto(
    IReadOnlyList<GameSummaryDto> TopGames,
    IReadOnlyList<GiveawayDto> Giveaways,
    IReadOnlyList<WeeklyReleaseDto> RecentReleases,
    IReadOnlyList<BoardGameEventDto> UpcomingEvents);
```

### 3.2 Contratos
- `IBoardGameEventRepository`:
  - `Task<IReadOnlyList<BoardGameEvent>> GetUpcomingEventsAsync(int limit = 20, CancellationToken ct = default);`
  - `Task<BoardGameEvent?> GetByIdAsync(Guid id, CancellationToken ct = default);`
- `IHomeDashboardService`:
  - `Task<HomeDashboardDto> GetDashboardDataAsync(CancellationToken ct = default);`

---

## 4. Persistencia en Infraestructura (`Ludeka.Infrastructure`)

### 4.1 Reconciliación de Esquema SQLite (`SqliteSchemaMigrator`)
1. Comprobar columna `IsPromoted` en tabla `Giveaways`:
   ```sql
   ALTER TABLE "Giveaways" ADD COLUMN "IsPromoted" INTEGER NOT NULL DEFAULT 0;
   ```
2. Crear tabla `BoardGameEvents`:
   ```sql
   CREATE TABLE IF NOT EXISTS "BoardGameEvents" (
       "Id" TEXT NOT NULL CONSTRAINT "PK_BoardGameEvents" PRIMARY KEY,
       "Title" TEXT NOT NULL,
       "Description" TEXT NOT NULL,
       "ImageUrl" TEXT NOT NULL,
       "StartDate" TEXT NOT NULL,
       "EndDate" TEXT NOT NULL,
       "Location" TEXT NOT NULL,
       "WebsiteUrl" TEXT NULL,
       "Organizer" TEXT NOT NULL,
       "IsOfficial" INTEGER NOT NULL,
       "CreatedAt" TEXT NOT NULL
   );
   CREATE INDEX IF NOT EXISTS "IX_BoardGameEvents_StartDate" ON "BoardGameEvents" ("StartDate");
   ```

### 4.2 Semillado de Grandes Ferias y Eventos (`BoardGameEventSeeder`)
- Festival Internacional de Juegos de Córdoba (Palacio de la Merced, Córdoba).
- Feria InterOcio (IFEMA, Madrid).
- SPIEL Essen (Messe Essen, Alemania).
- Gen Con (Indiana Convention Center, Indianápolis).
- DAU Barcelona (Fabra i Coats, Barcelona).

---

## 5. Componentes Blazor y Experiencia UI/UX (`Ludeka.Web`)

### 5.1 Enrutamiento y Páginas
- `HomeDashboard.razor`: Página `@page "/"`.
  - Hero estilizado con buscador reactivo integrado y lema de marca.
  - 4 Secciones horizontales con cabecera de carril (Título, icono, contador y enlace "Ver todos &rarr;"):
    1. 🏆 **Top 20 Juegos de Mesa** &rarr; `/catalogo`
    2. 🎁 **Sorteos Activos & Promocionados** &rarr; `/radar` (o `/sorteos`)
    3. 🚀 **Novedades del Sector & Lanzamientos** &rarr; `/radar` (o `/novedades`)
    4. 🎪 **Próximos Eventos & Ferias Lúdicas** &rarr; `/eventos`
  - Clases Tailwind para desplazamiento táctil horizontal:
    `flex gap-4 overflow-x-auto snap-x snap-mandatory scrollbar-none pb-4 pt-1 px-1`
- `Home.razor`: Reasignado a `@page "/catalogo"`.

### 5.2 Limpieza de Navbar (`MainLayout.razor`)
- Supresión completa de las líneas 21 a 63 (selector de temas de 5 botones).
- Reemplazo de `href="/"` por `href="/catalogo"` en el elemento de menú Catálogo.

### 5.3 Enlace Canónico a BGG (`GameDetail.razor`)
- Incorporación de botón destacado en la cabecera:
  ```razor
  @if (Game.BggId > 0)
  {
      <a href="https://boardgamegeek.com/boardgame/@Game.BggId"
         target="_blank"
         rel="noopener noreferrer"
         class="px-3 py-1 rounded-lg bg-[var(--bg-surface-elevated)] hover:bg-[var(--bg-card)] text-[var(--text-primary)] border border-[var(--border-subtle)] font-bold text-xs transition-colors inline-flex items-center gap-1.5 shadow-sm"
         title="Ver ficha canónica en BoardGameGeek">
          <span>🌐</span> Ver en BoardGameGeek
      </a>
  }
  ```
