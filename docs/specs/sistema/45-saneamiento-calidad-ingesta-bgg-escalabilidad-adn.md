# 45. Saneamiento de Calidad en Ingesta BGG: Años Históricos, Inferencia de Estilo, Escalabilidad Real y Duración por Jugador

> **Estado:** Implementado y Verificado en Código  
> **Alcance:** `src/Ludeka.Core`, `src/Ludeka.Application`, `src/Ludeka.Infrastructure`, `tests/Ludeka.UnitTests`  
> **Incremento Asociado:** INC-77 (Saneamiento de Calidad en Ingesta BGG)  
> **Pruebas Automatizadas Verificadas:** 2.032 pruebas (2.022 unitarias + 10 de integración) al 100% en verde  

---

## 1. Propósito y Contexto de Negocio

Durante la fase final de ingesta masiva de ~4.300 juegos de BoardGameGeek (BGG), el proceso de enriquecimiento retroactivo de calidad de catálogo se detuvo abruptamente ante los últimos 37 títulos, al tiempo que se detectaron tres anomalías críticas en los datos ya incorporados:
1. **Bloqueo por año de publicación:** Juegos históricos milenarios y clásicos sin año formal asignado (como Go [-2200], Senet [-3500], Ajedrez moderno [1475], Crokinole [1876] o títulos con año 0 en BGG) arrojaban una excepción `ArgumentOutOfRangeException: El año de publicación debe estar entre 1900 y 2100` en `Game.UpdateCatalogInformation`.
2. **Homogeneización artificial a Eurogame:** La práctica totalidad del catálogo incorporado figuraba clasificado como `GameStyle.Eurogame`, debido a que `PromoteReadyToCatalogBatchAsync` hardcodeaba dicho valor y `BggXmlParser.InferGameDna` ignoraba los subdominios de BGG (`boardgamesubdomain`).
3. **Colapso de duración por jugador a 15 minutos:** En títulos de duración media/larga (ej. Dune: Imperium, 120 minutos), la ficha mostraba un tiempo estimado por jugador colapsado a 15 minutos debido a una doble división en el ciclo de staging y promoción.
4. **Degradación de escalabilidad comunitaria:** Juegos de alta interacción como Brass: Birmingham mostraban un rango sintético y homogéneo de «1-4 jugadores» en lugar de su rango comunitario óptimo («3-4 jugadores»), debido a que `SqliteGameRepository.UpdateAsync` no transfería la colección `Scalability` a la entidad rastreada y ejecutaba `UpdateCatalogInformation` reseteando a entradas sintéticas con 0 votos.

Este módulo documenta las correcciones de dominio, inferencia, staging, cálculo y persistencia implementadas para resolver de forma definitiva estas cuatro anomalías.

---

## 2. Decisiones Arquitectónicas y Modificaciones de Dominio

### 2.1 Flexibilización de Años Históricos y Futuros (`Game.cs`)
Se amplía el rango permitido para el año de publicación en `Game.UpdateCatalogInformation`:
```csharp
int maxAllowedYear = DateTime.UtcNow.Year + 10;
if (yearPublished < -5000 || yearPublished > maxAllowedYear)
{
    throw new ArgumentOutOfRangeException(nameof(yearPublished),
        $"El año de publicación debe situarse entre -5000 y {maxAllowedYear}.");
}
```
Esto da soporte íntegro a los juegos milenarios y de dominio público recogidos en BGG, respetando las invariantes de dominio.

### 2.2 Preservación de Escalabilidad Comunitaria con Votos Reales
En `Game.UpdateCatalogInformation`, se condiciona la invocación de `AdjustScalability` para que **no descarte ni degrade** colecciones que ya poseen votos comunitarios reales de los usuarios de BGG:
```csharp
if (!Scalability.Any(s => s.TotalVotes > 0))
{
    AdjustScalability(minPlayers, maxPlayers);
}
```

### 2.3 Mutación Explícita de ADN Lúdico
Se añaden métodos específicos en `Game` para permitir la actualización de estilo, confrontación y modo solitario sin alterar el resto de la ficha:
```csharp
public void UpdateDna(GameStyle style, ConfrontationType confrontation, bool isOfficialSolo)
{
    Style = style;
    Confrontation = confrontation;
    IsOfficialSolo = isOfficialSolo;
}

public void UpdateStyle(GameStyle style)
{
    Style = style;
}
```

---

## 3. Inferencia de ADN Lúdico desde Subdominios y Categorías BGG (`BggXmlParser`)

En `BggXmlParser.InferGameDna`, se incorporan las etiquetas nativas `<link type="boardgamesubdomain" ...>` como señal primaria de categorización lúdica:
- **Subdominios Primarios:**
  - `Thematic Games`, `Wargames` ➔ `GameStyle.Ameritrash`
  - `Party Games`, `Children's Games` ➔ `GameStyle.PartyGame`
  - `Abstract Games` ➔ `GameStyle.FillerAbstract`
  - `Strategy Games` ➔ `GameStyle.Eurogame`
- **Jerarquía y Categorías Secundarias:**
  1. Si contiene `Campaign`, `Legacy` o `Storytelling` ➔ `GameStyle.NarrativeCampaign` (prioridad absoluta).
  2. Si contiene subdominio o categorías de fiesta (`Party Game`, `Trivia`, `Word Game`, `Humor`) ➔ `GameStyle.PartyGame`.
  3. Subdominios temáticos / wargames ➔ `GameStyle.Ameritrash`.
  4. Subdominios abstractos ➔ `GameStyle.FillerAbstract`.
  5. Subdominios de estrategia ➔ `GameStyle.Eurogame`.
  6. Categorías y mecánicas temáticas (`Miniatures`, `Dungeon Crawl`, `Horror`, `Fighting`, `Zombies`, `Adventure`, `Sci-Fi`) ➔ `GameStyle.Ameritrash`.
  7. Categorías abstractas (`Abstract Strategy`) ➔ `GameStyle.FillerAbstract`.
  8. Fallback determinista ➔ `GameStyle.Eurogame`.
- **Confrontación:** Se ajusta la precedencia para que `Semi-Cooperative` se evalúe antes de `Cooperative` genérico, reconociendo además `Traitor` y `Secret Identity` como `ConfrontationType.HiddenRolesOrTeams`.

---

## 4. Contrato de Staging y Duración No Colapsada (`BggMassIngestionService`)

### 4.1 Staging: `PlayingTimeMinutes` como Duración Total
`staging.PlayingTimeMinutes` representa **la duración total de una partida** en minutos (ej. 60, 90, 120), y no los minutos por jugador. En `ProcessPendingDetailsBatchAsync` y `ExecuteBackfillCatalogQualityBatchAsync`, se calcula:
```csharp
int totalPlayTime = (fetchedGame.Duration != null && fetchedGame.Duration.MaxMinutes > 0)
    ? fetchedGame.Duration.MaxMinutes
    : (fetchedGame.Duration != null && fetchedGame.Duration.MinMinutes > 0 ? fetchedGame.Duration.MinMinutes : 30);
```
Se serializa además el ADN inferido en `rawXml` (`<dna style="..." confrontation="..." solo="..." />`) para su consumo posterior en la fase de promoción.

### 4.2 Promoción y Backfill con Estimación Proporcional
En `PromoteReadyToCatalogBatchAsync` y `ExecuteBackfillCatalogQualityBatchAsync`:
```csharp
int estPerPlayer = Math.Max(15, (item.PlayingTimeMinutes > 0 ? item.PlayingTimeMinutes : maxPlay) / Math.Max(1, item.MaxPlayers));
```
Para títulos de 120 minutos a 4 jugadores (como Dune: Imperium), `estPerPlayer = 120 / 4 = 30` minutos por comensal, erradicando el colapso arbitrario a 15 minutos.

En el backfill:
- Se actualiza incondicionalmente `game.UpdateDuration(fetched.Duration)`.
- Se actualiza el ADN lúdico: `game.UpdateDna(fetched.Style, fetched.Confrontation, fetched.IsOfficialSolo)`.
- Si la escalabilidad del catálogo carece de votos (`game.Scalability.All(s => s.BestVotes == 0 && s.RecommendedVotes == 0)`), se reemplaza con los votos comunitarios reales de BGG.

---

## 5. Persistencia y Consultas de Backfill (`SqliteGameRepository`)

### 5.1 Sincronización Completa en `UpdateAsync`
`SqliteGameRepository.UpdateAsync` transfiere incondicionalmente todas las colecciones complejas y metadatos de calidad desde la entidad desacoplada a la rastreada:
```csharp
existing.UpdateScalability(game.Scalability);
existing.UpdateSleeves(game.Sleeves);
if (!string.IsNullOrWhiteSpace(game.SpanishPublisher))
{
    existing.UpdateSpanishPublisher(game.SpanishPublisher);
}
if (game.RegionalPublishers != null && game.RegionalPublishers.Count > 0)
{
    existing.UpdateRegionalPublishers(game.RegionalPublishers);
}
existing.UpdateImages(game.CoverImageUrl, game.ThumbnailUrl);
existing.UpdateMediaUrls(game.CoverImageUrl, game.ThumbnailUrl, game.BackCoverImageUrl, game.TableImageUrl);
```
Además, incorpora resolución con fallback por `BggId` si la entidad desacoplada posee un `Guid` generado de forma independiente.

### 5.2 Ampliación del Criterio de Selección de Backfill
Para permitir que los títulos degradados en ejecuciones previas recuperen sus votos comunitarios reales, `GetGamesPendingQualityBackfillAsync` y `GetGamesPendingQualityBackfillCountAsync` amplían su filtro:
```csharp
.Where(g => g.Scalability.Count == 0 || g.Scalability.All(s => s.BestVotes == 0 && s.RecommendedVotes == 0))
```
Utilizando `.AsNoTracking()` en las consultas para posibilitar la proyección de colecciones propiedad (`OwnsMany`) en SQLite sin conflictos de tracking.

---

## 6. Verificación de Pruebas Automatizadas

El saneamiento integral de INC-77 se encuentra respaldado por pruebas unitarias de regresión en todas las capas:
- `GameEditorDomainTests`: Pruebas de años históricos (-2200, -3500, 1475, 1876, 0, 2026), límites de rango y preservación de votos comunitarios en `UpdateCatalogInformation`.
- `BggXmlParserTests`: Pruebas de inferencia de `GameStyle` a partir de subdominios (`Thematic Games`, `Wargames`, `Party Games`, `Children's Games`, `Abstract Games`, `Strategy Games`), categorías y mecánicas temáticas (`Miniatures`, `Zombies`, `Dungeon Crawl`, `Trivia`, `Campaign`), y precedencia de confrontación (`Semi-Cooperative`, `Traitor`).
- `BggMassIngestionBackfillTests`: Pruebas de promoción con estilo inferido y duración calculada no colapsada (~30 min/jugador en 120 min), y enriquecimiento retroactivo de ADN y escalabilidad comunitaria.
- `SqliteGameRepositoryTests`: Pruebas de persistencia real de `MustPlay`, `BestVotes` y `Sleeves` tras `UpdateAsync`, y selección correcta en el filtro de backfill.

**Total Verificado:** 2.032 pruebas automatizadas en verde al 100% (2.022 unitarias + 10 de integración).
