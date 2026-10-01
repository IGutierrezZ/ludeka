# Incremento 91: Runner Desatendido de Volcado Masivo de Snapshots Crudos BGG y Sincronización Continua Web

> **ID:** INC-91  
> **Slug:** `bgg-raw-runner`  
> **Rama:** `inc/bgg-raw-runner`  
> **Estado:** ✅ Archivado  
> **Módulos Impactados:** Módulo 34 (`docs/specs/sistema/34-trabajos-en-segundo-plano-cloud-run.md`), Módulo 47 (`docs/specs/sistema/47-snapshots-crudos-bgg-expansiones-sincronizacion.md`)  
> **Dependencias:** INC-90.  

---

## 1. Contexto y Diagnóstico

Tras la introducción de la tabla satélite `BggRawSnapshots` en el incremento INC-90, el catálogo de Ludeka cuenta con ~13.853 títulos que requieren disponer de su correspondiente snapshot crudo XML/JSON para permitir cálculos heurísticos locales y descubrimiento de expansiones.

La sincronización interactiva inicial operaba en lotes manuales de 20 juegos. Teniendo en cuenta la limitación de tasa obligatoria impuesta por los términos de servicio de BoardGameGeek (~1.200 ms entre peticiones consecutivas para evitar respuestas `429 Too Many Requests`), volcar la totalidad del catálogo requiere aproximadamente 4,6 horas de transferencia continua.

Mantener una pestaña de navegador abierta con un circuito SignalR activo durante horas es frágil frente a caídas de conexión, suspensiones del equipo o cierres accidentales. Por ello, se requería:
1. Un runner de consola desatendido, autónomo e interrumpible en `Ludeka.Jobs` (`bgg-raw-backfill`) ejecutable vía CLI (`dotnet run --project src/Ludeka.Jobs -- bgg-raw-backfill`) o como Cloud Run Job.
2. Un botón interactivo de sincronización continua en segundo plano en `/admin/cola-catalogacion` que encadene lotes automáticamente con opción de pausa reactiva y telemetría en vivo.
3. Al término del volcado de todos los títulos pendientes, disparo automático de auto-vinculación de expansiones huérfanas existentes y descubrimiento/encolado de expansiones satélite faltantes.

---

## 2. Objetivos Técnicos Implementados

### 2.1 Contratos y Métodos de Sistema sin Guarda de Sesión
- En `IBggRawSnapshotSyncService` y `BggRawSnapshotSyncService`:
  - `RunScheduledSyncBatchAsync(batchSize, delayMs, ct)`: variante de sistema sin comprobación de permisos de sesión interactiva (`DenyAllSessionPermissionGuard` en `Ludeka.Jobs`).
  - `RunScheduledDiscoverAndEnqueueMissingExpansionsAsync(maxToEnqueue, ct)`.
  - `RunScheduledAutoLinkExistingExpansionsAsync(ct)`.

### 2.2 Runner en `Ludeka.Jobs` (`BggRawBackfillJobRunner`)
- Nombre de job: `JobNames.BggRawBackfill = "bgg-raw-backfill"`.
- Coordinación con arrendamiento de ventana temporal (`IJobExecutionCoordinator.ExecuteWithWindowLeaseAsync`).
- Procesamiento en lotes de 50 títulos hasta agotar pendientes (`ProcessedCount == 0`).
- Latencia defensiva de 1.200 ms entre peticiones XMLAPI2.
- Fase final de auto-vinculación de expansiones y encolado de descubrimientos.
- Registro en `JobRunnerServiceCollectionExtensions` y cobertura en `JobSelectionResolver`.

### 2.3 Sincronización Continua en Interfaz Administrativa (`CatalogQueueAdmin.razor`)
- Botón dual «Sincronización Total en Segundo Plano» / «Pausar Sincronización Continua».
- Control cooperativo con `CancellationTokenSource` y limpieza en `Dispose()`.
- Polling encadenado con actualización reactiva de estadísticas y banner informativo con el progreso de cada lote y títulos pendientes restantes.
- Documentación contextual visible en la tarjeta con el comando CLI para ejecución desatendida en terminal.

---

## 3. Verificación y Resultados

- **Pruebas Unitarias:** 2.185 pruebas aprobadas al 100% (+6 pruebas respecto a INC-90).
- **Cobertura Específica Añadida:**
  - `BggRawBackfillJobRunnerTests`: Validación de inyección nula, resolución de nombre de trabajo e iteración secuencial de lotes con auto-vinculación y descubrimiento final.
  - `LudekaJobsCompositionTests`: Verificación de composición del contenedor DI con 9 runners activos (incluyendo `BggRawBackfillJobRunner`).
  - `JobSelectionResolverTests`: Resolución de comando `bgg-raw-backfill` validada en tabla de parámetros.
