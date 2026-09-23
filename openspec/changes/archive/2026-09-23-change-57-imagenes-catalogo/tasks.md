# Tareas de Implementación — INC-57: Diagnóstico y Optimización de Imágenes del Catálogo

- [x] **1. Diagnóstico Empírico de Rendimiento y Pesos de Imágenes**
  - [x] 1.1 Inventario de pesos y formatos en `wwwroot/images/games` y `seed-games.json`.
  - [x] 1.2 Auditoría de componentes Razor (`GameCard`, `HomeGameCard`, `HomeGiveawayCard`, `HomeReleaseCard`, `GameDetail`).
  - [x] 1.3 Redactar informe de diagnóstico en `docs/specs/diagnostico-imagenes-catalogo.md`.

- [x] **2. Optimización de Componentes Frontend**
  - [x] 2.1 Refactorizar `GameCard.razor` para priorizar `ThumbnailUrl` y asegurar atributos anti-CLS.
  - [x] 2.2 Refactorizar `HomeGameCard.razor` para priorizar `ThumbnailUrl`.
  - [x] 2.3 Añadir `width` y `height` a `HomeGiveawayCard.razor` y `HomeReleaseCard.razor`.
  - [x] 2.4 Añadir dimensiones a `ExpansionSisterList.razor` y `ExpansionEcosystemSection.razor`.
  - [x] 2.5 Asegurar renderizado óptimo y responsivo en `GameDetail.razor`.

- [x] **3. Optimización de Assets Locales y Semillas**
  - [x] 3.1 Comprimir y optimizar `patchwork.png` y assets pesados a WebP.
  - [x] 3.2 Actualizar `seed-games.json` para alinear `ThumbnailUrl` con las versiones optimizadas.

- [x] **4. Pruebas de Contrato y Verificación de No-Regresión**
  - [x] 4.1 Añadir pruebas unitarias/contrato en `Ludeka.UnitTests/Web/CatalogImageOptimizationContractTests.cs`.
  - [x] 4.2 Ejecutar suite completa `dotnet test Ludeka.sln` y validar verde total (1.663 pruebas).

- [x] **5. Especificación Viva, SDD y PR**
  - [x] 5.1 Actualizar módulos correspondientes en `docs/specs/sistema/` y total de pruebas en `README.md`.
  - [x] 5.2 Actualizar `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md`.
  - [x] 5.3 Generar artefactos SDD en `openspec/changes/change-57-imagenes-catalogo/`.
  - [ ] 5.4 Ejecutar `scripts/sdd-worktree.ps1 pr imagenes-catalogo`.
