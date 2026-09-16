# Propuesta de Cambio: change-21-home-dashboard

## 1. Resumen Ejecutivo
El Incremento 21 transforma la experiencia de bienvenida de Ludeka dotando a la ruta raíz (`/`) de un **Dashboard Editorial Vivo** al estilo de las mejores plataformas de recomendación cultural (Letterboxd/streaming lúdico). La portada pasa a organizarse en cuatro carriles horizontales fluidos (*mobile-first* con desplazamiento táctil por *snap*): **Top 20 Mejores Juegos**, **Sorteos Activos y Destacados**, **Novedades del Sector** y **Próximos Eventos Lúdicos**.

Para optimizar la arquitectura de información, el catálogo general y su potente motor de filtros por ADN lúdico se desacoplan hacia su propia ruta dedicada (`/catalogo`). En la barra superior (`MainLayout.razor`) se retira el selector de temas de colores redundante (centralizado en el perfil de usuario), aligerando la carga visual del encabezado. Finalmente, en la ficha de juego (`GameDetail.razor`) se añade un enlace canónico directo y visible hacia BoardGameGeek (`[ 🌐 Ver en BoardGameGeek ]`).

---

## 2. Justificación y Valor para el Ecosistema
1. **Impacto Visual y Retención en 3 Segundos:** La portada actual, centrada puramente en un grid de catálogo denso, no comunica la vibrante actividad de la comunidad ni las oportunidades inmediatas (sorteos a punto de expirar, primicias de tiendas o eventos del mes).
2. **Ergonomía Mobile-First Real:** Los carriles con desplazamiento horizontal y fijación táctil (*touch snap*) permiten ojear decenas de títulos, eventos y sorteos con el pulgar sin saturar verticalmente la pantalla del móvil.
3. **Claridad en la Navegación:** Separar la portada editorial (`/`) del catálogo exhaustivo (`/catalogo`) clarifica el propósito de cada vista y mejora la indexación y usabilidad en navegadores y motores de búsqueda.
4. **Respeto a las Fuentes Canónicas:** Incorporar el enlace directo a BoardGameGeek en la cabecera de la ficha reconoce la fuente de datos internacional y ofrece al usuario avanzado acceso a foros, archivos y variantes oficiales en un solo clic.
5. **Rendimiento Instantáneo (< 100ms):** La combinación de consultas acotadas (`Take(20)`) y una caché en memoria de dos capas (`CachedHomeDashboardService`) garantiza una respuesta fulgurante tanto en SSR como en conexiones móviles intermitentes.

---

## 3. Alcance de la Propuesta por Capas

### 3.1 Dominio (`Ludeka.Core`)
- **Entidad `Giveaway`:**
  - Incorporar la propiedad `public bool IsPromoted { get; private set; } = false;` y el método de mutación `SetPromoted(bool isPromoted)`.
  - Actualizar constructores pertinentes manteniendo compatibilidad.
- **Entidad `BoardGameEvent` (Nueva):**
  - Ubicación: `src/Ludeka.Core/Entities/BoardGameEvent.cs`.
  - Propiedades: `Guid Id`, `string Title`, `string Description`, `string ImageUrl`, `DateOnly StartDate`, `DateOnly EndDate`, `string Location`, `string? WebsiteUrl`, `string Organizer`, `bool IsOfficial`, `DateTimeOffset CreatedAt`.
  - Métodos y validaciones de dominio: validación de fechas (`EndDate >= StartDate`), longitud de textos y generación de estado `IsUpcoming` o `DaysUntilStart`.

### 3.2 Aplicación (`Ludeka.Application`)
- **DTOs (`HomeDashboardDtos.cs` & `CommunityDtos.cs`):**
  - Actualizar `GiveawayDto` con `bool IsPromoted`.
  - `BoardGameEventDto`: Datos enriquecidos para tarjetas de eventos (fechas formateadas, estado relativo "En X días", ciudad, enlace).
  - `HomeDashboardDto`: Agregado de las 4 colecciones (`TopGames`, `Giveaways`, `RecentReleases`, `UpcomingEvents`).
- **Contratos:**
  - `IBoardGameEventRepository`: `GetUpcomingEventsAsync(int limit = 20, CancellationToken ct = default)` y `GetByIdAsync`.
  - `IHomeDashboardService`: `GetDashboardDataAsync(CancellationToken ct = default)`.
- **Servicio y Caché:**
  - `HomeDashboardService`: Orquesta la carga de los 4 carriles priorizando juegos por ranking BGG / valoración Ludeka, sorteos con `IsPromoted == true` seguidos de fecha límite inminente, novedades cronológicas descendentes y eventos cronológicos ascendentes.
  - `CachedHomeDashboardService`: Decorador con `IMemoryCache` (TTL de 10 minutos) con invalidación ante nuevos eventos o sorteos.

### 3.3 Infraestructura (`Ludeka.Infrastructure`)
- **Persistencia en SQLite (`LudekaDbContext`):**
  - `DbSet<BoardGameEvent> BoardGameEvents` con índices en `StartDate` y `IsOfficial`.
- **Actualización Defensiva de Esquema (`SqliteSchemaMigrator`):**
  - Inclusión de columna `IsPromoted` en la tabla `Giveaways`.
  - Creación de tabla `BoardGameEvents` con sus índices si no existe.
- **Repositorio SQLite:**
  - `SqliteBoardGameEventRepository`: Consultas optimizadas con EF Core indexadas por fecha.
- **Semillado Inicial (`BoardGameEventSeeder`):**
  - Precarga de los grandes festivales y ferias del circuito lúdico:
    - *Festival Internacional de Juegos de Córdoba* (Córdoba, España).
    - *InterOcio* (IFEMA Madrid, España).
    - *Essen SPIEL* (Essen, Alemania).
    - *Gen Con* (Indianápolis, EE.UU.).
    - *DAU Barcelona* (Barcelona, España).
- **Inyección de Dependencias:** Registro en `DependencyInjection.cs`.

### 3.4 Presentación Web (`Ludeka.Web`)
- **Página de Portada Editorial (`HomeDashboard.razor`):**
  - Ruta `@page "/"`.
  - Hero editorial minimalista con buscador rápido asistido y enlaces destacados.
  - **Carril 1 (Top 20 Juegos):** Carrusel horizontal con tarjetas compactas (`DashboardGameCard.razor` / estilos optimizados), nota media, año y semáforo de comensales.
  - **Carril 2 (Sorteos Destacados):** Carrusel de sorteos con badge "⭐ Promocionado" y cuenta atrás.
  - **Carril 3 (Novedades del Sector):** Carrusel de estrenos de tiendas y editoriales con indicación de novedad/reimpresión y fecha.
  - **Carril 4 (Próximos Eventos):** Tarjetas panorámicas de convenciones y ferias con fechas formateadas y enlace oficial.
  - Controles de scroll táctil suave y flechas accesibles opcionales para navegación de escritorio.
- **Página de Catálogo Exhaustivo (`CatalogPage.razor`):**
  - Reubicación de la vista completa de filtros y cuadrícula en `@page "/catalogo"`.
- **Limpieza de Cabecera en `MainLayout.razor`:**
  - Retirada del bloque selector de temas en el Navbar superior.
  - Actualización del enlace "Catálogo" para apuntar a `/catalogo`.
  - Logo enlazando a `/` (Portada).
- **Ficha de Juego (`GameDetail.razor`):**
  - Enlaces de retorno actualizados a `/catalogo`.
  - Inclusión del botón visible `[ 🌐 Ver en BoardGameGeek ]` apuntando a `https://boardgamegeek.com/boardgame/{BggId}` (`target="_blank" rel="noopener noreferrer"`).

---

## 4. Criterios de Aceptación Clave

1. **Acceso a la Portada Editorial (`/`):** Al acceder a la raíz del sitio, el usuario visualiza el Dashboard Editorial con los 4 carriles funcionales y sin la cuadrícula densa del catálogo.
2. **Acceso al Catálogo Completo (`/catalogo`):** La ruta `/catalogo` mantiene el 100% de la funcionalidad de búsqueda, filtros de ADN lúdico, comensales, duración y tipos de juego.
3. **Navegación Táctil Móvil:** En pantallas pequeñas, cada carril permite deslizamiento horizontal fluido con el pulgar mediante *scroll snap*, visualizándose entre 2 y 4 elementos a la vez sin desbordamientos de página.
4. **Prioridad de Sorteos Promocionados:** Los sorteos con `IsPromoted == true` aparecen en las primeras posiciones del Carril 2 con su distinción visual, seguidos por los sorteos estándar ordenados por proximidad de vencimiento.
5. **Calendario de Eventos Reales:** El Carril 4 expone las principales citas lúdicas ordenadas por fecha de inicio más cercana, con cartel promocional y enlace externo.
6. **Cabecera Limpia:** El encabezado del sitio no muestra el selector de temas de colores redundante.
7. **Enlace Canónico BGG:** La ficha de juego muestra un botón destacado hacia BoardGameGeek que abre la URL oficial en una nueva pestaña segura.
8. **Suite de Pruebas:** Todos los tests existentes continúan en verde y se añaden pruebas unitarias completas para los nuevos servicios y ordenaciones.
