# Diseño Arquitectónico: Ingesta de Feeds Comerciales, Auto-asignación de EAN y Panel de Afiliados

## 1. Modelo de Dominio y Datos

### 1.1 Entidades en `Ludeka.Core`
```csharp
namespace Ludeka.Core.Entities;

public enum FeedFormat
{
    GoogleShoppingXml = 1,
    GenericCsv = 2
}

public class AffiliateFeedSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StoreName { get; set; } = string.Empty;
    public string FeedUrl { get; set; } = string.Empty;
    public FeedFormat Format { get; set; } = FeedFormat.GoogleShoppingXml;
    public string? AffiliateTag { get; set; }
    public string Country { get; set; } = "España";
    public bool IsEnabled { get; set; } = true;
    public int SyncIntervalHours { get; set; } = 6;
    public DateTimeOffset? LastSyncUtc { get; set; }
    public string? LastSyncStatus { get; set; }
    public int MatchedProductsCount { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public class AffiliateEanDiscrepancyLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public string GameTitle { get; set; } = string.Empty;
    public string GameSlug { get; set; } = string.Empty;
    public string? CurrentEan { get; set; }
    public string FeedEan { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public DateTimeOffset DetectedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public bool IsResolved { get; set; }
    public string? ResolutionNote { get; set; }
}
```

### 1.2 Persistencia en EF Core
- Se añaden los `DbSet<AffiliateFeedSource> AffiliateFeedSources` y `DbSet<AffiliateEanDiscrepancyLog> AffiliateEanDiscrepancies` a `LudekaDbContext`.
- Migración correspondiente de EF Core para PostgreSQL (Supabase) y reconciliación defensiva en `SqliteSchemaMigrator`.

---

## 2. Capa de Aplicación (`Ludeka.Application`)

### 2.1 Contratos e Interfaces
- `IAffiliateFeedSourceRepository`: CRUD de fuentes de feeds y consulta de fuentes habilitadas.
- `IAffiliateEanDiscrepancyRepository`: Registro y resolución de discrepancias.
- `IFeedParser`: Abstracción para el parsing en streaming de productos:
  ```csharp
  public record FeedProductItem(
      string Sku,
      string Title,
      string? RawBarcode,
      string? NormalizedEan,
      decimal Price,
      string Currency,
      bool InStock,
      string ProductUrl);

  public interface IFeedParser
  {
      IAsyncEnumerable<FeedProductItem> ParseStreamAsync(Stream stream, CancellationToken ct = default);
  }
  ```
- `ICatalogFeedSyncService`: Orquestador de la sincronización de feeds:
  - Descarga mediante `HttpClient` en streaming.
  - Ejecuta el parser.
  - Aplica las tres reglas de matching (EAN directo, auto-asignación por título, detección de discrepancias).
  - Actualiza `Game.PurchaseLinks`.

---

## 3. Capa de Trabajos en Segundo Plano (`Ludeka.Jobs`)

### 3.1 `CatalogFeedSyncJobRunner`
- Registrado en `JobNames.FeedSync = "feed-sync"`.
- Invoca a `ICatalogFeedSyncService.SyncAllActiveFeedsAsync(ct)`.
- Envuelto en `JobExecutionCoordinator` para trazabilidad de latidos, métricas de ejecución y prevención de solapamientos distribuidos.

---

## 4. Capa Web y Presentación (`Ludeka.Web`)

### 4.1 Componente `/admin/afiliados` (`AffiliatesAdmin.razor`)
- Ruta `/admin/afiliados`, accesible con permisos administrativos.
- Tab 1: Fuentes de Catálogo (Listado, creación/edición, conmutador de activación y botón de forzar sincronización).
- Tab 2: Cola de Discrepancias EAN (Listado con comparativa de EAN actual vs EAN del comercio, y acción directa `Promover a EAN principal` o `Descartar`).
