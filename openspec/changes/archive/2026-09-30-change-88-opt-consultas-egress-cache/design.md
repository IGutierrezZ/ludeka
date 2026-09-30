# Documento de Diseño Arquitectónico: INC-87 — Optimización de Consultas SQL, Paginación Quirúrgica y Caché en Memoria

> **ID del Cambio:** `change-87-opt-consultas-egress-cache`  
> **Incremento Asociado:** INC-87  
> **Estado:** ⏳ Diseño en revisión  

---

## 1. Decisiones de Arquitectura (ADRs)

### Decisión D1: Paginación en Dos Fases en `SearchAsync` frente a JSON Functions en SQL
* **Contexto:** Las facetas avanzadas del catálogo (`Scalability`, `Complexities`, `EspecialParejas`) evalúan colecciones complejas en JSON almacenadas en columnas de la entidad `Game`.
* **Alternativas consideradas:**
  1. *Reescribir las consultas a operadores JSON específicos de PostgreSQL (`jsonb_array_elements`, `->`, `@>`):* Rompería la persistencia dual en desarrollo local (SQLite).
  2. *Descarga completa en memoria (`query.ToListAsync` actual):* Provoca el consumo masivo de egress en Supabase al transferir miles de descripciones e imágenes.
  3. *Paginación en Dos Fases (Seleccionada):* Se proyectan únicamente los 7 campos estrictamente necesarios para evaluar el filtro (`Id`, `Scalability`, `Duration`, `Age`, `Style`, `BggRank`, `BggRating`), se filtran y ordenan en memoria, se aíslan los 24 IDs de la página solicitada, y finalmente se cargan por clave primaria únicamente esas 24 entidades completas.
* **Consecuencias:** Se reduce el payload de red más de un 95%, se mantiene la compatibilidad 100% idéntica entre SQLite y PostgreSQL, y se preserva la lógica de filtrado probada y validada en incrementos anteriores.

### Decisión D2: Patrón Decorador para la Capa de Caché de Directorios
* **Contexto:** Se requiere cachear los listados de tiendas y editoriales para evitar consultas repetidas al servidor de base de datos.
* **Alternativas consideradas:**
  1. *Inyectar `IMemoryCache` directamente en `StoreService` y `PublisherService`:* Ensucia la capa de aplicación con dependencias de infraestructura y dificulta las pruebas unitarias puras del dominio.
  2. *Decoradores `CachedStoreService` y `CachedPublisherService` (Seleccionada):* Mismo patrón arquitectónico de éxito utilizado en `CachedCatalogService` y `CachedHomeDashboardService`. Los servicios base se mantienen puros; los decoradores gestionan las claves de caché, TTL e invalidación.
* **Consecuencias:** Adherencia estricta a Principios SOLID (Single Responsibility, Open/Closed) y Clean Architecture.

---

## 2. Contratos e Interfaces (`Ludeka.Application`)

### 2.1. Nuevos métodos en `IGameRepository`
```csharp
namespace Ludeka.Application.Contracts;

public partial interface IGameRepository
{
    /// <summary>
    /// Obtiene un diccionario indexado por nombre normalizado de tienda con el conteo de juegos
    /// que disponen de ofertas de compra para dicha tienda, mediante proyección mínima de enlaces.
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> GetOfferCountsByStoreAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene un diccionario con el recuento de títulos asociados a cada editorial,
    /// mediante proyección selectiva de campos de editorial sin transferir la entidad Game completa.
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> GetGameCountsByPublisherAsync(CancellationToken ct = default);

    /// <summary>
    /// Recupera únicamente los juegos que contienen al menos una oferta vinculada a la tienda especificada.
    /// </summary>
    Task<IReadOnlyList<Game>> GetGamesWithStoreOffersAsync(string storeName, CancellationToken ct = default);

    /// <summary>
    /// Recupera los juegos que disponen de enlaces de compra (PurchaseLinks) existentes.
    /// </summary>
    Task<IReadOnlyList<Game>> GetGamesWithPurchaseLinksAsync(int? limit = null, CancellationToken ct = default);
}
```

### 2.2. Proyección Ligera para Paginación de Catálogo
```csharp
namespace Ludeka.Application.DTOs;

public sealed record GameFilterIndexItem(
    Guid Id,
    IReadOnlyList<ScalabilityEntry> Scalability,
    GameDuration Duration,
    AgeRating Age,
    GameStyle Style,
    int? BggRank,
    double BggRating
);
```

---

## 3. Implementación de Repositorios (`Ludeka.Infrastructure`)

### 3.1. Proyecciones en `SqliteGameRepository`
* `GetOfferCountsByStoreAsync`:
  ```csharp
  var linksList = await scope.Context.Games
      .AsNoTracking()
      .Where(g => g.PurchaseLinks != null)
      .Select(g => g.PurchaseLinks)
      .ToListAsync(ct);
  ```
  Calcula en memoria los conteos agrupados por tienda sobre colecciones pequeñas de enlaces, sin tocar descripciones ni imágenes.
* `GetGameCountsByPublisherAsync`:
  ```csharp
  var publisherData = await scope.Context.Games
      .AsNoTracking()
      .Select(g => new { g.Publisher, g.SpanishPublisher, g.RegionalPublishers })
      .ToListAsync(ct);
  ```
  Solo transfiere strings de nombres de editoriales.
* `GetGamesWithPurchaseLinksAsync`:
  Filtra en base de datos los juegos con ofertas:
  ```csharp
  var query = scope.Context.Games
      .AsNoTracking()
      .Where(g => g.PurchaseLinks != null && g.PurchaseLinks.Count > 0);
  if (limit.HasValue && limit.Value > 0) query = query.Take(limit.Value);
  return await query.ToListAsync(ct);
  ```

### 3.2. Dos Fases en `SearchAsync`
```csharp
if (!hasInMemoryFilters)
{
    // Ruta directa nativa SQL existente (rápida para filtros escalares)
    ...
}

// FASE 1: Proyección ligera de indexación
var candidateIndexItems = await query
    .Select(g => new GameFilterIndexItem(
        g.Id,
        g.Scalability,
        g.Duration,
        g.Age,
        g.Style,
        g.BggRank,
        g.BggRating))
    .ToListAsync(ct);

// FASE 2: Filtrado y ordenación sobre índices ligeros
var filtered = ApplyInMemoryFilters(candidateIndexItems, criteria);
int totalCount = filtered.Count;
var pagedIds = filtered
    .OrderBy(g => g.BggRank.HasValue ? 0 : 1)
    .ThenBy(g => g.BggRank ?? int.MaxValue)
    .ThenByDescending(g => g.BggRating)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .Select(g => g.Id)
    .ToList();

// FASE 3: Materialización acotada de las entidades de la página
var fullGames = await scope.Context.Games
    .AsNoTracking()
    .Where(g => pagedIds.Contains(g.Id))
    .ToListAsync(ct);

// Reordenar para preservar el orden resuelto en Fase 2
var idToIndex = pagedIds.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
var orderedResult = fullGames.OrderBy(g => idToIndex.GetValueOrDefault(g.Id, int.MaxValue)).ToList();

return (orderedResult, totalCount);
```

### 3.3. Sorteos en `SqliteGiveawayRepository`
```csharp
var query = scope.Context.Giveaways.AsNoTracking().AsQueryable();
if (!includeExpired)
{
    var now = DateTimeOffset.UtcNow;
    query = query.Where(g => g.DeadlineAt >= now);
}
return await query.OrderBy(g => g.DeadlineAt).ToListAsync(ct);
```

---

## 4. Capa de Caché Decoradora (`Ludeka.Application.Features`)

### 4.1. `CachedStoreService`
* Clave: `directory:stores:all:c_{country}:t_{type}:s_{search}`
* Expiración: Deslizante 15 min / Absoluta 30 min.
* Invalidación: Al crear, editar o eliminar tiendas.

### 4.2. `CachedPublisherService`
* Clave: `directory:publishers:all:s_{search}`
* Expiración: Deslizante 15 min / Absoluta 30 min.
* Invalidación: Al crear, editar o eliminar editoriales.

### 4.3. Caché de Sorteos en `GiveawayService`
* Clave: `giveaways:active:c_{country}`
* Expiración: Deslizante 2 min / Absoluta 5 min.
* Invalidación: Al publicar o moderar un sorteo.

### 4.4. Caché de Radar de Chollos en `PriceRadarService`
* Clave: `radar:deals:c_{country}:l_{limit}`
* Expiración: Deslizante 5 min / Absoluta 15 min.

---

## 5. Registro e Inyección de Dependencias
En `LudekaServiceCollectionExtensions.cs`:
```csharp
// Tiendas con decorador de caché
services.AddScoped<StoreService>();
services.AddScoped<IStoreService, CachedStoreService>(sp =>
    new CachedStoreService(sp.GetRequiredService<StoreService>(), sp.GetRequiredService<IMemoryCache>()));

// Editoriales con decorador de caché
services.AddScoped<PublisherService>();
services.AddScoped<IPublisherService, CachedPublisherService>(sp =>
    new CachedPublisherService(sp.GetRequiredService<PublisherService>(), sp.GetRequiredService<IMemoryCache>()));
```
