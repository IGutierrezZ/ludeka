# Diseño Técnico: change-41-ingesta-bgg-catalogo (Incremento 41)

## 1. Arquitectura General y Flujo de Datos

```
[ BGG bg_ranks Dump (CSV/GZ) ]
            │
            ▼ (usersrated >= 30)
 ┌────────────────────────────────────────────────────────┐
 │           BggCatalogStaging (SQLite / PostgreSQL)       │
 │                                                        │
 │  BggId | Title | Year | Rank | UsersRated | Statuses   │
 └──────────────────────────┬─────────────────────────────┘
                            │
        ┌───────────────────┼──────────────────────────┐
        │ 1. Thing Batch    │ 2. GeekDo Photos         │ 3. Gemini Batch
        ▼                   ▼                          ▼
 [ BGG XMLAPI2 ]     [ GeekDo Images API ]     [ Gemini Flash API ]
 (/xmlapi2/thing     (3 fotos más votadas)     (5 a 10 juegos/req)
  hasta 20 IDs)             │                          │
        │                   ▼                          ▼
        │            [ Cloudflare R2 ]          [ JSON Resumen ]
        │          (cover/back/table.webp)             │
        │                   │                          │
        └───────────────────┼──────────────────────────┘
                            ▼
               ┌────────────────────────┐
               │    Validar Staging     │
               └────────────┬───────────┘
                            ▼
               ┌────────────────────────┐
               │   Promoción a Game     │
               │  (Catálogo Definitivo) │
               └────────────────────────┘
```

---

## 2. Modelos de Dominio (`Ludeka.Core`)

### 2.1 Entidad `BggCatalogStagingItem`
```csharp
public class BggCatalogStagingItem
{
    public int BggId { get; private set; }
    public string OriginalTitle { get; private set; } = string.Empty;
    public string? SpanishTitle { get; private set; }
    public int? YearPublished { get; private set; }
    public int? BggRank { get; private set; }
    public int UsersRated { get; private set; }

    // Estados independientes
    public StagingFetchStatus FetchStatus { get; private set; } = StagingFetchStatus.Pending;
    public StagingImagesStatus ImagesStatus { get; private set; } = StagingImagesStatus.Pending;
    public StagingAiStatus AiStatus { get; private set; } = StagingAiStatus.Pending;
    public StagingPromotionStatus PromotionStatus { get; private set; } = StagingPromotionStatus.Pending;

    // Caché de detalles BGG Thing XML o JSON
    public string? RawThingXml { get; private set; }
    public string? Designer { get; private set; }
    public string? Publisher { get; private set; }
    public string? Description { get; private set; }
    public double BggRating { get; private set; }
    public int MinPlayers { get; private set; }
    public int MaxPlayers { get; private set; }
    public int PlayingTimeMinutes { get; private set; }
    public int MinAge { get; private set; }

    // URLs de medios en Cloudflare R2
    public string? CoverImageUrl { get; private set; }
    public string? ThumbnailUrl { get; private set; }
    public string? BackCoverImageUrl { get; private set; }
    public string? TableImageUrl { get; private set; }

    // Síntesis de IA serializada
    public string? AiSummaryJson { get; private set; }

    // Control y Auditoría
    public int RetryCount { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; private set; }

    // Métodos de transición de estado
    public void MarkFetched(string rawXml, string? designer, string? publisher, string? description, double rating, int minP, int maxP, int time, int age);
    public void MarkImagesCompleted(string coverUrl, string thumbUrl, string? backUrl, string? tableUrl);
    public void MarkAiCompleted(string summaryJson);
    public void MarkAiQuotaExceeded();
    public void MarkPromoted();
    public void MarkFailed(string stage, string error);
}
```

### 2.2 Enums de Estado de Staging
```csharp
public enum StagingFetchStatus { Pending, InProgress, Fetched, Failed }
public enum StagingImagesStatus { Pending, InProgress, Completed, Skipped, Failed }
public enum StagingAiStatus { Pending, InProgress, Completed, QuotaExceeded, Skipped, Failed }
public enum StagingPromotionStatus { Pending, InProgress, Promoted, Failed }
```

### 2.3 Ampliación de `Game`
- Incorporar `BackCoverImageUrl` y `TableImageUrl` como propiedades de solo lectura con métodos de actualización `UpdateMediaUrls(string? cover, string? thumb, string? back, string? table)`.

---

## 3. Contratos de Aplicación (`Ludeka.Application`)

### 3.1 `IBggCatalogStagingRepository`
```csharp
public interface IBggCatalogStagingRepository
{
    Task UpsertBatchAsync(IEnumerable<BggCatalogStagingItem> items, CancellationToken ct = default);
    Task<BggCatalogStagingItem?> GetByBggIdAsync(int bggId, CancellationToken ct = default);
    Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingFetchBatchAsync(int batchSize = 20, CancellationToken ct = default);
    Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingImagesBatchAsync(int batchSize = 10, CancellationToken ct = default);
    Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingAiBatchAsync(int batchSize = 10, CancellationToken ct = default);
    Task<IReadOnlyList<BggCatalogStagingItem>> GetPendingPromotionBatchAsync(int batchSize = 50, CancellationToken ct = default);
    Task UpdateAsync(BggCatalogStagingItem item, CancellationToken ct = default);
    Task UpdateBatchAsync(IEnumerable<BggCatalogStagingItem> items, CancellationToken ct = default);
    Task<BggStagingMetricsDto> GetMetricsAsync(CancellationToken ct = default);
    Task ResetQuotaExceededStatusAsync(CancellationToken ct = default);
}
```

### 3.2 `IGeekDoImagesClient`
```csharp
public interface IGeekDoImagesClient
{
    Task<GeekDoGalleryImagesDto> GetTopVotedImagesAsync(int bggId, CancellationToken ct = default);
}

public record GeekDoGalleryImagesDto(
    string? FrontCoverUrl,
    string? BackCoverUrl,
    string? TableOrGameplayUrl);
```

### 3.3 Extensión de `IAiGameSummaryService` para Batching
```csharp
public interface IAiGameSummaryService
{
    // Métodos existentes...
    Task<AiGameSummaryDto> GenerateSummaryAsync(Game game, CancellationToken ct = default);

    // Nuevo método para procesar lotes de 5 a 10 juegos
    Task<AiBatchResultDto> GenerateBatchSummariesAsync(
        IReadOnlyList<AiGameBatchInputDto> games,
        CancellationToken ct = default);
}

public record AiGameBatchInputDto(
    int BggId,
    string SpanishTitle,
    string OriginalTitle,
    string? Designer,
    string? Publisher,
    int YearPublished,
    string? Description,
    double Rating,
    int MinPlayers,
    int MaxPlayers,
    int MinAge);

public record AiBatchResultDto(
    bool Success,
    bool QuotaExhausted,
    IReadOnlyDictionary<int, AiGameSummaryDto> Summaries,
    string? ErrorMessage);
```

### 3.4 `IBggMassIngestionService`
```csharp
public interface IBggMassIngestionService
{
    Task<int> IngestRanksDumpAsync(Stream dumpStream, int minUsersRated = 30, CancellationToken ct = default);
    Task<int> ProcessDetailsBatchAsync(int batchSize = 20, CancellationToken ct = default);
    Task<int> ProcessImagesBatchAsync(int batchSize = 10, CancellationToken ct = default);
    Task<AiBatchProcessingResultDto> ProcessAiBatchAsync(int batchSize = 10, CancellationToken ct = default);
    Task<int> PromoteReadyToCatalogBatchAsync(int batchSize = 50, CancellationToken ct = default);
    Task<BggStagingMetricsDto> GetProgressAsync(CancellationToken ct = default);
}
```

---

## 4. Implementación en Infraestructura (`Ludeka.Infrastructure`)

1. **Parser de Dump BGG:** Lectura eficiente de CSV por streaming sin cargar el archivo completo en memoria, filtrando filas donde `UsersRated >= minUsersRated`.
2. **Cliente GeekDo:**
   - Deserialización de JSON con `System.Text.Json`.
   - Búsqueda por `imageurl` o campos de alta resolución `images.original` / `images.large`.
   - Agrupación por tipos y ordenación por `numpositive` descendente.
3. **Descarga y subida de imágenes con `IImageStorageService`:**
   - Empleo de `HttpClient` con streams sin crear ficheros temporales en disco.
   - Llamada a `UploadGameImageVariantsAsync` de INC-40 para generar WebP optimizados y guardar las URLs en el registro de staging.
4. **Gemini Flash Batching:**
   - Construcción de prompt conjunto con instrucciones de salida de un único array JSON: `[{ "bggId": 174430, "generalVerdict": "...", ... }]`.
   - Captura de `HttpRequestException` con status 429 (`TooManyRequests`) para devolver `QuotaExhausted = true` sin romper la ejecución.
5. **Mapeo EF Core en `LudekaDbContext`:**
   - Tabla `BggCatalogStaging` con índices en `BggId` (único), `FetchStatus`, `ImagesStatus`, `AiStatus`, `PromotionStatus`, `UsersRated`.
   - Compatibilidad nativa con SQLite y PostgreSQL/Supabase.
