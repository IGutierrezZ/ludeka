# Incremento 88: Optimización de Consultas SQL, Paginación Quirúrgica y Caché en Memoria para Erradicación del Egress Excesivo en Base de Datos

> **ID:** INC-88  
> **Slug:** `opt-consultas-egress-cache`  
> **Rama:** `inc/opt-consultas-egress-cache`  
> **Estado:** ✅ Archivado  
> **Módulos Impactados:** Módulo 01 (`docs/specs/sistema/01-catalogo-base.md`), Módulo 11 (`docs/specs/sistema/11-enlaces-compra-afiliados.md`), Módulo 19 (`docs/specs/sistema/19-directorio-editoriales-creadores-tiendas.md`), Módulo 26 (`docs/specs/sistema/26-radar-precios-alertas.md`), Módulo 35 (`docs/specs/sistema/35-persistencia-produccion-y-medios-con-fallback.md`)  
> **Dependencias:** INC-85, INC-86 (planificado).

---

## 1. Contexto y Diagnóstico del Problema

1. **Alerta de superación de cuota de Supabase:**
   - La organización de producción de Ludeka ha superado el límite gratuito de ancho de banda saliente (**Egress Bandwidth > 5.5 GB**).
   - Aunque Cloudflare R2 gestiona los medios y la interactividad web usa SignalR propio, el tráfico entre Google Cloud Run (en Bélgica/europe-west1) y Supabase PostgreSQL (en AWS) computa como egress de internet en Supabase.
2. **Consultas masivas no paginadas en memoria (`GetAllGamesAsync`):**
   - Varios servicios invocan `GetAllGamesAsync(ct)` para computar conteos o filtrar:
     - `/tiendas` (`StoreService.GetAllAsync` y `GetBySlugAsync`) descarga el catálogo entero para contar ofertas.
     - `/editoriales` (`PublisherService.GetAllAsync`) descarga el catálogo entero para contar títulos por editorial.
     - `/radar` y el Cloud Run Job `price-radar` (`PriceRadarService.GetTopDiscountsAsync` y `ScanWantToBuyPricesAsync`) descargan el catálogo entero.
   - Cada entidad `Game` incluye textos largos, resúmenes IA y colecciones JSON serializadas; transferir miles de filas genera megabytes por consulta.
3. **Paginación rota en filtros avanzados (`SqliteGameRepository.SearchAsync`):**
   - Cuando se filtra por comensales, parejas o complejidad, el repositorio desactiva la paginación SQL (`hasInMemoryFilters = true`) y ejecuta `query.ToListAsync(ct)`, descargando todos los juegos a RAM antes de aplicar `Skip`/`Take`.
4. **Filtro de sorteos caducados en memoria:**
   - `SqliteGiveawayRepository.GetGiveawaysAsync` descarga toda la tabla de sorteos antes de evaluar `!g.IsExpired` en memoria.

---

## 2. Objetivos Técnicos del Incremento

1. **Erradicar `GetAllGamesAsync` de flujos habituales:**
   - Implementar métodos de proyección agregada (`GetGamesOfferCountsByStoreAsync`, `GetGamesCountByPublisherAsync`, `GetGamesWithOffersAsync`) en el repositorio para que solo viajen por red los datos estrictamente necesarios para los conteos.
2. **Paginación en dos fases para filtros complejos de catálogo:**
   - Proyectar únicamente campos mínimos para filtrado en memoria (`Id`, `Scalability`, `Duration`, `Age`, `Style`, `BggRank`, `BggRating`).
   - Paginar sobre la lista ligera para obtener los 24 IDs de la página.
   - Materializar únicamente las 24 entidades completas finales por ID.
3. **Filtro de expiración en SQL para sorteos:**
   - Traducir `DeadlineAt >= now` a condición `WHERE` en PostgreSQL/SQLite.
4. **Capa de Caché en Memoria (`IMemoryCache`):**
   - Incorporar caché con TTL de 30 min en directorios de tiendas y editoriales.
   - Incorporar caché con TTL de 15 min en el radar de precios.
   - Incorporar caché con TTL de 5 min en el listado de sorteos activos.
5. **Cobertura Integral con Pruebas (TDD):**
   - Garantizar que ninguna de las operaciones optimizadas materializa la tabla completa y que las políticas de expiración e invalidación de caché funcionan determinísticamente.

---

## 3. Criterios de Aceptación (TDD)

- **Criterio 1:** `StoreService.GetAllAsync` y `PublisherService.GetAllAsync` ejecutan consultas proyectadas sin transferir campos innecesarios de `Game`.
- **Criterio 2:** `SqliteGameRepository.SearchAsync` con filtros en memoria solo descarga entidades completas para el lote paginado (24 títulos).
- **Criterio 3:** `SqliteGiveawayRepository.GetGiveawaysAsync(includeExpired: false)` no carga sorteos vencidos en memoria.
- **Criterio 4:** Las peticiones sucesivas a directorios y radar se atienden desde `IMemoryCache` sin impacto en base de datos.
- **Criterio 5:** El conjunto completo de pruebas unitarias e integración se mantiene en verde al 100%.
