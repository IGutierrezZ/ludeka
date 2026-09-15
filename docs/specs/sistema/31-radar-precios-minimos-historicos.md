# Módulo 31 — Radar de Bajadas de Precios, Mínimos Históricos y Alertas para 'Quiero comprar'

> **Incremento:** [INC-45 (docs/increments/archive/inc-45-price-radar-discounts.md)](file:///c:/repos/Ludeka/docs/increments/archive/inc-45-price-radar-discounts.md)  
> **Estado:** Implementado y Verificado (1.005 tests unitarios en verde)  
> **Dependencias:** Módulo 01 (Catálogo), Módulo 02 (Ludoteca), Módulo 17 (Territorio), Módulo 20 (Verificación de Stock), Módulo 25 (Motor de Afiliados)

---

## 1. Propósito y Alcance

El módulo **Radar de Precios** proporciona a la comunidad de Ludeka una herramienta transparente y desatendida para monitorizar las variaciones de precio en tiendas colaboradoras en español, detectar mínimos históricos reales y alertar a los usuarios de bajadas significativas en los títulos que tienen guardados en su lista de seguimiento «Quiero comprar».

### Capacidades Principales
1. **Histórico y Mínimos Históricos por Juego:** Registro cronológico inmutable de instantáneas de precio (`GamePriceSnapshot`) y cálculo de métricas agregadas (`GamePriceMetrics`).
2. **Alertas Contextuales en Ludoteca:** Los usuarios reciben avisos visuales discretos y banners de oportunidad en `/mi-ludoteca` (pestaña 'Comprar') cuando un título de su interés baja de precio o iguala su récord histórico más bajo.
3. **Pestaña de Ofertas y Chollos en el Radar:** `/radar` ofrece un conmutador de pestañas entre "Ofertas & Mínimos Históricos" y "Sorteos Comunitarios", con filtrado territorial por país y enlaces directos con atribución de afiliados limpia.
4. **Alimentación Dual de Precios:**
   - **Pasiva:** Cada consulta en vivo de stock en `StoreOffersCard.razor` registra una lectura sin coste adicional.
   - **Activa/Desatendida:** El worker `PriceRadarHostedService` escanea periódicamente el stock y precio en vivo de los títulos más demandados en listas de compra.
5. **Anti-Saturación y Resiliencia:** Filtro de cooldown de 6 horas que evita duplicar registros consecutivos idénticos para el mismo juego, tienda y precio.

---

## 2. Modelo de Dominio (`Ludeka.Core`)

### 2.1. Entidad `GamePriceSnapshot`
Representa una lectura de precio puntual verificada para un juego en una tienda específica:
- `Id`: `Guid` identificador único.
- `GameId`: `Guid` del juego asociado.
- `StoreName`: Nombre normalizado de la tienda colaboradora.
- `AffiliateUrl`: Enlace directo o de afiliado verificado.
- `Price`: `decimal` (invariante: debe ser estrictamente positivo).
- `Currency`: Divisa (`€`).
- `InStock`: Booleano que refleja disponibilidad inmediata para compra.
- `Country`: País de operación de la tienda.
- `RecordedAtUtc`: Marca de tiempo UTC inmutable de la lectura.

### 2.2. Value Object `GamePriceMetrics`
Calculado de forma determinista a partir de la colección de instantáneas históricas de un juego:
- `AllTimeLowPrice` / `AllTimeLowStore` / `AllTimeLowDateUtc`: Mínimo histórico registrado.
- `LowestCurrentOfferPrice` / `LowestCurrentOfferStore` / `LowestCurrentOfferUrl`: Oferta actual más barata disponible en stock.
- `HighestHistoricalPrice`: Precio máximo histórico para ponderar la escala de descuento.
- `CurrentDiscountPercentage`: Porcentaje de bajada relativo respecto al precio de referencia histórico (`((Max - Actual) / Max) * 100`).
- `IsCurrentAllTimeLow`: `true` si la mejor oferta actual iguala o mejora el mínimo histórico registrado.

### 2.3. Value Object `PriceDropAlert`
Representa una oportunidad o aviso emitido para el usuario o para el radar público:
- Relación juego, tienda, precio actual, precio de referencia, porcentaje de descuento y condición de mínimo histórico.

---

## 3. Capa de Persistencia e Infraestructura (`Ludeka.Infrastructure`)

### 3.1. Repositorio `IGamePriceRepository` / `SqliteGamePriceRepository`
- `AddSnapshotAsync(GamePriceSnapshot snapshot, CancellationToken ct)`: Inserta una lectura siempre que supere el filtro anti-saturación de 6 horas para la tupla `(GameId, StoreName, Price)`.
- `GetSnapshotsByGameIdAsync(Guid gameId, int limit, CancellationToken ct)`: Recupera las lecturas más recientes para el histórico.
- `GetAllSnapshotsByGameIdAsync(Guid gameId, CancellationToken ct)`: Obtiene la serie completa para el cálculo de métricas.
- `GetRecentSnapshotsAsync(DateTimeOffset sinceUtc, string? country, CancellationToken ct)`: Lecturas en ventana temporal para detección de ofertas en el catálogo.

### 3.2. Mapeo y Migración Dual
- **SQLite:** Mapeado en `LudekaDbContext` con índices sobre `(GameId, RecordedAtUtc)` y `(StoreName, Price)`. Auto-creación de tabla e índices defensivos en `SqliteSchemaMigrator`.
- **PostgreSQL / Supabase:** Compatible con el esquema DDL canónico de producción.

---

## 4. Capa de Aplicación (`Ludeka.Application`)

### 4.1. Servicio `PriceRadarService`
Implementa la interfaz `IPriceRadarService`:
- `GetTopDiscountsAsync(limit, country, ct)`: Agrupa precios recientes por juego, computa métricas históricas y devuelve las ofertas con descuento significativo (≥ 10%) o mínimo histórico.
- `GetUserWantToBuyAlertsAsync(userId, ct)`: Consulta los juegos en lista `WantToBuy` del usuario y devuelve las alertas vigentes asociadas a sus títulos.
- `GetGamePriceMetricsAsync(gameId, ct)`: Devuelve las métricas consolidadas del juego.
- `RecordPriceObservationAsync(...)`: Registra la observación con validación de parámetros.
- `ScanWantToBuyPricesAsync(maxGames, ct)`: Barrido en vivo consultando `IStoreStockService`.

### 4.2. Opciones de Configuración `PriceRadarOptions`
- `MinDiscountPercentage`: 10% por defecto.
- `SnapshotCooldownHours`: 6 horas.
- `WorkerIntervalHours`: 12 horas.
- `BatchScanSize`: 25 juegos por ciclo.

---

## 5. Background Worker (`PriceRadarHostedService`)

Servicio en segundo plano (`Microsoft.Extensions.Hosting.BackgroundService`):
- Se ejecuta cada 12 horas (configurable) con un retraso inicial de arranque de 2 minutos para no competir con el semillado ni con el inicio de la aplicación.
- Orquesta barridos desatendidos usando scopes independientes de DI para no retener conexiones de base de datos.
- Resiliencia total ante caídas de red o timeouts en tiendas colaboradoras.

---

## 6. Componentes de Interfaz Blazor (`Ludeka.Web`)

1. **`StoreOffersCard.razor`:**
   - Badge de "Mínimo histórico: XX,XX €" en la cabecera.
   - Badge de "Mínimo histórico" en cada tarjeta de oferta que iguale el mínimo.
   - Registro pasivo de lecturas de precio tras el refresco en vivo de stock.
2. **`MyLibrary.razor` (Pestaña 'Comprar'):**
   - Banner destacado cuando se detectan ofertas activas en la lista de compra del usuario.
   - Badge de bajada porcentual o mínimo histórico sobre la carátula del juego.
   - Tarjeta con desglose de precio de oferta y tienda colaboradora.
3. **`Radar.razor`:**
   - Conmutador accesible de pestañas: "Ofertas & Mínimos Históricos" y "Sorteos Comunitarios".
   - Filtros de "Todas las Ofertas" vs "Solo Mínimos Históricos".
   - Filtrado territorial por país (Global, Local, Otros países).
   - Enlace directo con atribución transparente de afiliados y botón a la ficha completa.

---

## 7. Pruebas y Verificación

- **Pruebas de Dominio:** `GamePriceMetricsTests.cs` (6 tests unitarios con verificación de invariantes, casos borde con único precio y descuentos extremos).
- **Pruebas de Persistencia:** `SqliteGamePriceRepositoryTests.cs` (5 tests de integración SQLite con cooldown de 6 horas, ordenación y filtrado territorial).
- **Pruebas de Aplicación:** `PriceRadarServiceTests.cs` (3 tests unitarios con mocks de repositorios y simulación de alertas de compra).
- **Pruebas de Contrato de Marcado:** `WebMarkupContractTests.cs` (3 tests verificando ausencia de emojis y presencia de identificadores canónicos en Razor).
- **Total verificado de la suite:** 1.005 pruebas en verde al 100%.
