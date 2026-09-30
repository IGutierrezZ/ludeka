# Especificación Técnica: INC-87 — Optimización de Consultas SQL, Paginación Quirúrgica y Caché en Memoria para Reducción Drástica de Egress en Base de Datos

## 1. Resumen Ejecutivo

Este incremento erradica la fuga masiva de ancho de banda saliente (**Egress Bandwidth > 5.5 GB**) de Supabase PostgreSQL hacia Google Cloud Run eliminando la transferencia de tablas completas a través de internet. Se sustituyen los métodos heredados de materialización en memoria (`GetAllGamesAsync` y `ToListAsync` indiscriminados) por proyecciones ligeras, paginación en dos fases y una capa de caché en memoria (`IMemoryCache`) para peticiones de lectura intensivas.

---

## 2. Requerimientos Funcionales y de Dominio

### REQ-1: Erradicación de `GetAllGamesAsync` en Directorio de Tiendas (`StoreService`)
- **Proyección agregada de ofertas:** En `IGameRepository` y `SqliteGameRepository`, se implementa:
  `Task<IReadOnlyDictionary<string, int>> GetOfferCountsByStoreAsync(CancellationToken ct = default);`
  que proyecta únicamente los enlaces de compra existentes sin cargar entidades `Game` completas.
- **Detalle de tienda optimizado:** En `IGameRepository` y `SqliteGameRepository`, se implementa:
  `Task<IReadOnlyList<Game>> GetGamesWithStoreOffersAsync(string storeName, CancellationToken ct = default);`
  para que `/tiendas/{slug}` solo cargue los juegos que contienen ofertas de la tienda solicitada.
- **Desacoplo en `StoreService`:** `StoreService.GetAllAsync` y `StoreService.GetBySlugAsync` dejan de invocar `GetAllGamesAsync(ct)`.

### REQ-2: Proyecciones Ligeras en Directorio de Editoriales (`PublisherService`)
- **Proyección de inventario por editorial:** En `IGameRepository` y `SqliteGameRepository`, se implementa:
  `Task<IReadOnlyDictionary<string, int>> GetGameCountsByPublisherAsync(CancellationToken ct = default);`
  que proyecta exclusivamente los identificadores y nombres de editoriales (`Publisher`, `SpanishPublisher`, `RegionalPublishers`) para computar los conteos en memoria sin transferir descripciones, resúmenes IA ni colecciones de fundas.
- **Desacoplo en `PublisherService`:** `PublisherService.GetAllAsync` consume esta proyección ligera en lugar de `GetAllGamesAsync(ct)`.

### REQ-3: Filtrado en Base de Datos para Radar de Precios (`PriceRadarService`) y Worker
- **Consulta acotada a juegos con ofertas:** En `IGameRepository` y `SqliteGameRepository`, se implementa:
  `Task<IReadOnlyList<Game>> GetGamesWithPurchaseLinksAsync(int? limit = null, CancellationToken ct = default);`
  que aplica el filtro a nivel relacional en el `IQueryable` de EF Core:
  `Where(g => g.PurchaseLinks != null && g.PurchaseLinks.Count > 0)`.
- **Uso en Radar y Worker:** `PriceRadarService.GetTopDiscountsAsync` y `PriceRadarService.ScanWantToBuyPricesAsync` (ejecutado por el Cloud Run Job `price-radar`) consumen `GetGamesWithPurchaseLinksAsync` en lugar de `GetAllGamesAsync(ct)`, procesando únicamente los títulos con tiendas vinculadas.

### REQ-4: Paginación Quirúrgica en Dos Fases en Catálogo (`SearchAsync`)
- **Problema actual:** Cuando se aplican filtros avanzados (`EspecialParejas`, `PlayerCounts`, `Complexities`), `SqliteGameRepository.SearchAsync` anula la paginación SQL y ejecuta `query.ToListAsync(ct)`, descargando toda la tabla `Games` con todas sus columnas a RAM.
- **Solución en Dos Fases:**
  1. **Fase 1 (Proyección Ligera):** Proyectar un registro plano ligero (`GameFilterIndexItem`) conteniendo únicamente los campos necesarios para evaluar filtros y ordenación: `Id`, `Scalability`, `Duration`, `Age`, `Style`, `BggRank`, `BggRating`.
  2. **Fase 2 (Filtrado y Paginación en Memoria):** Aplicar los predicados en memoria sobre la lista ligera, ordenar y aislar los IDs de la página solicitada:
     `var pagedIds = filteredList.Skip((page - 1) * pageSize).Take(pageSize).Select(x => x.Id).ToList();`
  3. **Fase 3 (Materialización Acotada):** Cargar únicamente los ~24 juegos completos correspondientes a esos IDs:
     `scope.Context.Games.AsNoTracking().Where(g => pagedIds.Contains(g.Id)).ToListAsync(ct)`.
- **Garantía:** Nunca se transfieren más de `pageSize` entidades completas desde la base de datos por página.

### REQ-5: Filtro SQL Directo para Sorteos Vigentes en `SqliteGiveawayRepository`
- En `SqliteGiveawayRepository.GetGiveawaysAsync`:
  - Cuando `includeExpired == false`, aplicar la condición `Where(g => g.DeadlineAt >= DateTimeOffset.UtcNow)` en el `IQueryable` de EF Core.
  - Ordenar por `DeadlineAt` en base de datos.
  - Erradicar la descarga incondicional de toda la tabla para filtrar en RAM.

### REQ-6: Capa de Caché en Memoria (`IMemoryCache`)
- **Directorio de Tiendas:** `CachedStoreService` (decorador de `IStoreService`) con clave `directory:stores:all`, TTL de 30 minutos e invalidación ante mutaciones.
- **Directorio de Editoriales:** `CachedPublisherService` (decorador de `IPublisherService`) con clave `directory:publishers:all`, TTL de 30 minutos e invalidación ante mutaciones.
- **Radar de Precios:** Caché en `PriceRadarService.GetTopDiscountsAsync` con clave `radar:discounts:{country}:{limit}`, TTL de 15 minutos.
- **Sorteos Activos:** Caché en `GiveawayService.GetGiveawaysAsync` con clave `giveaways:active:{country}`, TTL de 5 minutos.

---

## 3. Criterios de Aceptación y Casos de Prueba (Gherkin)

### Escenario 1: Visita al Directorio de Tiendas sin Materialización Masiva
```gherkin
Given una base de datos con 5.000 juegos en catálogo y 50 tiendas
When se invoca StoreService.GetAllAsync
Then se devuelven las 50 tiendas con sus conteos correctos de ofertas
And no se invoca IGameRepository.GetAllGamesAsync
And las entidades Game completas no se transfieren por red
```

### Escenario 2: Visita al Directorio de Editoriales con Proyección Ligera
```gherkin
Given una base de datos con catálogo poblado y múltiples editoriales
When se invoca PublisherService.GetAllAsync
Then se obtienen las editoriales con su número exacto de títulos asociados
And el repositorio ejecuta una proyección Select de campos mínimos de editorial sin transferir descripciones ni resúmenes IA
```

### Escenario 3: Búsqueda en Catálogo con Filtro Complejo de Jugadores
```gherkin
Given un catálogo con 3.000 juegos
When se ejecuta IGameRepository.SearchAsync solicitando la página 1 de tamaño 24 con filtro de 2 jugadores (EspecialParejas = true)
Then la operación devuelve exactamente 24 entidades Game
And el total de juegos completos materializados desde la base de datos es menor o igual a 24
And el TotalCount reporta fielmente la cantidad total de juegos que cumplen el criterio
```

### Escenario 4: Escaneo Periódico de Price Radar
```gherkin
Given un catálogo con 4.000 juegos, de los cuales solo 80 tienen enlaces de compra (PurchaseLinks)
When se ejecuta PriceRadarService.ScanWantToBuyPricesAsync(maxGames: 20)
Then la consulta a base de datos solo recupera títulos con PurchaseLinks existentes
And no se descarga el catálogo completo
```

### Escenario 5: Caché en Memoria para Directorios
```gherkin
Given el servicio decorador CachedStoreService registrado en la composición de dependencias
When se invoca GetAllAsync dos veces consecutivas en un intervalo de 1 minuto
Then la segunda invocación se resuelve inmediatamente desde memoria RAM sin consultar la base de datos
```
