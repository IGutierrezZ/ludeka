# Tareas de Implementación: change-73-ingesta-enriquecimiento-catalogo

> **Incremento:** INC-73 (Ampliación Ingesta Masiva BGG >100 opiniones, Anti-Duplicados, Enriquecimiento Integral y Localización Territorial Multipaís de Editoriales y Títulos)  
> **Estado:** Pendiente de aprobación  
> **Metodología:** Implementación directa por capas sin ciclo estricto TDD paso a paso, con verificación final de la suite completa al 100% en verde  
> **Fecha:** 2026-09-27  

---

## Tareas

- [ ] **1. Dominio y Contratos**
  - [ ] 1.1 Crear los value objects `RegionalPublisherEntry` y `LocalizedTitleEntry` en `Ludeka.Core.ValueObjects`.
  - [ ] 1.2 Incorporar en `BggCatalogStagingItem.cs` los campos `MinPlayTimeMinutes`, `MaxPlayTimeMinutes`, `InferredFootprint`, `ScalabilityJson`, `SleevesJson`, `SpanishPublisher` y `RegionalPublishersJson`, con métodos auxiliares `GetScalability()`, `GetSleeves()`, `GetRegionalPublishers()`, `UpdateInferredFootprint()`, `UpdateSpanishPublisher()` y `UpdateRegionalPublishers()`.
  - [ ] 1.3 Añadir en `Game.cs` las propiedades `SpanishPublisher`, `RegionalPublishers` y `LocalizedTitles`, junto con `GetPublisherForCountry()`, `GetTitleForCountry()` y los métodos aditivos de actualización (`UpdateScalability`, `UpdateDuration`, `UpdateFootprint`, `UpdateSleeves`, `UpdateRegionalPublishers`, `UpdateLocalizedTitles`).
  - [ ] 1.4 Actualizar `BggMassIngestionOptions.cs` para fijar `MinUsersRated = 100` por defecto.
  - [ ] 1.5 Actualizar `BggDumpParser.cs` con el valor por defecto `minUsersRated = 100`.
  - [ ] 1.6 Ampliar `IBggMassIngestionService.cs` con los métodos de backfill retroactivo.
  - [ ] 1.7 Ampliar `IGameRepository.cs` con `GetGamesPendingQualityBackfillAsync(int limit = 50, CancellationToken ct = default)`.

- [ ] **2. Infraestructura, Padrón Multipaís y Parsers**
  - [ ] 2.1 Ampliar `seed-directory.json` incorporando las editoriales líderes de México, Argentina, Chile, Colombia, Perú y Uruguay.
  - [ ] 2.2 Crear `RegionalPublisherMatcher.cs` con el catálogo multipaís y lógica de normalización.
  - [ ] 2.3 Configurar en `LudekaDbContext.cs` las propiedades de calidad, `RegionalPublishers` y `LocalizedTitles` mapeados como `.ToJson()` en `Game`, y columnas en `BggCatalogStaging`.
  - [ ] 2.4 Incorporar la migración EF Core correspondiente y actualizar `LudekaDbContextModelSnapshot.cs`.
  - [ ] 2.5 Refactorizar `BggXmlParser.cs`:
    - Extracción de duraciones reales `minplaytime`, `maxplaytime` y cálculo de `EstimatedPerPlayerMinutes`.
    - Fallback determinista de escalabilidad cuando la encuesta comunitaria no tiene votos.
    - Inferencia analítica de huella en mesa (`SmallTable`, `StandardTable`, `TableMonster`).
    - Detección de editoriales en múltiples países mediante `RegionalPublisherMatcher`, asociando `RegionalPublishers` y `SpanishPublisher`.
  - [ ] 2.6 Implementar `GetGamesPendingQualityBackfillAsync` en `SqliteGameRepository.cs` y actualizar `SearchAsync` y `GetByPublisherAsync` para buscar también en `SpanishPublisher` y `RegionalPublishers`.

- [ ] **3. Orquestador de Ingesta, Deduplicación y Backfill**
  - [ ] 3.1 En `BggMassIngestionService.ProcessPendingDetailsBatchAsync`: capturar y persistir en staging escalabilidad, fundas, huella, tiempos, `SpanishPublisher` y `RegionalPublishersJson` de `fetchedGame`.
  - [ ] 3.2 En `BggMassIngestionService.PromoteReadyToCatalogBatchAsync`:
    - Pasar metadatos completos al instanciar `new Game(...)`.
    - Detectar existencia por `BggId` y actualizar aditivamente escalabilidad, fundas, tiempos, huella y editoriales regionales en el registro existente, descartando duplicación y evitando excepciones de clave única.
  - [ ] 3.3 Implementar `RunScheduledBackfillCatalogQualityBatchAsync` y `BackfillCatalogQualityBatchAsync` en `BggMassIngestionService.cs` con estrategia Staging-First.

- [ ] **4. Interfaz de Usuario (Ficha, Tarjetas y Directorio)**
  - [ ] 4.1 En `GameDetail.razor`: mostrar la editorial del país del usuario (`GetPublisherForCountry`) con enlace a su ficha en `/directorios/editoriales/{slug}`, y la editorial original si difiere. Desglose de otras ediciones de la comunidad hispanohablante. Mostrar subtítulo con título original si difiere de `SpanishTitle`.
  - [ ] 4.2 En `GameCard.razor`: mostrar la editorial local relevante según contexto.
  - [ ] 4.3 En `PublisherDetail.razor` y `PublisherService.cs`: verificar que los juegos asociados a una editorial de cualquier país soportado se listen en su página.

- [ ] **5. Pruebas Automatizadas y Verificación Global**
  - [ ] 5.1 Añadir pruebas unitarias en `BggXmlParserTests.cs` (tiempos reales, fallback de escalabilidad sin votos, inferencia de huella, detección de editoriales multipaís).
  - [ ] 5.2 Añadir prueba de streaming en `BggDumpParserTests.cs` con umbral 100.
  - [ ] 5.3 Añadir pruebas unitarias en `BggMassIngestionServiceTests.cs`:
    - Promoción con persistencia de fundas, escalabilidad y editoriales regionales.
    - Promoción de juego ya existente con actualización aditiva anti-duplicados.
    - Ejecución de backfill local (staging) y remoto (BGG Thing).
  - [ ] 5.4 Añadir pruebas en `SqliteGameRepositoryTests.cs` para `GetGamesPendingQualityBackfillAsync` y búsqueda multipaís de editoriales.
  - [ ] 5.5 Ejecutar la suite completa y asegurar 100% de pruebas en verde (>1.950 tests).
