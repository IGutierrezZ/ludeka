# Tareas: INC-137 — Resiliencia en Extracción de Novedades (Devir y Maldito Games) y Despliegue de Jobs

## Fase 1: Extractor de Devir y Job de Barrido
- [ ] 1.1 Actualizar `DevirCatalogPageResultDto` para incluir bandera `Success` (indicando si la llamada HTTP fue exitosa).
- [ ] 1.2 Implementar método auxiliar de construcción de peticiones de navegador `CreateBrowserNavRequest` en `DevirReleasesExtractor`.
- [ ] 1.3 Eliminar la cabecera `User-Agent` duplicada en `ExtractCatalogPageAsync` y dotar a la consulta de 1 reintento comedido (1.500 ms) ante 403, 429 o 5xx.
- [ ] 1.4 Refactorizar `DevirImagesBackfillJobRunner` para aplicar un throttle cortés de 750 ms entre páginas y tolerar hasta 2 fallos consecutivos antes de interrumpir el barrido.
- [ ] 1.5 Crear pruebas unitarias en `DevirReleasesExtractorTests` y `DevirImagesBackfillJobRunnerTests` validando reintentos y tolerancia.

## Fase 2: Extractor de Maldito Games y Servicio de Sincronización
- [ ] 2.1 Desacoplar la descarga de portada y catálogo en `MalditoReleasesExtractor.ExtractReleasesAsync`, asegurando que el fallo de catálogo permita seguir procesando las novedades de la portada.
- [ ] 2.2 Aplicar cabeceras completas de navegador y reintento comedido en las peticiones HTTP de `MalditoReleasesExtractor`.
- [ ] 2.3 Crear pruebas unitarias en `MalditoReleasesExtractorTests` verificando la extracción exitosa ante fallo del catálogo.
- [ ] 2.4 Verificar que `EditorialReleasesSyncService` inserte correctamente en moderación las novedades no emparejadas con el catálogo local.

## Fase 3: Despliegue CI/CD y Documentación de Roadmap
- [ ] 3.1 Añadir `devir-images-backfill` a la lista de Cloud Run Jobs en `.github/workflows/ci-cd.yml`.
- [ ] 3.2 Actualizar `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md` incorporando INC-137 en estado `⏳ En progreso`.
- [ ] 3.3 Crear `docs/increments/inc-137-resiliencia-devir-maldito-novedades.md`.
- [ ] 3.4 Ejecutar suite completa de pruebas unitarias y verificar 100% verde.
