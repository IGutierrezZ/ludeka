# Lista de Tareas: INC-87 — Optimización de Consultas SQL, Paginación Quirúrgica y Caché en Memoria

> **ID del Cambio:** `change-87-opt-consultas-egress-cache`  
> **Incremento Asociado:** INC-87  
> **Estado:** ✅ Tareas completadas  

---

## Tareas de Implementación

### Fase 1: Contratos de Dominio y Repositorio
- [x] 1.1 Crear DTO `GameFilterIndexItem` en `src/Ludeka.Application/DTOs/GameFilterIndexItem.cs` con propiedades mínimas de filtrado e indexación.
- [x] 1.2 Extender la interfaz `IGameRepository` en `src/Ludeka.Application/Contracts/IGameRepository.cs` con los métodos especializados:
  - `GetOfferCountsByStoreAsync(CancellationToken ct = default)`
  - `GetGameCountsByPublisherAsync(CancellationToken ct = default)`
  - `GetGamesWithStoreOffersAsync(string storeName, CancellationToken ct = default)`
  - `GetGamesWithPurchaseLinksAsync(int? limit = null, CancellationToken ct = default)`

### Fase 2: Implementación de Consultas Quirúrgicas en `SqliteGameRepository`
- [x] 2.1 Implementar `GetOfferCountsByStoreAsync` en `SqliteGameRepository.cs` proyectando exclusivamente `PurchaseLinks`.
- [x] 2.2 Implementar `GetGameCountsByPublisherAsync` en `SqliteGameRepository.cs` proyectando exclusivamente campos de editorial.
- [x] 2.3 Implementar `GetGamesWithStoreOffersAsync` en `SqliteGameRepository.cs` filtrando en base de datos por ofertas de la tienda solicitada.
- [x] 2.4 Implementar `GetGamesWithPurchaseLinksAsync` en `SqliteGameRepository.cs` filtrando en base de datos juegos con enlaces de compra.
- [x] 2.5 Refactorizar `SearchAsync` en `SqliteGameRepository.cs` para implementar la paginación en dos fases cuando `hasInMemoryFilters == true` (proyección ligera `GameFilterIndexItem` ➔ filtrado/ordenación ➔ paginación de IDs ➔ materialización exclusiva de `pageSize` juegos por ID).

### Fase 3: Optimización en `SqliteGiveawayRepository`
- [x] 3.1 Refactorizar `SqliteGiveawayRepository.GetGiveawaysAsync` para filtrar `DeadlineAt >= now` y ordenar directamente en el `IQueryable` de EF Core cuando `includeExpired == false`.

### Fase 4: Refactorización de Servicios de Negocio (Erradicación de `GetAllGamesAsync`)
- [x] 4.1 Modificar `StoreService.cs` para utilizar `GetOfferCountsByStoreAsync` en `GetAllAsync` y `GetGamesWithStoreOffersAsync` en `GetBySlugAsync`.
- [x] 4.2 Modificar `PublisherService.cs` para utilizar `GetGameCountsByPublisherAsync` en `GetAllAsync`.
- [x] 4.3 Modificar `PriceRadarService.cs` para utilizar `GetGamesWithPurchaseLinksAsync` en `GetTopDiscountsAsync` y `ScanWantToBuyPricesAsync`.

### Fase 5: Capa de Caché en Memoria (`IMemoryCache`)
- [x] 5.1 Implementar `CachedStoreService` en `src/Ludeka.Application/Features/Directory/CachedStoreService.cs` decorando `IStoreService` con TTL de 30 min e invalidación.
- [x] 5.2 Implementar `CachedPublisherService` en `src/Ludeka.Application/Features/Directory/CachedPublisherService.cs` decorando `IPublisherService` con TTL de 30 min e invalidación.
- [x] 5.3 Incorporar caché en `PriceRadarService.GetTopDiscountsAsync` con TTL de 15 min.
- [x] 5.4 Incorporar caché en `GiveawayService.GetGiveawaysAsync` con TTL de 5 min.
- [x] 5.5 Registrar los decoradores en `LudekaServiceCollectionExtensions.cs`.

### Fase 6: Pruebas Unitarias y de Integración (TDD)
- [x] 6.1 Crear pruebas unitarias para las nuevas consultas de `SqliteGameRepository` en `tests/Ludeka.UnitTests/Infrastructure/SqliteGameRepositoryOptimizationTests.cs`.
- [x] 6.2 Crear pruebas unitarias para `CachedStoreService` y `CachedPublisherService` en `tests/Ludeka.UnitTests/Application/CachedDirectoryServicesTests.cs`.
- [x] 6.3 Crear pruebas unitarias para la paginación en dos fases de `SearchAsync` garantizando que el total devuelto y los filtros de jugadores/complejidad son correctos.
- [x] 6.4 Ejecutar la suite completa de pruebas unitarias (`dotnet test Ludeka.sln --configuration Release`) y asegurar 100% verde sin regresiones.
