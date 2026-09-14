# 27. Ingesta Masiva de Catálogo BGG (~8.000 títulos), Fotos GeekDo y Síntesis IA en Lotes

> **Incremento Asociado:** INC-41 (`change-41-ingesta-bgg-catalogo`)  
> **Estado:** Implementado, Verificado y Documentado  
> **Módulo:** Catálogo, Ingesta BGG, Medios Comunitarios y Síntesis IA  

---

## 1. Visión General y Propósito

El módulo de **Ingesta Masiva de Catálogo BGG, Galería GeekDo y Síntesis IA en Lotes** transforma radicalmente el aprovisionamiento de catálogo de Ludeka. En lugar de depender exclusivamente de solicitudes reactivas de usuarios o de un proceso nocturno limitado a 20 juegos arbitrarios, este módulo permite:

1. **Ingesta Masiva Selectiva:** Cargar el volcado completo de rankings de BoardGameGeek (`bg_ranks.csv`) filtrando únicamente los títulos con comunidad y relevancia contrastada (`usersrated >= 30`), lo que incorpora aproximadamente **8.000 juegos de mesa** en una tabla intermedia aislada de staging.
2. **Tabla Intermedia de Staging (`BggCatalogStaging`):** Desacopla la ingesta masiva de la tabla definitiva `Games`, permitiendo procesar el pipeline en etapas independientes con seguimiento de estado (`FetchStatus`, `ImagesStatus`, `AiStatus`, `PromotionStatus`).
3. **Galería Visual GeekDo y Cloudflare R2:** Extrae las 3 fotografías más votadas por la comunidad en GeekDo (carátula frontal, contraportada/trasera y foto de componentes/despliegue en mesa) y las convierte a formato WebP optimizado en memoria (`SkiaSharp`) antes de subirlas a Cloudflare R2 con nomenclatura canónica (`games/{bggId}/cover.webp`, `back.webp`, `table.webp`).
4. **Síntesis Editorial IA en Lotes:** Optimiza el consumo de la cuota diaria gratuita de Google Gemini Flash (1.500 llamadas/día) agrupando de 5 a 10 juegos por llamada estructurada JSON, multiplicando por 5x a 10x la capacidad de enriquecimiento. Detecta de forma nativa la saturación de cuota (`RESOURCE_EXHAUSTED` / HTTP 429) para pausar elegantemente sin errores.
5. **Orquestador Nocturno Híbrido:** Reestructura el proceso en segundo plano para que priorice primero las peticiones manuales de usuarios en cola, continúe con el drenaje de juegos listos de staging hacia el catálogo y rellene el cupo con novedades de BGG.
6. **Panel de Monitorización y Galería Visual:** Ofrece un cuadro de mandos con métricas en `/admin/cola-catalogacion` y una sección visual de componentes en `GameDetail.razor`.

---

## 2. Modelo de Dominio (`Ludeka.Core`)

### 2.1. Estados de Staging (`StagingStatuses.cs`)
Ubicación: `Ludeka.Core.Enums`

- **`StagingFetchStatus`**: `Pending`, `InProgress`, `Fetched`, `Failed`.
- **`StagingImagesStatus`**: `Pending`, `InProgress`, `Completed`, `Skipped`, `Failed`.
- **`StagingAiStatus`**: `Pending`, `InProgress`, `Completed`, `QuotaExceeded`, `Skipped`, `Failed`.
- **`StagingPromotionStatus`**: `Pending`, `InProgress`, `Promoted`, `Failed`.

### 2.2. Entidad de Staging (`BggCatalogStagingItem.cs`)
Ubicación: `Ludeka.Core.Entities.BggCatalogStagingItem`
Almacena metadatos transitorios, datos en crudo (`RawBggXml`), URLs de variantes visuales optimizadas, JSON de síntesis IA y marcas de tiempo de procesamiento:

```csharp
public class BggCatalogStagingItem
{
    public Guid Id { get; private set; }
    public int BggId { get; private set; }
    public string Title { get; private set; }
    public int? YearPublished { get; private set; }
    public int? BggRank { get; private set; }
    public int UsersRated { get; private set; }
    public double BayesAverage { get; private set; }
    public double AverageRating { get; private set; }
    // Metadatos BGG Thing
    public string? SpanishTitle { get; private set; }
    public string? Designer { get; private set; }
    public string? Publisher { get; private set; }
    public string? Description { get; private set; }
    public int MinPlayers { get; private set; }
    public int MaxPlayers { get; private set; }
    public int PlayingTimeMinutes { get; private set; }
    public int MinAge { get; private set; }
    public double Weight { get; private set; }
    // Estados y auditoría
    public StagingFetchStatus FetchStatus { get; private set; }
    public StagingImagesStatus ImagesStatus { get; private set; }
    public StagingAiStatus AiStatus { get; private set; }
    public StagingPromotionStatus PromotionStatus { get; private set; }
    // Imágenes R2
    public string? CoverImageUrl { get; private set; }
    public string? ThumbnailUrl { get; private set; }
    public string? BackCoverImageUrl { get; private set; }
    public string? TableImageUrl { get; private set; }
    // Síntesis IA
    public string? AiSummaryJson { get; private set; }
    // Métodos de transición: MarkFetched, MarkImagesCompleted, MarkAiCompleted, MarkPromoted, etc.
}
```

### 2.3. Ampliación de la Entidad `Game`
Ubicación: `Ludeka.Core.Entities.Game`
Se agregaron propiedades para la galería comunitaria y soporte de actualización de medios:
- `string? BackCoverImageUrl { get; private set; }`
- `string? TableImageUrl { get; private set; }`
- `void UpdateMediaUrls(string? coverImageUrl, string? thumbnailUrl, string? backCoverImageUrl, string? tableImageUrl)`

---

## 3. Casos de Uso y Contratos (`Ludeka.Application`)

### 3.1. DTOs de Ingesta Masiva (`BggMassIngestionDtos.cs`)
- `BggRanksDumpRowDto`: Fila extraída del volcado BGG.
- `BggStagingMetricsDto`: Contadores agregados de la tabla staging.
- `GeekDoGalleryImagesDto`: URLs de fotos más votadas (`FrontCoverUrl`, `BackCoverUrl`, `TableOrGameplayUrl`).
- `AiGameBatchInputDto`: Datos esenciales de juego para síntesis en lote.
- `AiBatchResultDto`: Resultado de la generación en lote (`Success`, `QuotaExhausted`, `Summaries`, `ErrorMessage`).
- `BggMassIngestionOptions`: Configuración de límites y tamaños de lotes.

### 3.2. Contratos de Persistencia y Clientes
- **`IBggCatalogStagingRepository`**: Operaciones masivas `UpsertBatchAsync`, consultas paginadas de ítems pendientes por estado, actualización por lotes, métricas y reseteo de estados de fallo o cuota.
- **`IGeekDoImagesClient`**: Extracción de las 3 imágenes más votadas desde GeekDo.
- **`IAiGameSummaryService.GenerateBatchSummariesAsync`**: Sobrecarga para procesar listas de `AiGameBatchInputDto`.
- **`IBggMassIngestionService`**: Orquestador integral con métodos:
  - `IngestRanksDumpAsync(Stream csvStream, int minUsersRated, ...)`
  - `ProcessPendingDetailsBatchAsync(int batchSize, ...)`
  - `ProcessPendingImagesBatchAsync(int batchSize, ...)`
  - `ProcessPendingAiBatchAsync(int gamesPerBatch, int maxBatches, ...)`
  - `PromoteReadyToCatalogBatchAsync(int batchSize, ...)`
  - `DrainStagingPipelineAsync(CancellationToken ct)`

### 3.3. Streaming CSV Parser (`BggDumpParser.cs`)
Parser de alto rendimiento en streaming con `StreamReader` y máquina de estados para CSV que filtra en un único pase descartando juegos con menos de 30 valoraciones, evitando cargar ficheros enteros de ~150 MB en memoria RAM.

---

## 4. Infraestructura y Persistencia (`Ludeka.Infrastructure`)

### 4.1. Repositorio EF Core de Staging (`SqliteBggCatalogStagingRepository.cs`)
Implementación sobre `LudekaDbContext` con índices optimizados en `BggId`, `UsersRated` y estados combinados para consultas de lote ordenadas por relevancia.

### 4.2. Cliente de Galería GeekDo (`GeekDoImagesClient.cs`)
Consulta a la API interna de imágenes comunitarias de BGG con selección determinista por tipo de imagen (`boxartfront`, `boxartback`, `gameplay` / `table`) y modo simulación para pruebas y entornos locales.

### 4.3. Síntesis Gemini en Lotes (`GeminiGameSummaryService.cs`)
Construcción de prompt JSON estructurado con array de juegos. Mapea la respuesta a cada `bggId`. Intercepta respuestas HTTP 429 (`RESOURCE_EXHAUSTED`) retornando `QuotaExhausted: true` para frenar el pipeline sin provocar excepciones no controladas. Modo simulación heurístico en `HeuristicGameSummaryGenerator`.

### 4.4. Migración Defensiva de Esquema (`SqliteSchemaMigrator.cs`)
Incorporación de `BackCoverImageUrl`, `TableImageUrl` en la tabla `Games` y creación automática con índices de la tabla `BggCatalogStaging`.

---

## 5. Capa Web y Presentación (`Ludeka.Web`)

### 5.1. Cuadro de Mandos en Administración (`CatalogQueueAdmin.razor`)
Sección dedicada a "Ingesta Masiva de Catálogo (Staging)" con:
- Métricas en tiempo real: Total en staging, pendientes de detalle, imágenes, IA, listos para promoción y promovidos.
- Alerta visual amarilla si la cuota de IA fue alcanzada (`QuotaExceeded`).
- Botón de acción rápida: *«Drenar Lote de Staging (10 títulos)»* con feedback interactivo.

### 5.2. Galería Visual en Detalle de Juego (`GameDetail.razor`)
Bloque *"Galería Visual y Componentes"* que renderiza la contraportada (`BackCoverImageUrl`) y la fotografía de despliegue en mesa (`TableImageUrl`) en diseño de tarjeta editorial con fallback seguro.

---

## 6. Verificación y Cobertura de Pruebas

Se añadieron 24 pruebas automáticas sin librerías externas de mocking, utilizando Fakes puros acordes a Clean Architecture:
- `BggCatalogStagingItemTests`: 12 pruebas de transiciones de dominio y validaciones.
- `BggDumpParserTests`: 3 pruebas de streaming CSV, filtro por umbral de votos y caracteres escapados.
- `GeekDoImagesClientTests`: 3 pruebas del cliente de galería comunitaria y modo simulación.
- `GeminiBatchSummaryTests`: 2 pruebas de síntesis en lotes y manejo de casos vacíos.
- `BggMassIngestionServiceTests`: 4 pruebas completas del ciclo de ingesta, detalles BGG, control de cuota 429 y promoción atómica a catálogo.
- `SqliteSchemaMigratorTests`: Validación de migración defensiva de esquema SQLite.

**Total verificado en la solución:** **930 pruebas automáticas en verde al 100%**.
