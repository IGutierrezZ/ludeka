# Propuesta de Cambio: Optimización de Consultas SQL, Paginación Quirúrgica y Caché en Memoria para Erradicación del Egress Excesivo en Base de Datos

> **ID del Cambio:** `change-87-opt-consultas-egress-cache`  
> **Incremento Asociado:** INC-87  
> **Rama de Trabajo:** `inc/opt-consultas-egress-cache`  
> **Fecha:** 2026-09-30  
> **Estado:** ⏳ Propuesta en revisión  
> **Autor:** Mesa Técnica Ludeka  

---

## 1. Motivación y Problema del Negocio

Supabase ha notificado la superación de la cuota mensual de ancho de banda saliente (**Egress Bandwidth > 5.5 GB**) en la organización de producción de Ludeka, con riesgo de aplicación de restricciones a partir del 30 de octubre.

Tras el análisis arquitectónico del código y la infraestructura, se han descartado los falsos positivos habituales (Ludeka no utiliza Supabase Storage —los medios se gestionan en Cloudflare R2—, no utiliza WebSockets de Supabase Realtime y los health checks no generan tráfico de datos apreciable).

La causa raíz radica en un **desfase de diseño de persistencia**:
1. Múltiples repositorios y servicios fueron programados asumiendo un motor SQLite local en disco, donde invocar `await query.ToListAsync()` sobre tablas completas para filtrar o contar en memoria RAM tenía coste de red cero.
2. Al desplegar contra PostgreSQL en Supabase (red externa a Google Cloud Run), cada llamada a métodos como `_gameRepository.GetAllGamesAsync(ct)` descarga la tabla `Games` completa con todos sus textos largos, resúmenes IA y colecciones JSON serializadas.
3. Este volcado masivo se ejecuta reiteradamente en:
   - Directorio de Tiendas (`StoreService.GetAllAsync` y `GetBySlugAsync`) para contar ofertas de tiendas.
   - Directorio de Editoriales (`PublisherService.GetAllAsync`) para contar juegos por editorial.
   - Radar de Precios (`PriceRadarService.GetTopDiscountsAsync`) y en el Cloud Run Job `price-radar` (`ScanWantToBuyPricesAsync`, cada 6 horas).
   - Catálogo con filtros avanzados (`SqliteGameRepository.SearchAsync`): al filtrar por número de jugadores, modo parejas o complejidad, se desactiva la paginación SQL nativa y se vuelcan a memoria todos los juegos candidatos.
   - Sorteos (`SqliteGiveawayRepository.GetGiveawaysAsync`): se descarga la tabla entera para evaluar `!g.IsExpired` en memoria en cada miss de la portada.

---

## 2. Alcance Propuesto

### A. Consultas Quirúrgicas y Proyecciones Ligeras en Repositorios
1. **Erradicación de `GetAllGamesAsync` en Servicios de Directorio y Radar:**
   - En `IGameRepository` y `SqliteGameRepository`:
     - Implementar `GetGamesOfferCountsByStoreAsync`: proyección agregada o selectiva exclusivamente de `PurchaseLinks` para resolver los conteos de ofertas por tienda sin transferir entidades `Game` completas.
     - Implementar `GetGamesCountByPublisherAsync`: proyección ligera de editoriales (`Publisher`, `SpanishPublisher`, `RegionalPublishers`) para computar el inventario de juegos por editorial.
     - Implementar `GetGamesWithOffersAsync(int? limit = null)`: consulta que filtre en base de datos únicamente los juegos que contienen enlaces de compra (`PurchaseLinks`), limitando drásticamente el volumen procesado por el Radar y por el worker `price-radar`.
2. **Paginación Quirúrgica en Dos Fases para Filtros Complejos en `SearchAsync`:**
   - Cuando se activen filtros de escalabilidad o complejidad cognitiva que requieran evaluación de estructuras JSON:
     - Fase 1 (Filtrado ligero): Proyectar únicamente los campos mínimos de filtrado e indexación (`Id`, `Scalability`, `Duration`, `Age`, `Style`, `BggRank`, `BggRating`).
     - Fase 2 (Resolución de página): Filtrar, ordenar y aplicar `.Skip().Take()` sobre la estructura ligera para aislar únicamente los ~24 IDs de la página solicitada.
     - Fase 3 (Materialización acotada): Cargar exclusivamente las 24 entidades completas mediante `Where(g => pagedIds.Contains(g.Id))`.
   - Reducción esperada del payload de red en búsquedas avanzadas: **>95%**.
3. **Sorteos Vigentes Directos en SQL:**
   - En `SqliteGiveawayRepository.GetGiveawaysAsync`: filtrar por `DeadlineAt >= DateTimeOffset.UtcNow` directamente en el `IQueryable` de EF Core cuando `includeExpired == false`.

### B. Capa de Caché en Memoria (`IMemoryCache`)
1. **Caché para Directorios (`StoreService` y `PublisherService`):**
   - Incorporar políticas de caché con TTL de 30 minutos para los listados agregados de tiendas y editoriales, con invalidación ante altas o ediciones administrativas.
2. **Caché para Radar de Precios (`PriceRadarService.GetTopDiscountsAsync`):**
   - Cachear las alertas de chollos por país con un TTL de 15 minutos.
3. **Caché para Sorteos Activos (`GiveawayService.GetGiveawaysAsync`):**
   - Cachear el conjunto de sorteos vigentes con un TTL de 5 minutos para blindar la portada (`HomeDashboardService`) ante ráfagas de visitas.

---

## 3. Criterios de Aceptación (TDD)

1. `StoreService.GetAllAsync` y `PublisherService.GetAllAsync` no invocan `GetAllGamesAsync` ni transfieren descripciones ni campos pesados no requeridos para los conteos.
2. `SqliteGameRepository.SearchAsync` con filtros de comensales o complejidad solo materializa entidades completas para el tamaño de página solicitado (`pageSize`), tras resolver los IDs mediante proyección ligera.
3. `SqliteGiveawayRepository.GetGiveawaysAsync(includeExpired: false)` delega la condición de vigencia temporal al motor SQL.
4. Las llamadas repetidas a los directorios de tiendas y editoriales se resuelven en memoria vía `IMemoryCache` sin consultas adicionales a la base de datos mientras la entrada sea válida.
5. El job `price-radar` y `PriceRadarService` solo procesan juegos con enlaces de compra existentes.
6. La suite completa de pruebas automáticas pasa al 100% sin regresiones.
