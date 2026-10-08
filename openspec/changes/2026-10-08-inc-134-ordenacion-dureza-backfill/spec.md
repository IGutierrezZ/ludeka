# Especificación: INC-134 — Backfill Autónomo de Dureza BGG, Extrapolación de Peso Efectivo y Ordenación Resiliente en Catálogo

## Requerimientos Funcionales y de Dominio

### REQ-134-1: Cálculo Canónico de Peso Efectivo
- `ComplexityCalculator.GetEffectiveWeight(double? bggWeight, GameStyle style, int maxMinutes, int communityAge)` debe retornar `bggWeight.Value` cuando éste sea no nulo y mayor a 0.
- Ante `bggWeight == null` o `<= 0`, debe calcular el peso extrapolado:
  - Si la heurística evalúa a `GameComplexity.Light`: devolver `1.60`.
  - Si evalúa a `GameComplexity.Heavy`: devolver `3.80`.
  - Si evalúa a `GameComplexity.Medium`: devolver `2.70`.
- Sobrecarga `ComplexityCalculator.GetEffectiveWeight(Game? game)`.

### REQ-134-2: Ordenación Continua y Resiliente en Catálogo
- `ApplyIndexSorting`: La ordenación `ComplexityAsc` debe ordenar ascendentemente por `GetEffectiveWeight(...)`, desempatando por `BggRank` y nombre. `ComplexityDesc` debe ordenar descendentemente por dicho peso efectivo.
- `ApplyQuerySorting`: En consultas directas de base de datos, proyectar el cálculo condicional del peso efectivo para evitar agrupar nulos al final.

### REQ-134-3: Backfill Autónomo en el Pipeline de Arranque
- `Program.cs` debe invocar `BackfillBggWeightsFromSnapshotsAsync` dentro del bloque de inicialización de base de datos, asegurando que todos los juegos con snapshots existentes actualicen su `BggWeight` en SQLite y PostgreSQL.

### REQ-134-4: Presentación de Dureza en Tarjetas de Catálogo
- `GameCard.razor` y `GameListItem.razor` deben incluir la información de dureza de manera accesible y armoniosa con los metadatos existentes de valoración, jugadores y duración.
