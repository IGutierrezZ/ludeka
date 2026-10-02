# INC-99: Reclasificación y Vinculación Masiva de Expansiones desde Snapshots BGG y Saneamiento de Fallidos en Staging

> **Estado:** ⏳ En progreso  
> **Fecha:** 2026-10-02  
> **Rama:** `inc/vinculacion-expansiones-snapshots`  
> **Worktree:** `F:\repos\ludeka-wt\vinculacion-expansiones-snapshots`  

---

## 1. Contexto y Diagnóstico del Problema

1. **Expansiones no reconocidas ni vinculadas en el catálogo caliente**:
   - De los 17.464 títulos presentes en la tabla `Games`, solo 41 estaban clasificados con `GameType.Expansion` (correspondientes a los datos semilla iniciales).
   - Los más de 17.000 juegos importados y promovidos desde el pipeline de Staging fueron instanciados mediante el constructor por defecto de `Game`, el cual asigna incondicionalmente `GameType.BaseGame`.
   - Por esta razón, el método `AutoLinkExistingExpansionsAsync` (que filtraba únicamente por `g.Type == GameType.Expansion && g.BaseGameId == null`) solo evaluaba 2 títulos huérfanos, ignorando los miles de títulos de expansión que figuran erróneamente como juego base.
   - Disponemos del 100% de los payloads brutos en la tabla satélite `BggRawSnapshots` (17.464 registros), conteniendo el atributo `@type="boardgameexpansion"` y las relaciones bidireccionales `<link type="boardgameexpansion">` (entrantes con `@inbound="true"` y salientes desde los juegos base).

2. **Confusión en la acción «Descubrir y Encolar Expansiones»**:
   - Dicha acción no opera sobre el catálogo existente, sino que extrae enlaces a expansiones no catalogadas y añade un lote (máximo 50) a la cola de importaciones pendientes (`PendingBggImports`). El usuario interpretaba que debía vincular las expansiones ya presentes en su catálogo.

3. **Métrica opaca de 2.020 «Fallidos» en Staging**:
   - `BggStagingMetricsDto.FailedCount` agregaba cualquier registro con fallo en `Fetch`, `Images`, `Ai` o `Promotion`.
   - De los 17.603 títulos en staging, 17.393 se promovieron con éxito al catálogo. Solo ~210 títulos quedaron estancados sin promover (generalmente por fallos en `Fetch` debidos a IDs retirados en BGG o errores de red).
   - Los ~1.810 restantes corresponden a títulos ya promovidos y publicados que sufrieron una incidencia accesoria (fotos en R2 o síntesis de Gemini).

---

## 2. Objetivos del Incremento

1. **Reconciliación y Vinculación Masiva desde Snapshots Locales**:
   - Desarrollar un proceso por lotes (`ReconcileAndLinkExpansionsFromSnapshotsAsync`) que recorra los 17.464 snapshots en `BggRawSnapshots` sin invocar a la API externa de BGG.
   - Identificar deterministamente los títulos que en BGG son expansiones:
     - Actualizar su `Type` a `GameType.Expansion`.
     - Si su juego base canónico existe en el catálogo, asignar `expGame.SetBaseGameId(baseGame.Id)`.
     - Si su juego base no existe en el catálogo, marcarlo como `GameType.Expansion` huérfana.
   - Cruzar relaciones salientes desde los juegos base: vincular toda expansión en catálogo que figure en la lista de expansiones hijas del juego base.

2. **Corrección de Promoción en Staging**:
   - En `BggMassIngestionService.PromoteReadyToCatalogBatchAsync`, determinar si el elemento de staging o su snapshot asociado corresponde a una expansión antes de instanciar `new Game(...)`, garantizando que las futuras promociones nazcan con `GameType.Expansion` y su `BaseGameId` asignado.

3. **Ejecución Dual (Web interactiva y CLI desatendida en Ludeka.Jobs)**:
   - Añadir en `/admin/cola-catalogacion` la acción interactiva «Reconciliar y Vincular Expansiones» con barra de progreso y telemetría de reclasificadas y vinculadas.
   - Crear el runner de consola `bgg-reconcile-expansions` en `Ludeka.Jobs` (`BggReconcileExpansionsJobRunner`) para su ejecución en segundo plano sin límite de sesión web.

4. **Desglose de la Métrica de Fallidos en Staging**:
   - Desglosar en `BggStagingMetricsDto` los fallos que bloquean la promoción (`PendingPromotionBlockedCount` / `FailedFetchCount`) de las advertencias accesorias en títulos ya promovidos.
   - Reflejar en la tarjeta de `/admin/cola-catalogacion` el estado real de los ~210 títulos no promovidos.

---

## 3. Criterios de Aceptación y Verificación

1. **Unit Tests**:
   - Prueba unitaria de reconciliación determinista: reclasificación de `GameType.BaseGame` a `GameType.Expansion` mediante snapshots.
   - Prueba unitaria de asignación de `BaseGameId` para expansiones entrantes (`inbound="true"`) y salientes (`outbound`).
   - Prueba unitaria de promoción en staging respetando el tipo de expansión.
   - Prueba unitaria del desglose de métricas de staging.
2. **Suite Completa en Verde**:
   - Las 2.257 pruebas automáticas deben mantenerse al 100% en verde.
