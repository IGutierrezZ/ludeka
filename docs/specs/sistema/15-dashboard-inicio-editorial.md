# 15. Dashboard de Inicio Editorial, Desacople de Catálogo y Enlace Canónico BGG

> **Nota (INC-35):** los 4 carriles descritos aquí se renderizan ahora mediante **componentes dedicados** (`Components/Home/`) con fallback de imagen por dominio, tokens `.rail-card` y microinteracciones hover/foco. La fuente de datos, ordenación y caché de este módulo siguen vigentes; el comportamiento de presentación actual está en el [módulo 23 — Portada Editorial](file:///c:/repos/Ludeka/docs/specs/sistema/23-portada-editorial.md).
>
> **Nota (INC-36):** el catálogo (`/catalogo`) adopta la cabecera editorial compartida `PageHeaderEditorial`, el buscador en bloque propio (`max-w-xl`) y la tira de filtros con la clase real `scrollbar-none`; el detalle transversal de tokens y componentes está en el [módulo 24](file:///c:/repos/Ludeka/docs/specs/sistema/24-fundaciones-editoriales-y-componentes.md).

## 1. Visión General y Propósito
El módulo de **Dashboard de Inicio Editorial** transforma la experiencia de bienvenida en la ruta raíz (`/`) de Ludeka en una portada viva inspirada en los mejores referentes editoriales de la cultura lúdica contemporánea.

Sus cuatro pilares fundamentales son:
1. **Página de Inicio Multi-Carril (`/`):** Sustituye la vista tabular/grid monolítica por cuatro carriles horizontales temáticos con deslizamiento táctil *mobile-first* (*snap-x snap-mandatory*), diseñados para enganchar al jugador en los primeros 3 segundos.
2. **Desacople del Catálogo a `/catalogo`:** La experiencia completa de catálogo exhaustivo con filtrado facetado por ADN lúdico (Parejas, Familiar, Solitario, Rápidas, Base vs. Expansiones), duración y paginación se consolida en su propia ruta semántica (`/catalogo`).
3. **Limpieza del Navbar Superior:** Se retira el conmutador de temas redundante de la barra de navegación superior (Header/Navbar), reduciendo el ruido visual en la cabecera. La preferencia de paleta de colores se gestiona centralizadamente desde el perfil personal del usuario (`/perfil`) y se sincroniza en segundo plano.
4. **Enlace Canónico a BoardGameGeek:** Cada ficha de juego (`GameDetail.razor`) expone de forma directa y visible el botón `[ 🌐 Ver en BoardGameGeek ]` hacia `https://boardgamegeek.com/boardgame/{BggId}` abriendo en una nueva pestaña segura (`target="_blank" rel="noopener noreferrer"`).

---

## 2. Arquitectura de los Cuatro Carriles Editoriales

```mermaid
graph TD
    Home[HomeDashboard.razor (/)] --> Svc[IHomeDashboardService]
    Svc --> Cache[CachedHomeDashboardService (IMemoryCache)]
    Cache --> Logic[HomeDashboardService]
    Logic --> C1[Carril 1: Top 20 Juegos]
    Logic --> C2[Carril 2: Sorteos Activos]
    Logic --> C3[Carril 3: Novedades en Tiendas]
    Logic --> C4[Carril 4: Ferias y Eventos]
```

### 2.1 Carril 1 — Top 20 Juegos de Mesa
- **Fuente de Datos:** `ICatalogService.GetCatalogAsync(criteria: default, page: 1, pageSize: 20)`.
- **Criterio de Ordenación:** Ranking BGG oficial (`BggRank` ascendente) y valoración comunitaria ponderada.
- **Presentación UI:** Carrusel horizontal de tarjetas compactas (`w-40 sm:w-48`) con carátula con esquinas redondeadas, nota media destacada (`★ 8.6`), año de publicación, título en español y semáforo de comensales ("Ideal 2J").

### 2.2 Carril 2 — Sorteos Activos & Promocionados
- **Fuente de Datos:** `IGiveawayService.GetGiveawaysAsync(includeExpired: false)`.
- **Criterio de Ordenación:** Prioridad absoluta para sorteos con `IsPromoted == true` situados en cabecera de carril; seguidamente ordenados por fecha límite más cercana (`DeadlineAt` ascendente).
- **Presentación UI:** Tarjeta con distintivo visual dorado `⭐ Promocionado`, contador dinámico de tiempo restante ("Finaliza en X días"), título del sorteo, organizador y botón saliente hacia la publicación de origen.

### 2.3 Carril 3 — Novedades del Sector & Lanzamientos
- **Fuente de Datos:** `IWeeklyReleaseService.GetReleasesAsync(fromDate: null)`.
- **Criterio de Ordenación:** Fecha de lanzamiento cronológica descendente (`ReleaseDate` descendente).
- **Presentación UI:** Tarjetas compactas con badge `🆕 Novedad` o `🔄 Reimpresión`, fecha de disponibilidad en tiendas, editorial y PVP recomendado aproximado.

### 2.4 Carril 4 — Grandes Citas & Ferias del Sector
- **Fuente de Datos:** `IBoardGameEventRepository.GetUpcomingEventsAsync(limit: 20)`.
- **Criterio de Ordenación:** Fecha de inicio cronológica ascendente (`StartDate` ascendente).
- **Presentación UI:** Tarjetas panorámicas con cartel promocional oficial, rango de fechas en español (ej. "9-12 Oct 2026"), badge relativo ("En 18 días" / "¡En curso!"), ciudad/recinto y botón saliente a la web oficial del evento.

---

## 3. Modelo de Dominio (`Ludeka.Core`)

### 3.1 Entidad `BoardGameEvent`
- **Ruta:** `src/Ludeka.Core/Entities/BoardGameEvent.cs`.
- **Propiedades:**
  - `Guid Id`: Identificador único.
  - `string Title`: Nombre del evento o feria.
  - `string Description`: Resumen y características de la jornada.
  - `string ImageUrl`: Cartel o fotografía oficial.
  - `DateOnly StartDate` y `DateOnly EndDate`: Fechas de celebración con validación invariante `EndDate >= StartDate`.
  - `string Location`: Ciudad y recinto (ej. "Palacio de la Merced, Córdoba").
  - `string? WebsiteUrl`: Enlace a la web oficial de venta de entradas o programa.
  - `string Organizer`: Entidad organizadora (ej. "Jugamos Tod@s", "Merz Verlag").
  - `bool IsOfficial`: Indicador de evento oficial del sector.
  - `DateTimeOffset CreatedAt` y `DateTimeOffset? UpdatedAt`: Marcas temporales UTC.
- **Métodos de Dominio:**
  - `IsOngoing(DateOnly today)`: Retorna `true` si la fecha se encuentra entre `StartDate` y `EndDate`.
  - `IsPast(DateOnly today)`: Retorna `true` si la fecha ya concluyó.
  - `DaysUntilStart(DateOnly today)`: Diferencia de días hasta el arranque.
  - `GetFormattedDates()`: Formato editorial condensado en castellano.

### 3.2 Ampliación de `Giveaway`
- Incorporación del campo `public bool IsPromoted { get; private set; } = false;` y método `SetPromoted(bool isPromoted)` para gobernar la precedencia en los carruseles comunitarios.

---

## 4. Capa de Aplicación e In-Memory Caching (`Ludeka.Application`)

- **DTOs (`HomeDashboardDtos.cs`):**
  - `BoardGameEventDto`: Encapsula datos de eventos con cálculo de tiempos relativos y formatos editoriales.
  - `HomeDashboardDto`: Agregado inmutable de las 4 colecciones de los carriles.
- **Servicio Base (`HomeDashboardService.cs`):**
  - Orquesta las consultas sobre catálogo, sorteos, novedades y eventos, aplicando las reglas de ordenación y filtrado.
- **Caché Nivel 1 (`CachedHomeDashboardService.cs`):**
  - Decorador sobre `IMemoryCache` con clave `"home:dashboard:editorial"`.
  - Política de expiración deslizante de 5 minutos y absoluta de 15 minutos.
  - Método `Invalidate()` para purga en tiempo real.

---

## 5. Persistencia e Infraestructura (`Ludeka.Infrastructure`)

- **Contexto EF Core (`LudekaDbContext`):**
  - Mapeo de `DbSet<BoardGameEvent> BoardGameEvents` con índices en `StartDate` e `IsOfficial`.
- **Reconciliación Defensiva (`SqliteSchemaMigrator`):**
  - Inserción idempotente de la columna `IsPromoted` en `Giveaways`.
  - Creación condicional de la tabla `BoardGameEvents` e índices asociados.
- **Repositorio SQLite (`SqliteBoardGameEventRepository`):**
  - Consultas optimizadas con EF Core, completando eventos futuros y recientes para garantizar que el carril nunca quede desierto.
- **Semillado Inicial (`BoardGameEventSeeder`):**
  - Precarga de los cinco grandes hitos del calendario lúdico: *Festival Internacional de Córdoba*, *SPIEL Essen*, *DAU Barcelona*, *Feria InterOcio Madrid* y *Gen Con Indianapolis*.

---

## 6. Componentes Razor Blazor (`Ludeka.Web`)

- `HomeDashboard.razor` (`@page "/"`): Portada editorial con buscador rápido, botones de salto temático y 4 carriles horizontales con *touch snap*. *(Desde INC-35 las tarjetas se renderizan vía `Components/Home/*Card.razor` con fallback de imagen y microinteracciones — ver [módulo 23](file:///c:/repos/Ludeka/docs/specs/sistema/23-portada-editorial.md)).*
- `Home.razor` (`@page "/catalogo"`): Catálogo desacoplado con soporte para query param `?q=...` y filtros facetados de situación real. INC-36: cabecera editorial compartida (`PageHeaderEditorial` con badge «Catálogo Colaborativo» e icono `dices`; h1 único «Descubre tu próxima partida.» con punto terracota, sin la variante centrada anterior), buscador `CatalogSearchBar` en bloque propio (`max-w-xl`), tira de filtros con la clase real `scrollbar-none` (la clase muerta `no-scrollbar` ya no existe) y tarjetas `.game-card-editorial` conservadas.
- `MainLayout.razor`: Cabecera optimizada sin selector de temas en el Navbar y enlace Catálogo actualizado a `/catalogo`.
- `GameDetail.razor`: Botones `[ 🌐 Ver en BoardGameGeek ]` en la barra superior de acciones y en la sección de metadatos de la cabecera.
