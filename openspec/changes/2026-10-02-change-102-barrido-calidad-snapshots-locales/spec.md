# Especificación: INC-102 — Barrido y Auditoría Integral de Calidad de Catálogo desde Snapshots Locales de BGG

## 1. Requerimientos Funcionales

### R1: Reconstitución Determinista de Snapshots a Metadatos de Calidad
- **R1.1:** El sistema DEBE proveer un método determinista `BggJsonToXmlConverter.ConvertToXml(string rawJson)` o equivalente en `BggRawSnapshotParser` que transforme el `RawJson` almacenado en `BggRawSnapshot` a un `XElement` equivalente al elemento `<item>` original de BGG XMLAPI2.
- **R1.2:** Si `rawJson` es nulo, vacío o contiene `"notFound":true`, el parser DEBE devolver `null` sin lanzar excepciones no controladas.
- **R1.3:** Los atributos JSON (con prefijo `@`) DEBEN convertirse a atributos XML (`XAttribute`), el texto con clave `#text` DEBE asignarse al valor textual del elemento, y las claves hijas (objetos o arrays) DEBEN generarse como elementos hijos recursivos.

### R2: Estrategia de Enriquecimiento Snapshot-First en `BggMassIngestionService`
- **R2.1:** Al enriquecer una entidad `Game` en `EnrichSingleGameQualityAsync`, el servicio DEBE evaluar las fuentes en el siguiente orden de precedencia:
  1. **Staging:** Si existe un `BggCatalogStagingItem` con `FetchStatus == StagingFetchStatus.Fetched` y `RawThingXml` contiene `<dna `, extraer metadatos de staging en memoria.
  2. **Snapshot Satélite Local:** Si no se satisfizo (1), consultar `_snapshotRepo.GetByBggIdAsync(game.BggId, ct)`. Si existe un snapshot válido, parsear sus metadatos con `BggXmlParser.ParseQualityMetadata` e `InferGameDna` sin realizar peticiones de red ni pausas de retardo.
  3. **Fallback BGG XMLAPI2:** Si no existe ni en staging ni en snapshots satélite, consultar `_bggClient.FetchGameByBggIdAsync(game.BggId, ct)` aplicando el retardo configurado de cortesía.
- **R2.2:** Al detectar cambios respecto a la entidad `Game`, el servicio DEBE actualizar exclusivamente los campos modificados:
  - `Style`, `Confrontation` e `IsOfficialSolo` (mediante `game.UpdateDna`).
  - Duraciones `MinMinutes`, `MaxMinutes` y `EstimatedPerPlayerMinutes` (mediante `game.UpdateDuration`).
  - `Scalability` (mediante `game.UpdateScalability`).
  - `Sleeves` (mediante `game.UpdateSleeves`).
  - `Footprint` (mediante `game.UpdateFootprint`).
  - Editoriales territoriales (`SpanishPublisher` y `RegionalPublishers`).
- **R2.3:** Idempotencia estricta: Si los valores recalculados son semánticamente idénticos a los ya presentes en `Game`, el método `EnrichSingleGameQualityAsync` DEBE devolver `false` y omitir la llamada de actualización en base de datos (`_gameRepo.UpdateAsync`). En particular, si tanto `game.Scalability` como la escalabilidad entrante carecen de votos comunitarios (`BestVotes == 0 && RecommendedVotes == 0`) y tienen el mismo número de comensales, NO se considerará enriquecido.

### R3: Saneamiento Anti-Bucle en Consulta de Pendientes
- **R3.1:** `IGameRepository.GetGamesPendingQualityBackfillAsync` DEBE soportar paginación por cursor `afterBggId` (por defecto 0) para asegurar avance monotónico sin re-evaluar los mismos registros indefinidamente.
- **R3.2:** Alternativamente o de forma complementaria, el criterio de pendientes de enriquecimiento en `GetGamesPendingQualityBackfillCountAsync` y `GetGamesPendingQualityBackfillAsync` DEBE priorizar estrictamente títulos con `Scalability.Count == 0`, o títulos no evaluados previamente.
- **R3.3:** El bucle continuo `StartContinuousBackfill` en `CatalogQueueAdmin.razor` DEBE pasar el cursor `afterBggId` al servicio y detenerse con éxito cuando `EvaluatedCount == 0` o `HasMore == false`.

### R4: Operabilidad y Telemetría en Barrido Total
- **R4.1:** El botón «Barrido Total Catálogo» en `/admin/cola-catalogacion` DEBE procesar los títulos a velocidad de memoria local (sin retardos HTTP de red cuando los snapshots existen).
- **R4.2:** El runner `BackfillQualityJobRunner` en `Ludeka.Jobs` DEBE reflejar en logs la velocidad de procesamiento (ej. títulos evaluados/segundo) y el desglose de títulos actualizados frente a omitidos.
