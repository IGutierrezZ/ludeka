# Incremento 93: Listado Diario de Juegos en Tendencia (BGG Hotness), Ingesta Inmediata Satélite y Conmutador de Portada y Catálogo

> **ID:** INC-93  
> **Slug:** `tendencias-bgg`  
> **Rama:** `inc/tendencias-bgg`  
> **Estado:** ✅ Archivado (2.215 pruebas unitarias en verde al 100% [2.225 totales con integración])  
> **Módulos Impactados:** Módulo 01 (`docs/specs/sistema/01-catalogo-y-fichas.md`), Módulo 05 (`docs/specs/sistema/05-integracion-bgg.md`), Módulo 15 (`docs/specs/sistema/15-dashboard-inicio-editorial.md`), Módulo 18 (`docs/specs/sistema/18-deteccion-novedades-y-cola-nocturna.md`), Módulo 29 (`docs/specs/sistema/29-ingesta-continua-novedades-bgg.md`), Módulo 47 (`docs/specs/sistema/47-snapshots-crudos-bgg-expansiones-sincronizacion.md`), Módulo 48 (`docs/specs/sistema/48-tendencias-diarias-bgg-portada-catalogo.md`)  
> **Dependencias:** INC-90, INC-91, INC-92.

---

## 1. Contexto y Diagnóstico

Actualmente el sistema consulta el *Hotness* de BGG (`/xmlapi2/hot?type=boardgame`) en el lote nocturno exclusivamente para descubrir títulos inéditos y encolarlos, pero:
1. **Descarte de la posición diaria:** Las posiciones del ranking de tendencias (puestos 1 al 50) de cada día se pierden tras el análisis; no existe una tabla histórica ni del día para consultar qué juegos están de moda hoy.
2. **Limitación por cupo diario en novedades de tendencia:** Si el top 50 contiene títulos ausentes que superan el cupo diario de catalogación (`DailyCatalogingLimit = 20`), se posponen para noches sucesivas en vez de procesarse de inmediato.
3. **Ausencia de volcado a la tabla satélite:** La ingesta de la cola nocturna histórica aún no depositaba el XML/JSON bruto en la nueva tabla `BggRawSnapshots`.
4. **Portada rígida:** El primer carril de portada solo expone el Top 20 por valoración histórica general, sin posibilidad de ver los juegos en tendencia.
5. **Catálogo sin ordenación por tendencia:** Los usuarios no pueden ordenar el catálogo lúdico por los títulos que marcan tendencia hoy.

---

## 2. Objetivos Técnicos por Unidades de Trabajo (Work Units)

### Unidad de Trabajo 1: Modelo de Dominio y Persistencia de Tendencias Diarias
- Entidad `DailyTrendingGame` con `DateUtc`, `Rank` (1..50), `BggId`, `Title`, `ThumbnailUrl`, `YearPublished`, `GameId?` y navegación a `Game`.
- Contrato `IDailyTrendingGameRepository` e implementación `SqliteDailyTrendingGameRepository`.
- Configuración en `LudekaDbContext` con índices únicos `(DateUtc, Rank)` y `(DateUtc, BggId)`.
- Migración EF Core `AddDailyTrendingGames` con soporte dual SQLite y PostgreSQL.
- Pruebas unitarias de entidad y repositorio.

### Unidad de Trabajo 2: Ingesta Inmediata Completa en el Trabajo Nocturno
- Integración en `BggDiscoveryService` / `NightlyCatalogingService`:
  - Al sondear tendencias, persiste la foto de los 50 puestos en `DailyTrendingGames`.
  - Para los títulos del top 50 ausentes en catálogo, los ingiere de inmediato en esa misma ejecución:
    - Descarga completa de BGG.
    - Volcado satélite en `BggRawSnapshots`.
    - Síntesis de ficha con IA Gemini.
    - Inserción en catálogo `Games` y enlace atómico `GameId` en `DailyTrendingGame`.
- Pruebas unitarias de orquestación e ingesta inmediata de tendencias.

### Unidad de Trabajo 3: Experiencia de Usuario en Portada y Catálogo
- `HomeDashboardDto` y `HomeDashboardService`: carga de los 20 juegos en tendencia del día y los 20 mejor valorados.
- `HomeDashboard.razor`: conmutador accesible de pestañas en Carril 1 («En tendencia» activo por defecto vs «Mejor valorados»).
- `HomeGameCard.razor`: visualización del distintivo de posición en tendencia (`#1`, `#2`...) mediante `GameSummaryDto.TrendingRank`.
- `GameSortOrder.Trending`: nuevo criterio de ordenación en catálogo (`/catalogo`) respaldado por `SqliteGameRepository.SearchAsync`.
- Pruebas de contrato web y componentes.

### Unidad de Trabajo 4: Cierre, Documentación Viva y Pull Request
- Sincronización de `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md`.
- Volcado a `docs/specs/sistema/` y apertura de PR.
