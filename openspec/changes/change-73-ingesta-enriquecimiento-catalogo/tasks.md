# Tareas de Implementación: change-73-ingesta-enriquecimiento-catalogo

> **Incremento:** INC-73 (Ampliación Ingesta Masiva BGG >100 opiniones, Anti-Duplicados, Enriquecimiento Integral y Localización Territorial de Editoriales y Títulos)  
> **Estado:** Pendiente de aprobación  
> **Metodología:** Implementación directa por capas sin ciclo estricto TDD paso a paso, con verificación final de la suite completa al 100% en verde  
> **Fecha:** 2026-09-27  

---

## Tareas

- [ ] **1. Dominio y Contratos**
  - [ ] 1.1 Incorporar en `BggCatalogStagingItem.cs` los campos `MinPlayTimeMinutes`, `MaxPlayTimeMinutes`, `InferredFootprint`, `ScalabilityJson`, `SleevesJson` y `SpanishPublisher`, con métodos auxiliares `GetScalability()`, `GetSleeves()`, `UpdateInferredFootprint()` y `UpdateSpanishPublisher()`.
  - [ ] 1.2 Añadir en `Game.cs` la propiedad `SpanishPublisher` y los métodos aditivos `UpdateScalability(...)`, `UpdateDuration(...)`, `UpdateFootprint(...)`, `UpdateSleeves(...)` y `UpdateSpanishPublisher(...)`.
  - [ ] 1.3 Actualizar `BggMassIngestionOptions.cs` para fijar `MinUsersRated = 100` por defecto.
  - [ ] 1.4 Actualizar `BggDumpParser.cs` con el valor por defecto `minUsersRated = 100`.
  - [ ] 1.5 Ampliar `IBggMassIngestionService.cs` con los métodos de backfill retroactivo.
  - [ ] 1.6 Ampliar `IGameRepository.cs` con `GetGamesPendingQualityBackfillAsync(int limit = 50, CancellationToken ct = default)`.

- [ ] **2. Infraestructura, Migración EF Core y Parsers**
  - [ ] 2.1 Crear `SpanishPublisherMatcher.cs` con el catálogo canónico de editoriales españolas de `seed-directory.json` y lógica de normalización.
  - [ ] 2.2 Configurar en `LudekaDbContext.cs` las propiedades de calidad y `SpanishPublisher` en `BggCatalogStagingItem` y `Game`.
  - [ ] 2.3 Incorporar la migración EF Core correspondiente y actualizar `LudekaDbContextModelSnapshot.cs`.
  - [ ] 2.4 Refactorizar `BggXmlParser.cs`:
    - Extracción de duraciones reales `minplaytime`, `maxplaytime` y cálculo de `EstimatedPerPlayerMinutes`.
    - Fallback determinista de escalabilidad cuando la encuesta comunitaria no tiene votos.
    - Inferencia analítica de huella en mesa (`SmallTable`, `StandardTable`, `TableMonster`).
    - Detección de `SpanishPublisher` evaluando todos los enlaces `boardgamepublisher` contra `SpanishPublisherMatcher`.
  - [ ] 2.5 Implementar `GetGamesPendingQualityBackfillAsync` en `SqliteGameRepository.cs` y actualizar `SearchAsync` y `GetByPublisherAsync` para buscar también en `SpanishPublisher`.

- [ ] **3. Orquestador de Ingesta, Deduplicación y Backfill**
  - [ ] 3.1 En `BggMassIngestionService.ProcessPendingDetailsBatchAsync`: capturar y persistir en staging escalabilidad, fundas, huella, tiempos y `SpanishPublisher` de `fetchedGame`.
  - [ ] 3.2 En `BggMassIngestionService.PromoteReadyToCatalogBatchAsync`:
    - Pasar metadatos completos al instanciar `new Game(...)`.
    - Detectar existencia por `BggId` y actualizar aditivamente escalabilidad, fundas, tiempos, huella y `SpanishPublisher` en el registro existente, descartando duplicación y evitando excepciones de clave única.
  - [ ] 3.3 Implementar `RunScheduledBackfillCatalogQualityBatchAsync` y `BackfillCatalogQualityBatchAsync` en `BggMassIngestionService.cs` con estrategia Staging-First.

- [ ] **4. Interfaz de Usuario (Ficha y Directorio)**
  - [ ] 4.1 En `GameDetail.razor`: mostrar `SpanishPublisher` con enlace a su ficha en `/directorios/editoriales/{slug}`, y la editorial original si difiere. Mostrar subtítulo con título original si difiere de `SpanishTitle`.
  - [ ] 4.2 En `GameCard.razor`: mostrar la editorial española relevante (`SpanishPublisher ?? Publisher`).
  - [ ] 4.3 En `PublisherDetail.razor` y `PublisherService.cs`: verificar que los juegos asociados a una editorial española se listen en su página.

- [ ] **5. Pruebas Automatizadas y Verificación Global**
  - [ ] 5.1 Añadir pruebas unitarias en `BggXmlParserTests.cs` (tiempos reales, fallback de escalabilidad sin votos, inferencia de huella, detección de editorial española).
  - [ ] 5.2 Añadir prueba de streaming en `BggDumpParserTests.cs` con umbral 100.
  - [ ] 5.3 Añadir pruebas unitarias en `BggMassIngestionServiceTests.cs`:
    - Promoción con persistencia de fundas, escalabilidad y editorial española.
    - Promoción de juego ya existente con actualización aditiva anti-duplicados.
    - Ejecución de backfill local (staging) y remoto (BGG Thing).
  - [ ] 5.4 Añadir pruebas en `SqliteGameRepositoryTests.cs` para `GetGamesPendingQualityBackfillAsync` y búsqueda por `SpanishPublisher`.
  - [ ] 5.5 Ejecutar la suite completa y asegurar 100% de pruebas en verde (>1.950 tests).
