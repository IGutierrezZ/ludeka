# 20. Monitorización y Verificación de Stock en Tiempo Real en Enlaces de Compra

> **Estado del Módulo:** ✅ Implementado y Verificado  
> **Incremento Asociado:** [INC-27 (inc-27-store-live-stock-check.md)](file:///c:/repos/Ludeka/docs/increments/archive/inc-27-store-live-stock-check.md)  
> **Pruebas Automatizadas:** 20 pruebas dedicadas  

---

## 1. Propósito y Filosofía
El módulo de **Verificación de Stock en Tiempo Real** elimina una de las mayores frustraciones en la experiencia de compra de juegos de mesa: acceder a un enlace comercial solo para encontrarse con que el juego está agotado, en reimpresión indeterminada o descatalogado.

Se implementa bajo los siguientes pilares irrenunciables:
1. **Cero Impacto en SSR (Non-blocking):** La ficha de juego (`GameDetail.razor`) se entrega al navegador de forma instantánea (<100ms) sin esperar a tiendas externas.
2. **Carga Diferida y Reactiva:** `StoreOffersCard.razor` solicita de forma asíncrona en segundo plano la disponibilidad tras el primer render (`OnAfterRenderAsync`), actualizando reactivamente los badges en la interfaz.
3. **Límite Estricto de Tiempo (Timeout 1.5s):** Cada consulta a una tienda se cancela irrevocablemente tras 1.500 ms con `CancellationTokenSource`, impidiendo retrasos o congelaciones en la navegación.
4. **Caché L1 en Memoria con TTL Diferenciado:** Uso de `IMemoryCache` con 30 minutos de TTL para respuestas exitosas y 5 minutos para fallos/timeouts, protegiendo tanto la infraestructura propia como la de las tiendas socias.
5. **Taxonomía Semántica Cuádruple:** Pasa de un booleano binario a cuatro estados bien definidos (`InStock`, `LowStock`, `OutOfStock`, `Unknown`).
6. **Diseño Anti-Frustración y Transparencia:** Los productos agotados reciben un tratamiento visual atenuado con el texto "Agotado en tienda / Ver disponibilidad", evitando compras frustradas.
7. **Refresco Manual Interactivo:** Cada oferta dispone de un botón "🔄" para re-comprobar el estado bajo demanda e invalidar la caché.

---

## 2. Componentes de Dominio (`Ludeka.Core`)

### 2.1 Enum `StockStatus`
Ubicación: [`src/Ludeka.Core/Enums/StockStatus.cs`](file:///c:/repos/Ludeka/src/Ludeka.Core/Enums/StockStatus.cs)

```csharp
public enum StockStatus
{
    Unknown = 0,    // No determinado, timeout o sin datos recientes
    InStock = 1,    // Disponible con existencias confirmadas
    LowStock = 2,   // Últimas unidades en inventario
    OutOfStock = 3  // Sin existencias o agotado
}
```

### 2.2 Value Object `StoreStockInfo`
Ubicación: [`src/Ludeka.Core/ValueObjects/StoreStockInfo.cs`](file:///c:/repos/Ludeka/src/Ludeka.Core/ValueObjects/StoreStockInfo.cs)

Encapsula el resultado inmutable de una verificación:
- `StockStatus Status`
- `int? AvailableQuantity`
- `decimal? CurrentPrice`
- `DateTime? LastCheckedUtc`
- `string? StatusNote`
- `bool IsChecking`
- Helpers: `IsInStock`, `IsLowStock`, `IsOutOfStock`, `IsUnknown`, `GetRelativeTimeText(DateTime? nowUtc)`.
- Métodos factoría: `InStock(...)`, `LowStock(...)`, `OutOfStock(...)`, `Unknown(...)`, `Checking()`.

### 2.3 Retrocompatibilidad en `GamePurchaseLink`
Ubicación: [`src/Ludeka.Core/ValueObjects/GamePurchaseLink.cs`](file:///c:/repos/Ludeka/src/Ludeka.Core/ValueObjects/GamePurchaseLink.cs)

- Propiedad calculada `InitialStockStatus => InStock ? StockStatus.InStock : StockStatus.OutOfStock;` para mantener interoperabilidad con ofertas estáticas existentes.

---

## 3. Capa de Aplicación (`Ludeka.Application`)

### 3.1 Contratos de Servicio y Cliente
Ubicaciones:
- [`src/Ludeka.Application/Contracts/IStoreStockClient.cs`](file:///c:/repos/Ludeka/src/Ludeka.Application/Contracts/IStoreStockClient.cs)
- [`src/Ludeka.Application/Contracts/IStoreStockService.cs`](file:///c:/repos/Ludeka/src/Ludeka.Application/Contracts/IStoreStockService.cs)

```csharp
public interface IStoreStockClient
{
    int Priority { get; }
    bool CanHandle(string storeName, string affiliateUrl);
    Task<StoreStockInfo> CheckStockAsync(string storeName, string affiliateUrl, CancellationToken cancellationToken = default);
}

public interface IStoreStockService
{
    ValueTask<StoreStockInfo> GetStockAsync(string storeName, string affiliateUrl, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyDictionary<string, StoreStockInfo>> GetStockBatchAsync(IEnumerable<GamePurchaseLink> offers, CancellationToken cancellationToken = default);
    void InvalidateStockCache(string affiliateUrl);
}
```

### 3.2 Opciones de Configuración `StoreStockOptions`
Ubicación: [`src/Ludeka.Application/Options/StoreStockOptions.cs`](file:///c:/repos/Ludeka/src/Ludeka.Application/Options/StoreStockOptions.cs)

- `DefaultTtlMinutes`: 30 min por defecto.
- `ErrorTtlMinutes`: 5 min por defecto.
- `TimeoutMilliseconds`: 1500 ms por defecto.
- `EnableLiveChecking`: true por defecto.
- `EnableSimulation`: false por defecto.

### 3.3 Orquestación y Resiliencia `StoreStockService`
Ubicación: [`src/Ludeka.Application/Features/Catalog/StoreStockService.cs`](file:///c:/repos/Ludeka/src/Ludeka.Application/Features/Catalog/StoreStockService.cs)

- Normalización de claves de caché por URL: `store_stock:{affiliateUrl}`.
- Manejo estricto de timeout (1.5s) con `CancellationTokenSource.CreateLinkedTokenSource`.
- Captura de `OperationCanceledException` y excepciones de red/HTTP degradando a `StoreStockInfo.Unknown` con caché reducida (5 min).
- Ejecución concurrente en lote con deduplicación por URL para todas las ofertas de la ficha.

---

## 4. Infraestructura y Adaptadores (`Ludeka.Infrastructure`)

### 4.1 Parser Estructurado `HtmlSchemaStoreStockClient`
Ubicación: [`src/Ludeka.Infrastructure/Stores/HtmlSchemaStoreStockClient.cs`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/Stores/HtmlSchemaStoreStockClient.cs)

- Inspección de microdatos Schema.org:
  - Microdata `itemprop="availability" href="https://schema.org/InStock"` y `OutOfStock`.
  - JSON-LD script `{"availability": "https://schema.org/InStock"}`.
- Metadatos OpenGraph: `<meta property="og:availability" content="instock|oos" />`.
- Heurísticas léxicas en español para tiendas especializadas (Zacatrus, Cuarto de Juegos, Dungeon Marvels).
- Detección de precio actualizado en microdatos.

### 4.2 Cliente Determinado de Simulación `SimulationStoreStockClient`
Ubicación: [`src/Ludeka.Infrastructure/Stores/SimulationStoreStockClient.cs`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/Stores/SimulationStoreStockClient.cs)

- Permite pruebas unitarias y desarrollo offline sin fragilidad de scraping.
- Simula estados de stock basados en patrones en la URL ("agotado", "ultimas", "timeout", "error").

---

## 5. Presentación Blazor (`Ludeka.Web`)

### Componente `StoreOffersCard.razor`
Ubicación: [`src/Ludeka.Web/Components/Shared/StoreOffersCard.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/StoreOffersCard.razor)

- **Render Inicial:** Muestra ofertas con PVP orientativo y estado inicial sin demorar la entrega SSR.
- **Carga en segundo plano:** Disparada en `OnAfterRenderAsync` mediante `GetStockBatchAsync`.
- **Badges:**
  - 🟢 **En stock:** Indicador esmeralda con unidades disponibles si existen.
  - 🟡 **Últimas unidades:** Indicador ámbar de advertencia de stock bajo.
  - 🔴 **Agotado en tienda:** Indicador carmesí.
  - ⚪ **Verificar en web:** Indicador neutro si la tienda tarda >1.5s.
- **Botón de Compra Diferenciado:** Para ofertas agotadas, el botón se atenúa y cambia el texto a "Agotado / Ver tienda".
- **Timestamp Relativo:** Muestra la frescura del dato ("Comprobado hace 5 min", "hace instantes").
- **Botón de Refresco:** Botón "🔄" por oferta para forzar la re-verificación e invalidación de caché.

### 5.2 Presentación Honesta en Fallback de Búsqueda (Sin Falsos Positivos)
- **Activación de Fallback:** Cuando un juego no dispone de ofertas comerciales vinculadas directamente (`_isReferenceFallback == true`), se generan enlaces deterministas de búsqueda en catálogo para tiendas de referencia de España (Zacatrus, Cuarto de Juegos, Dracotienda, Jugamos Otra).
- **Tratamiento Honesto Anti-Frustración:**
  - Se suprime la generación de tarjetas simuladas con semáforos verdes o precios ficticios.
  - Se omiten las comprobaciones HTTP en segundo plano contra URLs de búsqueda para evitar bloqueos de cortafuegos (Cloudflare 403) y degradaciones erróneas a *«Verificar en web»*.
  - Se despliega un contenedor sobrio e informativo con el estado *«Sin precios ni stock confirmados»* y enlaces directos de búsqueda rápida en un clic (`[Buscar en {Tienda} ↗]`).
