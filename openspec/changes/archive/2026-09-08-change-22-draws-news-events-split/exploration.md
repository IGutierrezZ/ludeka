# Exploración Técnica y Análisis de Dominio: change-22-draws-news-events-split

## 1. Contexto y Objetivos del Incremento

El Incremento 22 tiene como meta disolver el concepto monolítico de "Radar" (`/radar`) en la arquitectura de Ludeka y segregar sus contenidos en tres pilares y rutas independientes:
1. **🎁 Sorteos (`/sorteos`):** Radar especializado en sorteos de juegos de mesa externos, con gobernanza de sorteos promocionados (`IsPromoted`), expiración automática y subida/captura de imagen de portada.
2. **📰 Novedades (`/novedades`):** Línea temporal de anuncios, lanzamientos y primicias editoriales y de tiendas (`WeeklyRelease`), con capacidad para dar de alta lanzamientos con carátula propia o externa.
3. **🎪 Eventos Lúdicos (`/eventos` y `/admin/eventos`):** Gran calendario de ferias, festivales y convenciones del sector (ej. Festival de Córdoba, InterOcio, Essen SPIEL, Gen Con, DAU), con soporte para eventos futuros y pasados, y panel editorial de gestión.
4. **Reestructuración de Navegación:** Retirar el enlace único a "Radar" de la barra superior y del pie de página (`MainLayout.razor`), e incorporar los tres accesos directos y semánticos, manteniendo redirección o retrocompatibilidad para la ruta `/radar`.

---

## 2. Diagnóstico del Estado Actual del Código

### 2.1 Dominio (`Ludeka.Core`)
- **`BoardGameEvent.cs`:** Ya fue introducido en el Incremento 21 con validaciones de fechas (`EndDate >= StartDate`), constructor, método `Update(...)`, métodos auxiliares `IsOngoing(today)`, `IsPast(today)`, `DaysUntilStart(today)` y `GetFormattedDates()`. Está 100% listo para ser utilizado.
- **`Giveaway.cs`:** Ya dispone de `IsPromoted` y el método de mutación `SetPromoted(bool isPromoted)`.
- **`WeeklyRelease.cs`:** Cuenta con `Title`, `Publisher`, `ReleaseDate`, `GameId`, `CoverImageUrl`, `EstimatedPvp`, `IsReprint`, `Notes`.

### 2.2 Aplicación (`Ludeka.Application`)
- **Eventos:**
  - `IBoardGameEventRepository` define `GetUpcomingEventsAsync`, `GetAllEventsAsync`, `GetByIdAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`.
  - Existe `BoardGameEventDto` en `HomeDashboardDtos.cs`.
  - Se requiere un servicio de aplicación dedicado `IBoardGameEventService` / `BoardGameEventService` para centralizar la gestión editorial, mapeo a DTOs y validaciones de creación/edición/borrado.
- **Sorteos:**
  - `IGiveawayService` y `SqliteGiveawayRepository` gestionan sorteos pero no exponen formalmente la mutación directa de `SetPromotedAsync`.
  - Se incorporará `SetPromotedAsync(Guid giveawayId, bool isPromoted, CancellationToken ct = default)` en `IGiveawayService` y `GiveawayService`.
- **Novedades:**
  - `IWeeklyReleaseService` y `SqliteWeeklyReleaseRepository` exponen consultas de lanzamientos semanales.
  - Se añadirá soporte para la creación manual de lanzamientos por moderadores `CreateReleaseAsync(CreateWeeklyReleaseRequest request, CancellationToken ct = default)`.
- **Almacenamiento de Imágenes (`IImageStorageService`):**
  - Actualmente `IImageStorageService` contiene únicamente `SaveGameCoverAsync`.
  - Debe extenderse para permitir el almacenamiento físico estructurado de imágenes de carteles de eventos (`SaveEventPosterAsync`), sorteos (`SaveGiveawayImageAsync`) y novedades (`SaveReleaseImageAsync`) bajo directorios dedicados en `wwwroot/images/`.

### 2.3 Infraestructura (`Ludeka.Infrastructure`)
- **`LudekaDbContext`:** Ya contiene `DbSet<BoardGameEvent> BoardGameEvents` y migración en `SqliteSchemaMigrator`.
- **`SqliteBoardGameEventRepository`:** Implementa `IBoardGameEventRepository` con soporte para eventos próximos y todos los eventos.
- **`BoardGameEventSeeder`:** Precarga 5 grandes ferias de referencia (*Festival de Córdoba*, *InterOcio*, *Essen SPIEL*, *Gen Con*, *DAU Barcelona*).
- **`PhysicalFileImageStorageService`:** Se ampliará para soportar las subcarpetas de eventos, sorteos y lanzamientos con las mismas salvaguardas de tamaño (<= 5 MB) y formatos permitidos (.jpg, .png, .webp).

### 2.4 Interfaz de Usuario Blazor (`Ludeka.Web`)
- **`MainLayout.razor`:**
  - Reemplazar `<a href="/radar">Radar</a>` en el Navbar por accesos limpios: Sorteos (`/sorteos`), Novedades (`/novedades`) y Eventos (`/eventos`).
  - Actualizar el footer sustituyendo el enlace de Radar por los tres nuevos enlaces.
- **`Radar.razor`:**
  - Actualmente alberga tanto sorteos como novedades en dos pestañas bajo `/radar` y `/sorteos`.
  - Se transformará en la página especializada `Draws.razor` / `/sorteos` (manteniendo alias `@page "/radar"` para redirección transparente).
  - La pestaña de novedades se trasladará a una página dedicada `News.razor` (`@page "/novedades"`).
- **Nueva página `Events.razor` (`@page "/eventos"`):**
  - Calendario visual con dos pestañas: "Próximas Ferias & Festivales" y "Histórico de Eventos".
  - Tarjetas panorámicas con cartel, fechas formateadas, ciudad, días restantes y enlace web.
  - Botón de administración rápida si el usuario es moderador/fundador.
- **Nueva página / modal de administración `EventsManagement.razor` (`@page "/admin/eventos"`):**
  - Panel CRUD para la Mesa Fundadora y moderadores autorizados, con carga directa de carteles.

---

## 3. Matriz de Riesgos y Mitigaciones

| Riesgo | Impacto | Mitigación |
|---|---|---|
| Enlaces antiguos rotos hacia `/radar` | Medio | Mantener `@page "/radar"` como alias en `/sorteos` con enlaces cruzados a novedades y eventos. |
| Carga de imágenes sin autenticación | Alto | Validar permisos de `FoundingTeam` o `Moderator` en endpoints y servicios antes de permitir almacenar ficheros físicos. |
| Fragmentación de navegación en pantallas móviles | Medio | Diseñar accesos compactos en la cabecera móvil y adaptar el menú inferior para que no desborde horizontalmente. |
| Inconsistencias de ordenación en sorteos | Bajo | Aplicar orden determinista en `GiveawayService`: `IsPromoted DESC`, `DeadlineAt ASC` para activos. |
