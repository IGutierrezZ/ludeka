# Especificación: caching-strategy (Estrategia de Caché en Memoria y Output Caching en ASP.NET Core 10)

## 1. Propósito y Contexto
Esta especificación define la arquitectura de caché en dos niveles para Ludeka:
- **Nivel 1 (Aplicación / En Memoria):** Decorador `CachedCatalogService` que implementa `ICatalogService` utilizando `IMemoryCache` para almacenar resultados de búsqueda, catálogos paginados y fichas individuales por slug, reduciendo la carga sobre SQLite en más de un 80%.
- **Nivel 2 (Pipeline HTTP / Output Caching):** Middleware `Output Caching` de ASP.NET Core 10 para almacenar en memoria el HTML resultante de rutas de lectura intensiva (`/`, `/catalogo`, `/radar`, `/transparencia`) con invalidación basada en etiquetas (*tags*).

---

## 2. Requerimientos de Comportamiento (RFC 2119)

### REQ-CACHE-01: Decorador `CachedCatalogService`
- La clase `CachedCatalogService` **DEBE** implementar `ICatalogService`.
- **DEBE** recibir por inyección de dependencias una instancia del `ICatalogService` interno y de `IMemoryCache`.
- En `GetCatalogAsync`:
  - **DEBE** generar una clave de caché determinista basada en los criterios de filtrado (`SearchTerm`, `MinPlayers`, `MaxPlayers`, `Style`, `IsSolo`, etc.), número de página y tamaño de página.
  - Si el resultado existe en caché, **DEBE** devolverlo inmediatamente sin invocar al repositorio subyacente (*Cache Hit*).
  - Si no existe, **DEBE** invocar al servicio interno, almacenar el resultado con una expiración deslizante de 10 minutos y expiración absoluta de 30 minutos (*Cache Miss*), y retornarlo.
- En `GetGameBySlugAsync`:
  - **DEBE** almacenar la ficha resultante indexada por la clave `game:slug:{slug.ToLowerInvariant()}`.
  - La expiración deslizante **DEBE** ser de 15 minutos.
- En `GetQuickSearchAsync`:
  - **DEBE** almacenar los resultados rápidos de autocompletado indexados por `quicksearch:{term.ToLowerInvariant()}:{limit}` con expiración de 5 minutos.

### REQ-CACHE-02: Mecanismo de Invalidación de Caché
- `CachedCatalogService` **DEBE** proporcionar un método de invalidación `Invalidate(string? slug = null)` que permita purgar entradas específicas de juego o el catálogo general cuando se produzcan mutaciones (seeding, nuevos veredictos, adición de juegos de BGG).

### REQ-CACHE-03: Configuración de Output Caching en ASP.NET Core 10
- En `Program.cs`, el sistema **DEBE** registrar:
  ```csharp
  builder.Services.AddOutputCache(options =>
  {
      options.AddPolicy("CatalogCache", p => p.Expire(TimeSpan.FromMinutes(10)).Tag("tag-catalog"));
      options.AddPolicy("RadarCache", p => p.Expire(TimeSpan.FromMinutes(5)).Tag("tag-radar"));
      options.AddPolicy("StaticPages", p => p.Expire(TimeSpan.FromHours(1)).Tag("tag-static"));
  });
  ```
- El pipeline HTTP **DEBE** invocar `app.UseOutputCache()`.

---

## 3. Criterios de Aceptación (Escenarios Gherkin)

### Escenario 1: Cache Hit en consulta de catálogo
```gherkin
Given una consulta de catálogo para la página 1 con criterio "Especial Parejas"
When se invoca GetCatalogAsync por primera vez
Then el servicio interno de catálogo es llamado 1 vez y el resultado se guarda en caché
When se invoca GetCatalogAsync por segunda vez con idénticos parámetros
Then el resultado se obtiene de IMemoryCache
And el servicio interno de catálogo no vuelve a ser invocado (llamadas totales = 1)
```

### Escenario 2: Cache Hit en ficha detallada por slug
```gherkin
Given un juego con slug "brass-birmingham"
When se invoca GetGameBySlugAsync("brass-birmingham")
Then se recupera la entidad y se almacena bajo la clave correspondiente
When se invoca GetGameBySlugAsync("brass-birmingham") inmediatamente después
Then el DTO devuelto es idéntico al anterior
And no se realiza ninguna consulta a la base de datos
```

### Escenario 3: Desalojo e invalidación programática
```gherkin
Given una ficha en caché para "ark-nova"
When se invoca Invalidate("ark-nova")
And se solicita nuevamente GetGameBySlugAsync("ark-nova")
Then se produce un Cache Miss y se vuelve a consultar el servicio interno
```
