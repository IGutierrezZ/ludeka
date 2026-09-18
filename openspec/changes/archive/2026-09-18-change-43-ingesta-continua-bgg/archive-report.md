# Informe de Archivo — INC-43: Ingesta Continua y Auto-Descubrimiento de Novedades BGG

**Estado del Archivo:** `archivado parcial`  
**Fecha de Archivo:** 2026-09-18  
**Cambio:** `change-43-ingesta-continua-bgg`  
**Rama Origen:** `inc/ingesta-continua-bgg`  
**Decisión de Archivo:** Archivado sin re-aplicación; código funcional entregado y mergeado en `main`.

---

## 1. Divergencia de Enrutamiento

El comando nativo `gentle-ai sdd-status change-43-ingesta-continua-bgg` reportó `nextRecommended: apply` porque el fichero `tasks.md` registraba **0 tareas marcadas de 25 totales**.

Esta contabilidad es **desactualizada y falsa**:
- El código fuente está íntegramente implementado, probado y mergeado en `main` desde el commit `3cb8a9c` (PR #8, rama `inc/ingesta-continua-bgg`).
- Una verificación independiente confirmó 16 entregas funcionales de 25 tareas contra evidencia de código real (rutas:línea verificadas en C#, Razor y tests unitarios).
- Las casillas de `tasks.md` nunca se marcaron durante la ejecución real. **Por qué no se marcaron es desconocido**: no hay evidencia en el repositorio ni en el historial que lo explique, y este informe no especula al respecto. El hecho observable es que el código se mergeó y la contabilidad no se actualizó. El maintainer autorizó archivar sin reimplementar.

**Corrección en esta fase:** Se archiva la rama sin reimplementación. Las casillas permanecen sin marcar porque marcarlas a posteriori sin haber observado cada tarea en su momento sería fabricar un registro histórico falso.

---

## 2. Recuento de Tareas — Estado Final Verificado

**Total de tareas en `tasks.md`:** 25  
**Entregadas:** 16 (con evidencia de código real)  
**No entregadas:** 1 (tarea 5.1, hueco declarado)  
**Indeterminadas:** 8 (7 cabeceras de sección + 1 tarea de ejecución)

### 2.1 Las 16 Tareas Entregadas (Evidencia de Código Real)

1. **Tarea 1.1** — Enum `BggNewReleases` y `BggHotness`
   - Ubicación: `src/Ludeka.Core/Enums/CatalogQueueOrigin.cs:26` (`BggNewReleases = 3`)
   - Ubicación: `src/Ludeka.Core/Enums/CatalogQueueOrigin.cs:31` (`BggHotness = 4`)
   - **Verificado:** Enumeradores presentes y con valores asignados correctamente.

2. **Tarea 1.2** — `BggDiscoveryCount` en `NightlyCatalogingExecutionLog`
   - Ubicación: `src/Ludeka.Core/Entities/NightlyCatalogingExecutionLog.cs:18` (propiedad)
   - Ubicación: `src/Ludeka.Core/Entities/NightlyCatalogingExecutionLog.cs:35-54` (método `Complete(...)` con parámetro `bggDiscovery: 0`)
   - **Verificado:** Propiedad y firma de método presentes.

3. **Tarea 2.1** — DTO `BggDiscoveryResultDto`
   - Ubicación: `src/Ludeka.Application/DTOs/BggDiscoveryDtos.cs:8`
   - **Verificado:** Record de DTO definido.

4. **Tarea 2.2** — Interfaz `IBggDiscoveryService`
   - Ubicación: `src/Ludeka.Application/Contracts/IBggDiscoveryService.cs:10`
   - **Verificado:** Interfaz de contrato definida.

5. **Tarea 2.3** — `NightlyCatalogingResultDto` con `BggDiscoveryCount`
   - Ubicación: `src/Ludeka.Application/DTOs/NightlyCatalogingDtos.cs:46-59`
   - **Verificado:** DTO actualizado con propiedad `BggDiscoveryCount`.

6. **Tarea 3.1** — Implementación `BggDiscoveryService`
   - Ubicación: `src/Ludeka.Application/Features/Bgg/BggDiscoveryService.cs:19`
   - **Verificado:** Clase implementada.

7. **Tarea 3.2** — Lógica de Triple Deduplicación
   - Ubicación: `src/Ludeka.Application/Features/Bgg/BggDiscoveryService.cs:136-168` (tres niveles: catálogo `_gameRepo`, cola `_pendingRepo`, staging `_stagingRepo`)
   - **Verificado:** Lógica de deduplicación presente contra tres repositorios.

8. **Tarea 3.3** — Clasificación Inteligente por Año
   - Ubicación: `src/Ludeka.Application/Features/Bgg/BggDiscoveryService.cs:174-175` (lógica `isRecentRelease` → `BggNewReleases`/`BggHotness`)
   - **Verificado:** Filtrado por año implementado.

9. **Tarea 4.1** — Inyección de `IBggDiscoveryService` en `NightlyCatalogingService`
   - Ubicación: `src/Ludeka.Application/Features/NightlyCatalogingService.cs:36` (campo privado)
   - Ubicación: `src/Ludeka.Application/Features/NightlyCatalogingService.cs:53` (parámetro opcional en constructor)
   - **Verificado:** Dependencia inyectada con parámetro nullable.

10. **Tarea 4.2** — Fase 1.5 con Manejo de Excepciones
    - Ubicación: `src/Ludeka.Application/Features/NightlyCatalogingService.cs:117-130` (bloque try/catch con log)
    - **Verificado:** Fase 1.5 implementada entre Fase 1 y Fase 2.

11. **Tarea 5.2** — Registro en DI (`Program.cs`)
    - Ubicación: `src/Ludeka.Web/Program.cs:164` (`AddScoped<IBggDiscoveryService, BggDiscoveryService>`)
    - **Verificado:** Dependencia registrada en contenedor.

12. **Tarea 6.1** — Botón "Escanear Novedades BGG"
    - Ubicación: `src/Ludeka.Web/Components/Pages/CatalogQueueAdmin.razor:59` (botón)
    - Ubicación: `src/Ludeka.Web/Components/Pages/CatalogQueueAdmin.razor:53` (spinner de carga)
    - **Verificado:** Botón y spinner presentes.

13. **Tarea 6.2** — Panel de Resultados
    - Ubicación: `src/Ludeka.Web/Components/Pages/CatalogQueueAdmin.razor:165-191` (panel de resultado)
    - Ubicación: `src/Ludeka.Web/Components/Pages/CatalogQueueAdmin.razor:341` (estado de carga en UI)
    - **Verificado:** Panel de visualización implementado.

14. **Tarea 6.3** — Filtros y Badges con Iconos Lucide
    - Ubicación: `src/Ludeka.Web/Components/Pages/CatalogQueueAdmin.razor:313-333` (filtros de origen)
    - Ubicación: `src/Ludeka.Web/Components/Pages/CatalogQueueAdmin.razor:403-410` (badges con `<Icon Name="sparkles"/"trending-up">`, sin emojis)
    - **Verificado:** Filtros e iconografía sin emojis implementados.

15. **Tarea 7.1** — Suite de Pruebas `BggDiscoveryServiceTests`
    - Ubicación: `tests/Ludeka.UnitTests/Bgg/BggDiscoveryServiceTests.cs:20,62,93,124,152` (5 métodos de prueba)
    - Métodos verificados por nombre exacto:
      - `DiscoverAndEnqueueBggTrendsAsync_WhenCandidatesAreNew_EnqueuesWithCorrectOrigin`
      - `DiscoverAndEnqueueBggTrendsAsync_WhenGameAlreadyInCatalog_SkipsAndCountsAsAlreadyCataloged`
      - `DiscoverAndEnqueueBggTrendsAsync_WhenGameAlreadyInQueue_SkipsAndCountsAsAlreadyInQueue`
      - `DiscoverAndEnqueueBggTrendsAsync_WhenGameInStaging_SkipsAndCountsAsAlreadyInQueue`
      - `DiscoverAndEnqueueNewReleasesAsync_FiltersOutOlderReleases`
    - **Verificado:** Cinco métodos de prueba implementados.

16. **Tarea 7.2** — Pruebas de `NightlyCatalogingServiceTests`
    - Ubicación: `tests/Ludeka.UnitTests/Application/NightlyCatalogingServiceTests.cs:184` (método `ExecuteNightlyCatalogingAsync_WithDiscoveryService_ExecutesPhase1_5AndPersistsBggDiscoveryCount`)
    - **Verificado:** Prueba de integración implementada.

### 2.2 La 1 Tarea No Entregada

**Tarea 5.1** — Actualización de Dataset Simulado de BGG con Lanzamientos 2025/2026
- **Requerimiento Original:** Actualizar `SimulatedBggClient` con lanzamientos y tendencias de 2025/2026 para que el descubrimiento de novedades tuviera datos que encontrar en modo simulado.
- **Estado Real Verificado:**
  - `grep -r "2025\|2026" src/Ludeka.Infrastructure/Bgg/`: **cero coincidencias** en archivos de código.
  - Catálogo simulado cubre años 2000–2022 (ej. `src/Ludeka.Infrastructure/Bgg/BggSimulationDataset.cs:290-712`).
  - Último commit tocando `SimulatedBggClient.cs` y `BggSimulationDataset.cs`: `a9b61bb` de **INC-28** (anterior y ajeno a INC-43).
- **Consecuencia Práctica Declarada:**
  - En modo simulado, la clasificación `BggNewReleases` (cuya activación requiere `YearPublished >= CurrentYear - 1`) **carece de datos que la disparen**.
  - La lógica de clasificación existe y fue probada con dobles inyectados en pruebas unitarias.
  - El dataset simulado no ejercita la rama reciente de punta a punta.
- **No se corrige en esta fase.** Tarea 5.1 permanece incompleta.

### 2.3 Las 8 Tareas Indeterminadas

1. **Cabecera "1. Dominio y Modelo de Datos"** — No es una tarea sino un rótulo; sus subtareas 1.1 y 1.2 están entregadas.
2. **Cabecera "2. Contratos y DTOs"** — No es una tarea sino un rótulo; sus subtareas 2.1, 2.2 y 2.3 están entregadas.
3. **Cabecera "3. Implementación del Servicio de Descubrimiento"** — No es una tarea sino un rótulo; sus subtareas 3.1, 3.2 y 3.3 están entregadas.
4. **Cabecera "4. Integración en el Orquestador Nocturno"** — No es una tarea sino un rótulo; sus subtareas 4.1 y 4.2 están entregadas.
5. **Cabecera "5. Simulación y Registro en DI"** — No es una tarea sino un rótulo; su subtarea 5.2 está entregada, 5.1 no.
6. **Cabecera "6. UI Editorial y Panel de Administración"** — No es una tarea sino un rótulo; sus subtareas 6.1, 6.2 y 6.3 están entregadas.
7. **Cabecera "7. Pruebas Unitarias y Verificación"** — No es una tarea sino un rótulo; sus subtareas 7.1 y 7.2 están entregadas.
8. **Tarea 7.3 — "Verificar que toda la suite de pruebas unitarias compile y pase al 100%"** — No verificable de forma estática (requiere ejecución). El estado actual en `main` es 1417/1417 tests verdes, 0 fallos. No marcada como completada en esta fase.

---

## 3. Especificaciones Archivadas

**Deltas De Spec:** Existían dos especificaciones delta en `openspec/changes/change-43-ingesta-continua-bgg/specs/`:
- `bgg-new-releases-discovery/spec.md`
- `nightly-batch-continuous-ingest/spec.md`

**Specs Canónicas No Existían:** Los dominios `bgg-new-releases-discovery` y `nightly-batch-continuous-ingest` no tenían specs canonicales en `openspec/specs/`.

**Acción Ejecutada:** Copia mecánica de ambas especificaciones delta a sus ubicaciones canónicas:
- `openspec/specs/bgg-new-releases-discovery/spec.md` (creada)
- `openspec/specs/nightly-batch-continuous-ingest/spec.md` (creada)

**Verificación:** Ambas copias fueron verificadas con hash criptográfico (SHA-256):
- `bgg-new-releases-discovery`: Hash idéntico (origen = destino)
- `nightly-batch-continuous-ingest`: Hash idéntico (origen = destino)

---

## 4. Artefactos Del Cambio Preservados

La carpeta archivada `openspec/changes/archive/2026-09-18-change-43-ingesta-continua-bgg/` contiene:
- ✅ `proposal.md` — Propuesta SDD completa.
- ✅ `design.md` — Diseño técnico y arquitectura.
- ✅ `tasks.md` — Plan de tareas (0/25 casillas marcadas, estado histórico preservado).
- ✅ `verify-report.md` — Informe de verificación de 2026-09-14.
- ✅ `specs/bgg-new-releases-discovery/spec.md` — Especificación delta (ahora canónica).
- ✅ `specs/nightly-batch-continuous-ingest/spec.md` — Especificación delta (ahora canónica).

---

## 5. Observación: Alcance del `verify-report.md`

El archivo `verify-report.md` (de fecha 2026-09-14) verificó exitosamente 7 criterios funcionales (RF-01 a RF-07) y reportó 100% de la suite en verde. Sin embargo, su alcance no cubre:

- **Tarea 5.1 (Dataset Simulado):** No aparece en el `verify-report.md` como completada ni como omitida. Es un hueco de alcance del informe de verificación, no una afirmación de incompletitud.
- **Tarea 7.3 (Ejecución de la Suite):** Reportó que "la suite está en 1417/1417 verdes", satisfaciendo de hecho el criterio; pero el informe no vinculó explícitamente la ejecución con esta tarea.

Ambas situaciones se registran como **huecos de alcance del informe**, no como trabajo no verificado.

---

## 6. Transición de Estado

| Fase | Estado |
|------|--------|
| `sdd-propose` | ✅ Completada |
| `sdd-spec` | ✅ Completada |
| `sdd-design` | ✅ Completada |
| `sdd-tasks` | ✅ Completada |
| `sdd-apply` | ⚠️ No re-aplicada (código existente mergeado desde `inc/ingesta-continua-bgg`) |
| `sdd-verify` | ✅ Verificación independiente realizada; 16/25 entregadas, 1/25 no entregada, 8/25 indeterminadas |
| `sdd-archive` | ✅ Completada (esta fase) — Cambio archivado sin re-aplicación |

---

## 7. Conclusión

El cambio `change-43-ingesta-continua-bgg` está **archivado parcialmente**:

- ✅ **16 entregas funcionales verificadas** contra código real en `main`.
- ⚠️ **1 entrega incompleta** (Tarea 5.1: dataset simulado sin años recientes).
- ⚠️ **8 items indeterminados** (7 cabeceras de sección + 1 tarea sin ejecución observable).
- ✅ Especificaciones canónicas compuestas y almacenadas.
- ✅ Todos los artefactos del cambio preservados en el archivo.
- ⚠️ `tasks.md` mantiene 0/25 marcadas (contabilidad histórica preservada por integridad).

No hay bloqueadores. El hueco de la tarea 5.1 está registrado de forma explícita; futuros incrementos pueden abordarlo si el producto lo requiere.

---

## 8. Metadata

- **Generado por:** Claude Haiku 4.5 (`sdd-archive`)
- **Almacén de destino:** openspec (hybrid mode, specs canónicas + Engram mirror)
- **Fecha de archivo:** 2026-09-18
- **Ruta archivada:** `openspec/changes/archive/2026-09-18-change-43-ingesta-continua-bgg/`
