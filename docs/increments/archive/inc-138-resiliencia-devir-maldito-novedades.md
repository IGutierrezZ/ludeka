# INC-138: Resiliencia en Extracción de Novedades (Devir y Maldito Games) y Despliegue de Jobs

## Estado
✅ Archivado

## Contexto y Motivación
Durante la ejecución del barrido de imágenes de Devir en Cloud Run (`devir-images-backfill`), Cloudflare bloqueó la paginación a partir de la página 2 con HTTP 403 Forbidden debido a colisiones de cabeceras User-Agent y ausencia de cabeceras de navegación humana. Además, el job `editorial-releases-sync` no cargaba novedades de Maldito Games porque un fallo o timeout en la consulta del catálogo cronológico abortaba el procesamiento completo de la portada mediante `Task.WhenAll`. Por último, el job `devir-images-backfill` no estaba dado de alta en el bucle de despliegue de Cloud Run en GitHub Actions.

## Objetivos
1. Saneamiento de cabeceras HTTP de navegación para `DevirReleasesExtractor` y `MalditoReleasesExtractor`.
2. Mecanismo de reintento comedido (máx. 1 reintento con 1.5s de retardo) ante respuestas 403, 429 o fallos de red en ambos extractores.
3. Desacoplo de la descarga de portada y catálogo en Maldito Games: si el catálogo no responde o falla, los más de 40 lanzamientos de la portada se extraen e incorporan a la bandeja de moderación sin perderse.
4. Ritmo cortés (throttle) y tolerancia a fallos aislados en `DevirImagesBackfillJobRunner`.
5. Registro de `devir-images-backfill` en `.github/workflows/ci-cd.yml` para despliegue automatizado.
