# Documento Vivo ODD — INC-58: Paginación Real del Catálogo y Modos de Vista (Cuadrícula y Lista)

> **Feature:** `paginacion-catalogo`  
> **Fichero:** `odd/tasks/inc-58-paginacion-catalogo.md` (fuente de verdad operativa)  
> **Cambio SDD de origen:** `openspec/changes/change-58-paginacion-catalogo/`  
> **Rama:** `inc/paginacion-catalogo`  
> **Worktree:** `C:\repos\ludeka-wt\paginacion-catalogo`  
> **Creado:** 2026-09-23 · **Ruta:** rama `inc/paginacion-catalogo` → PR a `main`  

---

## 1. Objetivo

Hacer navegable la totalidad del catálogo lúdico de Ludeka (~8.000 títulos) mediante una paginación robusta en la interfaz web (`/catalogo`) respaldada por sincronización bidireccional en la URL (parámetros `page`, `view`, `q` y filtros), exponer de manera precisa el total de títulos disponibles y permitir alternar conmutativamente entre la vista en cuadrícula (`grid`) y una vista de lista enriquecida (`list`), garantizando accesibilidad WCAG 2.2 AA, rendimiento SSR óptimo y respeto estricto por las optimizaciones gráficas de INC-57.

---

## 2. Problema y Diagnóstico Previo

1. **Contradicción entre capa de datos y capa de presentación:**  
   `SqliteGameRepository.SearchAsync` y `CatalogResult` ya soportaban paginación con `Skip`/`Take` y cálculo de `TotalCount`. Sin embargo, `Home.razor` solicitaba de forma fija `page: 1, pageSize: 50` sin controles de avance, dejando inaccesible la inmensa mayoría de los títulos.
2. **Ausencia de estado en URL:**  
   La página actual no se reflejaba en la query string (`?page=...`). Si el usuario recargaba la página o compartía el enlace, la posición se perdía y volvía invariablemente a la primera página.
3. **Monocultura de visualización:**  
   Solo existía vista de cuadrícula (`grid` con `GameCard.razor`). No existía vista de lista (`list`) que permitiese escaneo denso y rápido con atributos comparativos (duración, jugadores, huella de mesa, editorial).
4. **Iconografía y accesibilidad:**  
   Se requería añadir los iconos oficiales Lucide `layout-grid`, `list`, `chevron-left` y `chevron-right` al catálogo whitelist `IconCatalog.cs`, e implementar controles accesibles con foco visible, navegación por teclado y roles ARIA.

---

## 3. Alcance Autorizado

### Dentro de Alcance:
- **Paginación en `/catalogo` (`Home.razor`):**  
  - Controles accesibles Anterior/Siguiente y navegación por páginas.
  - Indicador numérico de página actual y total de páginas («Página X de Y»).
  - Contador claro y visible de títulos encontrados («Mostrando X–Y de Z títulos»).
- **Sincronización Bidireccional de URL:**  
  - Parámetros `page` (entero positivo), `view` (`grid` o `list`), `q` (búsqueda de texto) y presets de filtro.
  - Actualización reactiva mediante `NavigationManager` con `replace: true` sin recarga destructiva.
  - Restablecimiento automático a `page = 1` ante cambios en el término de búsqueda o filtros.
  - Soporte de historial de navegador (Back/Forward) con `Navigation.LocationChanged` y limpieza en `Dispose()`.
- **Modos de Vista Conmutables (Cuadrícula ↔ Lista):**  
  - Selector conmutable con iconos accesibles (`layout-grid` y `list`) y atributos ARIA (`role="group"`, `aria-label="Modo de visualización"`, `aria-pressed`).
  - Nuevo componente `GameListItem.razor` con marcado editorial optimizado, miniatura WebP de 64x64 anti-CLS (regla INC-57), metadatos clave (jugadores idóneos, duración, huella en mesa, editorial, rating BGG/Ludeka) y enlace accesible a `/juegos/{slug}`.
  - Alternancia instantánea en memoria sin reconsulta a SQLite ni pérdida del estado de filtrado o paginado.
- **Ampliación Whitelist de Iconografía (`IconCatalog.cs`):**  
  - Incorporación de los paths oficiales SVG de Lucide para `layout-grid`, `list`, `chevron-left` y `chevron-right`.
- **Pruebas y Verificación:**  
  - 15 nuevas pruebas en `Ludeka.UnitTests/Web/CatalogPaginationContractTests.cs`.
  - Verificación del 100% de la suite de pruebas (`dotnet test Ludeka.sln`, 1.678 pruebas en verde).

### Fuera de Alcance:
- Paginación de la ludoteca personal (`/mi-ludoteca` o `/cuenta/ludoteca`) o del hub multimedia.
- Implementación de scroll infinito (descartado explícitamente en INC-58 por accesibilidad, indexabilidad y compatibilidad SSR limpia).
- Modificación de filtros avanzados o huella en mesa como criterio de búsqueda (reservado para INC-59).

---

## 4. Decisiones de Arquitectura y Diseño

| ID | Decisión | Fundamento Técnico |
|---|---|---|
| **D-01** | Paginación tradicional con URL como fuente de la verdad (`?page=X&view=Y`) | Garantiza enlaces compartibles, navegación en el historial del navegador (`popstate`), compatibilidad con SSR e indexabilidad clara sin la complejidad de scroll infinito ni problemas de accesibilidad. |
| **D-02** | Conmutación de vista en memoria sin recarga | Los datos de `GameSummaryDto` de la página actual ya contienen todos los campos requeridos para cuadrícula y lista. Alternar la vista solo muta el layout visual y el parámetro `view` en URL, sin latencia de red. |
| **D-03** | `GameListItem.razor` con anti-CLS y WebP optimizado | Aplica las directrices de INC-57: contenedor de 64x64px con `ThumbnailUrl` (WebP 400px), `width="64"`, `height="64"`, `loading="lazy"`, `decoding="async"` y fallback a SVG en caso de error. |
| **D-04** | TotalPages calculado en `CatalogResult` | Proporcionar `TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;` en el DTO centraliza la lógica aritmética y previene inconsistencias entre vistas. |
| **D-05** | Accesibilidad WCAG 2.2 AA en botones y controles | Botones con tamaño táctil adecuado (mínimo 44px), anillos de foco visibles (`focus-visible:ring-2`), etiquetas `aria-label` descriptivas y estados deshabilitados accesibles. |

---

## 5. Checklist de Tareas (IDs Estables)

- [x] **ODD-1 — Iconografía y Contratos de Aplicación**
  - [x] 1.1 Registrar iconos Lucide oficiales `layout-grid`, `list`, `chevron-left` y `chevron-right` en `IconCatalog.cs`.
  - [x] 1.2 Extender `CatalogResult` en `ICatalogService.cs` con la propiedad calculada `TotalPages`.
- [x] **ODD-2 — Componente de Vista en Lista (`GameListItem.razor`)**
  - [x] 2.1 Diseñar e implementar `src/Ludeka.Web/Components/Shared/GameListItem.razor` con marcado editorial accesible, metadatos comparativos y carátula anti-CLS.
- [x] **ODD-3 — Paginación y Modos de Vista en Catálogo (`Home.razor`)**
  - [x] 3.1 Integrar selector de modo de vista (Cuadrícula ↔ Lista) con roles y accesibilidad.
  - [x] 3.2 Implementar paginación completa con controles Anterior/Siguiente y navegación por páginas en `Home.razor`.
  - [x] 3.3 Sincronizar lectura y persistencia de parámetros en URL (`page`, `view`, `q`, presets) mediante `NavigationManager` y `LocationChanged`.
  - [x] 3.4 Mostrar contador claro de títulos («Mostrando X–Y de Z títulos»).
- [x] **ODD-4 — Pruebas de Contrato y Verificación de No-Regresión**
  - [x] 4.1 Crear `tests/Ludeka.UnitTests/Web/CatalogPaginationContractTests.cs` (15 pruebas de contrato superadas).
  - [x] 4.2 Ejecutar suite completa `dotnet test Ludeka.sln` y verificar 100% verde (1.678 pruebas totales: 1.668 unitarias + 10 de integración).
- [x] **ODD-5 — Especificación Viva, SDD y PR**
  - [x] 5.1 Actualizar módulos de la especificación viva `01-catalogo-y-fichas.md` y `README.md`.
  - [x] 5.2 Actualizar `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md`.
  - [x] 5.3 Generar artefactos SDD en `openspec/changes/change-58-paginacion-catalogo/` y archivar en `archive/2026-09-24-change-58-paginacion-catalogo/`.
  - [x] 5.4 Abrir y mergear PR #106 hacia `main` (`scripts/sdd-worktree.ps1 pr paginacion-catalogo`).

---

## 6. Verificación Final y Resultados

- **Línea Base Inicial:** 1.653 pruebas unitarias + 10 de integración en verde (1.663 en total).
- **Pruebas Unitarias Finales:** 1.668 superadas (0 fallos).
- **Pruebas de Integración Finales:** 10 superadas (0 fallos).
- **Total Automatizado:** 1.678 pruebas superadas al 100%.
- **Compilación de Ludeka.sln:** 0 errores, 0 advertencias nuevas.
- **Paginación y Modos:** Operativo en `/catalogo` con 24 títulos por página, vista en cuadrícula y lista conmutable en memoria y preservación completa en URL.
