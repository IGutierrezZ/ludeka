# Especificación Técnica: INC-77 — Saneamiento de Calidad en Ingesta BGG (Años Históricos, Inferencia de Estilo, Escalabilidad Real y Duración por Jugador)

## 1. Resumen Ejecutivo

Este incremento resuelve de raíz cuatro fallos críticos detectados durante la ejecución del proceso masivo de enriquecimiento de catálogo en producción (`ExecuteBackfillCatalogQualityBatchAsync` sobre ~4.300 juegos de mesa):
1. El bloqueo abrupto del proceso por excepción `ArgumentOutOfRangeException` en títulos milenarios o sin fecha (como Go, Ajedrez o Senet).
2. La homogeneización artificial del 100% de los títulos promovidos bajo el estilo `GameStyle.Eurogame`.
3. El colapso sistemático de la duración por jugador a ~15 min/jugador en juegos extensos (como Dune) debido a un error de mapeo de campos en staging.
4. La pérdida total de los votos comunitarios de la encuesta de escalabilidad BGG (`BestVotes`, `RecommendedVotes`, estatus `MustPlay`) en `SqliteGameRepository.UpdateAsync`, sustituidos indebidamente por entradas sintéticas genéricas que distorsionan el rango ideal (ej. Brass: Birmingham mostrando "Ideal: 1-4 jugadores" en lugar de 3-4).

---

## 2. Requerimientos Funcionales y de Dominio

### REQ-1: Soporte de Años Históricos en la Entidad `Game`
- **Flexibilización de Validación:** En `Game.UpdateCatalogInformation`, se modifica la restricción `yearPublished < 1900 || yearPublished > 2100` para admitir el rango histórico lúdico válido:
  `yearPublished < -5000 || yearPublished > (DateTime.UtcNow.Year + 10)`.
- **Compatibilidad con Juegos Milenarios:** Juegos con años anteriores a la era común (ej. Go con -2200, Backgammon con -3000, Senet con -3500, Mancala con -700), juegos clásicos medievales/renacentistas (Ajedrez con 1475) y títulos contemporáneos con año no definido en BGG (año 0) deben actualizarse sin lanzar `ArgumentOutOfRangeException`.
- **Actualización de ADN Lúdico en Dominio:** Se incorpora en `Game` un método para actualizar selectivamente el estilo y confrontación:
  `UpdateDna(GameStyle style, ConfrontationType confrontation, bool isOfficialSolo)`
  garantizando que la actualización de estilo no reinicialice ni altere la colección de escalabilidad comunitaria.

### REQ-2: Inferencia Precisa de ADN Lúdico en `BggXmlParser`
- **Lectura Prioritaria de Subdominios (`boardgamesubdomain`):** Se extiende `InferGameDna` para procesar los enlaces de subdominio devueltos por la API de BGG:
  - `Thematic Games` y `Wargames` ➔ `GameStyle.Ameritrash`.
  - `Party Games` y `Children's Games` ➔ `GameStyle.PartyGame`.
  - `Abstract Games` ➔ `GameStyle.FillerAbstract`.
  - `Strategy Games` ➔ `GameStyle.Eurogame`.
  - `Customizable Games` (ej. LCG/CCG) ➔ `GameStyle.Eurogame` o `Ameritrash` según temática.
- **Categorías y Mecánicas Complementarias:** Se enriquecen los criterios secundarios:
  - Temáticas/Ameritrash: *Dungeon Crawl*, *Miniatures*, *Fighting*, *Horror*, *Zombies*, *Science Fiction* (con azar/dados), *Adventure*.
  - Party Games: *Trivia*, *Word Game*, *Humor*, *Party Game*.
  - Filler / Abstracto: *Abstract Strategy*.
  - Campaña: *Legacy*, *Campaign*.
  - Eurogame: *Worker Placement*, *Economic*, *City Building*, *Industry / Manufacturing*.

### REQ-3: Corrección de Duración y Almacenamiento en Staging
- **Duración Total en Staging (`PlayingTimeMinutes`):** En `BggMassIngestionService.ProcessPendingDetailsBatchAsync` (y en el método de enriquecimiento de staging), `item.MarkFetched` debe recibir en el parámetro `playingTimeMinutes` la duración total de la partida devuelta por BGG (`quality.PlayingTime > 0 ? quality.PlayingTime : quality.MaxPlayTime`), NUNCA `EstimatedPerPlayerMinutes`.
- **Cálculo Consistente de `EstimatedPerPlayerMinutes`:**
  - En la promoción a catálogo y en el enriquecimiento de staging, `EstimatedPerPlayerMinutes` debe calcularse como `playingTime / Math.Max(1, maxPlayers)`.
  - Para juegos de 120 minutos y 4 jugadores (como Dune: Imperium o Dune clásico), la estimación debe resultar en 25–30 min/jugador y no 15 min planos.
- **Actualización en Backfill de Calidad:** En `ExecuteBackfillCatalogQualityBatchAsync`, cuando BGG devuelva `fetched.Duration != null`, se debe actualizar la duración del juego (`game.UpdateDuration(fetched.Duration)`) sin la condición excluyente `game.Duration.MinMinutes == 0`.

### REQ-4: Persistencia Íntegra en `SqliteGameRepository.UpdateAsync`
- **Sincronización Completa de Propiedades Complejas:**
  Cuando `!ReferenceEquals(existing, game)`, `SqliteGameRepository.UpdateAsync` debe transferir fielmente todas las propiedades enriquecidas:
  ```csharp
  existing.UpdateScalability(game.Scalability);
  existing.UpdateSleeves(game.Sleeves);
  existing.UpdateSpanishPublisher(game.SpanishPublisher);
  existing.UpdateRegionalPublishers(game.RegionalPublishers);
  ```
- **Protección de Escalabilidad Rica en `UpdateCatalogInformation`:**
  - `UpdateCatalogInformation` NO debe machacar la escalabilidad comunitaria rica (`existing.Scalability`) invocando `AdjustScalability` si la entidad ya cuenta con entradas detalladas provistas de votos (`TotalVotes > 0`).
  - `AdjustScalability` solo debe intervenir como fallback defensivo cuando la colección esté completamente vacía.
- **Reactivación del Criterio de Selección de Backfill:**
  - En `SqliteGameRepository.GetGamesPendingQualityBackfillAsync`, se amplía el criterio de selección:
    `g.Scalability.Count == 0 || g.Scalability.All(s => s.BestVotes == 0 && s.RecommendedVotes == 0)`
    de modo que los juegos que sufrieron la sobreescritura previa con entradas sintéticas genéricas sean seleccionados y sanados con los datos reales de BGG.

---

## 3. Criterios de Aceptación y Casos de Prueba (Gherkin)

### Escenario 1: Actualización de Juego Milenario (Año Histórico)
```gherkin
Given un juego histórico en catálogo con BggId 188 ("Go") y YearPublished = -2200
When se invoca UpdateCatalogInformation o se ejecuta SqliteGameRepository.UpdateAsync
Then la operación concluye con éxito sin lanzar ArgumentOutOfRangeException
And el año de publicación en el registro persiste como -2200
```

### Escenario 2: Inferencia de Estilo Temático desde Subdominio BGG
```gherkin
Given un elemento XML de BGG para un juego con subdominio "Thematic Games"
When BggXmlParser.InferGameDna procesa el elemento
Then el GameStyle resultante es Ameritrash y no Eurogame
```

### Escenario 3: Inferencia de Estilo Party Game
```gherkin
Given un elemento XML de BGG para un juego con subdominio "Party Games" o categoría "Trivia"
When BggXmlParser.InferGameDna procesa el elemento
Then el GameStyle resultante es PartyGame
```

### Escenario 4: Persistencia de Votos Comunitarios y Estatus MustPlay en Repositorio
```gherkin
Given una entidad Game para "Brass: Birmingham" con Scalability conteniendo 3J (MustPlay, 1200 votos) y 4J (MustPlay, 1850 votos)
When se persiste mediante SqliteGameRepository.UpdateAsync
Then la entidad rastreada en base de datos almacena exactamente las entradas de Scalability con sus BestVotes y estatus MustPlay
And CalculateIdealPlayerCountText() devuelve "Ideal: 3-4 jugadores" y no "1-4 jugadores"
```

### Escenario 5: Cálculo Consistente de Duración por Jugador (Dune)
```gherkin
Given un juego con PlayingTime de 120 minutos y MaxPlayers de 4
When se procesa en el pipeline de ingesta o backfill
Then la duración estimada por jugador resultante es de ~30 min/jugador y no se colapsa a 15 min
```
