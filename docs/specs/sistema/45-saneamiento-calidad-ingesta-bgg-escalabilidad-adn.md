# 45. Saneamiento de Calidad en Ingesta BGG: Años Históricos, Inferencia de Estilo, Escalabilidad Real y Duración por Jugador

> **Estado:** Implementado y Verificado en Código  
> **Alcance:** `src/Ludeka.Core`, `src/Ludeka.Application`, `src/Ludeka.Infrastructure`, `tests/Ludeka.UnitTests`  
> **Incremento Asociado:** INC-77 (Saneamiento de Calidad en Ingesta BGG) e INC-78 (Barrido Completo de Calidad de Catálogo)  
> **Pruebas Automatizadas Verificadas:** 2.037 pruebas (2.027 unitarias + 10 de integración) al 100% en verde  

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

## 6. Barrido Completo de Calidad de Catálogo por Cursor Paginado Determinista (INC-78)

Para auditar y sanear la totalidad de los títulos promovidos al catálogo (~10.000 juegos en producción), el filtro de títulos sin votos comunitarios era insuficiente (dejaba fuera ~6.000 juegos que ya tenían votos pero conservaban el estilo artificial `Eurogame` y tiempos incorrectos). Tampoco era viable filtrar por `Style == Eurogame` debido a que los Eurogames legítimos (*Catán*, *Agrícola*, *Concordia*) mantendrían ese estilo tras el saneamiento, provocando un bucle infinito de re-evaluación.

### 6.1 Paginación Determinista $O(1)$ por Cursor Ascendente
Se implementa en `IGameRepository` y `SqliteGameRepository`:
```csharp
public async Task<IReadOnlyList<Game>> GetGamesCursorPagedAsync(int afterBggId, int limit = 50, CancellationToken ct = default)
{
    await using var scope = await CreateScopeAsync(ct);
    return await scope.Context.Games
        .AsNoTracking()
        .Where(g => g.BggId > afterBggId)
        .OrderBy(g => g.BggId)
        .Take(limit)
        .ToListAsync(ct);
}
```
Esto garantiza un recorrido estrictamente monótono, finito y sin repeticiones a lo largo de todo el catálogo.

### 6.2 Servicio de Barrido Idempotente y Estrategia *Staging-First*
En `BggMassIngestionService`:
- `SweepCatalogQualityBatchAsync` y `RunScheduledSweepCatalogQualityBatchAsync` evalúan lotes ordenados devolviendo `BggQualitySweepBatchResultDto`.
- Se extrae el método unificado `EnrichSingleGameQualityAsync(Game game, CancellationToken ct)`.
- Si el título ya contiene el ADN, duración y escalabilidad correctos, `EnrichSingleGameQualityAsync` devuelve `false`, incrementando `SkippedCount` y **omitiendo la llamada a base de datos** (`UpdateAsync`), lo que maximiza el rendimiento y reduce la contención de I/O.
- Si el juego requiere actualización, se consume primero el staging con ADN precacheado (`<dna `); en su ausencia, consulta BGG XMLAPI2 y actualiza staging retroactivamente.

### 6.3 Ejecución Autónoma y Superficie de Control
- **Runner Desatendido (`BackfillQualityJobRunner`):** Itera mediante el cursor `currentAfterBggId = result.LastBggIdProcessed` hasta completar `!result.HasMore`.
- **Panel Administrativo (`CatalogQueueAdmin.razor`):** Incorpora el botón **«Barrido Total Catálogo (~10.000)»** con ejecución continua en segundo plano, cancelación segura con `CancellationTokenSource`, y telemetría reactiva en tiempo real (`Evaluados X/Total`, `Y actualizados`, `Z ya correctos`, `W errores`).

---

## 8. Saneamiento Anti-Bucle e Idempotencia Semántica en Calidad (INC-103)

### 8.1 Causa Raíz del Bucle en «Pendientes Sin Votos»
En `/admin/cola-catalogacion`, la consulta `GetGamesPendingQualityBackfillAsync` filtraba por títulos cuya escalabilidad tuviera 0 elementos o 0 votos con `Take(limit)` sin cursor de desplazamiento. Aquellos títulos (~421 juegos) que legítimamente carecen de encuestas de comensales en BGG recibían el fallback (con 0 votos), volviendo a ser devueltos una y otra vez en sucesivas consultas e impidiendo que el proceso continuo terminase.

### 8.2 Paginación Monotónica por Cursor (`afterBggId`)
- Se incorpora la sobrecarga `GetGamesPendingQualityBackfillAsync(int afterBggId, int limit, CancellationToken ct)` en `IGameRepository` y `SqliteGameRepository`, ordenando por `BggId ASC` y filtrando por `g.BggId > afterBggId`.
- `BggQualityBackfillResultDto` incorpora `LastBggIdProcessed` y `HasMore`.
- `CatalogQueueAdmin.razor` avanza el cursor secuencialmente garantizando que cada juego se evalúa a lo sumo una única vez por barrido.

### 8.3 Idempotencia Semántica (`ScalabilityNeedsUpdate`)
En `BggMassIngestionService`:
- Se previene marcar `enriched = true` si tanto el catálogo existente como el snapshot carecen de votos comunitarios (`TotalVotes == 0`) y tienen el mismo número de comensales.
- Esto elimina escrituras innecesarias en base de datos, reduce contención de I/O y asegura estabilidad estricta en el catálogo.

**Total Verificado Actualizado:** 2.319 pruebas automatizadas en verde al 100% (2.309 unitarias + 10 de integración).
