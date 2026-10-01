# Incremento 90: Snapshots Crudos BGG, Refinamiento Integral de Catálogo, Ficha Editorial y Retorno de Sesión

> **ID:** INC-90  
> **Slug:** `bgg-raw-snapshots`  
> **Rama:** `inc/bgg-raw-snapshots`  
> **Estado:** ⏳ En progreso  
> **Módulos Impactados:** Módulo 01 (`docs/specs/sistema/01-catalogo-base.md`), Módulo 05 (`docs/specs/sistema/05-importador-bgg.md`), Módulo 18 (`docs/specs/sistema/18-editor-editorial-fichas.md`), Módulo 37 (`docs/specs/sistema/37-ingesta-bgg-catalogo.md`), Módulo 46 (`docs/specs/sistema/46-optimizacion-consultas-egress-cache.md`)  
> **Dependencias:** INC-89.  

---

## 1. Contexto y Diagnóstico

Este incremento aúna la solución de almacenamiento desacoplado de snapshots crudos de BGG con un barrido de mejoras de experiencia de usuario y coherencia funcional detectadas en el catálogo y las fichas editoriales:

1. **Pérdida de datos crudos de BGG:** BGG se descarta tras parsear a `Game`. Para permitir recálculos locales sin penalizar el rendimiento ni agotar cuotas externas, se introduce `BggRawSnapshots`.
2. **Ficha de Juego:** Enlace BGG duplicado, botón «Cartel para Redes» visible sin permisos, modal de edición limitado a una sola imagen (imposibilitando alimentar el carrusel de tres fotos de INC-85) y exposición innecesaria del identificador de modelo IA en `AiSummaryCard`.
3. **Autenticación:** `/login/external` fuerza redirección a portada ignorando `returnUrl`.
4. **Catálogo:** Filtros rápidos redundantes, tarjetas con texto recargado en badges y ausencia de selector de ordenación por dureza/duración en filtros avanzados.

---

## 2. Objetivos Técnicos por Slices

### Slice A — Ficha Editorial, Permisos y Retorno de Sesión
- Unificación a un solo enlace canónico a BGG en `GameDetail.razor`.
- Restricción de «Cartel para Redes» a usuarios con permiso `CanEditGames`.
- Modal de edición `GameEditorModal` ampliado con soporte de carátula, trasera y mesa.
- Retirada de la etiqueta de modelo de IA en `AiSummaryCard.razor`.
- Corrección de `returnUrl` en `/login/external` en `Program.cs`.

### Slice B — Refinamiento del Catálogo y Tarjetas Lúdicas
- Supresión de la hilera de filtros rápidos en `Home.razor`, focalizando en el buscador y el panel avanzado.
- Incorporación de ordenación configurable (`GameSortOrder`: ranking, valoración, dureza, duración, año) en `GameFilterCriteria`, repositorio y UI.
- Rediseño de `GameCard.razor`: jugadores en formato compacto `3-4J` (idéntico a portada), badges inferiores reducidos a solo iconos con tooltip y retirada del badge «Solo».
- Leyenda accesible de iconografía lúdica en el catálogo.

### Slice C — Tabla Satélite de Snapshots Crudos BGG y Poblado Defensivo
- Entidad y tabla satélite `BggRawSnapshots` con soporte dual SQLite/PostgreSQL.
- Auto-captura en pipeline de importación y catálogo.
- Servicio `IBggRawSnapshotSyncService` con *rate-limiting* estricto y botón administrativo con telemetría en vivo en `/admin/cola-catalogacion`.
