# Diseño Técnico: INC-77 — Saneamiento de Calidad en Ingesta BGG (Años Históricos, Inferencia de Estilo, Escalabilidad Real y Duración por Jugador)

## 1. Arquitectura y Enfoque de Diseño

El diseño de INC-77 aborda los cuatro fallos detectados respetando la arquitectura limpia del monorepo (`Ludeka.Core` -> `Ludeka.Application` -> `Ludeka.Infrastructure`), garantizando:
- Invariantes de dominio no arbitrarios que respeten la realidad del catálogo de juegos de mesa.
- Inferencia determinista de metadatos BGG aprovechando subdominios nativos.
- Consistencia matemática en el cálculo y persistencia de duraciones.
- Integridad total de entidades EF Core en repositorios desacoplados sin sobreescrituras destructivas.

```
┌─────────────────────────────────────────────────────────────┐
│                       Ludeka.Core                           │
│  - Game.cs: YearPublished [-5000, Ahora+10]                 │
│  - UpdateCatalogInformation(): preserva Scalability rica    │
│  - UpdateDna() / UpdateStyle(): mutaciones puras de ADN     │
└──────────────────────────────▲──────────────────────────────┘
                               │
┌──────────────────────────────┴──────────────────────────────┐
│                    Ludeka.Application                       │
│  - BggMassIngestionService:                                 │
│    * Staging: playingTimeMinutes = Duración Total           │
│    * Promoción: usa estilo inferido, no Eurogame fijo       │
│    * Backfill: actualiza Duración, Estilo y Escalabilidad   │
└──────────────────────────────▲──────────────────────────────┘
                               │
┌──────────────────────────────┴──────────────────────────────┐
│                   Ludeka.Infrastructure                     │
│  - BggXmlParser: InferGameDna con boardgamesubdomain        │
│  - SqliteGameRepository: UpdateAsync copia Scalability,     │
│    Sleeves, SpanishPublisher y RegionalPublishers           │
│  - GetGamesPendingQualityBackfillAsync: selecciona juegos   │
│    con 0 votos comunitarios para su curación retroactiva    │
└─────────────────────────────────────────────────────────────┘
```

---

## 2. Decisiones de Diseño Detalladas

### A. Dominio: `Ludeka.Core.Entities.Game`
1. **Validación de Año:**
   ```csharp
   int maxAllowedYear = DateTime.UtcNow.Year + 10;
   if (yearPublished < -5000 || yearPublished > maxAllowedYear)
   {
       throw new ArgumentOutOfRangeException(nameof(yearPublished),
           $"El año de publicación debe situarse entre -5000 y {maxAllowedYear}.");
   }
   ```
2. **Preservación de Escalabilidad en `UpdateCatalogInformation`:**
   Actualmente, `UpdateCatalogInformation` invoca incondicionalmente `AdjustScalability(minPlayers, maxPlayers)`.
   Si `Scalability` ya contiene entradas con votos comunitarios (`Scalability.Any(s => s.TotalVotes > 0)`), `AdjustScalability` **no debe descartar ni sobreescribir** los datos reales. Solo ajustará o complementará rangos si la colección está vacía o si faltan números dentro de la horquilla oficial.
3. **Métodos de Mutación de ADN:**
   ```csharp
   public void UpdateDna(GameStyle style, ConfrontationType confrontation, bool isOfficialSolo)
   {
       Style = style;
       Confrontation = confrontation;
       IsOfficialSolo = isOfficialSolo;
   }
   ```

### B. Inferencia de Estilo: `Ludeka.Infrastructure.Bgg.BggXmlParser`
1. **Procesamiento de `boardgamesubdomain`:**
   En `InferGameDna`, se extraen los enlaces de tipo `boardgamesubdomain`:
   - `Thematic Games`, `Wargames` ➔ `GameStyle.Ameritrash`
   - `Party Games`, `Children's Games` ➔ `GameStyle.PartyGame`
   - `Abstract Games` ➔ `GameStyle.FillerAbstract`
   - `Strategy Games` ➔ `GameStyle.Eurogame`
2. **Priorización de Señales:**
   - Si existe subdominio explícito, se utiliza como señal primaria.
   - Si no existe subdominio, se evalúan categorías y mecánicas de forma jerárquica:
     1. Presencia de `Party Game`, `Trivia`, `Word Game` ➔ `PartyGame`.
     2. Presencia de `Campaign`, `Legacy` ➔ `NarrativeCampaign`.
     3. Presencia de `Thematic`, `Wargame`, `Miniatures`, `Dungeon Crawl`, `Horror`, `Fighting` ➔ `Ameritrash`.
     4. Presencia de `Abstract Strategy` ➔ `FillerAbstract`.
     5. Fallback a `Eurogame` (o según mecánicas de gestión de recursos/colocación de trabajadores).

### C. Ingesta y Cálculo de Tiempos: `Ludeka.Application.Features.Bgg.BggMassIngestionService`
1. **Contrato de Staging:**
   `staging.PlayingTimeMinutes` representa **la duración total de partida** en minutos (ej. 60, 90, 120).
   En `ProcessPendingDetailsBatchAsync`:
   ```csharp
   int totalPlayingTime = fetchedGame.Duration.MaxMinutes > 0
       ? fetchedGame.Duration.MaxMinutes
       : (fetchedGame.Duration.MinMinutes > 0 ? fetchedGame.Duration.MinMinutes : 30);
   ```
   Se pasa `totalPlayingTime` a `item.MarkFetched(..., playingTimeMinutes: totalPlayingTime, ...)`.
2. **Cálculo de `EstimatedPerPlayerMinutes`:**
   Tanto en promoción como en backfill:
   `int estPerPlayer = Math.Max(15, totalPlayingTime / Math.Max(1, maxPlayers));`
   Para Dune (120 min, 4 jugadores), `estPerPlayer = 120 / 4 = 30` (~30 min/jugador).
3. **Actualización en `ExecuteBackfillCatalogQualityBatchAsync`:**
   - Se actualiza incondicionalmente `game.UpdateDuration(fetched.Duration)`.
   - Se actualiza el estilo y confrontación: `game.UpdateDna(fetched.Style, fetched.Confrontation, fetched.IsOfficialSolo)`.

### D. Persistencia en `Ludeka.Infrastructure.Data.SqliteGameRepository`
1. **Sincronización en `UpdateAsync`:**
   ```csharp
   if (!ReferenceEquals(existing, game))
   {
       existing.UpdateLudistRating(game.LudistRating);
       if (game.AiSummary != null)
       {
           existing.SetAiSummary(game.AiSummary);
       }

       int minPlayers = game.Scalability.Count > 0 ? game.Scalability.Min(s => s.PlayerCount) : 1;
       int maxPlayers = game.Scalability.Count > 0 ? game.Scalability.Max(s => s.PlayerCount) : 4;

       existing.UpdateCatalogInformation(
           game.SpanishTitle,
           game.OriginalTitle,
           game.Designer,
           game.Publisher,
           game.YearPublished,
           game.Description,
           game.Confrontation,
           game.Style,
           game.IsOfficialSolo,
           game.Age,
           game.Language,
           game.Footprint,
           game.Duration,
           minPlayers,
           maxPlayers
       );

       // INC-77: Sincronización incondicional de colecciones complejas y metadatos enriquecidos
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
   }
   ```
2. **Criterio de Selección de Backfill:**
   En `GetGamesPendingQualityBackfillAsync` y `GetGamesPendingQualityBackfillCountAsync`:
   ```csharp
   .Where(g => g.Scalability.Count == 0 || g.Scalability.All(s => s.BestVotes == 0 && s.RecommendedVotes == 0))
   ```
   Esto asegura que los juegos degradados por el backfill previo entren en la cola y recuperen sus votos comunitarios reales.
