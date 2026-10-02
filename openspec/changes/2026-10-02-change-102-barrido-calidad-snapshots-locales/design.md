# Diseño Técnico: INC-102 — Barrido y Auditoría Integral de Calidad de Catálogo desde Snapshots Locales de BGG

## 1. Arquitectura de Componentes

```
+-------------------------------------------------------------------------+
|                              Ludeka.Web                                 |
|  CatalogQueueAdmin.razor                                                |
|  - Barrido Total Catálogo (~17.505) -> SweepCatalogQualityBatchAsync    |
|  - Pendientes Sin Votos (~421)       -> BackfillQualityBatchAsync(cursor)|
+-------------------------------------------------------------------------+
                                     |
                                     v
+-------------------------------------------------------------------------+
|                           Ludeka.Application                            |
|  BggMassIngestionService                                                |
|  - ExecuteSweepCatalogQualityBatchAsync (Cursor afterBggId)             |
|  - ExecuteBackfillCatalogQualityBatchAsync (Cursor afterBggId)          |
|  - EnrichSingleGameQualityAsync(Game, ct)                               |
|       1. StagingRepo.GetByBggIdAsync                                    |
|       2. SnapshotRepo.GetByBggIdAsync  <-- [NUEVO FLUJO SNAPSHOT-FIRST] |
|            BggJsonToXmlConverter.ConvertToXml                           |
|            BggXmlParser.ParseQualityMetadata                            |
|            BggXmlParser.InferGameDna                                    |
|       3. BggClient.FetchGameByBggIdAsync (Solo fallback remoto)         |
+-------------------------------------------------------------------------+
                                     |
        +----------------------------+----------------------------+
        v                                                         v
+-------------------------------+         +-------------------------------+
|     Ludeka.Infrastructure     |         |          Ludeka.Jobs          |
|  BggJsonToXmlConverter        |         |  BackfillQualityJobRunner     |
|  SqliteGameRepository         |         |  (Runner desatendido CLI      |
|  (Cursor paged backfill)      |         |   para ejecución masiva)      |
+-------------------------------+         +-------------------------------+
```

## 2. Reconstitución XML desde JSON Normalizado (`BggJsonToXmlConverter`)

El componente `BggXmlToJsonConverter` almacena los nodos BGG como JSON con atributos `@attr` y texto `#text`.
Se implementa `BggJsonToXmlConverter.ConvertToXml(string rawJson)` en `Ludeka.Infrastructure.Bgg` (o `BggRawSnapshotParser` en `Ludeka.Application.Features.Bgg`):

```csharp
public static XElement? ConvertToItemElement(string rawJson)
{
    if (string.IsNullOrWhiteSpace(rawJson) || rawJson.Contains("\"notFound\":true"))
        return null;

    using var doc = JsonDocument.Parse(rawJson);
    var root = doc.RootElement;
    if (root.TryGetProperty("item", out var itemElem))
        root = itemElem;

    return ConvertJsonElementToXElement("item", root);
}
```

## 3. Flujo en `BggMassIngestionService.EnrichSingleGameQualityAsync`

```csharp
// 1. Staging
var staging = await _stagingRepo.GetByBggIdAsync(game.BggId, ct);
if (staging != null && staging.FetchStatus == StagingFetchStatus.Fetched && !string.IsNullOrWhiteSpace(staging.RawThingXml) && staging.RawThingXml.Contains("<dna "))
{
    // Extraer desde staging
}
// 2. Snapshot satélite local (desacoplado de la red)
else if (_snapshotRepo != null && (snapshot = await _snapshotRepo.GetByBggIdAsync(game.BggId, ct)) != null && !string.IsNullOrWhiteSpace(snapshot.RawJson))
{
    var itemElement = BggJsonToXmlConverter.ConvertToItemElement(snapshot.RawJson);
    if (itemElement != null)
    {
        var quality = BggXmlParser.ParseQualityMetadata(itemElement);
        var (confrontation, style, isSolo) = BggXmlParser.InferGameDna(itemElement, quality.Scalability);
        
        // Actualizar game si difiere
        // Idempotencia: no marcar enriched = true si no hay cambios reales
    }
}
// 3. Fallback remoto solo si no existe en local
else
{
    var fetched = await _bggClient.FetchGameByBggIdAsync(game.BggId, ct);
    ...
}
```

## 4. Cursor Monotónico en `GetGamesPendingQualityBackfillAsync`

Se amplía la firma del contrato:
```csharp
Task<IReadOnlyList<Game>> GetGamesPendingQualityBackfillAsync(int afterBggId = 0, int limit = 50, CancellationToken ct = default);
```
En la implementación:
```csharp
var matchingIds = candidates
    .Where(g => g.BggId > afterBggId && (g.Scalability.Count == 0 || g.Scalability.All(s => s.BestVotes == 0 && s.RecommendedVotes == 0)))
    .Take(limit)
    .Select(g => g.Id)
    .ToList();
```
El bucle en `StartContinuousBackfill` avanza `afterBggId = res.LastBggIdProcessed`. De este modo, aunque un título reciba fallback con 0 votos comunitarios, el cursor avanza al siguiente título y nunca queda atrapado en el mismo conjunto.
