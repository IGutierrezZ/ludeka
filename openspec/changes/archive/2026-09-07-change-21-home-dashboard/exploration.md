# Exploración Técnica y Análisis de Dominio: change-21-home-dashboard

## 1. Contexto y Objetivos del Incremento

El Incremento 21 aborda una transformación radical en la experiencia inicial y navegación de **Ludeka** ("El Letterboxd de los juegos de mesa en español"):
1. **Página de Inicio (`/`) como Dashboard Editorial Vivo:** Sustituir la vista de catálogo monolítico por un Dashboard estructurado en cuatro carriles con desplazamiento horizontal fluido (*mobile-first* con *touch snap*), diseñado para enganchar al usuario en 3 segundos.
   - **Carril 1 — Top 20 Mejores Juegos:** Títulos más valorados (ranking BGG / valoración comunitaria Ludeka), con carátula, nota, año y semáforo de comensales.
   - **Carril 2 — Sorteos Activos y Destacados:** Sorteos comunitarios vigentes priorizando promocionados (`IsPromoted`) y ordenados por fecha límite próxima.
   - **Carril 3 — Novedades del Sector:** Estrenos y lanzamientos recientes de editoriales y tiendas de juegos de mesa.
   - **Carril 4 — Próximos Eventos y Grandes Citas Lúdicas:** Ferias y festivales de referencia (Festival de Córdoba, InterOcio, Essen SPIEL, Gen Con, DAU Barcelona) ordenados por proximidad cronológica.
2. **Desacople del Catálogo a `/catalogo`:** Reubicar la vista exhaustiva con filtros facetados (ADN lúdico, comensales, duración) en una ruta dedicada y semántica (`/catalogo`).
3. **Limpieza del Navbar Superior:** Retirar el conmutador de paleta de colores de la cabecera (Header/Navbar), eliminando sobrecarga cognitiva, dado que dicha preferencia ya se gestiona en el perfil de usuario.
4. **Enlace Canónico a BoardGameGeek:** En la ficha de juego (`GameDetail.razor`), incorporar un botón directo y accesible `[ 🌐 Ver en BoardGameGeek ]` apuntando a `https://boardgamegeek.com/boardgame/{BggId}` en pestaña nueva.

---

## 2. Diagnóstico del Estado Actual del Código

### 2.1 Enrutamiento y Navegación
- `src/Ludeka.Web/Components/Pages/Home.razor` responde actualmente a `@page "/"` y `@page "/catalogo"`.
- `src/Ludeka.Web/Components/Layout/MainLayout.razor` contiene:
  - Enlace al catálogo apuntando a `/`.
  - Selector de 5 temas ("Papel", "Madera", "Mesa", "Noche", "Carbón") insertado en la barra superior (líneas 21-63).
- `src/Ludeka.Web/Components/Pages/GameDetail.razor`:
  - Enlaces de retroceso apuntan a `/`.
  - No dispone de enlace canónico externo directo a la ficha en BoardGameGeek.

### 2.2 Fuentes de Datos de los Carriles
- **Juegos (Carril 1):** `ICatalogService` y `ICachedCatalogService` disponen de `GetCatalogAsync(GameFilterCriteria, page, pageSize)`. Se puede obtener el Top 20 ordenado por ranking BGG o valoración comunitaria.
- **Sorteos (Carril 2):** `Giveaway.cs` no cuenta con propiedad `IsPromoted`. Para soportar el orden prioritario de promocionados exigido por los criterios de aceptación, se debe incorporar `IsPromoted` tanto en la entidad de dominio `Giveaway` como en `GiveawayDto`.
- **Novedades (Carril 3):** `IWeeklyReleaseService` y `SqliteWeeklyReleaseRepository` devuelven la lista de novedades y lanzamientos vigentes.
- **Eventos Lúdicos (Carril 4):** Actualmente no existe en `Ludeka.Core` una entidad para eventos del calendario lúdico. Para garantizar solidez y sentar las bases del Incremento 22, se definirá la entidad de dominio `BoardGameEvent` con su correspondiente persistencia y repositorio.

### 2.3 Rendimiento y Estrategia de Caché
- La carga del Dashboard inicial debe ser instantánea (< 100ms en SSR).
- Se implementará un servicio orquestador `IHomeDashboardService` decorado con `CachedHomeDashboardService` usando `IMemoryCache` (TTL de 5-10 minutos).

---

## 3. Plan de Acción y Riesgos Identificados

| Elemento | Riesgo Identificado | Mitigación |
|---|---|---|
| Desacople de `/` y `/catalogo` | Posibles enlaces rotos hacia `/` que esperaban ver el catálogo completo | Se reubica el catálogo en `CatalogPage.razor` (`@page "/catalogo"`), se actualizan todos los enlaces de la aplicación (`MainLayout`, `GameDetail`, `NotFound`) y el logo sigue apuntando a `/` (ahora Dashboard). |
| Rendimiento de 4 consultas simultáneas | Latencia agregada al consultar juegos, sorteos, novedades y eventos | Agregación paralela o eficiente con `CachedHomeDashboardService` y `IMemoryCache`. |
| Scroll horizontal en móvil | Barras de desplazamiento toscas o falta de feedback táctil | Clases Tailwind con `snap-x snap-mandatory overflow-x-auto scrollbar-none` y márgenes de desborde (*peek*) para indicar continuidad. |
| Compatibilidad de base de datos SQLite | Inserción de nuevas columnas y tablas | Inclusión en `SqliteSchemaMigrator` para aplicar defensivamente `IsPromoted` en `Giveaways` y crear la tabla `BoardGameEvents` con índices. |
