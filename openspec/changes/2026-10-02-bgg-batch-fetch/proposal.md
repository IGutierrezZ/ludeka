# Propuesta de Cambio: Peticiones en Bloque Multi-ID a BGG (Batch Fetch) y Ampliación de Timeout en Runner de Snapshots Crudos

> **ID de Incremento:** INC-97  
> **Slug:** `bgg-batch-fetch`  
> **Rama:** `inc/bgg-batch-fetch`  
> **Fecha:** 2026-10-02  
> **Estado:** ⏳ Propuesta  

---

## 1. Motivación y Diagnóstico

En la implementación actual de volcado y sincronización de snapshots crudos de BoardGameGeek (`BggRawSnapshotSyncService`), las llamadas a la API XML2 (`/xmlapi2/thing?id={bggId}&stats=1`) se realizan de manera estrictamente secuencial y unitaria (1 petición HTTP por cada título con una pausa forzosa de ~1.200 ms). 

Para un catálogo de ~17.000 títulos, este patrón implica:
1. **Volumen desmesurado de llamadas HTTP:** ~17.000 peticiones de ida y vuelta a los servidores de BGG.
2. **Duración excesiva:** ~1,5 segundos por juego sumando la pausa y la latencia de red, lo que se traduce en **~7,1 horas continuas**.
3. **Corte por timeout del host de Jobs:** En `src/Ludeka.Jobs/Program.cs`, el timeout interno por defecto está fijado en 30 minutos (`Workers:JobTimeoutMinutes = 30`). Por este motivo, el job se detiene automáticamente tras procesar ~1.000 a 1.200 juegos en cada ejecución, obligando a relanzarlo repetidamente.

La API XML2 de BoardGameGeek admite formalmente la consulta de múltiples identificadores en una sola petición HTTP mediante una lista separada por comas:
```http
GET https://boardgamegeek.com/xmlapi2/thing?id=13,42,828,1542&stats=1
```
BGG devuelve un único documento raíz `<items>` conteniendo los elementos `<item id="...">` correspondientes a cada juego.

---

## 2. Solución Propuesta

### 2.1 Peticiones Multi-ID en `IBggClient` y `BggXmlApiClient`
- Incorporar en `IBggClient` el método:
  ```csharp
  Task<string?> FetchRawThingsXmlAsync(IEnumerable<int> bggIds, CancellationToken ct = default);
  ```
- Implementar en `BggXmlApiClient` la construcción de URL multi-ID acotada a bloques de hasta 20 identificadores (`id=id1,id2,id3...`), reutilizando el `TokenBucketRateLimiter`, reintentos en 202 (Accepted) y backoff ante 429.
- Implementar la versión simulada correspondiente en `SimulatedBggClient` para pruebas locales y suites de integración.

### 2.2 Sincronización en Bloques en `BggRawSnapshotSyncService`
- En `SyncBatchCoreAsync`, particionar los identificadores pendientes obtenidos de `_snapshotRepo.GetMissingBggIdsAsync(batchSize)` en bloques de 20 (`Chunk(20)`).
- Realizar una sola petición HTTP por bloque de 20 IDs.
- Iterar sobre la colección de `<item>` devueltos en el XML, extrayendo el ID individual, convirtiendo cada uno a su snapshot JSON estructurado mediante `BggXmlToJsonConverter`, realizando el `UpsertAsync` en repositorio y evaluando sus enlaces de expansiones (inbound/outbound).
- Mantener la pausa de cortesía de ~1.200 ms **entre bloques de 20 juegos** (en lugar de entre cada juego individual).
- **Rendimiento resultante:** ~20 juegos cada 1,5 segundos = ~800 juegos/minuto. Los 14.000 títulos restantes se completarán en **~17-20 minutos** en una sola ejecución.

### 2.3 Ampliación del Timeout por Defecto en `Ludeka.Jobs`
- En `src/Ludeka.Jobs/Program.cs`, elevar el valor por defecto de `Workers:JobTimeoutMinutes` de 30 a **120 minutos** (2 horas), permitiendo que trabajos intensivos de migración o backfill no se corten anticipadamente si el usuario no especifica variables adicionales en Cloud Run.

---

## 3. Criterios de Aceptación y Pruebas

1. **Unitarias de Cliente BGG:** Verificar que `FetchRawThingsXmlAsync` construye adecuadamente la URL con comas, descarta IDs inválidos o duplicados y parsea respuestas multi-item.
2. **Unitarias de Servicio de Sincronización:** Verificar que `SyncBatchCoreAsync` procesa bloques de 20 juegos con una sola llamada HTTP por bloque, guarda individualmente cada snapshot satélite y asocia correctamente expansiones.
3. **Regresión:** Suite completa de pruebas unitarias xUnit en verde (2.261+ pruebas).
