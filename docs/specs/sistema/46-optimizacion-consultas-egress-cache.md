# 46. Optimización de Consultas SQL, Paginación Quirúrgica en Dos Fases y Caché en Memoria L1

> **ID del Módulo:** `46-optimizacion-consultas-egress-cache`  
> **Incremento Asociado:** INC-87 (`change-87-opt-consultas-egress-cache`)  
> **Estado:** ✅ Implementado y Verificado  
> **Pruebas Automatizadas:** 2.134 pruebas unitarias + 10 de integración (2.144 pruebas totales verificadas al 100%)

---

## 1. Motivación y Diagnóstico de Egress

Tras la notificación de consumo de ancho de banda de salida (*Egress Bandwidth* > 5.5 GB/mes) en Supabase (PostgreSQL) por la política de uso justo (*Fair Use Policy*), se realizó una auditoría forense descartando causas fantasma:
- **Supabase Storage:** Descartado. Ludeka sirve todos sus assets desde Cloudflare R2 (`r2.dev`) con zero egress.
- **Supabase Realtime:** Descartado. Ludeka emplea SignalR nativo sobre Cloud Run.
- **Health Checks:** Descartado. El endpoint `/healthz` evalúa `Predicate = _ => false` sin realizar consultas de datos.

La causa raíz fue la materialización indiscriminada de entidades `Game` completas (con descripciones kilométricas, metadatos y resúmenes de IA) transferidas por internet desde Supabase hacia Cloud Run en:
1. `StoreService.GetAllAsync` / `GetBySlugAsync` / `UpdateAsync` (visitas y recuentos de ofertas por tienda).
2. `PublisherService.GetAllAsync` (recuentos de catálogo por editorial).
3. `PriceRadarService.GetTopDiscountsAsync` y `ScanWantToBuyPricesAsync` (escaneo de ofertas activas).
4. `SqliteGameRepository.SearchAsync` cuando se activaban filtros en memoria (`EspecialParejas`, `PlayerCounts`, `Complexities`).
5. `SqliteGiveawayRepository.GetGiveawaysAsync` descargando la tabla completa para filtrar expirados en cliente.

---

## 2. Decisiones de Arquitectura e Implementación

### D1: Paginación en Dos Fases en Catálogo (`SearchAsync`)
Para mantener el soporte dual SQLite (local) y PostgreSQL (producción) sin acoplarse a funciones nativas JSON no portables:
- **Fase 1 (Proyección Ligera):** Se proyecta únicamente el índice de facetas (`GameFilterIndexItem`: `Id`, `Scalability`, `Duration`, `Age`, `Style`, `BggRank`, `BggRating`), eliminando >95% del payload de red.
- **Filtro y Ordenación:** Se evalúan las condiciones complejas en memoria sobre este índice ligero y se aíslan los 24 IDs de la página solicitada.
- **Fase 2 (Hidratación Quirúrgica):** Se materializan exclusivamente las 24 entidades completas finales mediante `Where(g => pagedIds.Contains(g.Id))`, preservando el orden determinista.

### D2: Consultas Especializadas en `IGameRepository` y `SqliteGameRepository`
Se introdujeron contratos de baja transferencia:
- `GetOfferCountsByStoreAsync`: Proyecta únicamente `PurchaseLinks`, calculando el mapa de ofertas activas por tienda en memoria sin descargar el resto de la entidad.
- `GetGameCountsByPublisherAsync`: Proyecta únicamente `Publisher`, `SpanishPublisher` y `RegionalPublishers`, construyendo el mapa de recuentos sin operaciones `SQL APPLY` incompatibles con SQLite.
- `GetGamesWithStoreOffersAsync`: Filtra los IDs con ofertas de la tienda específica y materializa únicamente los juegos asociados.
- `GetGamesWithPurchaseLinksAsync`: Proyecta y pagina únicamente los juegos que realmente poseen ofertas comerciales con stock/enlaces de compra.

### D3: Optimización de Consultas en `SqliteGiveawayRepository`
- `GetGiveawaysAsync`: En PostgreSQL (producción) empuja la cláusula `Where(g => g.DeadlineAt >= DateTimeOffset.UtcNow)` y `OrderBy` directamente al motor SQL, evitando la transferencia de sorteos históricos caducados. En SQLite mantiene la compatibilidad de tipos `DateTimeOffset`.

### D4: Capa de Caché L1 en Memoria (`IMemoryCache`)
Siguiendo el patrón existente de `CachedCatalogService` y `CachedHomeDashboardService`:
- **`CachedStoreService`:** Decorador de `IStoreService` con TTL de 30 min e invalidación reactiva por versión ante `CreateAsync`, `UpdateAsync` o `DeleteAsync`.
- **`CachedPublisherService`:** Decorador de `IPublisherService` con TTL de 30 min e invalidación reactiva ante mutaciones.
- **`PriceRadarService`:** Caché en `GetTopDiscountsAsync` con clave por país y límite y TTL de 15 min.
- **`GiveawayService`:** Caché en `GetGiveawaysAsync` con clave por país y estado de expiración y TTL de 5 min con invalidación automática ante altas o promociones.

---

## 3. Impacto y Ahorro Estimado

| Flujo / Endpoint | Comportamiento Anterior | Comportamiento INC-87 | Reducción de Egress |
| :--- | :--- | :--- | :--- |
| **`/tiendas` (Listado)** | Descarga 100% de `Games` completos | Proyección exclusiva de `PurchaseLinks` + Caché 30m | **>99%** |
| **`/tiendas/{slug}` (Ficha)** | Descarga 100% de `Games` completos | Descarga solo juegos de esa tienda + Caché 30m | **>95%** |
| **`/editoriales` (Listado)** | Descarga 100% de `Games` completos | Proyección exclusiva de editoriales + Caché 30m | **>99%** |
| **`/radar` (Top Chollos)** | Descarga 100% de `Games` completos | Solo juegos con links + Caché 15m | **>90%** |
| **Catálogo con Filtros** | Descarga miles de `Games` completos | 2 fases: Índice ligero ➔ 24 juegos finales | **>95%** |
| **`/sorteos` (Radar)** | Descarga sorteos históricos caducados | Filtro SQL `DeadlineAt >= now` + Caché 5m | **>80%** |
