# Especificación: bgg-catalog-mass-enrichment

Capacidad de ampliación masiva del catálogo de juegos de mesa desde BoardGameGeek con filtro de tracción comunitaria rebajado a >100 opiniones, protección estricta contra duplicados por `BggId`, y enriquecimiento integral y determinista de metadatos (escalabilidad comunitaria con fallback, fundas de cartas, huella en mesa y tiempos reales de juego).

---

## 1. Requerimientos Funcionales

### R1.1: Ampliación del Umbral de Tracción a >100 Opiniones
- `BggMassIngestionOptions.MinUsersRated` se establece en 100 por defecto (configurable por `appsettings.json`).
- `BggDumpParser.ParseRanksDumpAsync` filtra cada fila CSV admitiendo registros donde `UsersRated >= minUsersRated` (con valor por defecto 100).
- `IBggMassIngestionService.IngestRanksDumpAsync` y `DownloadAndIngestLatestRanksAsync` procesan el volcado con el umbral 100.
- `SeedStagingJobRunner` lee el umbral configurado (100) en el arranque del trabajo diario.

### R1.2: Protección Anti-Duplicados y Actualización Incremental
- En el pipeline de ingesta (`BggMassIngestionService`):
  - Al procesar el volcado masivo en staging: `IBggCatalogStagingRepository.UpsertBatchAsync` agrupa por `BggId` e inserta solo los nuevos ítems, actualizando métricas de clasificación en los ya existentes.
  - Al promover a catálogo definitivo (`PromoteReadyToCatalogBatchAsync`):
    - Se consulta `_gameRepo.GetByBggIdAsync(item.BggId)`.
    - Si no existe: se crea un nuevo `Game` con todos sus metadatos (escalabilidad, fundas, huella, tiempos) y se añade a la base de datos.
    - Si ya existe: **no** se intenta insertar otro registro (impidiendo violaciones del índice único `Games.BggId`). Se realiza una actualización aditiva:
      - Actualización de URLs de medios (`CoverImageUrl`, `BackCoverImageUrl`, `TableImageUrl`).
      - Actualización de `Scalability` si el juego existente no tenía escalabilidad o carecía de votos comunitarios.
      - Actualización de `Sleeves` si el juego existente no tenía fundas registradas.
      - Actualización de `Duration` y `Footprint` con los valores de calidad calculados.

### R1.3: Enriquecimiento de Escalabilidad por Jugadores con Fallback
- `BggXmlParser.ParseItem` analiza el nodo `<poll name="suggested_numplayers">`:
  - Extrae los votos para `Best`, `Recommended` y `Not Recommended` para cada recuento de jugadores.
  - Si la encuesta contiene votos válidos (`TotalVotes > 0`), genera las entradas de escalabilidad con su estado semafórico (`MustPlay`, `Recommended`, `NotRecommended`).
  - **Fallback determinista:** Si la encuesta comunitaria no existe o tiene 0 votos:
    - Genera entradas para todo el rango de jugadores entre `minplayers` y `maxplayers`.
    - Si `minplayers == maxplayers`: asigna `MustPlay` (ej. juegos exclusivos para 2 jugadores como *7 Wonders Duel* o juegos en solitario).
    - Si `minplayers < maxplayers`: asigna `Recommended` a todos los recuentos del rango oficial.
  - Ningún juego generado o enriquecido queda con una colección `Scalability` vacía.

### R1.4: Ingesta de Fundas de Cartas (Sleeves)
- `BggXmlParser.ParseItem` invoca `BggSleeveParser.ParseSleeves(item)` para extraer enlaces `boardgamecardsleeve`.
- `BggCatalogStagingItem` almacena las fundas serializadas en `SleevesJson`.
- Al promover a catálogo (`Game`) o ejecutar backfill, se asignan las fundas a la propiedad `Game.Sleeves`.
- Cuando BGG incluya fundas, la ficha del juego en Ludeka dispondrá de las dimensiones para alimentar la guía de fundas y enlaces de compra de INC-66.

### R1.5: Inferencia Analítica de Huella en Mesa (TableFootprint)
- `BggXmlParser` infiere el tamaño en mesa evaluando categorías, mecánicas y duración:
  - `SmallTable`: Si contiene categorías de cartas, dados, microjuegos, viaje, fiesta o deducción (`Card Game`, `Dice`, `Microgame`, `Travel`, `Party Game`, `Deduction`) y `MaxPlayTime <= 45`.
  - `TableMonster`: Si contiene miniaturas, wargames, civilización, 4X o cajas grandes (`Miniatures`, `Wargame`, `Civilization`, `4x`, `Big Box`) o `MaxPlayTime >= 150`.
  - `StandardTable`: Para el resto de juegos de tablero estándar.
- El valor inferido se persiste en `BggCatalogStagingItem.InferredFootprint` y se traslada a `Game.Footprint`.

### R1.6: Tiempos de Juego Reales y Consistentes
- `BggXmlParser` lee `minplaytime` y `maxplaytime` del XML:
  - Garantiza `MinMinutes <= MaxMinutes`.
  - Deriva `EstimatedPerPlayerMinutes` de forma balanceada dividiendo el tiempo medio entre la media de jugadores: `(minTime + maxTime) / (2 * avgPlayers)`.
- Se elimina en todo el código la fórmula ficticia de multiplicar por 1.5.

### R1.7: Servicio de Backfill Retroactivo para el Catálogo Existente
- `IBggMassIngestionService` dispone de `BackfillCatalogQualityBatchAsync(int batchSize)` (y su variante programada `RunScheduledBackfillCatalogQualityBatchAsync`).
- `IGameRepository.GetGamesPendingQualityBackfillAsync(int limit)` devuelve juegos que requieran enriquecimiento (sin escalabilidad, o huella `StandardTable`, o duraciones con `MinMinutes == MaxMinutes`).
- El backfill enriquece prioritariamente desde `BggCatalogStaging` si el juego ya fue parseado previamente; de lo contrario, consulta `FetchGameByBggIdAsync`.

---

## 2. Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Ingesta de volcado BGG filtrando por >100 opiniones
  Dado un volcado de BGG con un juego que tiene 120 votos y otro con 80 votos
  Cuando se ejecuta IngestRanksDumpAsync con el umbral por defecto (100)
  Entonces el juego con 120 votos se inserta en staging
  Y el juego con 80 votos se descarta

Escenario: Prevención de duplicados en promoción de juego ya existente
  Dado un juego con BggId 342942 ya almacenado en la tabla Games
  Y un registro en BggCatalogStagingItem listo para promover con el mismo BggId 342942
  Cuando se ejecuta PromoteReadyToCatalogBatchAsync
  Entonces no se produce ninguna excepción de clave duplicada
  Y el juego existente actualiza su escalabilidad, fundas y huella en mesa

Escenario: Fallback determinista cuando la encuesta de escalabilidad carece de votos
  Dado un XML de juego con minplayers=2, maxplayers=4 y totalvotes="0" en la encuesta
  Cuando BggXmlParser parsea el juego
  Entonces la lista de escalabilidad contiene exactamente 3 entradas (2J, 3J, 4J)
  Y todas las entradas tienen estado Recommended

Escenario: Inferencia de tamaño en mesa SmallTable para juego de cartas corto
  Dado un XML de juego con categoría "Card Game" y tiempo máximo de 30 minutos
  Cuando BggXmlParser parsea el juego
  Entonces su Footprint es TableFootprint.SmallTable

Escenario: Backfill retroactivo de calidad para juegos existentes en catálogo
  Dado un juego en catálogo con Scalability vacía y huella StandardTable
  Y su correspondiente registro en staging con ScalabilityJson y fundas calculadas
  Cuando se ejecuta RunScheduledBackfillCatalogQualityBatchAsync
  Entonces el juego en catálogo queda enriquecido con la escalabilidad y fundas de staging
```
