# Tareas Técnicas SDD — INC-58: Paginación Real del Catálogo y Modos de Vista

- [x] **1. Contratos y Dominio (Backend / Application)**
  - [x] 1.1 Añadir `TotalPages` a `CatalogResult` en `src/Ludeka.Application/Features/Catalog/ICatalogService.cs`.
  - [x] 1.2 Incorporar iconos `layout-grid`, `list`, `chevron-left` y `chevron-right` en `src/Ludeka.Web/Components/Shared/IconCatalog.cs`.

- [x] **2. Componente de Vista en Lista (Frontend)**
  - [x] 2.1 Crear `src/Ludeka.Web/Components/Shared/GameListItem.razor` con marcado editorial accesible, anti-CLS y metadatos del juego.

- [x] **3. Paginación y Modos de Vista en Catálogo (Frontend)**
  - [x] 3.1 Agregar selector conmutable Cuadrícula / Lista en la cabecera de resultados de `src/Ludeka.Web/Components/Pages/Home.razor`.
  - [x] 3.2 Añadir controles de paginación Anterior / Siguiente e indicador de página en `Home.razor`.
  - [x] 3.3 Implementar sincronización bidireccional de parámetros en URL (`page`, `view`, `q`, presets) y soporte de historial (`LocationChanged`).
  - [x] 3.4 Mostrar contador explícito de títulos disponibles en catálogo.

- [x] **4. Pruebas y Verificación**
  - [x] 4.1 Añadir `tests/Ludeka.UnitTests/Web/CatalogPaginationContractTests.cs` (15 pruebas de contrato).
  - [x] 4.2 Ejecutar `dotnet test Ludeka.sln` y verificar 100% verde (1.678 pruebas superadas: 1.668 unitarias + 10 de integración).

- [ ] **5. Especificación Viva y PR**
  - [x] 5.1 Actualizar `docs/specs/sistema/01-catalogo-y-fichas.md` y `docs/specs/sistema/README.md`.
  - [x] 5.2 Actualizar `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md`.
  - [ ] 5.3 Abrir Pull Request hacia `main` mediante `scripts/sdd-worktree.ps1 pr paginacion-catalogo`.
