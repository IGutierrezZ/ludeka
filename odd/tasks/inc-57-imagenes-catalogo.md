# Documento Vivo ODD — INC-57: Diagnóstico y Optimización de Imágenes del Catálogo

> **Feature:** `imagenes-catalogo`  
> **Fichero:** `odd/tasks/inc-57-imagenes-catalogo.md` (fuente de verdad operativa)  
> **Cambio SDD de origen:** `openspec/changes/change-57-imagenes-catalogo/`  
> **Rama:** `inc/imagenes-catalogo`  
> **Worktree:** `C:\repos\ludeka-wt\imagenes-catalogo`  
> **Creado:** 2026-09-23 · **Ruta:** rama `inc/imagenes-catalogo` → PR a `main`  

---

## 1. Objetivo

Realizar un diagnóstico empírico riguroso sobre el peso, tiempos de transferencia y estabilidad visual (LCP/CLS) de los recursos gráficos en las vistas críticas de Ludeka (portada `/`, catálogo `/catalogo` y ficha de juego `/juegos/{slug}`), y aplicar sobre datos contrastados las optimizaciones que eliminen el sobrepeso de red, prevengan el Cumulative Layout Shift (CLS) y maximicen la eficiencia del pipeline de medios (Cloudflare R2 + SkiaSharp + WebP) tanto en local como en producción.

---

## 2. Problema y Diagnóstico Previo

1. **Consumo de carátulas a resolución completa en tarjetas pequeñas:**
   En [`GameCard.razor`](file:///C:/repos/ludeka-wt/imagenes-catalogo/src/Ludeka.Web/Components/Shared/GameCard.razor) y [`HomeGameCard.razor`](file:///C:/repos/ludeka-wt/imagenes-catalogo/src/Ludeka.Web/Components/Home/HomeGameCard.razor), el componente enlaza directamente a `Game.CoverImageUrl` (hasta 1000px o imágenes originales externas) para contenedores de 192px–240px, ignorando `Game.ThumbnailUrl` (variante canónica de 400px ya contemplada en el dominio y en R2). En una vista con decenas de juegos, esto multiplica innecesariamente los megabytes transferidos.
2. **Riesgo crítico de CLS por falta de dimensiones intrínsecas en tarjetas del Home:**
   Componentes como [`HomeGiveawayCard.razor`](file:///C:/repos/ludeka-wt/imagenes-catalogo/src/Ludeka.Web/Components/Home/HomeGiveawayCard.razor) y [`HomeReleaseCard.razor`](file:///C:/repos/ludeka-wt/imagenes-catalogo/src/Ludeka.Web/Components/Home/HomeReleaseCard.razor) usan `<img>` con `loading="lazy"` pero omiten atributos `width` y `height`, provocando saltos bruscos de maquetación durante la carga asíncrona.
3. **Assets estáticos locales sin comprimir:**
   En `wwwroot/images/games`, archivos como `patchwork.png` alcanzan 1.898 KB (~1,9 MB) sin optimización previa, y múltiples carátulas existen duplicadas en formatos PNG y JPG sin conversión a WebP.
4. **Política de caché HTTP `immutable` a 1 año sin versionado en clave:**
   [`CloudflareR2StorageService.cs`](file:///C:/repos/ludeka-wt/imagenes-catalogo/src/Ludeka.Infrastructure/Services/CloudflareR2StorageService.cs) asigna `Cache-Control: public, max-age=31536000, immutable` a claves fijas tipo `games/{bggId}/cover.webp`. Si una variante se regenera o se sustituye, los navegadores y la CDN retienen la versión previa durante un año a menos que se fuerce purga o se soporte versionado/busting.

---

## 3. Alcance Autorizado

### Dentro de Alcance:
- **Diagnóstico Empírico Documentado:**
  - Auditoría de pesos de assets locales en `wwwroot` y `seed-games.json`.
  - Medición y cálculo de ahorro de payload en catálogo (`/catalogo`) y portada (`/`).
  - Documentación de hallazgos en `docs/specs/diagnostico-imagenes-catalogo.md`.
- **Refactorización de Componentes de Interfaz (`Ludeka.Web`):**
  - `GameCard.razor`: Priorizar `ThumbnailUrl` con fallback a `CoverImageUrl` / placeholder; asegurar `width`, `height`, `loading="lazy"` y `decoding="async"`.
  - `HomeGameCard.razor`: Priorizar `ThumbnailUrl` para la tarjeta de 192px del carril Top 20.
  - `HomeGiveawayCard.razor` y `HomeReleaseCard.razor`: Añadir dimensiones intrínsecas (`width` y `height`) coincidentes con su contenedor `rail-cover--wide` para eliminar CLS.
  - `GameDetail.razor`: Implementar marcado optimizado con `fetchpriority="high"`, dimensiones intrínsecas y fallback seguro.
- **Optimización de Assets Locales Críticos:**
  - Conversión a WebP optimizado de `patchwork.png` y normalización de assets pesados del catálogo local.
  - Actualización de `seed-games.json` para que `ThumbnailUrl` referencie variantes optimizadas cuando estén disponibles.
- **Revisión de Estrategia de Caché R2:**
  - Documentar y ajustar el comportamiento de cabeceras de caché o soporte de hash/versión para prevenir estancamiento de caché al reemplazar imágenes.
- **Pruebas y Verificación:**
  - Tests unitarios y de contrato en `Ludeka.UnitTests` validando dimensiones intrínsecas, atributos de rendimiento y selección de miniaturas.
  - Verificación de no-regresión: suite completa en verde (`dotnet test`).

### Fuera de Alcance:
- Migración a otro proveedor de almacenamiento o CDN distinto a Cloudflare R2.
- Reescritura del algoritmo de compresión de SkiaSharp (ya probado y validado en INC-40).

---

## 4. Decisiones de Arquitectura y Diseño

| ID | Decisión | Fundamento Técnico |
|---|---|---|
| **D-01** | Priorización de `ThumbnailUrl` en tarjetas de catálogo y carril | La miniatura canónica de 400px WebP es más que suficiente para pantallas de alta densidad (2x) en tarjetas de 192–240px, reduciendo el peso por imagen entre un 60% y un 85%. |
| **D-02** | Dimensiones intrínsecas (`width`/`height`) universales | La especificación HTML y las métricas Core Web Vitals requieren que el navegador conozca la relación de aspecto antes de descargar el recurso para reservar el espacio y conseguir CLS = 0. |
| **D-03** | Optimización de assets estáticos heredados | Sustituir recursos no comprimidos como `patchwork.png` (1,9 MB) por WebP reduce drásticamente el tiempo de carga en desarrollo y en instalaciones con fallback local. |
| **D-04** | Política de caché R2 balanceada | Mantener `max-age` elevado para contenido inmutable pero clarificar la estrategia de invalidación o re-subida en la especificación viva. |

---

## 5. Checklist de Tareas (IDs Estables)

- [x] **ODD-1 — Diagnóstico Empírico de Rendimiento y Pesos de Imágenes**
  - [x] 1.1 Inventario de pesos y formatos en `wwwroot/images/games` y `seed-games.json`.
  - [x] 1.2 Auditoría de componentes Razor (`GameCard`, `HomeGameCard`, `HomeGiveawayCard`, `HomeReleaseCard`, `GameDetail`).
  - [x] 1.3 Redactar informe de diagnóstico en `docs/specs/diagnostico-imagenes-catalogo.md`.
- [x] **ODD-2 — Optimización de Componentes Frontend**
  - [x] 2.1 Refactorizar `GameCard.razor` para priorizar `ThumbnailUrl` y asegurar atributos anti-CLS.
  - [x] 2.2 Refactorizar `HomeGameCard.razor` para priorizar `ThumbnailUrl`.
  - [x] 2.3 Añadir `width` y `height` a `HomeGiveawayCard.razor` y `HomeReleaseCard.razor`.
  - [x] 2.4 Asegurar renderizado óptimo y responsivo en `GameDetail.razor`.
- [x] **ODD-3 — Optimización de Assets Locales y Semillas**
  - [x] 3.1 Comprimir y optimizar `patchwork.png` y assets pesados a WebP.
  - [x] 3.2 Actualizar `seed-games.json` para alinear `ThumbnailUrl` con las versiones optimizadas.
- [x] **ODD-4 — Pruebas de Contrato y Verificación de No-Regresión**
  - [x] 4.1 Añadir pruebas unitarias/contrato en `Ludeka.UnitTests/Web/CatalogImageOptimizationContractTests.cs`.
  - [x] 4.2 Ejecutar suite completa `dotnet test Ludeka.sln` y validar verde total.
- [x] **ODD-5 — Especificación Viva, SDD y PR**
  - [x] 5.1 Actualizar módulos correspondientes en `docs/specs/sistema/` y total de pruebas en `README.md`.
  - [x] 5.2 Actualizar `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md`.
  - [x] 5.3 Generar artefactos SDD en `openspec/changes/change-57-imagenes-catalogo/`.
  - [ ] 5.4 Ejecutar `scripts/sdd-worktree.ps1 pr imagenes-catalogo`.

---

## 6. Verificación Final y Resultados

- **Línea Base Inicial:** 1.646 pruebas unitarias + 10 de integración en verde (1.656 en total).
- **Pruebas Unitarias Finales:** 1.653 superadas (0 fallos).
- **Pruebas de Integración Finales:** 10 superadas (0 fallos).
- **Total Automatizado:** 1.663 pruebas superadas al 100%.
- **Compilación de Ludeka.sln:** 0 errores, 0 advertencias nuevas.
- **Reducción de Payload:** -81% de datos de red en `/catalogo` (~4,8 MB a ~908 KB) y -83% en `patchwork.png` (1.898 KB a 323 KB PNG / 49 KB WebP).
