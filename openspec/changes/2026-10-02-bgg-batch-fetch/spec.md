# Especificación: Peticiones en Bloque Multi-ID a BGG y Ampliación de Timeout en Runner de Snapshots Crudos

> **ID:** INC-97  
> **Slug:** `bgg-batch-fetch`  
> **Fecha:** 2026-10-02  

---

## 1. Requerimientos Funcionales y de Dominio

### REQ-1: Método Multi-ID en `IBggClient`
- Se debe añadir `Task<string?> FetchRawThingsXmlAsync(IEnumerable<int> bggIds, CancellationToken ct = default);` a `IBggClient`.
- En `BggXmlApiClient`:
  - Recibe `IEnumerable<int> bggIds`.
  - Normaliza: filtra `id > 0`, elimina duplicados y limita a un máximo de 20 IDs por llamada (si la colección excede 20, toma los primeros 20).
  - Si no hay IDs válidos, retorna `null`.
  - Construye la URL: `https://boardgamegeek.com/xmlapi2/thing?id={string.Join(",", ids)}&stats=1`.
  - Reutiliza el `_rateLimiter` (máx. 2 req/s) y la gestión de 202 (Accepted) y 429 con backoff ya presente en `FetchRawThingXmlAsync`.
- En `SimulatedBggClient`:
  - Genera un documento XML envolvente `<items>...</items>` con los `<item>` simulados para cada ID solicitado.

### REQ-2: Procesamiento en Bloques en `BggRawSnapshotSyncService`
- En `SyncBatchCoreAsync`:
  - En lugar de iterar `missingIds` juego por juego realizando una llamada HTTP individual por cada uno, divide la lista `missingIds` en lotes de tamaño 20 utilizando `.Chunk(20)`.
  - Para cada chunk de 20 IDs:
    - Ejecuta `await _bggClient.FetchRawThingsXmlAsync(chunk, ct)`.
    - Si el XML devuelto es nulo o vacío, computa como fallidos los IDs de ese chunk y continúa con el siguiente.
    - Parsea los elementos `<item>` presentes en el XML devuelto:
      - Extrae `bggId = int.Parse(item.Attribute("id")!.Value)`.
      - Serializa a JSON estructurado mediante `BggXmlToJsonConverter.ConvertToJson(item)`.
      - Realiza `_snapshotRepo.UpsertAsync(new BggRawSnapshot(bggId, rawJson, apiVersion: 2, fetchedAt: DateTimeOffset.UtcNow), ct)`.
      - Procesa auto-vinculación de expansiones (inbound a juego base y outbound a expansiones hijas).
      - Registra éxito e incrementa `successCount`.
    - Detecta si algún ID del chunk no fue devuelto por BGG e incrementa `failedCount`.
    - Aplica la pausa de cortesía `await Task.Delay(delayMs, ct)` **una sola vez por cada bloque de 20 juegos**.

### REQ-3: Timeout Ampliado en `Ludeka.Jobs`
- En `src/Ludeka.Jobs/Program.cs`:
  - Modificar el valor por defecto de `Workers:JobTimeoutMinutes` a **120 minutos** (2 horas), permitiendo que la ejecución desatendida en Cloud Run o consola tenga tiempo sobrado para finalizar los 14.000 títulos restantes sin cortarse.

---

## 2. Invariantes Arquitectónicas
- Inmutabilidad de snapshots: cada payload de BGG se almacena íntegro en `BggRawSnapshots`.
- Cero fugas de memoria: parseo de XML con XDocument acotado a cada bloque de 20 items.
- Preservación de contratos existentes: `FetchRawThingXmlAsync(int bggId)` se mantiene intacto en `IBggClient` para compatibilidad con llamadas individuales.
