1: # 27. Ingesta Masiva de Catálogo BGG (~8.000 títulos), Fotos GeekDo y Síntesis IA en Lotes
2: 
3: > **Incrementos Asociados:** INC-41 (`change-41-ingesta-bgg-catalogo`), INC-53 (`2026-09-21-change-53-ingesta-masiva-autonoma-bgg`) e INC-73 (`change-73-ingesta-enriquecimiento-catalogo`)  
4: > **Estado:** Implementado, Verificado y Documentado  
5: > **Módulo:** Catálogo, Ingesta BGG, Medios Comunitarios, Síntesis IA, Calidad de Mesa y Localización Territorial  
6: 
7: ---
8: 
9: ## 1. Visión General y Propósito
10: 
11: El módulo de **Ingesta Masiva de Catálogo BGG, Galería GeekDo y Síntesis IA en Lotes** transforma radicalmente el aprovisionamiento de catálogo de Ludeka. En lugar de depender exclusivamente de solicitudes reactivas de usuarios o de un proceso nocturno limitado a 20 juegos arbitrarios, este módulo permite:
12: 
13: 1. **Ingesta Masiva Selectiva y Umbral Ampliado (INC-73):** Cargar el volcado completo de rankings de BoardGameGeek (`bg_ranks.csv`) filtrando únicamente los títulos con comunidad y tracción comunitaria contrastada (`usersrated >= 100`, umbral ampliado en INC-73 desde los 1.000 previos), lo que incorpora decenas de miles de juegos de mesa en la tabla intermedia de staging.
14: 2. **Tabla Intermedia de Staging (`BggCatalogStaging`):** Desacopla la ingesta masiva de la tabla definitiva `Games`, permitiendo procesar el pipeline en etapas independientes con seguimiento de estado (`FetchStatus`, `ImagesStatus`, `AiStatus`, `PromotionStatus`).
15: 3. **Galería Visual GeekDo y Cloudflare R2:** Extrae las 3 fotografías más votadas por la comunidad en GeekDo (carátula frontal, contraportada/trasera y foto de componentes/despliegue en mesa) y las convierte a formato WebP optimizado en memoria (`SkiaSharp`) antes de subirlas a Cloudflare R2 con nomenclatura canónica (`games/{bggId}/cover.webp`, `back.webp`, `table.webp`).
16: 4. **Síntesis Editorial IA en Lotes:** Optimiza el consumo de la cuota diaria gratuita de Google Gemini Flash (1.500 llamadas/día) agrupando de 5 a 10 juegos por llamada estructurada JSON, multiplicando por 5x a 10x la capacidad de enriquecimiento. Detecta de forma nativa la saturación de cuota (`RESOURCE_EXHAUSTED` / HTTP 429) para pausar elegantemente sin errores.
17: 5. **Orquestador Nocturno Híbrido:** Reestructura el proceso en segundo plano para que priorice primero las peticiones manuales de usuarios en cola, continúe con el drenaje de juegos listos de staging hacia el catálogo y rellene el cupo con novedades de BGG.
18: 6. **Panel de Monitorización y Galería Visual:** Ofrece un cuadro de mandos con métricas en `/admin/cola-catalogacion` y una sección visual de componentes en `GameDetail.razor`.
19: 7. **Descarga y Auto-Siembra 100% Autónoma (INC-53):** Descarga remota desatendida mediante streaming HTTP directo (`ResponseHeadersRead`) en memoria acotada (< 30 MB) desde el mirror público diario en GitHub Raw / Fastly CDN (`{yyyy-MM-dd}.csv`), con retroceso resiliente de fechas de hasta 5 días ante posibles desfases en la publicación de BGG, botón interactivo con permisos en el panel administrativo, subcomando `seed-staging` en Cloud Run Jobs e integración como auto-siembra inicial condicional en el ciclo nocturno si staging se encuentra vacío.
20: 8. **Blindaje Anti-Duplicados y Promoción Aditiva (INC-73):** Al promover desde staging a `Games`, el orquestador comprueba la existencia previa por `BggId`. Si el juego ya existe, descarta la inserción para prevenir colisiones de clave única y actualiza aditivamente los campos enriquecidos (escalabilidad, fundas, huella, duraciones reales y editoriales territoriales).
21: 9. **Enriquecimiento Integral de Mesa y Localización Multipaís (INC-73):** Extracción de encuestas de jugadores con fallback determinista, captura de fundas de cartas (`Sleeves`), inferencia de huella en mesa (`TableFootprint`), duraciones reales y estimadas por comensal, mapeo multipaís de editoriales (`RegionalPublishers` y `SpanishPublisher`) con `RegionalPublisherMatcher` y proceso por lotes de enriquecimiento retroactivo (`BackfillCatalogQualityBatchAsync`).

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
- `BggMassIngestionOptions`: Configuración de límites y tamaños de lotes. En INC-53 incorpora `RanksDumpUrlPattern` (plantilla URL hacia el mirror público diario de GitHub Raw / Fastly CDN) y `MaxFallbackDays` (5 días de retroceso resiliente ante posibles 404).

### 3.2. Contratos de Persistencia y Clientes
- **`IBggCatalogStagingRepository`**: Operaciones masivas `UpsertBatchAsync`, consultas paginadas de ítems pendientes por estado, actualización por lotes, métricas y reseteo de estados de fallo o cuota.
- **`IGeekDoImagesClient`**: Extracción de las 3 imágenes más votadas desde GeekDo.
- **`IAiGameSummaryService.GenerateBatchSummariesAsync`**: Sobrecarga para procesar listas de `AiGameBatchInputDto`.
99: - **`IBggMassIngestionService`**: Orquestador integral con métodos:
100:   - `DownloadAndIngestLatestRanksAsync(int? minUsersRated, ...)` (interactivo con guarda de permisos `CanEditGames`, INC-53).
101:   - `RunScheduledDownloadAndIngestLatestRanksAsync(int? minUsersRated, ...)` (ruta de sistema desatendida para Cloud Run Jobs y auto-siembra nocturna, INC-53).
102:   - `IngestRanksDumpAsync(Stream csvStream, int minUsersRated, ...)` (con `minUsersRated = 100` por defecto en INC-73).
103:   - `ProcessPendingDetailsBatchAsync(int batchSize, ...)` (enriquece metadatos de calidad, fundas, huella y editoriales regionales).
104:   - `ProcessPendingImagesBatchAsync(int batchSize, ...)`
105:   - `ProcessPendingAiBatchAsync(int gamesPerBatch, int maxBatches, ...)`
106:   - `PromoteReadyToCatalogBatchAsync(int batchSize, ...)` (promoción atómica y actualización incremental anti-duplicados por `BggId` en INC-73).
107:   - `BackfillCatalogQualityBatchAsync(int batchSize, ...)` (enriquecimiento retroactivo de catálogo por lotes, INC-73).
108:   - `RunScheduledBackfillCatalogQualityBatchAsync(int batchSize, ...)` (ruta de sistema desatendida para backfill, INC-73).
109:   - `RunDrainCycleAsync(...)` y `RunScheduledDrainCycleAsync(...)`

### 3.3. Streaming CSV Parser y Robustez de Red (`BggDumpParser.cs`)
Parser de alto rendimiento en streaming con `StreamReader` y máquina de estados para CSV que filtra en un único pase descartando juegos con menos de 30 valoraciones (`usersrated >= 30`), evitando cargar ficheros enteros en memoria RAM.
En INC-53 se optimizó para streams de red continuos (`ResponseHeadersRead`) donde `inputStream.CanSeek` es falso: el parser detecta si el stream permite o no rebobinado (`Seek`), evitando excepciones y procesando directamente el flujo como texto UTF-8 con huella de memoria estrictamente acotada (< 30 MB).

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
Sección dedicada a "Ingesta Masiva BGG & Staging (Top ~4.000 títulos, ≥1.000 votos)" con:
- Métricas en tiempo real: Total en staging, pendientes de detalle, imágenes, IA, listos para promoción y promovidos.
- Alerta visual amarilla si la cuota de IA fue alcanzada (`QuotaExceeded`).
- Botón de acción rápida: *«Drenar Ciclo de Staging Ahora»* con feedback interactivo.
- Botón de descarga y siembra autónoma: *«Descargar y Poblar Catálogo BGG (Top ~4.000, ≥1.000 votos)»*, protegido por la política de permisos `CanEditGames`, con animación reactiva (`_isSeedingStaging`), bloqueo concurrente mutuo y reporte detallado de títulos sembrados.
- Botón de acción destructiva protegida: *«Vaciar Staging»* con confirmación reactiva en dos pasos (*«¿Seguro? Sí, vaciar / Cancelar»*), invocando `ClearStagingAsync` mediante `ExecuteDeleteAsync` atómico en base de datos.

### 5.2. Galería Visual en Detalle de Juego (`GameDetail.razor`)
Bloque *"Galería Visual y Componentes"* que renderiza la contraportada (`BackCoverImageUrl`) y la fotografía de despliegue en mesa (`TableImageUrl`) en diseño de tarjeta editorial con fallback seguro.

---

## 6. Integración en Ciclos Desatendidos y Cloud Run Jobs

### 6.1. Runner Autónomo `seed-staging` (`Ludeka.Jobs`)
Incorporado en INC-53 como quinto trabajo fino de consola (`JobNames.SeedStaging`). Ejecuta una sola unidad de trabajo idempotente con clave diaria UTC, descargando y sembrando la tabla staging con el umbral configurado (`usersrated >= 1000`) sin intervención humana.

### 6.2. Auto-Siembra Inteligente en el Ciclo Nocturno (`NightlyCatalogingService.cs`)
En la Fase 3 del ciclo nocturno, antes del drenaje de staging, se evalúan las métricas actuales: si `stagingMetrics.TotalInStaging == 0`, el servicio invoca automáticamente `RunScheduledDownloadAndIngestLatestRanksAsync()`, garantizando que la base de datos comience su ciclo de drenaje sin requerir que un operador humano haya poblado staging previamente.

---

158: ## 7. Verificación y Cobertura de Pruebas
159: 
160: Se cuenta con una batería completa de pruebas unitarias sin dependencias externas de red ni mocks pesados, empleando Fakes deterministas:
161: - `BggCatalogStagingItemTests`: 12 pruebas de transiciones de dominio y validaciones.
162: - `BggDumpParserTests`: 3 pruebas de streaming CSV, filtro por umbral de votos y caracteres escapados.
163: - `BggMassIngestionAutonomousDownloadTests`: 10 pruebas de descarga remota, resolución de fechas, fallback resiliente, streaming no buscable, simulación, permisos de moderador, umbral 100 por defecto (INC-73) y vaciado atómico `ClearStagingAsync`.
164: - `BggXmlParserQualityTests` (INC-73): 7 pruebas de extracción de duraciones reales, fallback de escalabilidad sin votos comunitarios, inferencia analítica de huella en mesa, fundas y detección de editoriales multipaís.
165: - `RegionalPublisherMatcherTests` (INC-73): 7 pruebas de resolución territorial de sellos y filiales en los 7 países hispanohablantes soportados.
166: - `BggMassIngestionBackfillTests` (INC-73): 3 pruebas de promoción aditiva anti-duplicados por `BggId` y ejecución de enriquecimiento retroactivo por lotes.
167: - `GeekDoImagesClientTests`: 3 pruebas del cliente de galería comunitaria y modo simulación.
168: - `GeminiBatchSummaryTests`: 2 pruebas de síntesis en lotes y manejo de casos vacíos.
169: - `BggMassIngestionServiceTests`: 4 pruebas completas del ciclo de ingesta, detalles BGG, control de cuota 429 y promoción atómica a catálogo.
170: - `SeedStagingJobRunnerTests`: 1 prueba del runner de Cloud Run Jobs coordinado bajo concesión de ventana.
171: - `NightlyCatalogingServiceTests`: 2 pruebas de auto-siembra condicional cuando staging está vacío frente a cuando ya contiene registros.
172: - `SqliteSchemaMigratorTests`: Validación de migración defensiva de esquema SQLite.
173: 
174: **Total verificado en la solución:** **1.966 pruebas unitarias en verde al 100% (0 errores, 0 omitidas)**.
