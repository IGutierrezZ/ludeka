# Documento de Diseño Arquitectónico: INC-130 — Dureza Numérica BGG, Extrapolación de Complejidad y Ordenación Precisa

> **ID del Cambio:** `2026-10-08-inc-130-dureza-numerica-bgg`  
> **Incremento Asociado:** INC-130  
> **Estado:** ⏳ Diseño formalizado  

---

## 1. Decisiones de Arquitectura (ADRs)

### Decisión D1: Columna `BggWeight` en la Entidad `Game`
* **Contexto:** La dureza es un atributo de primer nivel del juego que afecta tanto al filtrado como a la ordenación del catálogo.
* **Diseño:**
  1. En `Ludeka.Core.Entities.Game`:
     ```csharp
     public double? BggWeight { get; private set; }
     ```
  2. Invariante: Si se asigna un valor, debe normalizarse:
     ```csharp
     public void UpdateBggWeight(double? weight)
     {
         if (!weight.HasValue || weight.Value <= 0.0)
         {
             BggWeight = null;
             return;
         }
         BggWeight = Math.Round(Math.Clamp(weight.Value, 1.0, 5.0), 2);
     }
     ```
  3. Mapeo en EF Core:
     * Columna nullable `BggWeight` (`REAL` en SQLite, `double precision` en PostgreSQL).

### Decisión D2: Función Canónica de Extrapolación de Complejidad
* **Contexto:** Actualmente existen dos implementaciones duplicadas de la heurística de complejidad: `SqliteGameRepository.CalculateComplexity` y `GameDetail.GetComplexityShortText`.
* **Diseño:**
  1. Se ubica en `Ludeka.Core.Helpers.ComplexityCalculator` (o método estático accesible):
     ```csharp
     public static class ComplexityCalculator
     {
         public const double LightThreshold = 2.20;
         public const double HeavyThreshold = 3.25;

         public static GameComplexity Calculate(double? bggWeight, GameStyle style, int maxMinutes, int communityAge)
         {
             if (bggWeight.HasValue && bggWeight.Value > 0)
             {
                 if (bggWeight.Value < LightThreshold)
                     return GameComplexity.Light;
                 if (bggWeight.Value >= HeavyThreshold)
                     return GameComplexity.Heavy;
                 return GameComplexity.Medium;
             }

             // Heurística de respaldo (fallback cuando no hay votos BGG)
             if (style == GameStyle.PartyGame || style == GameStyle.FillerAbstract || (maxMinutes <= 30 && communityAge <= 10))
                 return GameComplexity.Light;
             if (maxMinutes >= 120 || communityAge >= 14 || (maxMinutes >= 90 && style == GameStyle.Eurogame))
                 return GameComplexity.Heavy;
             return GameComplexity.Medium;
         }

         public static GameComplexity Calculate(Game game)
         {
             return Calculate(game.BggWeight, game.Style, game.Duration?.MaxMinutes ?? 0, game.Age?.CommunityAge ?? 0);
         }
     }
     ```
  2. Esto garantiza que UI, backend y tests compartan un único contrato de extrapolación.

### Decisión D3: Extracción en `BggXmlParser`
* **Contexto:** BGG XMLAPI2 devuelve `<averageweight value="X.XX" />` dentro de `<statistics><ratings>`.
* **Diseño:**
  * En `BggXmlParser.ParseStatistics(XElement item)`:
    ```csharp
    double? bggWeight = null;
    if (double.TryParse(stats.Element("averageweight")?.Attribute("value")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double weight) && weight > 0)
    {
        bggWeight = Math.Round(Math.Clamp(weight, 1.0, 5.0), 2);
    }
    return (rating, rank, bggWeight);
    ```

### Decisión D4: Ordenación Matemática Continua en Catálogo
* **Contexto:** Al ordenar por complejidad, los juegos deben ordenarse de forma granular por su valor decimal, no agrupados en tres bloques homogéneos.
* **Diseño:**
  * En `SqliteGameRepository.ApplyQuerySorting`:
    ```csharp
    GameSortOrder.ComplexityAsc => query
        .OrderBy(g => g.BggWeight.HasValue ? 0 : 1)
        .ThenBy(g => g.BggWeight)
        .ThenBy(g => g.BggRank.HasValue ? 0 : 1)
        .ThenBy(g => g.BggRank ?? int.MaxValue),

    GameSortOrder.ComplexityDesc => query
        .OrderBy(g => g.BggWeight.HasValue ? 0 : 1)
        .ThenByDescending(g => g.BggWeight)
        .ThenBy(g => g.BggRank.HasValue ? 0 : 1)
        .ThenBy(g => g.BggRank ?? int.MaxValue),
    ```
  * En la paginación en dos fases (`ApplyIndexSorting`), se incluye `g.BggWeight` en `GameFilterIndexItem` y se replica el mismo ordenamiento preciso.

### Decisión D5: Backfill Idempotente desde `BggRawSnapshots`
* **Contexto:** Miles de juegos ya tienen su snapshot crudo descargado en la tabla `BggRawSnapshots`.
* **Diseño:**
  * Se implementa un método `BackfillBggWeightsFromSnapshotsAsync` que lee los snapshots en lotes, extrae el campo `averageweight` del JSON estructurado y actualiza `BggWeight` en la entidad `Game` mediante una consulta eficiente sin llamadas a red.
