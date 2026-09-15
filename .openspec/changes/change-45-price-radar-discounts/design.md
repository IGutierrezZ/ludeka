# Diseño Técnico: INC-45 — Radar de Bajadas de Precios, Mínimos Históricos y Alertas de Ofertas para 'Quiero comprar'

## 1. Arquitectura General y Flujo de Datos

```
[Tiendas / Microdatos Web] ──> [IStoreStockService] (Live Check)
                                        │
                                        ▼ (Snapshot)
                              [IGamePriceRepository]
                                        │
                                        ▼
                             [IPriceRadarService]
                                ┌───────┴─────────────────┐
                                ▼                         ▼
                     [Fichas de Juego /              [Mi Ludoteca /
                      StoreOffersCard]                Quiero comprar]
                                │                         │
                                ▼                         ▼
                        (Mínimo Histórico)        (Alertas de Chollo)
```

---

## 2. Definición de Contratos y Modelos de Dominio (`Ludeka.Core`)

### 2.1. Entidad `GamePriceSnapshot`
```csharp
namespace Ludeka.Core.Entities;

public class GamePriceSnapshot
{
    public Guid Id { get; private set; }
    public Guid GameId { get; private set; }
    public string StoreName { get; private set; } = string.Empty;
    public string AffiliateUrl { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = "€";
    public bool InStock { get; private set; }
    public DateTimeOffset RecordedAtUtc { get; private set; }

    private GamePriceSnapshot() { } // Para EF Core / deserialización

    public GamePriceSnapshot(
        Guid gameId,
        string storeName,
        string affiliateUrl,
        decimal price,
        bool inStock = true,
        string currency = "€",
        DateTimeOffset? recordedAtUtc = null,
        Guid? id = null)
    {
        if (gameId == Guid.Empty) throw new ArgumentException("GameId no puede ser vacío.", nameof(gameId));
        if (string.IsNullOrWhiteSpace(storeName)) throw new ArgumentException("StoreName no puede estar vacío.", nameof(storeName));
        if (string.IsNullOrWhiteSpace(affiliateUrl)) throw new ArgumentException("AffiliateUrl no puede estar vacía.", nameof(affiliateUrl));
        if (price < 0) throw new ArgumentOutOfRangeException(nameof(price), "El precio no puede ser negativo.");

        Id = id ?? Guid.NewGuid();
        GameId = gameId;
        StoreName = storeName.Trim();
        AffiliateUrl = affiliateUrl.Trim();
        Price = Math.Round(price, 2);
        Currency = string.IsNullOrWhiteSpace(currency) ? "€" : currency.Trim();
        InStock = inStock;
        RecordedAtUtc = recordedAtUtc ?? DateTimeOffset.UtcNow;
    }
}
```

### 2.2. Value Object `GamePriceMetrics`
```csharp
namespace Ludeka.Core.ValueObjects;

public record GamePriceMetrics
{
    public Guid GameId { get; init; }
    public decimal? CurrentLowestPrice { get; init; }
    public string? CurrentLowestStore { get; init; }
    public decimal? AllTimeLowPrice { get; init; }
    public string? AllTimeLowStore { get; init; }
    public DateTimeOffset? AllTimeLowDateUtc { get; init; }
    public decimal? AveragePrice { get; init; }
    public double? PriceDropPercentage { get; init; }
    public bool IsAllTimeLow { get; init; }
    public int TotalObservations { get; init; }

    public static GamePriceMetrics Empty(Guid gameId) => new() { GameId = gameId };

    public static GamePriceMetrics Calculate(
        Guid gameId,
        IEnumerable<GamePriceSnapshot> snapshots,
        IEnumerable<GamePurchaseLink>? currentOffers = null);
}
```

### 2.3. Value Object `PriceDropAlert`
```csharp
namespace Ludeka.Core.ValueObjects;

public record PriceDropAlert
{
    public Guid GameId { get; init; }
    public string GameTitle { get; init; } = string.Empty;
    public string GameSlug { get; init; } = string.Empty;
    public string? GameCoverUrl { get; init; }
    public string StoreName { get; init; } = string.Empty;
    public string Country { get; init; } = "España";
    public string AffiliateUrl { get; init; } = string.Empty;
    public decimal CurrentPrice { get; init; }
    public decimal? ReferencePrice { get; init; }
    public double DiscountPercentage { get; init; }
    public bool IsAllTimeLow { get; init; }
    public DateTimeOffset DetectedAtUtc { get; init; }
}
```

---

## 3. Capa de Aplicación (`Ludeka.Application`)

### 3.1. Interfaz `IGamePriceRepository`
```csharp
namespace Ludeka.Application.Contracts;

public interface IGamePriceRepository
{
    Task RecordSnapshotAsync(GamePriceSnapshot snapshot, CancellationToken ct = default);
    Task RecordSnapshotsBatchAsync(IEnumerable<GamePriceSnapshot> snapshots, CancellationToken ct = default);
    Task<IReadOnlyList<GamePriceSnapshot>> GetHistoryAsync(Guid gameId, int limit = 50, CancellationToken ct = default);
    Task<GamePriceMetrics> GetMetricsAsync(Guid gameId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, GamePriceMetrics>> GetMetricsBatchAsync(IEnumerable<Guid> gameIds, CancellationToken ct = default);
    Task<IReadOnlyList<GamePriceSnapshot>> GetRecentSnapshotsAsync(int limit = 100, CancellationToken ct = default);
}
```

### 3.2. Interfaz `IPriceRadarService`
```csharp
namespace Ludeka.Application.Contracts;

public interface IPriceRadarService
{
    Task<IReadOnlyList<PriceDropAlert>> GetTopDiscountsAsync(int limit = 20, string? country = null, CancellationToken ct = default);
    Task<IReadOnlyList<PriceDropAlert>> GetUserWantToBuyAlertsAsync(string userId, CancellationToken ct = default);
    Task<GamePriceMetrics> GetGamePriceMetricsAsync(Guid gameId, CancellationToken ct = default);
    Task RecordPriceObservationAsync(Guid gameId, string storeName, string affiliateUrl, decimal price, bool inStock, string currency = "€", CancellationToken ct = default);
    Task<int> ScanWantToBuyPricesAsync(int maxGames = 20, CancellationToken ct = default);
}
```

---

## 4. Capa de Infraestructura (`Ludeka.Infrastructure`)

### 4.1. Persistencia SQLite y PostgreSQL
- Tabla `GamePriceSnapshots`:
  - `Id TEXT PRIMARY KEY`
  - `GameId TEXT NOT NULL`
  - `StoreName TEXT NOT NULL`
  - `AffiliateUrl TEXT NOT NULL`
  - `Price REAL NOT NULL`
  - `Currency TEXT NOT NULL`
  - `InStock INTEGER NOT NULL`
  - `RecordedAtUtc TEXT NOT NULL`
  - Índices: `(GameId, RecordedAtUtc DESC)` y `(Price)`.
- `SqliteSchemaMigrator`: método `MigrateGamePriceSnapshotsTableAsync`.
- `LudekaDbContext`: configuración con `builder.Entity<GamePriceSnapshot>()`.

### 4.2. Background Worker `PriceRadarHostedService`
- Orquesta un ciclo de muestreo cada `PriceRadarOptions.CheckIntervalHours` (def: 6h).
- Invoca `ScanWantToBuyPricesAsync` para los juegos con mayor concurrencia en listas de compra.
- Totalmente cancelable y deshabilitable mediante `PriceRadarOptions.Enabled`.

---

## 5. Capa Web Blazor (`Ludeka.Web`)

### 5.1. `StoreOffersCard.razor`
- Inyección de `IPriceRadarService`.
- En la cabecera: badge editorial con `AllTimeLowPrice` si existe registro ("Mínimo histórico: 34,90 € en Zacatrus").
- En cada tarjeta de oferta: si `CurrentPrice <= AllTimeLowPrice`, muestra píldora de `"¡Mínimo Histórico!"`.

### 5.2. `MyLibrary.razor` (Pestaña 'Quiero comprar')
- Banner de resumen de oportunidades de compra:
  - *"Tienes X ofertas activas en tu lista de compra"* con enlaces rápidos.
- Badges dinámicos en cada tarjeta de juego:
  - Píldora con el mejor precio disponible y porcentaje de bajada o etiqueta de mínimo histórico.

### 5.3. `Radar.razor`
- Pestañas superiores:
  - `[ 🎁 Sorteos Activos ]` | `[ 🏷️ Ofertas & Chollos ]`
- Vista de Ofertas: Grid responsive de chollos con portada del juego, porcentaje de descuento destacado, tienda, precio rebajado y precio habitual, con filtro territorial.
