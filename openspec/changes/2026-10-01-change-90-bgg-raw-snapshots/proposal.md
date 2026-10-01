# Propuesta de Cambio: Tabla Satélite de Snapshots Crudos BGG, Ingesta Automática y Poblado Retroactivo con Respeto de Límites

> **ID del Cambio:** `change-90-bgg-raw-snapshots`  
> **Incremento Asociado:** INC-90  
> **Rama de Trabajo:** `inc/bgg-raw-snapshots`  
> **Fecha:** 2026-10-01  
> **Estado:** ⏳ Propuesta en revisión  
> **Autor:** Mesa Técnica Ludeka  

---

## 1. Motivación y Diagnóstico

Actualmente, cuando Ludeka consulta la API XML2 de BoardGameGeek (`/xmlapi2/thing`), los datos se parsean en memoria y se mapean exclusivamente a los campos de la entidad de dominio `Game` ([`BggXmlParser.cs`](file:///f:/repos/Ludeka/src/Ludeka.Infrastructure/Bgg/BggXmlParser.cs)). Todo lo no contemplado en el modelo actual (artistas, mecánicas/categorías crudas, descripciones extendidas, versiones, votos desglosados de encuestas) se descarta inmediatamente al finalizar la petición HTTP.

### Problemas identificados:
1. **Pérdida irreversible de datos crudos:** Si en el futuro se refina una heurística determinista (p. ej. inferencia de estilo, huella en mesa o nuevas categorías) o se decide incorporar nuevos atributos (artistas/ilustradores, familias, dificultad/weight), es imposible recalcularlos localmente; se requeriría volver a consultar la API de BGG para miles de títulos.
2. **Restricciones de la API de BGG:** La API de BGG impone límites estrictos de concurrencia y cortesía (máximo 2 req/s) y sufre latencias elevadas y errores 504 frecuentes. Re-consultar el catálogo completo de BGG es lento y frágil.
3. **Aislamiento arquitectónico necesario:** Guardar el payload crudo directamente dentro de la tabla caliente `Games` provocaría una degradación severa de rendimiento por inflado de registros (blobs de 20-100 KB) y violaría los límites de Clean Architecture / DDD al acoplar el modelo canónico a la estructura externa de un tercero.

---

## 2. Alcance Propuesto

### A. Entidad de Dominio y Persistencia Satélite (`BggRawSnapshot`)
1. **Nueva entidad satélite `BggRawSnapshot`:**
   - `int BggId`: Clave primaria e identificador canónico de BGG.
   - `string RawJson`: Representación JSON estructurada y fiel del payload completo devuelto por BGG (mapeado a `jsonb` en PostgreSQL y `TEXT` en SQLite).
   - `int ApiVersion`: Versión del formato de API (ej. `2` para XMLAPI2).
   - `DateTimeOffset FetchedAtUtc`: Fecha y hora de captura del snapshot.
   - `DateTimeOffset? UpdatedAtUtc`: Fecha y hora de la última actualización.
2. **Repositorio `IBggRawSnapshotRepository`:**
   - Métodos para `GetByBggIdAsync`, `UpsertAsync`, `GetMissingBggIdsAsync(limit)` y `GetMetricsAsync()`.
   - Mapeo EF Core en [`LudekaDbContext.cs`](file:///f:/repos/Ludeka/src/Ludeka.Infrastructure/Data/LudekaDbContext.cs) con tabla satélite `BggRawSnapshots`, índices por `BggId` y soporte dual SQLite / PostgreSQL.

### B. Auto-Captura en el Pipeline de BGG
1. Modificar [`BggXmlApiClient`](file:///f:/repos/Ludeka/src/Ludeka.Infrastructure/Bgg/BggXmlApiClient.cs) y los servicios de ingesta ([`BggCatalogQueueService`](file:///f:/repos/Ludeka/src/Ludeka.Application/Features/Bgg/BggCatalogQueueService.cs) y [`BggMassIngestionService`](file:///f:/repos/Ludeka/src/Ludeka.Application/Features/Bgg/BggMassIngestionService.cs)) para que, cada vez que se obtenga con éxito un juego desde BGG, se registre o actualice de forma transparente su `BggRawSnapshot` en la tabla satélite.

### C. Servicio de Sincronización y Poblado Retroactivo (Backfill)
1. **`IBggRawSnapshotSyncService`:**
   - Identifica títulos existentes en `Games` que carecen de snapshot en `BggRawSnapshots`.
   - Ejecuta la descarga por lotes con retraso configurable (*rate-limiting* defensivo, ej. 1.200 ms entre llamadas) para respetar los servidores de BGG.
   - Manejo robusto de errores, reintentos y soporte de cancelación cooperativa (`CancellationToken`).
2. **Panel y Botón en Administración:**
   - En la sección administrativa de catálogo/cola ([`/admin/cola-catalogacion`](file:///f:/repos/Ludeka/src/Ludeka.Web/Components/Pages/Admin/CatalogQueueAdmin.razor)), añadir una tarjeta de control de "Snapshots Crudos de BGG":
     - Muestra métricas: Total juegos en catálogo, snapshots almacenados y faltantes.
     - Botón interactivo para iniciar/pausar la sincronización por lotes con barra de progreso y telemetría en tiempo real.
     - Exige sesión y permiso de moderación `CanEditGames` (INC-46).
3. **Runner Desatendido (Ludeka.Jobs):**
   - Incorporar un comando en `Ludeka.Jobs` para poder ejecutar el poblado retroactivo como Cloud Run Job en segundo plano.

---

## 3. Criterios de Aceptación y Pruebas (TDD)

1. La tabla `BggRawSnapshots` persiste el JSON estructurado completo de BGG sin alterar ni sobrecargar las columnas de `Games`.
2. Las nuevas importaciones de juegos guardan automáticamente su snapshot crudo.
3. El proceso de sincronización retroactiva respeta escrupulosamente los intervalos de espera (*rate limiting*) entre llamadas a BGG.
4. El botón administrativo muestra el progreso en vivo y requiere permisos de moderación válidos.
5. El 100% de la suite de pruebas unitarias existente se mantiene en verde y se añaden pruebas unitarias y de integración para la persistencia del snapshot y el servicio de sincronización.
