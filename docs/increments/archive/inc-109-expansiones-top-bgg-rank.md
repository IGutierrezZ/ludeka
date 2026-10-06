# INC-109: Descubrimiento de Expansiones Priorizado por BggRank de Juegos Base y Ampliación de Cupo a 1.200 Títulos

> **Estado:** ⏳ Verificado (Listo para PR y Cierre)  
> **Fecha de Inicio:** 2026-10-05  
> **Fecha de Cierre:** 2026-10-05  
> **Tipo:** Feature / Optimización de Catálogo / Priorización de Dominio  
> **Rama:** `inc/expansiones-top-bgg-rank`  
> **Worktree:** `F:\repos\ludeka-wt\expansiones-top-bgg-rank`

---

## 1. Contexto y Diagnóstico del Problema

En el análisis empírico tras la implantación de INC-107, se detectó que el botón de administración «Descubrir y Encolar Expansiones» reportaba más de 9.000 candidatas a expansiones pero el usuario observaba que no correspondían a los juegos conocidos y populares de Ludeka.

### Causa Raíz
1. `DiscoverAndEnqueueMissingExpansionsCoreAsync` en `BggRawSnapshotSyncService.cs` obtenía los snapshots llamando a `_snapshotRepo.GetAllSnapshotsAsync(2000)`.
2. Sin cláusula `ORDER BY`, SQLite/PostgreSQL recuperaba los primeros 2.000 registros por orden natural de clave primaria (`BggId` ASC: 1, 2, 3... correspondientes a títulos de 1995-2001 como *Die Macher*, *Samarkand*, etc.).
3. Como consecuencia, las expansiones descubiertas pertenecían a juegos arcaicos en lugar de los juegos base más jugados y de mayor tracción comunitaria en Ludeka (*Terraforming Mars*, *Wingspan*, *Dune: Imperium*, *Catan*, *Ark Nova*, *Root*, etc.).
4. Adicionalmente, el cupo de encolado interactivo estaba prefijado en 50 títulos, lo que obligaba a cientos de ejecuciones manuales para nutrir la cola con el ecosistema de expansiones de referencia.

---

## 2. Objetivos y Solución Implementada

1. **Priorización por BggRank de Juegos Base (Opción A):**
   - Incorporado en `IGameRepository` y `SqliteGameRepository` el método `GetTopRankedBaseGameBggIdsAsync(int limit = 1500, CancellationToken ct = default)` para obtener los identificadores BGG de los juegos base del catálogo ordenados por `BggRank` ascendente (`g.BggRank.HasValue && g.BggRank.Value > 0 && g.Type == GameType.BaseGame && g.BggId > 0`).
2. **Carga y Enlace de Snapshots de Juegos Base Top:**
   - Incorporado en `IBggRawSnapshotRepository` y `SqliteBggRawSnapshotRepository` el método `GetSnapshotsByBggIdsAsync(IEnumerable<int> bggIds, CancellationToken ct = default)`.
   - Refactorizado `DiscoverAndEnqueueMissingExpansionsCoreAsync` en `BggRawSnapshotSyncService` para procesar los snapshots de los juegos base en estricto orden de popularidad (`BggRank` ASC) de Ludeka, con complementación defensiva desde el resto de snapshots (`GetAllSnapshotsAsync`) si no se alcanza el cupo o para entornos de prueba.
3. **Ampliación de Cupo de Descubrimiento a 1.200 Títulos:**
   - Elevado el parámetro `maxToEnqueue = 1200` en contratos y en la invocación interactiva de `CatalogQueueAdmin.razor`.
   - El filtrado inteligente de dos niveles de INC-107 (pre-filtro léxico anti-promos + verificación de umbrales comunitarios/edición en español en bloques de 20) cribará y seleccionará hasta las 1.200 expansiones más relevantes del catálogo.
4. **Actualización Editorial de UI:**
   - Actualizado el botón en `CatalogQueueAdmin.razor` a «Descubrir Expansiones Top (1.200)».
5. **Cobertura de Pruebas Unitarias:**
   - Pruebas unitarias específicas:
     - `DiscoverAndEnqueueMissingExpansionsAsync_PrioritizesTopRankedBaseGames_OverOldSnapshots`
     - `GetTopRankedBaseGameBggIdsAsync_ShouldReturnBaseGamesOrderedByRank`
     - `GetSnapshotsByBggIdsAsync_ShouldReturnRequestedSnapshots`
   - Total: 2.447 pruebas unitarias superadas al 100% en verde.

---

## 3. Plan de Trabajo ODD (Work Units)

- [x] **Unidad 1:** Métodos de contrato e infraestructura para consultar juegos base Top por `BggRank` y snapshots por lote de IDs (`IGameRepository`, `SqliteGameRepository`, `IBggRawSnapshotRepository`, `SqliteBggRawSnapshotRepository`).
- [x] **Unidad 2:** Refactorización de `DiscoverAndEnqueueMissingExpansionsCoreAsync` en `BggRawSnapshotSyncService` para priorizar juegos base por `BggRank` y tests unitarios TDD asociados.
- [x] **Unidad 3:** Actualización de `CatalogQueueAdmin.razor` (cupo interactivo 1.200 y feedback visual editorial).
- [x] **Unidad 4:** Verificación de la suite completa (`dotnet test`), actualización de especificación viva del sistema y ciclo de worktree/PR.
