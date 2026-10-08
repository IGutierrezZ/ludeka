# Propuesta: INC-137 — Resiliencia en Extracción de Novedades (Devir y Maldito Games) y Despliegue de Jobs

## 1. Contexto y Problema

Durante la ejecución en producción sobre Google Cloud Run (`europe-west1`), se han identificado dos anomalías críticas en el sistema de sincronización y enriquecimiento de novedades editoriales:

1. **Bloqueo 403 Forbidden por WAF de Cloudflare en Devir**:
   - Al ejecutar el job `devir-images-backfill` (`DevirImagesBackfillJobRunner`), la página 1 del catálogo general de Devir (`https://devir.es/catalogo/juegos-de-mesa`) se descargó con éxito (36 productos evaluados, 11 emparejados, 3 actualizados en la base de datos).
   - Sin embargo, la página 2 (`https://devir.es/catalogo/juegos-de-mesa?p=2`) fue rechazada de inmediato en 8.9 ms con código `HTTP 403 Forbidden` por el WAF de Cloudflare.
   - **Causa raíz identificada**: `DevirReleasesExtractor.ExtractCatalogPageAsync` agregaba explícitamente una cabecera `User-Agent: Mozilla/5.0... Chrome/120.0.0.0` a nivel de `HttpRequestMessage`. Dado que `LudekaServiceCollectionExtensions.AddLudekaExternalIntegrations` ya configuraba `Chrome/122.0.0.0` en los `DefaultRequestHeaders` de `HttpClient`, el runtime de .NET combinaba ambas cabeceras en una sola lista separada por comas (`Chrome/122.0.0.0, Mozilla/5.0...`), lo que Cloudflare detecta instantáneamente como cabecera sintética de bot/scraper desde IPs de centro de datos. Además, la petición carecía de cabeceras de navegación habituales del navegador (`Accept`, `Accept-Language`, `Referer`, `Sec-Ch-Ua`).
   - Adicionalmente, el bucle en `DevirImagesBackfillJobRunner` abortaba inmediatamente ante cualquier página sin resultados o con error HTTP, sin pausa entre páginas y sin tolerancia frente a cortes transitorios.

2. **Fallo en Extracción de Novedades de Maldito Games y Ausencia en Moderación**:
   - En `ludeka-job-editorial-releases-sync` (`EditorialReleasesSyncJobRunner`), no se registraba ninguna novedad de Maldito Games en base de datos ni en la bandeja de moderación.
   - **Causa raíz identificada**: En `MalditoReleasesExtractor.ExtractReleasesAsync`, se ejecutaba de forma simultánea `Task.WhenAll(homeHtmlTask, catalogHtmlTask)` hacia `DefaultMalditoHomeUrl` (`https://tienda.malditogames.com/`) y `DefaultMalditoCatalogUrl` (`https://tienda.malditogames.com/juegos?product_list_order=creation_time&product_list_dir=desc`). Si la petición de catálogo (pesada, ~192 KB en Magento) experimentaba latencia, timeout o desafío de Cloudflare por falta de cabeceras de navegación, `Task.WhenAll` arrojaba una excepción. El bloque `catch` descartaba por completo los 47 lanzamientos válidamente extraídos de la portada y devolvía una lista vacía.
   - Al devolver 0 elementos, `EditorialReleasesSyncService` registraba una advertencia y no insertaba ninguna novedad en estado `PendingModeration` ni vinculaba juegos locales.

3. **Ausencia de `devir-images-backfill` en el Workflow de Despliegue de CI/CD**:
   - En `.github/workflows/ci-cd.yml`, el bucle de despliegue de Cloud Run Jobs (`for JOB in ...`) no incluía `devir-images-backfill`. Esto provocaba que el recurso `ludeka-job-devir-images-backfill` no existiera en GCP Cloud Run a menos que se desplegase manualmente.

## 2. Alcance de la Solución (Opción 2 — Reintento Comedido y Aislamiento Resiliente)

Conforme a la directriz ("probar la opción 2 sin ser demasiado pesado con los reintentos y revisar las novedades de Maldito Games"), se implementan las siguientes mejoras:

1. **Saneamiento de Cabeceras HTTP de Navegador**:
   - Eliminar cabeceras duplicadas o en conflicto de `User-Agent`.
   - Inyectar cabeceras consistentes de navegación en las peticiones de `DevirReleasesExtractor` y `MalditoReleasesExtractor`: `Accept` para documentos HTML, `Accept-Language: es-ES,es;q=0.9,en;q=0.8`, `Sec-Ch-Ua`, y `Referer` contextual (`https://devir.es/catalogo/juegos-de-mesa` para paginación de Devir; `https://tienda.malditogames.com/` para catálogo de Maldito).

2. **Reintento Comedido (1 reintento con retardo moderado)**:
   - Ante respuestas `403 Forbidden`, `429 Too Many Requests` o fallos transitorios en la paginación de Devir o en las fuentes de Maldito Games, ejecutar a lo sumo **un único reintento** tras un intervalo de 1.5 a 2.0 segundos.
   - Incorporar un ritmo de espera cortés (throttle de 600 ms a 1.000 ms) entre páginas consecutivas de Devir para no activar la heurística de ráfaga del WAF.

3. **Desacoplo y Resiliencia en Extracción de Maldito Games**:
   - Aislar la obtención de `homeHtml` y `catalogHtml`. Si la consulta al catálogo cronológico falla o sufre timeout, procesar los lanzamientos de la portada (garantizando los 47 ítems de novedades, preventas y reimpresiones).
   - Solo si ambas fuentes fallan se reportará fallo total.
   - Garantizar que los elementos no enlazados entren a moderación en estado `PendingModeration` con su respectiva sugerencia IA.

4. **Inclusión de `devir-images-backfill` en CI/CD**:
   - Incorporar `devir-images-backfill` en la lista de trabajos desplegados automáticamente en `.github/workflows/ci-cd.yml`.
