# Propuesta de Cambio: Saneamiento Integral de Consultas SQL Restantes, Eliminación de Egress O(N) y Blindaje de Caché de Fichas Públicas

> **ID del Cambio:** `change-89-opt-consultas-egress-fase2`  
> **Incremento Asociado:** INC-89  
> **Rama de Trabajo:** `inc/opt-consultas-egress-fase2`  
> **Fecha:** 2026-10-01  
> **Estado:** ⏳ Propuesta en revisión  
> **Autor:** Mesa Técnica Ludeka  

---

## 1. Motivación y Diagnóstico Forense

Tras la entrega del incremento INC-88 (que optimizó el listado general del catálogo y agregó decoradores L1), la auditoría forense del sistema destapó varias consultas restantes con comportamiento $O(N)$ en el repositorio principal [`SqliteGameRepository.cs`](file:///f:/repos/Ludeka/src/Ludeka.Infrastructure/Data/SqliteGameRepository.cs) que continúan descargando tablas completas o colecciones JSON indiscriminadas a través de la red:

1. **`GetByPublisherAsync` (Línea 337):**  
   Ejecuta `await scope.Context.Games.AsNoTracking().ToListAsync(ct)` sin ninguna cláusula `WHERE` en SQL. Cada consulta a una ficha de editorial (`/editoriales/{slug}`) descarga **la totalidad de la tabla `Games`** (con sus títulos, sinopsis, resúmenes IA y colecciones) a la memoria de la aplicación para después ejecutar un `.Where()` en LINQ to Objects.
2. **`GetGamesPendingQualityBackfillAsync` (Línea 356):**  
   Materializa todos los juegos del catálogo mediante `ToListAsync()` ordenados por ranking para filtrar en RAM los títulos con escalabilidad vacía.
3. **`GetGamesWithStoreOffersAsync` (Línea 500) y `GetGamesWithPurchaseLinksAsync` (Línea 547):**  
   Aunque proyectan campos reducidos, descargan colecciones de enlaces de compra de todos los registros en memoria para comprobar la existencia de ofertas de una tienda.
4. **`SqliteFoundingVerdictRepository.GetAllAsync` (Línea 66):**  
   Descarga toda la tabla de veredictos fundadores para ordenar por `CreatedAt` en memoria sin discriminación por proveedor de base de datos.
5. **Caché L1 a nivel de Ficha de Editorial (`PublisherDetailDto`):**  
   `CachedPublisherService` implementa caché para el listado general `GetAllAsync()`, pero `GetBySlugAsync()` e `GetByIdAsync()` delegan directamente al servicio base, provocando que cada impacto a una editorial golpee la consulta no optimizada de base de datos.

---

## 2. Alcance Propuesto

### A. Filtrado SQL Nativo en `SqliteGameRepository`
1. **`GetByPublisherAsync` Quirúrgico:**  
   Empujar la condición de búsqueda a la cláusula `WHERE` de SQL utilizando `EF.Functions.Like` sobre `Publisher` y `SpanishPublisher`. Devolver exclusivamente los juegos que pertenecen a la editorial solicitada, limitando la transferencia de red de miles de entidades a unas pocas decenas por petición.
2. **`GetGamesPendingQualityBackfillAsync` y Conteo Acotado:**  
   Reemplazar la descarga masiva por una consulta paginada por cursor o proyecciones selectivas acotadas con `Take(limit)`.
3. **`GetGamesWithStoreOffersAsync` Acotado:**  
   Optimizar la resolución de juegos con ofertas por tienda para evitar exploraciones globales y acoplarlo a la capa de caché.

### B. Blindaje de Caché L1 en Fichas de Editorial (`CachedPublisherService`)
1. Extender `CachedPublisherService` para interceptar `GetBySlugAsync(slug)` y `GetByIdAsync(id)`, almacenando en memoria el `PublisherDetailDto` resultante durante 30 minutos con clave normalizada por slug/id.
2. Mantener la invalidación reactiva ante mutaciones (`CreateAsync`, `UpdateAsync`, `DeleteAsync`).

### C. Ordenación SQL en `SqliteFoundingVerdictRepository`
1. Si el proveedor activo es PostgreSQL (`db.Database.IsNpgsql()`), aplicar `OrderByDescending(v => v.CreatedAt)` directamente en la consulta SQL y limitar resultados. En SQLite, mantener la compatibilidad sin penalizar PostgreSQL.

---

## 3. Criterios de Aceptación y Pruebas (TDD)

1. `SqliteGameRepository.GetByPublisherAsync` genera una consulta SQL con cláusula `WHERE` sobre los campos de editorial y nunca ejecuta `ToListAsync()` sobre la tabla `Games` sin filtrar.
2. `CachedPublisherService.GetBySlugAsync` sirve peticiones sucesivas desde la memoria sin invocar al repositorio subyacente.
3. La suite de pruebas unitarias existente (2.161 pruebas) se mantiene en verde al 100% y se añaden pruebas de regresión y cobertura para las nuevas optimizaciones.
