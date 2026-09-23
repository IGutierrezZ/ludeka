# Propuesta SDD — INC-57: Diagnóstico y Optimización de Imágenes del Catálogo

## 1. Motivación y Contexto

El pipeline de procesamiento y almacenamiento de imágenes en Cloudflare R2 con SkiaSharp y formato WebP fue implementado y probado en INC-40. Sin embargo, no se ha realizado una evaluación empírica sistemática del comportamiento en producción y desarrollo sobre la experiencia de usuario real:
- Las tarjetas del catálogo general (`/catalogo`) y del carril Top 20 en la portada (`/`) cargan la carátula a máxima resolución (`CoverImageUrl`) en lugar de la miniatura optimizada (`ThumbnailUrl`), descargando megabytes superfluos para tarjetas que se renderizan a menos de 240px.
- Determinadas tarjetas del carril en el Home carecen de atributos de dimensiones intrínsecas (`width`/`height`), introduciendo riesgo de CLS (*Cumulative Layout Shift*).
- Existen assets estáticos heredados sin optimizar en local (ej. `patchwork.png` con un tamaño de 1.898 KB).

Este incremento atiende la directriz expresa del maintainer: **«medir antes de cortar»**, levantando un diagnóstico empírico documentado y aplicando las optimizaciones técnicas que los datos avalen.

---

## 2. Alcance Propuesto

1. **Diagnóstico Cuantitativo y Cualitativo:**
   - Elaborar `docs/specs/diagnostico-imagenes-catalogo.md` documentando el inventario de recursos, pesos, formatos actuales y cálculo de ahorro proyectado.
2. **Optimización de Componentes de Presentación:**
   - `GameCard.razor`: Priorizar `ThumbnailUrl` con fallback a `CoverImageUrl` y placeholder; dimensiones y atributos anti-CLS (`loading="lazy"`, `decoding="async"`).
   - `HomeGameCard.razor`: Emplear `ThumbnailUrl` para la escala de 192px en el carril.
   - `HomeGiveawayCard.razor` y `HomeReleaseCard.razor`: Incorporar `width` y `height` acordes a la caja contenedora `rail-cover--wide`.
   - `GameDetail.razor`: Garantizar carga prioritaria (`fetchpriority="high"`), dimensiones intrínsecas y fallback robusto.
3. **Optimización de Recursos Estáticos:**
   - Reducción y conversión a WebP de assets sobredimensionados en `wwwroot/images/games`.
   - Normalización de rutas en `seed-games.json`.
4. **Batería de Pruebas Automatizadas:**
   - Creación de pruebas de contrato en `CatalogImageOptimizationContractTests.cs` para validar atributos HTML anti-CLS, preferencia de thumbnails y consistencia de URLs.

---

## 3. Criterios de Aceptación

1. Informe de diagnóstico redactado y contrastado con datos del repositorio.
2. Cero regresiones visuales en carátulas de catálogo, portada y detalle.
3. Eliminación de saltos de maquetación (CLS) provocados por ausencia de dimensiones en tarjetas de carril.
4. Reducción sustancial del payload de imágenes en listados masivos mediante el uso sistemático de `ThumbnailUrl`.
5. Suite completa de pruebas en verde (`dotnet test`).
