# Tareas de Implementación: change-73-ingesta-enriquecimiento-catalogo

> **Incremento:** INC-73 (Ampliación Ingesta Masiva BGG >100 opiniones, Anti-Duplicados y Enriquecimiento Integral)  
> **Estado:** Pendiente de aprobación  
> **Fecha:** 2026-09-27  

---

## Tareas

- [ ] **1. Dominio y Contratos de Calidad**
  - [ ] 1.1 Incorporar en `BggCatalogStagingItem.cs` los campos `MinPlayTimeMinutes`, `MaxPlayTimeMinutes`, `InferredFootprint`, `ScalabilityJson` y `SleevesJson`, junto con métodos auxiliares `GetScalability()`, `GetSleeves()` y `UpdateInferredFootprint()`.
  - [ ] 1.2 Añadir en `Game.cs` los métodos aditivos `UpdateScalability(...)`, `UpdateDuration(...)`, `UpdateFootprint(...)` y `UpdateSleeves(...)`.
  - [ ] 1.3 Actualizar `BggMassIngestionOptions.cs` para fijar `MinUsersRated = 100` por defecto.
  - [ ] 1.4 Actualizar `BggDumpParser.cs` con el valor por defecto `minUsersRated = 100`.
  - [ ] 1.5 Ampliar `IBggMassIngestionService.cs` con los métodos de backfill retroactivo.
  - [ ] 1.6 Ampliar `IGameRepository.cs` con `GetGamesPendingQualityBackfillAsync(int limit = 50, CancellationToken ct = default)`.

- [ ] **2. Infraestructura, Migración EF Core y Parsers**
  - [ ] 2.1 Configurar en `LudekaDbContext.cs` las propiedades de calidad de `BggCatalogStagingItem` con valores por defecto.
  - [ ] 2.2 Incorporar la migración EF Core `AddStagingQualityFields` y actualizar `LudekaDbContextModelSnapshot.cs`.
  - [ ] 2.3 Refactorizar `BggXmlParser.cs`:
    - Extracción de duraciones reales `minplaytime`, `maxplaytime` y cálculo de `EstimatedPerPlayerMinutes`.
    - Fallback determinista de escalabilidad cuando la encuesta comunitaria no tiene votos.
    - Inferencia analítica de huella en mesa (`SmallTable`, `StandardTable`, `TableMonster`).
  - [ ] 2.4 Implementar `GetGamesPendingQualityBackfillAsync` en `SqliteGameRepository.cs`.

- [ ] **3. Orquestador de Ingesta, Deduplicación y Backfill**
  - [ ] 3.1 En `BggMassIngestionService.ProcessPendingDetailsBatchAsync`: capturar y persistir en staging escalabilidad, fundas, huella y tiempos de `fetchedGame`.
  - [ ] 3.2 En `BggMassIngestionService.PromoteReadyToCatalogBatchAsync`:
    - Pasar metadatos completos al instanciar `new Game(...)`.
    - Detectar existencia por `BggId` y actualizar aditivamente escalabilidad, fundas, tiempos y huella en el registro existente, descartando duplicación y evitando excepciones de clave única.
  - [ ] 3.3 Implementar `RunScheduledBackfillCatalogQualityBatchAsync` y `BackfillCatalogQualityBatchAsync` en `BggMassIngestionService.cs` con estrategia Staging-First.

- [ ] **4. Pruebas Automatizadas y Verificación**
  - [ ] 4.1 Añadir pruebas unitarias en `BggXmlParserTests.cs` (tiempos reales, fallback de escalabilidad sin votos, inferencia de huella).
  - [ ] 4.2 Añadir prueba de streaming en `BggDumpParserTests.cs` con umbral 100.
  - [ ] 4.3 Añadir pruebas unitarias en `BggMassIngestionServiceTests.cs`:
    - Promoción con persistencia de fundas y escalabilidad.
    - Promoción de juego ya existente con actualización aditiva anti-duplicados.
    - Ejecución de backfill local (staging) y remoto (BGG Thing).
  - [ ] 4.4 Añadir prueba en `SqliteGameRepositoryTests.cs` para `GetGamesPendingQualityBackfillAsync`.
  - [ ] 4.5 Ejecutar la suite completa y asegurar 100% de pruebas en verde (>1.950 tests).
