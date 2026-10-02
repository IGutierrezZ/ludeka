# Propuesta de Cambio: Reclasificación y Vinculación Masiva de Expansiones desde Snapshots BGG y Saneamiento de Fallidos en Staging (INC-99)

## 1. Problema y Motivación

En la base de datos de producción (`ludeka.es`), de los 17.464 juegos dados de alta en el catálogo caliente:
1. **Solo 41 títulos están clasificados con `GameType.Expansion`**, a pesar de que hay miles de expansiones reales. El motivo es que el pipeline de Staging (`BggMassIngestionService.PromoteReadyToCatalogBatchAsync`) instanciaba `new Game(...)` sin parámetro de tipo, asignando el valor predeterminado `GameType.BaseGame`.
2. **Las acciones existentes no pueden vincularlas**:
   - «Auto-vincular Huérfanas» (`AutoLinkExistingExpansionsAsync`) busca únicamente registros con `g.Type == GameType.Expansion && g.BaseGameId == null`, por lo que solo evaluaba 2 títulos huérfanos y pasaba por alto el resto.
   - «Descubrir y Encolar Expansiones» busca expansiones inexistentes en el catálogo y las añade a la cola de pendientes (`PendingBggImports`), no vincula las ya existentes.
3. **Métrica opaca de 2.020 «Fallidos» en Staging**:
   - `BggStagingMetricsDto.FailedCount` agrega cualquier incidencia en `Fetch`, `Images`, `Ai` o `Promotion`.
   - 17.393 de los 17.603 títulos de staging ya están promovidos y publicados. Solo ~210 títulos están realmente estancados sin haber podido llegar al catálogo (principalmente por fallos en `Fetch` de BGG). Los ~1.810 restantes sufrieron fallos secundarios (imágenes en R2 o IA) pero ya están en el catálogo.

Disponemos de **17.464 payloads completos en `BggRawSnapshots`** (100% sincronizados en la tabla satélite). Esta propuesta plantea explotar estos snapshots locales de forma determinista para reclasificar y vincular todas las expansiones del catálogo sin peticiones HTTP externas.

---

## 2. Enfoque de la Solución

1. **Servicio de Reconciliación Masiva (`ReconcileAndLinkExpansionsFromSnapshotsAsync`)**:
   - Procesa los 17.464 snapshots locales en lotes de 500 registros.
   - Para cada snapshot:
     - Si `@type == "boardgameexpansion"` o contiene un enlace entrante `<link type="boardgameexpansion" id="{baseId}" inbound="true"/>`:
       - Busca el juego en `Games` por `BggId`. Si existe y su tipo es `BaseGame`, actualiza su tipo a `GameType.Expansion`.
       - Si su juego base está en `Games`, asigna `expGame.SetBaseGameId(baseGame.Id)`.
     - Si el snapshot contiene enlaces salientes de expansiones (`<link type="boardgameexpansion" id="{expId}"/>` sin inbound):
       - Busca si esas expansiones hijas existen en `Games`. Si existen, actualiza su tipo a `GameType.Expansion` y asigna `exp.SetBaseGameId(baseGame.Id)`.
   - Métricas y telemetría de retorno: `ReconciledExpansionsCount`, `LinkedExpansionsCount`, `TotalSnapshotsEvaluated`.

2. **Blindaje de la Promoción en Staging**:
   - En `PromoteReadyToCatalogBatchAsync`, consultar si el juego a promover es una expansión en su snapshot asociado. Si lo es, crearlo directamente con `GameType.Expansion` y enlazar `BaseGameId` si su base ya está en catálogo.

3. **Módulo Desatendido y UI**:
   - Nuevo runner de consola en `Ludeka.Jobs`: `dotnet run --project src/Ludeka.Jobs -- bgg-reconcile-expansions` (`BggReconcileExpansionsJobRunner`).
   - Botón interactivo en `/admin/cola-catalogacion`: «Reconciliar Expansiones desde Snapshots» con telemetría en tiempo real.
   - Desglose en `BggStagingMetricsDto` distinguiendo títulos no promovidos (`BlockedCount` / `FailedFetchCount`) de advertencias accesorias en juegos ya promovidos.

---

## 3. Impacto Arquitectónico y Dependencias

- **Cero peticiones HTTP externas**: Todo el proceso se apoya en los snapshots satélite ya almacenados en `BggRawSnapshots`.
- **Rendimiento**: Procesamiento optimizado por lotes con scopes limpios de `DbContext` para evitar sobrecarga del change tracker de EF Core.
- **Retrocompatibilidad**: Sin roturas de esquema ni migraciones bloqueantes; se utilizan las entidades y propiedades existentes (`Game.Type`, `Game.BaseGameId`).

---

## 4. Criterios de Aceptación

- `ReconcileAndLinkExpansionsFromSnapshotsAsync` reclasifica correctamente los títulos identificados como expansiones a `GameType.Expansion`.
- Se asigna `BaseGameId` de forma bidireccional (entrante y saliente).
- El recuento de `Expansiones en Catálogo` en `/admin/cola-catalogacion` refleja fielmente todas las expansiones reconocidas.
- La métrica de staging desglosa los fallos reales sin promover (~210) de los fallos accesorios.
- La suite completa de pruebas unitarias e integración se mantiene en verde.
