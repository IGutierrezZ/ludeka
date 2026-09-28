# Propuesta de Cambio: Saneamiento de Calidad en Ingesta BGG (Años Históricos, Inferencia de Estilo, Escalabilidad Real y Duración por Jugador)

> **ID de Cambio:** `change-77-calidad-ingesta-bgg`  
> **Incremento Asociado:** INC-77  
> **Fecha:** 2026-09-28  
> **Autor:** Ludeka Systems Architecture  
> **Estado:** Propuesto (Pendiente de Aprobación de la Mesa Fundadora)  

---

## 1. Motivación y Problema

Durante el backfill retroactivo de calidad de catálogo en producción sobre los ~4.300 juegos (`ExecuteBackfillCatalogQualityBatchAsync`), el proceso se interrumpió de forma abrupta en los últimos 37 juegos con un error no capturado:
`System.ArgumentOutOfRangeException: El año de publicación debe estar entre 1900 y 2100. (Parameter 'yearPublished')`
al ejecutar `Ludeka.Core.Entities.Game.UpdateCatalogInformation` en `Ludeka.Infrastructure.Data.SqliteGameRepository.UpdateAsync`.

El análisis técnico en profundidad del código fuente destapó 4 fallos estructurales interconectados:

1. **Años Históricos en Juegos:** La entidad de dominio `Game` restringe en `UpdateCatalogInformation` el año a `[1900, 2100]`. Juegos milenarios (Ajedrez con 1475, Go con -2200, Backgammon con -3000, Senet con -3500, Mancala con -700) o títulos con año 0 en BGG provocan una excepción fatal cuando el repositorio intenta persistir sus datos.
2. **Homogeneización a `Eurogame`:** En `BggMassIngestionService.PromoteReadyToCatalogBatchAsync`, la promoción a catálogo fijó ciegamente `style: GameStyle.Eurogame`. A su vez, `BggXmlParser.InferGameDna` omitió la inspección de subdominios (`boardgamesubdomain`), que es el mecanismo oficial de BGG para clasificar juegos temáticos, familiares, abstractos o party games. Adicionalmente, el backfill nunca actualizaba el estilo de los juegos existentes.
3. **Colapso a 15 min/jugador en Tiempos de Partida:** En `BggMassIngestionService`, el campo `staging.PlayingTimeMinutes` se guardó erróneamente con la estimación por jugador (`EstimatedPerPlayerMinutes`, típicamente 15-25 min) en vez de la duración total de partida (60-120 min). Posteriormente, al calcular `playingTimeMinutes / maxPlayers`, el cálculo resultaba en 3 o 4 minutos, activando la guardia `Math.Max(15, ...)` y fijando 15 minutos planos por jugador en títulos de larga duración como Dune o Dune: Imperium.
4. **Pérdida de Escalabilidad Comunitaria ("Best") en `UpdateAsync`:** `SqliteGameRepository.UpdateAsync` nunca sincronizaba `game.Scalability`, `game.Sleeves` ni `game.RegionalPublishers` hacia la entidad de EF Core. En su lugar, invocaba `AdjustScalability`, rellenando la colección con entradas sintéticas genéricas de 1 a 4 jugadores, todas marcadas como `Recommended` con 0 votos. Esto suprimió las votaciones reales y el estatus `MustPlay`, provocando que juegos de 3-4 jugadores como Brass: Birmingham mostraran un rango plano e incorrecto de "Ideal: 1-4 jugadores".

---

## 2. Solución Propuesta

### A. Flexibilización de Años en Dominio (`Ludeka.Core`)
- Permitir años históricos en `Game.UpdateCatalogInformation` ampliando el rango admisible a `[-5000, DateTime.UtcNow.Year + 10]`, o eliminando la cota inferior arbitraria de 1900.
- Exponer métodos explícitos para actualizar `Style` y `Confrontation` en `Game` sin efectos secundarios destructivos sobre la escalabilidad.

### B. Inferencia Precisa de ADN Lúdico (`Ludeka.Infrastructure.Bgg`)
- Incorporar en `BggXmlParser.InferGameDna` la lectura prioritaria de `boardgamesubdomain`:
  - `Thematic Games`, `Wargames` ➔ `GameStyle.Ameritrash`.
  - `Party Games`, `Children's Games` ➔ `GameStyle.PartyGame`.
  - `Abstract Games` ➔ `GameStyle.FillerAbstract`.
  - `Strategy Games` ➔ `GameStyle.Eurogame`.
- Enriquecer la categorización secundaria por categorías de BGG (Dungeon Crawl, Miniatures, Wargame, Horror, Dice, etc.).

### C. Corrección del Almacenamiento y Cálculo de Duraciones (`Ludeka.Application`)
- Guardar en `staging.PlayingTimeMinutes` la duración total real devuelta por BGG (`quality.PlayingTime` o `MaxPlayTime`).
- Corregir el cálculo de `EstimatedPerPlayerMinutes` para que divida la duración total entre el número representativo de jugadores y no sobre un valor ya reducido.
- Actualizar la duración en el backfill de catálogo cuando BGG suministre datos válidos.

### D. Persistencia Íntegra en Repositorio (`SqliteGameRepository.UpdateAsync`)
- En `SqliteGameRepository.UpdateAsync`, sincronizar incondicionalmente:
  - `existing.UpdateScalability(game.Scalability);`
  - `existing.UpdateSleeves(game.Sleeves);`
  - `existing.UpdateSpanishPublisher(game.SpanishPublisher);`
  - `existing.UpdateRegionalPublishers(game.RegionalPublishers);`
- Evitar que `AdjustScalability` machaque la escalabilidad rica con votos comunitarios cuando ya existen datos válidos.
- Ajustar `GetGamesPendingQualityBackfillAsync` para considerar pendientes aquellos títulos cuya escalabilidad esté vacía o tenga 0 votos comunitarios, permitiendo que el proceso sane los juegos afectados previamente.

---

## 3. Plan de Verificación y Pruebas
1. Pruebas unitarias en `Ludeka.UnitTests`:
   - Pruebas de dominio para `Game.UpdateCatalogInformation` con años históricos (año -2200, año 1475, año 0, año 2026).
   - Pruebas en `BggXmlParser` verificando la inferencia de subdominios BGG (Thematic ➔ Ameritrash, Party ➔ PartyGame, Abstract ➔ FillerAbstract).
   - Pruebas de repositorio simulado / integración comprobando que `UpdateAsync` conserva intactos los votos `BestVotes`, `RecommendedVotes`, el estatus `MustPlay` y las fundas.
   - Pruebas de cálculo de duración verificando que juegos de 120 minutos a 4 jugadores arrojan ~30 min/jugador y no 15 min.
2. Ejecución completa de la suite de pruebas unitarias (`dotnet test`), asegurando el 100% de tests en verde.
