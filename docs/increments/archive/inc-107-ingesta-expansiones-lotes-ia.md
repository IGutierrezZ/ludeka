# INC-107: Ingesta Masiva en Lotes BGG (x20) e IA (x10), Filtrado Inteligente de Expansiones y Saneamiento de Cola Administrativa

> **Estado:** ✅ Archivado  
> **Fecha:** 2026-10-05  
> **Rama:** `inc/ingesta-expansiones-lotes-ia`  
> **Worktree:** `F:\repos\ludeka-wt\ingesta-expansiones-lotes-ia`  
> **Documento Vivo ODD:** `odd/tasks/inc-107-ingesta-expansiones-lotes-ia.md`  
> **Pruebas Automatizadas:** 2.436 pruebas unitarias en verde al 100% (+28 nuevas)  

---

## 1. Contexto y Objetivos

1. **Eficiencia en Ingesta Nocturna:**
   - La catalogación nocturna previa procesaba los títulos pendientes uno a uno (`DailyCatalogingLimit = 20`), requiriendo cientos de llamadas individuales a BGG y a Gemini.
   - Migración de `NightlyCatalogingService` a procesamiento por bloques:
     - Bloques de hasta 20 IDs por llamada HTTP a BGG XMLAPI2 (`/xmlapi2/thing?id=...&stats=1&versions=1`).
     - Bloques de hasta 10 juegos por llamada estructurada a Google Gemini Flash (`GenerateBatchSummariesAsync`).
   - Aumento del cupo diario por defecto a 400 juegos por noche (`DailyCatalogingLimit = 400`).
2. **Cribado Inteligente de Expansiones Anti-Promos:**
   - Previamente, el descubrimiento encolaba indiscriminadamente cualquier enlace `boardgameexpansion` sin distinguir expansiones completas en caja de promos de dos cartas o accesorios menores.
   - Implementación de un filtro en dos fases:
     1. **Pre-filtro léxico:** Detección y descarte de términos típicos de accesorios o cartas sueltas (`promo`, `bonus`, `pack`, `miniature`, `playmat`, `dice`, etc.).
     2. **Umbral de tracción comunitaria y comercial:** Enriquecimiento de candidatos consultando BGG con `stats=1` y versiones, admitiendo únicamente aquellos con tracción real (`usersrated >= 30` o `owned >= 100`) o con edición comercial registrada en español (título en español o editorial).
3. **Saneamiento Editorial de `/admin/cola-catalogacion`:**
   - Retirada de botones de un solo uso o redundantes de incrementos anteriores (sembrado del Directorio Lúdico ya automatizado en su runner propio, botones manuales de lote rápido de 20 para snapshots y versiones redundantes ante los procesos continuos y CLI, y reconciliación masiva de expansiones de INC-100 ya amortizada).
   - Eliminación de más de 260 líneas de marcado y lógica huérfana en `CatalogQueueAdmin.razor`.

---

## 2. Cambios de Arquitectura y Componentes Afectados

- **`Ludeka.Application`:**
  - `IBggClient`: Incorporación de `FetchGamesByBggIdsAsync(IEnumerable<int> bggIds, bool includeVersions = true, CancellationToken ct = default)` con implementación predeterminada.
  - `NightlyCatalogingService`: Orquestación por chunks de 20 para BGG y chunks de 10 para síntesis IA en `ProcessPriorityQueueAsync` y `BackfillTopBggGamesAsync`.
  - `NightlyCatalogingDtos`: `DailyCatalogingLimit = 400` en `NightlyCatalogingOptions`.
  - `BggRawSnapshotParser`:
    - `IsProbablePromoOrAccessory(string? title)`
    - `HasStatisticsFromJson(string rawJson)`
    - `ExtractCommunityStatsFromJson(string rawJson)`
    - `MeetsExpansionCommunityThresholdFromJson(string rawJson, int minUsersRated = 30, int minOwned = 100)`
- **`Ludeka.Infrastructure`:**
  - `BggXmlApiClient`: Implementación nativa de `FetchGamesByBggIdsAsync` construyendo URLs multi-item y extrayendo juegos completos con versiones unificadas.
  - `BggRawSnapshotSyncService`: `DiscoverAndEnqueueMissingExpansionsCoreAsync` aplica el filtro léxico sobre candidatos salientes de snapshots locales y consulta BGG en bloques de 20 para verificar umbrales antes de persistir en `PendingBggImports`.
- **`Ludeka.Web`:**
  - `CatalogQueueAdmin.razor`: Saneamiento de botones huérfanos, eliminación de inyecciones no utilizadas (`IDirectorySeederService`) y actualización del cupo diario mostrado a 400.
  - `appsettings.json`: Configuración por defecto de `DailyCatalogingLimit: 400`.
- **`Ludeka.UnitTests`:**
  - `NightlyCatalogingServiceTests.cs`: Nuevas pruebas para ingestión en lotes de 20 (BGG) y 10 (IA).
  - `BggExpansionFilterTests.cs`: 26 casos exhaustivos para filtrado léxico y de métricas comunitarias.
  - `BggRawSnapshotSyncServiceTests.cs`: Pruebas de integración del cribado de expansiones con umbrales y versiones en español.

---

## 3. Verificación Automatizada

- **Suite Unitaria:** 2.436 pruebas ejecutadas y superadas al 100% (0 fallos, 0 omitidos).
- **Compilación:** Limpia, 0 errores, warnings solo de analizadores preexistentes.
