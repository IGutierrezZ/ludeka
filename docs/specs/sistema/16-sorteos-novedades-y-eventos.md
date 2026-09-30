# 16. Módulo de Sorteos, Novedades y Grandes Eventos Lúdicos

> **Estado:** Implementado y Verificado (INC-36, INC-79, INC-87)  
> **Incrementos SDD:** `change-22-draws-news-events-split` (INC-22) · [`rediseno-paginas-editoriales` (INC-36)](file:///c:/repos/Ludeka/openspec/changes/archive/2026-09-10-rediseno-paginas-editoriales/proposal.md) · `change-79-giveaways-compact-card-and-detail-page` (INC-79) · [`change-87-novedades-eventos-fichas-y-tarjetas` (INC-87)](file:///f:/repos/Ludeka/docs/increments/archive/inc-87-novedades-eventos-fichas-y-tarjetas.md)  
> **Rutas:** `/sorteos` (alias `/radar`), `/sorteos/{id}` (alias `/sorteo/{id}`), `/novedades`, `/novedades/{id}` (alias `/novedad/{id}`), `/eventos`, `/eventos/{id}` (alias `/evento/{id}`), `/admin/eventos`  
> **Tests:** suite total **2.139** en verde al 100% (incluye pruebas unitarias de `WeeklyReleaseService`, pruebas de contrato de páginas `ReleaseDetailPage` y `EventDetailPage`, y contratos de marcado anti-CLS).

---

## 1. Propósito y Visión del Módulo

Este módulo segrega el ecosistema informativo de la comunidad de juegos de mesa en tres verticales independientes, accesibles y plenamente moderables:
1. **Radar de Sorteos (`/sorteos` y `/sorteos/{id}`):** Centraliza sorteos de juegos de mesa en redes sociales (Instagram, Twitter/X, YouTube, Comunidad), control de expiración automática, gobernanza de sorteos destacados (`IsPromoted`), ficha de detalle interactiva con CTA oficial y edición para moderadores.
2. **Calendario de Estrenos y Novedades (`/novedades` y `/novedades/{id}`):** Línea temporal de anuncios, preventas y lanzamientos semanales (`WeeklyRelease`), con buscador, cuadrícula de catálogo responsive, ficha de detalle dedicada con enlace a la fuente original (`SourceUrl`), vinculación a la ficha de juego y panel modal de edición/borrado y subida WebP a Cloudflare R2.
3. **Grandes Citas y Convenciones Lúdicas (`/eventos` y `/eventos/{id}`):** Directorio oficial de ferias, festivales y macro-eventos del sector de los juegos de mesa en España y el circuito internacional, con cuadrícula compacta de catálogo, ficha de detalle con metadatos territoriales (`CountryCatalog`), botón CTA a la web oficial y panel modal de edición/borrado y subida de cartel WebP a Cloudflare R2.

---

## 2. Modelo de Dominio (`Ludeka.Core`)

### 2.1 Entidad `BoardGameEvent`
Ubicación: `src/Ludeka.Core/Entities/BoardGameEvent.cs`
- **Atributos:**
  - `Guid Id`: Identificador único del evento.
  - `string Title`: Título oficial de la feria o festival (obligatorio).
  - `string Description`: Descripción y aspectos destacados de la edición.
  - `string ImageUrl`: Cartel promocional u oficial (obligatorio).
  - `DateOnly StartDate` y `DateOnly EndDate`: Fechas de celebración con validación invariante `EndDate >= StartDate`.
  - `string Location`: Ciudad y recinto (ej. "Córdoba — Palacio de la Merced").
  - `string? WebsiteUrl`: Enlace a la web oficial de venta de entradas o programa.
  - `string Organizer`: Entidad organizadora (ej. "Jugamos Tod@s", "IFEMA").
  - `bool IsOfficial`: Distintivo de gran cita oficial del calendario.
  - `string Country`: País territorial del evento (con soporte para "Internacional").
- **Métodos de Dominio:**
  - `IsOngoing(DateOnly today)`: Determina si el evento está en curso.
  - `IsPast(DateOnly today)`: Determina si el evento ya concluyó.
  - `DaysUntilStart(DateOnly today)`: Calcula los días exactos hasta el inicio.
  - `GetFormattedDates()`: Genera formato editorial en español (ej. "11-13 Oct 2026").
  - `Update(...)`: Mutación controlada con validación y marca temporal `UpdatedAt`.

### 2.2 Entidad `Giveaway`
Ubicación: `src/Ludeka.Core/Entities/Giveaway.cs`
- **Atributos Clave:** `IsPromoted` (booleano), `ThumbnailUrl`, `DeadlineAt`, `Platform`, `IsCommunityExclusive`, `Country`, `GameId`, `GameTitle`.
- **Métodos:**
  - `SetPromoted(bool isPromoted)` para conmutar estado de patrocinio con marca `UpdatedAt`.
  - `Update(...)` (INC-79) para mutación determinista de todos los campos con normalización de país y actualización de `UpdatedAt`.

### 2.3 Entidad `WeeklyRelease`
Ubicación: `src/Ludeka.Core/Entities/WeeklyRelease.cs`
- **Atributos Clave:** `Title`, `Publisher`, `ReleaseDate`, `EstimatedPvp`, `IsReprint`, `Notes`, `CoverImageUrl`, `GameId`, `SourceUrl`.
- **Métodos:**
  - `Update(...)` (INC-87): Mutación determinista de todos los campos del lanzamiento, incluyendo `SourceUrl`, vinculación `GameId` y actualización automática de `UpdatedAt`.

---

## 3. Capa de Aplicación (`Ludeka.Application`)

### 3.1 Contratos y Servicios
- **`IBoardGameEventService` / `BoardGameEventService`:**
  - `GetUpcomingEventsAsync(int limit = 50, string? country = null)`: Retorna eventos donde `EndDate >= Hoy` ordenados por `StartDate` ascendente.
  - `GetPastEventsAsync(int limit = 50, string? country = null)`: Retorna eventos concluidos (`EndDate < Hoy`) ordenados por `EndDate` descendente.
  - `GetByIdAsync(Guid id)`: Retorna el detalle completo de un evento por identificador.
  - `CreateEventAsync(CreateBoardGameEventRequest request)`: Alta de eventos con invariantes.
  - `UpdateEventAsync(Guid id, UpdateBoardGameEventRequest request)`: Actualización de datos protegida por permisos.
  - `DeleteEventAsync(Guid id)`: Eliminación del evento protegida por permisos.
- **`IGiveawayService` / `GiveawayService`:**
  - `GetGiveawaysAsync(bool includeExpired, string? country)`: Orden prioritario estricto: `IsPromoted DESC`, seguido de `DeadlineAt ASC`.
  - `GetByIdAsync(Guid id)`: Consulta individual de sorteo para la ficha de detalle.
  - `UpdateGiveawayAsync(UpdateGiveawayRequest request)` (INC-79): Actualización de sorteo con verificación de permisos de moderación (`CanApproveMedia` o Mesa Fundadora).
  - `DeleteGiveawayAsync(Guid id)` (INC-79): Eliminación controlada de sorteo.
  - `SetPromotedAsync(Guid id, bool isPromoted)`: Mutación inmediata del flag de patrocinio.
- **`IWeeklyReleaseService` / `WeeklyReleaseService`:**
  - `GetReleasesAsync(int limit = 50)`: Retorna lanzamientos ordenados por fecha descendente.
  - `GetByIdAsync(Guid id)` (INC-87): Retorna el detalle completo de un lanzamiento por identificador.
  - `CreateReleaseAsync(CreateWeeklyReleaseRequest request)`: Alta manual por moderación con soporte para `SourceUrl`.
  - `UpdateReleaseAsync(Guid id, UpdateWeeklyReleaseRequest request)` (INC-87): Actualización con validación de moderación (`CanApproveMedia` o Mesa Fundadora).
  - `DeleteReleaseAsync(Guid id)` (INC-87): Eliminación con validación de moderación.
- **`IImageStorageService`:**
  - `UploadOptimizedImageAsync(...)`: Optimización WebP (calidad 85) y almacenamiento en Cloudflare R2 (`giveaways/{id:N}/cover.webp`, `releases/{id:N}/cover.webp` y `events/{id:N}/cover.webp`).
  - Fallback local o simulado en entornos de desarrollo sin credenciales Cloudflare.

---

## 4. Componentes y Vistas Blazor (`Ludeka.Web`)

1. **`MainLayout.razor`:**
   - Barra de navegación y menú móvil con accesos a `/sorteos`, `/novedades` y `/eventos`.
   - Botón directo de administración `/admin/eventos` para usuarios con roles `FoundingTeam` o `Moderator`.
2. **`Radar.razor` (`/sorteos` y alias `/radar`):**
   - Cuadrícula responsive de catálogo (`grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-3 sm:gap-4`) coherente con la sección de juegos.
   - Tarjetas `GiveawayCard` compactas con `rail-cover--square` (240×240) y enlace a `/sorteos/{id}`.
3. **`GiveawayDetail.razor` (`/sorteos/{Id:guid}` y `/sorteo/{Id:guid}`):**
   - Ficha inteligente de detalle con cartel en alta resolución, fallback anti-CLS por dominio (`DefaultImageDomain.Sorteo`).
   - Botón CTA de participación externa hacia enlace oficial (`target="_blank" rel="noopener noreferrer"`).
   - Metadatos territoriales dinámicos, plataforma, plazos y tarjeta del juego asociado si existe.
   - Modal de edición `EditorialModal` para moderadores con subida WebP a Cloudflare R2 y modal de confirmación de borrado.
4. **`News.razor` (`/novedades`):**
   - Cuadrícula responsive de catálogo idéntica (`grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-3 sm:gap-4`).
   - Tarjetas compactas con portada 1:1 (`rail-cover--square`, 240×240), badges de novedad/reimpresión y fecha, enlaces directos a `/novedades/{id}` y botón de acceso a la noticia original.
   - Modal editorial de alta rápida con soporte para `SourceUrl`.
5. **`ReleaseDetail.razor` (`/novedades/{Id:guid}` y `/novedad/{Id:guid}`) (INC-87):**
   - Ficha de detalle de lanzamiento editorial con carátula en alta definición y fallback anti-CLS (`DefaultImageDomain.Novedad`).
   - Metadatos editoriales: editorial/distribuidora, fecha de salida, PVP estimado, condición de reimpresión y notas.
   - Botón CTA prominente hacia la noticia/fuente original (`SourceUrl`).
   - Bloque integrado de juego vinculado con carátula, año, diseñador y enlace a su ficha si está en catálogo.
   - Modales contextuales de edición y borrado para moderadores con subida WebP a Cloudflare R2 (`releases/{id:N}/cover.webp`).
6. **`Events.razor` (`/eventos`):**
   - Cuadrícula responsive de catálogo (`grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-3 sm:gap-4`).
   - Tarjetas compactas con cartel 1:1 (`rail-cover--square`, 240×240), badges de país, estado temporal y fechas, enlace a la ficha `/eventos/{id}` y botón a la web oficial.
7. **`EventDetail.razor` (`/eventos/{Id:guid}` y `/evento/{Id:guid}`) (INC-87):**
   - Ficha de detalle con cartel oficial y fallback anti-CLS (`DefaultImageDomain.Evento`).
   - Metadatos de la cita: país con bandera dinámica (`CountryCatalog`), fechas, oficialidad, localización/sede y entidad organizadora.
   - Botón CTA destacado a la web oficial del evento (`WebsiteUrl`).
   - Modales contextuales de edición y borrado para moderadores con subida WebP a Cloudflare R2 (`events/{id:N}/cover.webp`).
8. **Carriles de Portada (`HomeReleaseCard.razor` y `HomeEventCard.razor`):**
   - Homogeneizados con `HomeGameCard` y `HomeGiveawayCard`: anchura compacta (`w-[28vw] min-w-[105px] max-w-[125px] sm:w-44 md:w-48`) con carátula cuadrada `rail-cover--square` (192×192) y enlace directo a la ficha individual correspondiente.
   - Activado el enlace del carril `Href="/eventos"` en `HomeDashboard.razor`.
