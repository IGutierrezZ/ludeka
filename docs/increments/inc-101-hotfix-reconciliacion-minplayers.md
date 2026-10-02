# INC-101: Saneamiento Defensivo de Escalabilidad (minPlayers) y Blindaje de Reconciliación Masiva de Expansiones

> **Estado:** ⏳ En progreso  
> **Fecha:** 2026-10-02  
> **Rama:** `inc/hotfix-reconciliacion-minplayers`  
> **Worktree:** `F:\repos\ludeka-wt\hotfix-reconciliacion-minplayers`  

---

## 1. Contexto y Causa Raíz

Al ejecutar en el entorno real la nueva acción administrativa **«Reconciliar Expansiones desde Snapshots»** implementada en INC-100, el proceso abortó arrojando la siguiente excepción:
```
Error al reconciliar expansiones: El número mínimo de jugadores debe ser mayor a 0. (Parameter 'minPlayers')
```

### Diagnóstico de la Causa Raíz
1. **Entradas espurias con 0 jugadores en encuestas BGG:** En BoardGameGeek, ciertas encuestas de número de jugadores (`suggested_numplayers`) para accesorios, variantes o expansiones contienen opciones con `numplayers="0"` o `numplayers="0+"`. Al parsear el XML en `BggXmlParser.ParseScalability`, se registraban entradas en `Scalability` con `PlayerCount = 0`.
2. **Cálculo no saneado en `SqliteGameRepository.UpdateAsync`:** Al sincronizar el catálogo caliente, se computaba:
   ```csharp
   int minPlayers = game.Scalability.Count > 0 ? game.Scalability.Min(s => s.PlayerCount) : 1;
   ```
   Si la lista contenía una entrada con `PlayerCount = 0`, `minPlayers` resultaba ser 0. Al invocar `existing.UpdateCatalogInformation(..., minPlayers, maxPlayers)`, el método de dominio lanzaba `ArgumentOutOfRangeException` porque `minPlayers <= 0`.
3. **Falta de aislamiento por unidad en la reconciliación:** En `BggRawSnapshotSyncService.ReconcileAndLinkExpansionsFromSnapshotsCoreAsync`, las actualizaciones sobre `_gameRepo.UpdateAsync(g, ct)` no contaban con un `try-catch` granular, provocando que un único juego con anomalías detuviera la reconciliación de los 17.464 títulos.

---

## 2. Solución Arquitectónica

1. **Invariante en Dominio (`Game.cs`):**
   - En `UpdateScalability`, filtrar incondicionalmente para ignorar cualquier entrada con `PlayerCount <= 0`.
2. **Parser XML Blindado (`BggXmlParser.cs`):**
   - En `ParseScalability`, descartar explícitamente cualquier resultado con `playerCount <= 0`.
3. **Persistencia Defensiva en Repositorio (`SqliteGameRepository.cs`):**
   - Filtrar `validPlayerCounts` con `PlayerCount > 0` antes de calcular `minPlayers` y `maxPlayers`, asegurando siempre `Math.Max(1, ...)` y `maxPlayers >= minPlayers`.
   - Saneamiento en cascada de `Scalability` al actualizar el catálogo.
4. **Saneamiento Preventivo en Servicios de Aplicación:**
   - En `BggMassIngestionService`, `ExpansionService`, `InstagramComposerService`, `BggSimulationDataset` y `GameEditorModal`.
5. **Aislamiento y Tolerancia a Fallos en Reconciliación (`BggRawSnapshotSyncService.cs`):**
   - Envoltura `try-catch` por juego dentro del bucle de reconciliación de snapshots, garantizando que un error aislado de integridad en un título no interrumpa el procesamiento del resto del lote.

---

## 3. Pruebas y Verificación

- **Nueva prueba unitaria en `BggExpansionReconciliationTests.cs`:**
  - `Reconcile_GameWithZeroPlayerCountInScalability_DoesNotThrowAndSanitizesScalability`: simula una expansión con `PlayerCount = 0` en su `Scalability`, verificando que la reconciliación no lanza excepción, reclasifica el juego a `GameType.Expansion`, lo enlaza al juego base y purga los comensales `<= 0`.
- **Suite completa:** 2.291 pruebas unitarias pasando al 100% en verde.
