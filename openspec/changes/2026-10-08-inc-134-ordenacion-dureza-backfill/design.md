# Diseño Técnico: INC-134 — Backfill Autónomo de Dureza BGG, Extrapolación de Peso Efectivo y Ordenación Resiliente en Catálogo

## 1. Arquitectura y Capas Impactadas

```
[ Ludeka.Core ] 
    └── Helpers/ComplexityCalculator.cs (GetEffectiveWeight)

[ Ludeka.Infrastructure ]
    ├── Data/SqliteGameRepository.cs (ApplyQuerySorting & ApplyIndexSorting resilientes con GetEffectiveWeight)
    └── Data/SqliteSchemaMigrator.cs (Llamada opcional / backfill)

[ Ludeka.Web ]
    ├── Program.cs (Invocación autónoma de BackfillBggWeightsFromSnapshotsAsync en arranque)
    └── Components/Shared/GameCard.razor & GameListItem.razor (Presentación de dureza)
```

## 2. Decisiones de Diseño

### Decisión 1: Peso Efectivo Continuo en `ComplexityCalculator`
Se introduce en `ComplexityCalculator`:
```csharp
public static double GetEffectiveWeight(double? bggWeight, GameStyle style, int maxMinutes, int communityAge)
{
    if (bggWeight.HasValue && bggWeight.Value > 0)
    {
        return bggWeight.Value;
    }

    var qualitative = Calculate(null, style, maxMinutes, communityAge);
    return qualitative switch
    {
        GameComplexity.Light => 1.60,
        GameComplexity.Heavy => 3.80,
        _ => 2.70
    };
}
```
Esto garantiza que todo juego posea un valor monotónico ordenable, alineado con los umbrales comunitarios (< 2.20 Ligero, 2.20 - 3.25 Medio, ≥ 3.25 Duro).

### Decisión 2: Ordenación SQL y en Memoria Resiliente
En `ApplyIndexSorting`:
Se utiliza `ComplexityCalculator.GetEffectiveWeight(g.BggWeight, g.Style, g.Duration.MaxMinutes, g.Age.CommunityAge)` para ordenar en lugar de `g.BggWeight`.

En `ApplyQuerySorting`:
Para consultas directas en base de datos:
```csharp
GameSortOrder.ComplexityAsc => query
    .OrderBy(g => g.BggWeight ?? (
        (g.Style == GameStyle.PartyGame || g.Style == GameStyle.FillerAbstract || (g.Duration.MaxMinutes <= 30 && g.Age.CommunityAge <= 10)) ? 1.60 :
        (g.Duration.MaxMinutes >= 120 || g.Age.CommunityAge >= 14 || (g.Duration.MaxMinutes >= 90 && g.Style == GameStyle.Eurogame)) ? 3.80 : 2.70
    ))
    .ThenBy(g => g.BggRank.HasValue ? 0 : 1)
    .ThenBy(g => g.BggRank ?? int.MaxValue),
```
Y análogo descendente para `ComplexityDesc`. Esto traduce limpiamente a un `CASE WHEN` SQL tanto en SQLite como en PostgreSQL, erradicando el problema de enviar todos los nulos al final agrupados por `BggRank`.

### Decisión 3: Backfill Autónomo en Arranque (`Program.cs`)
En `Program.cs`, tras la inicialización del esquema y semillado:
```csharp
var gameRepo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
await gameRepo.BackfillBggWeightsFromSnapshotsAsync();
```
El método ya es idempotente (`g.BggWeight == null`) y procesa en lotes de 250 elementos sin degradar la memoria ni bloquear el arranque.

### Decisión 4: Visualización Editorial en Catálogo
En `GameCard.razor` y `GameListItem.razor`, incluir la indicación de dureza compacta:
- Icono `feather` (Ligero) o `shield-alert` (Duro) o `dumbbell` / peso decimal si tiene `BggWeight` o etiqueta `Ligero`, `Medio`, `Duro`.
- Cumplimiento WCAG 2.2 AA y preservación de estética de Revista Lúdica.
