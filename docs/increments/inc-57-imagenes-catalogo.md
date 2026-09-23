# INC-57: Diagnóstico y Optimización de Imágenes del Catálogo

> **Estado:** ⏳ Planificado (backlog 2026-09-22, Fase B)
> **Fecha de Inicio:** pendiente
> **Rama de Trabajo:** `inc/imagenes-catalogo`
> **Worktree:** `C:\repos\ludeka-wt\imagenes-catalogo`
> **Dependencias:** —
> **Especificación Viva:** [23. Almacenamiento y Optimización de Medios](file:///c:/repos/Ludeka/docs/specs/sistema/23-almacenamiento-y-optimizacion-de-medios.md) · [26. Pipeline de Medios e Imágenes](file:///c:/repos/Ludeka/docs/specs/sistema/26-pipeline-de-medios-e-imagenes.md) · [27. Ingesta Masiva BGG y Galería GeekDo](file:///c:/repos/Ludeka/docs/specs/sistema/27-ingesta-masiva-bgg-galeria-geekdo-ia-lotes.md)

---

## 1. Cómo se descubrió

El pipeline de medios ya existe desde INC-40 (R2 + SkiaSharp + WebP), pero nadie ha medido si las imágenes del catálogo cargan bien en la práctica. El maintainer percibe la portada y el catálogo como pesados y pide un diagnóstico fino antes de optimizar a ciegas.

## 2. El agujero, verificado

Lo que SÍ existe (evidencia de código):

- `SkiaSharpImageOptimizationService` (redimensionado y conversión WebP).
- `CloudflareR2StorageService` con `CacheControl = "public, max-age=31536000, immutable"` (línea 74).
- Variantes via `ImageVariantUrls(FullUrl, ThumbUrl)`; claves tipo `games/{bggId}/cover_thumb.webp`.
- `loading="lazy"` ya presente en `GameCard`, `MultimediaHub`, componentes `Home*`, `StoresDirectory`, `MyLibrary`, `PublicProfile`.
- Patrón `<picture>` con AVIF/WebP/JPG en `HeroEditorial` (INC-07).

Lo que NO está verificado (hueco de evidencia):

- **Diagnóstico fino pendiente:** no hay medición real de pesos por variante, tiempos de carga ni LCP/CLS del catálogo.
- `<picture>` no es sistemático fuera del hero (probable que `GameCard` use `<img>` simple).
- No se ha verificado el uso de `width`/`height` intrínsecos (riesgo de CLS).
- No está auditado si todas las variantes se sirven desde R2 o hay caídas al fallback local.

## 3. Lo que pide el maintainer

Medir antes de cortar: un diagnóstico real de peso y tiempos de las imágenes del catálogo, y a partir de ahí aplicar las optimizaciones que el dato justifique (formatos modernos, lazy, variantes).

## 4. Alcance y decisiones que hay que tomar

1. Diagnóstico empírico (Lighthouse / DevTools) de portada, catálogo y ficha de juego, con pesos y LCP/CLS medidos.
2. Extender `<picture>` (AVIF/WebP/JPG) a las tarjetas del catálogo si el diagnóstico lo apoya.
3. Dimensiones intrínsecas (`width`/`height`) en todas las imágenes de contenido.
4. **Decisión abierta:** revisar la política `immutable` de 1 año frente a la necesidad de invalidación al re-generar variantes.

## 5. Fuera de alcance (salvo que el maintainer diga lo contrario)

Re-ingeniería del pipeline R2/SkiaSharp, CDN nuevo y migración de proveedor de almacenamiento.

## 6. Criterios de aceptación

1. Existe un informe de diagnóstico con pesos y métricas reales por vista (portada, catálogo, ficha).
2. Las optimizaciones aplicadas citan el dato del diagnóstico que las justifica.
3. Sin regresión visual en `GameCard` ni en el hero.
4. Línea base: suite completa en verde con `dotnet test` al abrir el incremento.

## 7. Riesgos

- CLS al cambiar dimensiones renderizadas de tarjetas existentes.
- Invalidación de caché si se mantiene `immutable` y se re-generan variantes con el mismo nombre de clave.
- Optimizar sin medir (el error que este incremento viene a evitar).
