# Tareas de Implementación: change-73-ingesta-enriquecimiento-catalogo

> **Incremento:** INC-73 (Ampliación Ingesta Masiva BGG >100 opiniones, Anti-Duplicados, Enriquecimiento Integral y Localización Territorial Multipaís de Editoriales y Títulos)  
> **Estado:** ✅ Completado y Verificado  
> **Metodología:** Implementación directa por capas sin ciclo estricto TDD, con verificación automatizada de la suite completa al 100% en verde  
> **Fecha:** 2026-09-27  
> **Resultado de Tests:** 1.966 pruebas unitarias superadas (0 errores, 0 omitidas)

---

## Tareas

- [x] **1. Dominio y Contratos**
  - [x] 1.1 Crear los value objects `RegionalPublisherEntry` y `LocalizedTitleEntry` en `Ludeka.Core.ValueObjects`.
  - [x] 1.2 Incorporar en `BggCatalogStagingItem.cs` los campos `MinPlayTimeMinutes`, `MaxPlayTimeMinutes`, `InferredFootprint`, `ScalabilityJson`, `SleevesJson`, `SpanishPublisher` y `RegionalPublishersJson`, con métodos auxiliares `GetScalability()`, `GetSleeves()`, `GetRegionalPublishers()`, `UpdateInferredFootprint()`, `UpdateSpanishPublisher()` y `UpdateRegionalPublishers()`.
  - [x] 1.3 Añadir en `Game.cs` las propiedades `SpanishPublisher`, `RegionalPublishers` y `LocalizedTitles`, junto con `GetPublisherForCountry()`, `GetTitleForCountry()` y los métodos aditivos de actualización (`UpdateScalability`, `UpdateDuration`, `UpdateFootprint`, `UpdateSleeves`, `UpdateRegionalPublishers`, `UpdateLocalizedTitles`).
  - [x] 1.4 Actualizar `BggMassIngestionOptions.cs` para fijar `MinUsersRated = 100` por defecto.
  - [x] 1.5 Actualizar `BggDumpParser.cs` con el valor por defecto `minUsersRated = 100`.
  - [x] 1.6 Ampliar `IBggMassIngestionService.cs` con los métodos de backfill retroactivo.
  - [x] 1.7 Ampliar `IGameRepository.cs` con `GetGamesPendingQualityBackfillAsync(int limit = 50, CancellationToken ct = default)`.

- [x] **2. Infraestructura, Padrón Multipaís y Parsers**
  - [x] 2.1 Ampliar `seed-directory.json` incorporando las editoriales líderes de México, Argentina, Chile, Colombia, Perú y Uruguay.
  - [x] 2.2 Crear `RegionalPublisherMatcher.cs` con el catálogo multipaís y lógica de normalización.
  - [x] 2.3 Configurar en `LudekaDbContext.cs` las propiedades de calidad, `RegionalPublishers` y `LocalizedTitles` mapeados como `.ToJson()` en `Game`, y columnas en `BggCatalogStaging`.
  - [x] 2.4 Incorporar la migración EF Core correspondiente y actualizar `LudekaDbContextModelSnapshot.cs`.
  - [x] 2.5 Refactorizar `BggXmlParser.cs`:
    - Extracción de duraciones reales `minplaytime`, `maxplaytime` y cálculo de `EstimatedPerPlayerMinutes`.
    - Fallback determinista de escalabilidad cuando la encuesta comunitaria no tiene votos.
    - Inferencia analítica de huella en mesa (`SmallTable`, `StandardTable`, `TableMonster`).
    - Detección de editoriales en múltiples países mediante `RegionalPublisherMatcher`, asociando `RegionalPublishers` y `SpanishPublisher`.
  - [x] 2.6 Implementar `GetGamesPendingQualityBackfillAsync` en `SqliteGameRepository.cs` y actualizar `SearchAsync` y `GetByPublisherAsync` para buscar también en `SpanishPublisher` y `RegionalPublishers`.

- [x] **3. Orquestador de Ingesta, Deduplicación y Backfill**
  - [x] 3.1 En `BggMassIngestionService.ProcessPendingDetailsBatchAsync`: capturar y persistir en staging escalabilidad, fundas, huella, tiempos, `SpanishPublisher` y `RegionalPublishersJson` de `fetchedGame`.
  - [x] 3.2 En `BggMassIngestionService.PromoteReadyToCatalogBatchAsync`:
    - Pasar metadatos completos al instanciar `new Game(...)`.
    - Detectar existencia por `BggId` y actualizar aditivamente escalabilidad, fundas, tiempos, huella y editoriales regionales en el registro existente, descartando duplicación y evitando excepciones de clave única.
  - [x] 3.3 Implementar `RunScheduledBackfillCatalogQualityBatchAsync` y `BackfillCatalogQualityBatchAsync` en `BggMassIngestionService.cs` con estrategia Staging-First.

- [x] **4. Interfaz de Usuario (Ficha, Tarjetas y Directorio)**
  - [x] 4.1 En `GameDetail.razor`: mostrar la editorial del país del usuario (`GetPublisherForCountry`) con enlace a su ficha en `/editoriales/{slug}`, y la editorial original si difiere. Desglose de otras ediciones de la comunidad hispanohablante. Mostrar subtítulo con título original si difiere de `SpanishTitle`.
  - [x] 4.2 En `GameCard.razor`: mostrar la editorial y título local relevante según contexto geográfico del usuario.
  - [x] 4.3 En `PublisherDetail.razor` y `PublisherService.cs`: verificar que los juegos asociados a una editorial de cualquier país soportado se listen en su página.

- [x] **5. Pruebas Automatizadas y Verificación Global**
  - [x] 5.1 Pruebas unitarias en `BggXmlParserQualityTests.cs` (tiempos reales, fallback de escalabilidad sin votos, inferencia de huella, detección de editoriales multipaís).
  - [x] 5.2 Pruebas en `RegionalPublisherMatcherTests.cs` (mapeo canónico en España, filiales en México, Chile, Colombia, Perú, Argentina, Uruguay).
  - [x] 5.3 Pruebas unitarias en `BggMassIngestionBackfillTests.cs`:
    - Promoción de juego ya existente con actualización aditiva anti-duplicados y blindaje de clave única.
    - Ejecución de ciclo de backfill retroactivo.
  - [x] 5.4 Actualización de contratos de esquema SQLite en `SqliteSchemaMigrator.cs` y pruebas de migraciones.
  - [x] 5.5 Ejecutar la suite completa y asegurar 100% de pruebas en verde (1.966 tests superados).
