# 46. Optimización de Consultas SQL, Paginación Quirúrgica en Dos Fases y Caché en Memoria L1

> **ID del Módulo:** `46-optimizacion-consultas-egress-cache`  
> **Incremento Asociado:** INC-88 (`change-88-opt-consultas-egress-cache`) e INC-89 (`change-89-opt-consultas-egress-fase2`)  
> **Estado:** ✅ Implementado y Verificado  
> **Pruebas Automatizadas:** 2.155 pruebas unitarias + 10 de integración (2.165 pruebas totales verificadas al 100%)

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
6. `SqliteGameRepository.GetByPublisherAsync` descargando la tabla `Games` completa incondicionalmente para filtrar editoriales en cliente en cada visita a `/editoriales/{slug}` (INC-89).
7. `SqliteGameRepository.GetGamesPendingQualityBackfillAsync` y su recuento descargando la tabla completa para comprobar escalabilidad (INC-89).
8. `SqliteFoundingVerdictRepository.GetAllAsync` ordenando en cliente en lugar de delegar en el motor PostgreSQL (INC-89).

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
- `GetByPublisherAsync` (INC-89): Filtrado directo en SQL con `EF.Functions.Like(g.Publisher, ...)` y `EF.Functions.Like(g.SpanishPublisher, ...)`, y proyección de IDs para `RegionalPublishers`, eliminando la descarga de toda la tabla `Games`.
- `GetGamesPendingQualityBackfillAsync` (INC-89): Proyección acotada de `{ Id, Scalability }` para filtrar en memoria únicamente los IDs que carecen de escalabilidad y materializar con `Take(limit)` sólo los juegos finales.

### D3: Optimización de Consultas en `SqliteGiveawayRepository` y `SqliteFoundingVerdictRepository`
- `GetGiveawaysAsync`: En PostgreSQL (producción) empuja la cláusula `Where(g => g.DeadlineAt >= DateTimeOffset.UtcNow)` y `OrderBy` directamente al motor SQL, evitando la transferencia de sorteos históricos caducados. En SQLite mantiene la compatibilidad de tipos `DateTimeOffset`.
- `SqliteFoundingVerdictRepository.GetAllAsync` (INC-89): En PostgreSQL delega la ordenación cronológica descendente en SQL (`OrderByDescending(v => v.CreatedAt)`).

### D4: Capa de Caché L1 en Memoria (`IMemoryCache`)
Siguiendo el patrón existente de `CachedCatalogService` y `CachedHomeDashboardService`:
- **`CachedStoreService`:** Decorador de `IStoreService` con TTL de 30 min e invalidación reactiva por versión ante `CreateAsync`, `UpdateAsync` o `DeleteAsync`.
- **`CachedPublisherService`:** Decorador de `IPublisherService` con TTL de 30 min e invalidación reactiva ante mutaciones, extendido en INC-89 para cachear `GetBySlugAsync` y `GetByIdAsync`.
- **`PriceRadarService`:** Caché en `GetTopDiscountsAsync` con clave por país y límite y TTL de 15 min.
- **`GiveawayService`:** Caché en `GetGiveawaysAsync` con clave por país y estado de expiración y TTL de 5 min con invalidación automática ante altas o promociones.

---

## 3. Impacto y Ahorro Estimado

| Flujo / Endpoint | Comportamiento Anterior | Comportamiento INC-88 e INC-89 | Reducción de Egress |
| :--- | :--- | :--- | :--- |
| **`/tiendas` (Listado)** | Descarga 100% de `Games` completos | Proyección exclusiva de `PurchaseLinks` + Caché 30m | **>99%** |
| **`/tiendas/{slug}` (Ficha)** | Descarga 100% de `Games` completos | Descarga solo juegos de esa tienda + Caché 30m | **>95%** |
| **`/editoriales` (Listado)** | Descarga 100% de `Games` completos | Proyección exclusiva de editoriales + Caché 30m | **>99%** |
| **`/editoriales/{slug}` (Ficha)** | Descarga 100% de `Games` completos | Filtro SQL nativo de editorial + Caché 30m L1 (INC-89) | **>98%** |
| **Backfill de Calidad** | Descarga 100% de `Games` completos | Proyección acotada `{Id, Scalability}` + `Take(limit)` (INC-89) | **>95%** |
| **`/radar` (Top Chollos)** | Descarga 100% de `Games` completos | Solo juegos con links + Caché 15m | **>90%** |
| **Catálogo con Filtros** | Descarga miles de `Games` completos | 2 fases: Índice ligero ➔ 24 juegos finales | **>95%** |
| **`/sorteos` (Radar)** | Descarga sorteos históricos caducados | Filtro SQL `DeadlineAt >= now` + Caché 5m | **>80%** |
