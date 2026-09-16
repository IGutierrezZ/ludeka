# Propuesta de Cambio: change-22-draws-news-events-split

## 1. Resumen Ejecutivo
El **Incremento 22** ejecuta la desintegración formal del módulo monolítico de "Radar" (`/radar`) para transformarlo en tres verticales independientes, especializadas y de alto valor lúdico para la comunidad de Ludeka:
1. **🎁 Sorteos (`/sorteos`):** Radar comunitario de sorteos activos y finalizados, con soporte nativo para sorteos destacados/patrocinados (`IsPromoted`), expiración automática, control de roles de moderación y visualización o subida de carátula/imagen original.
2. **📰 Novedades (`/novedades`):** Muro y cronología de anuncios, lanzamientos editoriales y primicias de tiendas (`WeeklyRelease`), permitiendo a la moderación registrar lanzamientos con precios sugeridos, fecha exacta y portadas.
3. **🎪 Eventos Lúdicos (`/eventos` y `/admin/eventos`):** Gran calendario de ferias, festivales y convenciones del sector lúdico nacional e internacional (Córdoba, InterOcio, Essen SPIEL, Gen Con, DAU), con separación entre eventos futuros y archivo histórico, y panel editorial de administración para la Mesa Fundadora.
4. **Reestructuración de la Navegación:** Limpieza de la barra superior y footer en `MainLayout.razor`, incorporando accesos claros a `/sorteos`, `/novedades` y `/eventos`, y manteniendo retrocompatibilidad mediante alias para `/radar`.

---

## 2. Justificación y Valor para el Ecosistema
1. **Claridad Conceptual y Menor Fricción:** Mezclar sorteos de Instagram con lanzamientos comerciales de novedades y ferias generaba sobrecarga y confusión en el usuario. Cada vertical responde a una intención de búsqueda distinta (ganar un juego, conocer qué sale a la venta este viernes, o planificar un viaje a una feria).
2. **Impulso a Sorteos Promocionados y Colaboraciones:** El flag `IsPromoted` permite a editoriales y tiendas destacar sorteos especiales en la cabecera tanto del carril de la home como de `/sorteos`, dinamizando el ecosistema y aportando valor comercial a Ludeka.
3. **Padrón Oficial de Eventos y Citas del Sector:** Los eventos de juegos de mesa carecen en español de un calendario limpio, mobile-first y sin spam. La sección `/eventos` se convertirá en la referencia para saber qué ferias se celebran y cuántos días faltan para asistir.
4. **Soporte Físico de Imágenes:** Extender `IImageStorageService` para almacenar carteles y fotos de eventos y sorteos evita depender exclusivamente de enlaces efímeros externos que puedan romperse con el tiempo.

---

## 3. Alcance de la Propuesta por Capas

### 3.1 Dominio (`Ludeka.Core`)
- **`BoardGameEvent`:** Entidad ya disponible con validaciones, cálculo de estado (`IsOngoing`, `IsPast`, `DaysUntilStart`) y formateo en español (`GetFormattedDates`).
- **`Giveaway`:** Soporta `IsPromoted` y método `SetPromoted(bool)`.
- **`WeeklyRelease`:** Entidad con `Title`, `Publisher`, `ReleaseDate`, `CoverImageUrl`, `EstimatedPvp`, `IsReprint`, `Notes`.

### 3.2 Aplicación (`Ludeka.Application`)
- **Gestión de Eventos:**
  - `CreateBoardGameEventRequest` y `UpdateBoardGameEventRequest` en `HomeDashboardDtos.cs` o nuevo `EventDtos.cs`.
  - Contrato `IBoardGameEventService` con métodos:
    - `GetUpcomingEventsAsync(int limit = 50, CancellationToken ct = default)`
    - `GetPastEventsAsync(int limit = 50, CancellationToken ct = default)`
    - `GetByIdAsync(Guid id, CancellationToken ct = default)`
    - `CreateEventAsync(CreateBoardGameEventRequest request, CancellationToken ct = default)`
    - `UpdateEventAsync(Guid id, UpdateBoardGameEventRequest request, CancellationToken ct = default)`
    - `DeleteEventAsync(Guid id, CancellationToken ct = default)`
  - Implementación `BoardGameEventService`.
- **Gestión de Sorteos:**
  - Añadir a `IGiveawayService`:
    - `SetPromotedAsync(Guid giveawayId, bool isPromoted, CancellationToken ct = default)`
- **Gestión de Novedades:**
  - Crear `CreateWeeklyReleaseRequest` en `CommunityDtos.cs`.
  - Añadir a `IWeeklyReleaseService`:
    - `CreateReleaseAsync(CreateWeeklyReleaseRequest request, CancellationToken ct = default)`
- **Almacenamiento de Imágenes (`IImageStorageService`):**
  - Añadir métodos:
    - `SaveEventPosterAsync(string slugOrId, Stream contentStream, string originalFileName, string contentType, CancellationToken ct = default)`
    - `SaveCommunityImageAsync(string subfolder, string slugOrId, Stream contentStream, string originalFileName, string contentType, CancellationToken ct = default)`

### 3.3 Infraestructura (`Ludeka.Infrastructure`)
- Ampliar `PhysicalFileImageStorageService` para persistir imágenes en `wwwroot/images/events/`, `wwwroot/images/giveaways/` y `wwwroot/images/releases/` con validación de extensiones seguras y 5 MB de tamaño límite.
- Registro de `IBoardGameEventService` en `DependencyInjection.cs`.

### 3.4 Presentación Web (`Ludeka.Web`)
- **`MainLayout.razor`:**
  - Reemplazar el enlace `/radar` por tres enlaces: `Sorteos` (`/sorteos`), `Novedades` (`/novedades`) y `Eventos` (`/eventos`).
  - Actualizar el footer con las tres rutas independientes.
- **`Draws.razor` (`/sorteos` y `@page "/radar"`):**
  - Página dedicada a Sorteos con filtros por activos / finalizados.
  - Tarjetas con badge "⭐ Promocionado", orden prioritario y botón para que moderadores puedan conmutar `IsPromoted` en un clic.
  - Modal para proponer / crear sorteo con opción de marcar promocionado para moderadores.
- **`News.razor` (`/novedades`):**
  - Muro editorial con los lanzamientos semanales y primicias de tiendas.
  - Filtros por editorial y botón para moderadores "➕ Añadir Novedad" con modal.
- **`Events.razor` (`/eventos`):**
  - Calendario con pestañas "Próximos Eventos" y "Eventos Pasados".
  - Tarjetas ricas con cartel, fechas, ciudad, contador de días y enlace a la web oficial.
  - Acceso directo para moderadores al panel de administración.
- **`EventsManagement.razor` (`/admin/eventos`):**
  - Panel editorial para la Mesa Fundadora y moderadores con listado, alta, edición, eliminación y subida directa de cartel promocional.

---

## 4. Estrategia de Pruebas Unitarias
- `BoardGameEventServiceTests`: Creación, actualización, borrado, validaciones de fechas y segregación entre próximos y pasados.
- `GiveawayServiceTests`: Mutación de `SetPromotedAsync` y verificación de orden prioritario.
- `WeeklyReleaseServiceTests`: Creación de novedades y consulta cronológica.
- `ImageStorageServiceTests`: Validación de almacenamiento físico para eventos y comunidad.
- Verificación del 100% de la suite de pruebas en verde (`dotnet test`).
