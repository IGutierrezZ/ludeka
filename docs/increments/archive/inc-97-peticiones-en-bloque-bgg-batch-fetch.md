# Incremento 97: Peticiones en Bloque Multi-ID a BGG (Batch Fetch) y Ampliación de Timeout en Runner de Snapshots Crudos

> **ID:** INC-97  
> **Slug:** `bgg-batch-fetch`  
> **Rama:** `inc/bgg-batch-fetch`  
> **Estado:** ✅ Archivado  
> **Módulos Impactados:** Módulo 05 (`docs/specs/sistema/05-integracion-bgg.md`), Módulo 34 (`docs/specs/sistema/34-trabajos-en-segundo-plano-cloud-run.md`), Módulo 47 (`docs/specs/sistema/47-snapshots-crudos-bgg-expansiones-sincronizacion.md`)  
> **Dependencias:** INC-90, INC-91.  

---

## 1. Contexto y Diagnóstico

En la implementación inicial del volcado masivo de snapshots crudos de BoardGameGeek (INC-90 e INC-91), las llamadas a la API XML2 (`/xmlapi2/thing?id={bggId}&stats=1`) se realizaban de manera secuencial y unitaria (1 petición HTTP por cada título con una pausa forzosa de ~1.200 ms).

Para un catálogo de ~17.000 títulos, este patrón presentaba dos limitaciones severas:
1. **Duración y volumen de red:** ~1,5 segundos por juego sumando la pausa y la latencia HTTP, lo que equivalía a **~7,1 horas continuas** y ~17.000 peticiones de ida y vuelta a los servidores de BGG.
2. **Corte sistemático por timeout:** En `src/Ludeka.Jobs/Program.cs`, el timeout interno por defecto estaba fijado en 30 minutos (`Workers:JobTimeoutMinutes = 30`). A los 30 minutos exactos, el proceso .NET cancelaba internamente su propio `CancellationTokenSource`, el bucle de lotes salía limpiamente y el contenedor se apagaba con código 0 habiendo procesado exactamente ~1.000-1.200 juegos por ejecución.

La API XML2 de BGG admite formalmente la consulta de múltiples identificadores en una sola petición HTTP mediante una lista separada por comas (`/xmlapi2/thing?id=13,42,828...&stats=1`), devolviendo un único documento raíz `<items>` con los `<item id="...">` correspondientes a cada juego.

---

## 2. Objetivos Técnicos Implementados

### 2.1 Método Multi-ID en `IBggClient` y `BggXmlApiClient`
- Incorporado a `IBggClient`:
  ```csharp
  Task<string?> FetchRawThingsXmlAsync(IEnumerable<int> bggIds, CancellationToken ct = default);
  ```
- Implementado en `BggXmlApiClient`:
  - Recibe `IEnumerable<int> bggIds`.
  - Normaliza: filtra `id > 0`, elimina duplicados y acota a bloques de hasta 20 identificadores (`Take(20)`).
  - Si no hay IDs válidos, devuelve `null`.
  - Construye la URL: `https://boardgamegeek.com/xmlapi2/thing?id={string.Join(",", ids)}&stats=1`.
  - Reutiliza el `TokenBucketRateLimiter` (máx. 2 req/s), sondeo de 202 (Accepted) y backoff ante 429 (`Retry-After`).
  - `FetchRawThingXmlAsync(int bggId)` se refactoriza para delegar de forma transparente en `FetchRawThingsXmlAsync([bggId])`.
- Implementado en `SimulatedBggClient` y `BggSimulationDataset` (`GetRawThingsXml`) para pruebas offline y suites de integración.

### 2.2 Sincronización en Bloques en `BggRawSnapshotSyncService`
- En `SyncBatchCoreAsync`:
  - Particiona la lista de IDs pendientes (`missingIds`) en fragmentos de 20 elementos (`.Chunk(20)`).
  - Realiza una sola petición HTTP por bloque de 20 IDs.
  - Parsea los elementos `<item>` presentes en el XML devuelto:
    - Extrae `bggId = int.Parse(item.Attribute("id")!.Value)`.
    - Serializa a JSON estructurado mediante `BggXmlToJsonConverter.ConvertToJson(item)`.
    - Realiza el `UpsertAsync` del snapshot en repositorio.
    - Evalúa y procesa enlaces de expansión (inbound a juego base y outbound a expansiones hijas).
  - Manejo de juegos retirados/privados en BGG: si algún ID solicitado no viene en el XML, se registra una advertencia y se genera un snapshot de control (`{"notFound":true}`) para evitar que el ID quede atascado en bucle continuo de consultas.
  - Aplica la pausa de cortesía de ~1.200 ms **una sola vez por bloque de 20 juegos** (en lugar de por juego individual).
  - **Rendimiento:** ~20 juegos cada 1,5 segundos = ~800 juegos/minuto. Los títulos restantes se completan en **~17-20 minutos** en una sola ejecución.

### 2.3 Ampliación del Timeout por Defecto en `Ludeka.Jobs`
- En `src/Ludeka.Jobs/Program.cs`:
  - Modificado el valor por defecto de `Workers:JobTimeoutMinutes` de 30 a **120 minutos** (2 horas), permitiendo que la ejecución desatendida en Cloud Run o consola tenga margen holgado para finalizar sin interrupción.

---

## 3. Verificación y Resultados

- **Pruebas Unitarias:** 2.264 pruebas ejecutadas y aprobadas al 100% (+3 pruebas respecto al inicio del incremento).
- **Cobertura Específica:**
  - `BggXmlApiClientResilienceTests.FetchRawThingsXmlAsync_ConstructsCommaSeparatedIdsUrl_AndReturnsXml`: Comprueba construcción de URL con comas, descarte de IDs inválidos y parseo de XML devuelto.
  - `BggXmlApiClientResilienceTests.FetchRawThingsXmlAsync_WhenIdsNullOrEmpty_ReturnsNull`: Comprueba manejo de nulos, listas vacías y números no positivos.
  - `BggRawSnapshotSyncServiceTests.SyncBatchAsync_ShouldProcessInChunksOf20_AndSaveAllSnapshots`: Verifica que 25 juegos sin snapshot se procesan en bloques de 20, guardando los 25 snapshots satélite en base de datos.
