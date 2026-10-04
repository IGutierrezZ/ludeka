# Documento Vivo ODD — INC-107: Ingesta Masiva en Lotes BGG (x20) e IA (x10), Filtrado Inteligente de Expansiones y Saneamiento de Cola Administrativa

> **Feature:** `ingesta-expansiones-lotes-ia`  
> **Fichero:** `odd/tasks/inc-107-ingesta-expansiones-lotes-ia.md` (fuente de verdad operativa)  
> **Incremento:** INC-107  
> **Rama:** `inc/ingesta-expansiones-lotes-ia`  
> **Worktree:** `F:\repos\ludeka-wt\ingesta-expansiones-lotes-ia`  
> **Creado:** 2026-10-05 · **Ruta:** rama `inc/ingesta-expansiones-lotes-ia` → PR a `main`  
> **TDD Mode:** Strict TDD (RED ➔ GREEN ➔ REFACTOR)  
> **Línea Base:** 2.408 pruebas unitarias en verde (0 fallos)  

---

## 1. Objetivo

Optimizar el rendimiento y la calidad de la ingesta de juegos y expansiones en Ludeka:
1. **Drenaje Masivo en Lotes BGG (x20) e IA (x10):** Refactorizar `NightlyCatalogingService` para procesar la cola pendiente en bloques de 20 IDs mediante `IBggClient.FetchRawThingsXmlAsync` con `includeVersions: true` y sintetizar resúmenes editoriales de IA en bloques de 10 juegos mediante `IAiGameSummaryService.GenerateBatchSummariesAsync`. Elevar el cupo diario de catalogación (`DailyCatalogingLimit`) a 400 juegos.
2. **Cribado Inteligente de Expansiones (Anti-Promos):** Implementar en el descubrimiento de expansiones un filtrado en dos niveles: (a) pre-filtro léxico sobre enlaces de snapshots locales para descartar promos y accesorios (`Promo`, `Pack`, `Bonus`, `Upgrade`, etc.), y (b) enriquecimiento por lotes consultando BGG con `stats=1` y versiones para admitir únicamente expansiones con tracción comunitaria real (`usersrated >= 30` o `owned >= 100`) o con edición comercial registrada en español.
3. **Saneamiento Editorial de `/admin/cola-catalogacion`:** Purgar de la interfaz web los botones transitorios u obsoletos (sembrado de creadores ya automatizado, lote rápido manual de snapshots redundante, reconciliación manual puntual de INC-100), consolidando la pantalla en las acciones vivas de mantenimiento y ciclo diario.
4. **Verificación y Contratos:** Garantizar cobertura unitaria de los nuevos flujos por lotes y filtros, manteniendo el 100% de la suite en verde.

---

## 2. Diagnóstico Técnico

1. **Cuello de botella I/O en la cola nocturna:**  
   `NightlyCatalogingService` procesa `PendingBggImports` de forma secuencial juego a juego con una pausa de 2,5 s entre llamadas. Para 200 o 400 juegos se requerían cientos de peticiones individuales y llamadas aisladas a Gemini. Con `FetchRawThingsXmlAsync` (20 juegos/petición) y `GenerateBatchSummariesAsync` (10 juegos/petición), 400 juegos se completan con solo 20 llamadas a BGG y 40 llamadas a Gemini.
2. **Contaminación de expansiones con promos y mini-accesorios:**  
   `SnapshotSyncService.DiscoverAndEnqueueMissingExpansionsAsync` encolaba cualquier enlace `boardgameexpansion` sin distinguir expansiones completas en caja de cartas promocionales de convenciones con escaso interés público.
3. **Sobrecarga de opciones en la consola de administración:**  
   La vista `CatalogQueueAdmin.razor` acumulaba botones de parche creados durante incrementos puntuales (INC-54, INC-90, INC-100) que generan confusión operativa.

---

## 3. Alcance

### Dentro de Alcance:
- **ODD-1 — Motor de Ingesta Nocturna en Lotes BGG (x20) e IA (x10)**
  - Procesamiento agrupado en chunks de 20 IDs con `FetchRawThingsXmlAsync(chunk, includeVersions: true, ct)`.
  - Generación de resúmenes de IA agrupados en lotes de 10 juegos con `GenerateBatchSummariesAsync`.
  - Persistencia y actualización atómica del catálogo y estados de cola.
  - Ajuste de `DailyCatalogingLimit` a 400 en `NightlyCatalogingOptions` y configuración.
- **ODD-2 — Filtro Inteligente Anti-Promos para Expansiones**
  - Heurística léxica en títulos de enlaces salientes de snapshots.
  - Validación de métricas BGG (`usersrated`, `owned`, ediciones comerciales con EAN o editorial).
  - Encolado priorizado de expansiones reales en `PendingBggImports`.
- **ODD-3 — Saneamiento y Reorganización de `CatalogQueueAdmin.razor`**
  - Retirada de botones obsoletos / de un solo uso.
  - Presentación clara de métricas, acciones diarias y herramientas de mantenimiento.
- **ODD-4 — Pruebas Unitarias, Suite en Verde y Sincronización Documental**
  - Nuevas pruebas unitarias para el procesamiento por lotes de catalogación y filtros de expansiones.
  - 100% de la suite en verde.
  - Actualización de `ROADMAP.md` y especificaciones vivas.

---

## 4. Checklist de Tareas

- [ ] **ODD-1 — Motor de Ingesta Nocturna en Lotes BGG (x20) e IA (x10)**
  - [ ] 1.1 Tests unitarios para procesamiento en lote en `NightlyCatalogingServiceTests.cs`.
  - [ ] 1.2 Refactorizar `NightlyCatalogingService.cs` para consumir `pendingList` en bloques de 20 con `_bggClient.FetchRawThingsXmlAsync` y sintetizar con `_aiSummaryService.GenerateBatchSummariesAsync` en lotes de 10.
  - [ ] 1.3 Elevar valor por defecto de `DailyCatalogingLimit` a 400 en `NightlyCatalogingDtos.cs` y `appsettings.json`.
- [ ] **ODD-2 — Filtro Inteligente Anti-Promos para Expansiones**
  - [ ] 2.1 Tests unitarios para el filtrado léxico y de tracción de expansiones en `BggRawSnapshotSyncServiceTests.cs`.
  - [ ] 2.2 Implementar método de detección de promos/accesorios léxico y comprobación de umbral (`usersrated >= 30` o `owned >= 100` o versiones comerciales).
  - [ ] 2.3 Conectar el cribado en `BggRawSnapshotSyncService.DiscoverAndEnqueueMissingExpansionsCoreAsync`.
- [ ] **ODD-3 — Saneamiento de `CatalogQueueAdmin.razor`**
  - [ ] 3.1 Purgar botones redundantes u obsoletos (sembrado de creadores, lote rápido manual, reconciliación INC-100 amortizada).
  - [ ] 3.2 Actualizar textos, métricas y controles en la interfaz.
- [ ] **ODD-4 — Verificación Integral y Cierre**
  - [ ] 4.1 Ejecutar suite completa de tests unitarios y verificar cero regresiones.
  - [ ] 4.2 Actualizar `docs/increments/ROADMAP.md` y `docs/increments/inc-107-ingesta-expansiones-lotes-ia.md`.
  - [ ] 4.3 Actualizar especificaciones en `docs/specs/sistema/`.
