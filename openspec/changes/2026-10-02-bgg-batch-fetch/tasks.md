# Tareas de Implementación: Peticiones en Bloque Multi-ID a BGG y Ampliación de Timeout

> **ID:** INC-97  
> **Slug:** `bgg-batch-fetch`  

---

- [ ] **1. Contrato e Implementación del Cliente BGG Multi-ID**
  - [ ] 1.1 Añadir `FetchRawThingsXmlAsync(IEnumerable<int> bggIds, CancellationToken ct)` en `IBggClient.cs`.
  - [ ] 1.2 Implementar `FetchRawThingsXmlAsync` en `BggXmlApiClient.cs` con acotación a bloques de 20 IDs, normalización y reintentos.
  - [ ] 1.3 Implementar `FetchRawThingsXmlAsync` en `SimulatedBggClient.cs`.
  - [ ] 1.4 Pruebas unitarias para `FetchRawThingsXmlAsync` en `BggXmlApiClientTests` o tests dedicados.

- [ ] **2. Refactorización de `BggRawSnapshotSyncService` para Procesamiento por Bloques de 20**
  - [ ] 2.1 Refactorizar `SyncBatchCoreAsync` en `BggRawSnapshotSyncService.cs` para dividir `missingIds` en chunks de 20 e invocar `FetchRawThingsXmlAsync`.
  - [ ] 2.2 Parsear todos los `<item>` del XML devuelto, upsert de cada snapshot y auto-vinculación de expansiones.
  - [ ] 2.3 Pausa `delayMs` por cada bloque de 20 en vez de por juego individual.
  - [ ] 2.4 Actualizar / añadir pruebas unitarias en `BggRawSnapshotSyncServiceTests.cs`.

- [ ] **3. Ajuste de Timeout en Host de Jobs (`Ludeka.Jobs`)**
  - [ ] 3.1 Elevar `Workers:JobTimeoutMinutes` por defecto a 120 en `src/Ludeka.Jobs/Program.cs`.

- [ ] **4. Verificación Completa y Documentación**
  - [ ] 4.1 Ejecutar suite completa `dotnet test tests/Ludeka.UnitTests`.
  - [ ] 4.2 Documentar y archivar en `docs/increments/archive/inc-97-peticiones-en-bloque-bgg-batch-fetch.md` y actualizar especificación viva.
