# INC-53: Ingesta Masiva Autónoma de Catálogo BGG (~8.000 Juegos) sin Manipulación Manual

> **Estado:** ✅ Archivado (completado el 2026-09-21)  
> **Fecha de Inicio:** 2026-09-21 · **Fecha de Cierre:** 2026-09-21  
> **Rama de Trabajo:** `inc/ingesta-masiva-autonoma-bgg`  
> **Worktree:** `C:\repos\ludeka-wt\ingesta-masiva-autonoma-bgg`  
> **Dependencias:** INC-41 (Ingesta Masiva BGG, archivado), INC-47 (Cloud Run Jobs, archivado), INC-48 (Persistencia Producción, archivado)  
> **Especificación Viva:** [27. Ingesta Masiva de Catálogo BGG (~8.000 títulos), Fotos GeekDo y Síntesis IA en Lotes](file:///c:/repos/Ludeka/docs/specs/sistema/27-ingesta-masiva-bgg-galeria-geekdo-ia-lotes.md) · [34. Trabajos en Segundo Plano Correctos en Google Cloud Run](file:///c:/repos/Ludeka/docs/specs/sistema/34-trabajos-en-segundo-plano-cloud-run.md)

---

## 1. Contexto y Motivación

Durante la verificación del primer despliegue en producción tras ejecutar manualmente el Cloud Run Job `ludeka-job-nightly-cataloging`, el maintainer observó que la Fase 3 de Staging reportaba:
`Ciclo de drenaje: 0 detalles, 0 imágenes, 0 síntesis IA, 0 promovidos`.

El módulo 27 (`INC-41`) construyó la infraestructura completa de staging, drenaje en lotes con Gemini y fotos hacia R2 a partir del parser de streaming `BggDumpParser`. Sin embargo, la premisa operativa requería que un operador suministrara manualmente el archivo de volcado `bg_ranks.csv` / `.csv.gz`.

El maintainer clarificó explícitamente el requisito de producto:
> «La idea no era generar yo un CSV, creía que podríamos traerlos de BGG con un filtro.»

**Principio rector:** El sistema debe ser 100% autónomo. El usuario/administrador no debe tener que buscar, descargar, convertir ni subir ficheros locales.

---

## 2. Alcance Técnico del Incremento y Resultados

### 2.1 Descarga Directa y Streaming desde el Backend
- Implementado en `BggMassIngestionService` el método autónomo `DownloadAndIngestLatestRanksAsync(...)` y `RunScheduledDownloadAndIngestLatestRanksAsync(...)` que obtiene el dataset de clasificación desde el mirror diario público en GitHub Raw / Fastly CDN (`beefsack/bgg-ranking-historicals`).
- Fallback temporal resiliente de hasta 5 días para mitigar desajustes de fecha o publicación.
- Streaming continuo con `HttpClient.SendAsync(..., HttpCompletionOption.ResponseHeadersRead)` en memoria acotada (< 30 MB) y soporte para streams no buscables (`inputStream.CanSeek == false`) en `BggDumpParser`.
- Filtrado al vuelo de tracción comunitaria (`usersrated >= 30`), extrayendo los ~8.000 títulos contrastados e insertando en lotes de 100 en `BggCatalogStaging`.

### 2.2 Botón de Disparo en el Panel de Administración
- En `/admin/cola-catalogacion`, incorporado el botón:
  *«Descargar y Poblar Catálogo BGG (~8.000 títulos)»*.
- Feedback reactivo y bloqueo mutuo concurrente con el drenaje (`_isSeedingStaging` y `_isDrainingStaging`).
- Revalidación estricta de permisos de moderación `ModeratorPermission.CanEditGames` (INC-46).

### 2.3 Modo Autónomo en Cloud Run Job y Auto-Siembra Nocturna
- Creado el runner fino de consola `SeedStagingJobRunner` (`seed-staging`) en `Ludeka.Jobs` coordinado bajo concesión de ventana e idempotencia diaria.
- Auto-siembra inteligente en Fase 3 del orquestador nocturno (`NightlyCatalogingService`): si staging está vacío (`TotalInStaging == 0`), descarga e ingesta automáticamente antes de iniciar el ciclo de drenaje progresivo.

---

## 3. Criterios de Aceptación Verificados

1. **Cero archivos manuales:** ✅ El maintainer pulsa un botón en la web o ejecuta el job en Cloud Shell y la tabla `BggCatalogStaging` se puebla con los ~8.000 juegos de mesa con comunidad real.
2. **Streaming en memoria acotada:** ✅ El proceso de descarga e ingesta no consume más de 30 MB de RAM adicionales gracias a `IAsyncEnumerable` y streaming HTTP continuo.
3. **Idempotencia:** ✅ Si la tabla ya contiene títulos, el proceso actualiza estadísticas y no genera duplicados.
4. **Drenaje continuo:** ✅ Una vez poblado Staging, los ciclos posteriores de `ludeka-job-nightly-cataloging` drenan progresivamente los lotes de IA con Gemini Flash y las fotos a Cloudflare R2.
5. **Suite de pruebas:** ✅ 1.604 pruebas unitarias + 10 de integración en verde al 100% (0 fallos).
