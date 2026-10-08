# Diseño Técnico: INC-136 — Refresco de Versiones BGG de Novedades, Soporte Editorial Lúdilo y Saneamiento de Catálogo

## 1. Arquitectura y Componentes Afectados

```
┌────────────────────────────────────────────────────────┐
│  RegionalPublisherMatcher (Ludeka.Infrastructure)      │
│  - Añadir Lúdilo a KnownPublishers (ES, ludilo)        │
└──────────────────────────┬─────────────────────────────┘
                           │
┌──────────────────────────▼─────────────────────────────┐
│  CatalogDataSanitizer (Ludeka.Infrastructure)          │
│  - EnsureKnownPriorityGamesRepairedAsync:              │
│    BggId 453526 -> "Código 5" / "Lúdilo"               │
└──────────────────────────┬─────────────────────────────┘
                           │
┌──────────────────────────▼─────────────────────────────┐
│  IBggRawSnapshotRepository / SqliteBggRawSnapshotRepo  │
│  - GetBggIdsNeedingVersionRefreshAsync(minYear, limit) │
└──────────────────────────┬─────────────────────────────┘
                           │
┌──────────────────────────▼─────────────────────────────┐
│  BggRawSnapshotSyncService (Ludeka.Infrastructure)     │
│  - Integrar candidatos de refresco en sincronización   │
└────────────────────────────────────────────────────────┘
```

## 2. Decisiones de Diseño

### A. Soporte Editorial en `RegionalPublisherMatcher`
En `src/Ludeka.Infrastructure/Bgg/RegionalPublisherMatcher.cs`:
```csharp
new("Lúdilo", "ES", "Lúdilo", "ludilo"),
new("Ludilo", "ES", "Lúdilo", "ludilo"),
new("Lúdilo Games", "ES", "Lúdilo", "ludilo"),
new("Ludilo Games", "ES", "Lúdilo", "ludilo"),
```
Garantiza que tanto la versión BGG como los enlaces del juego raíz mapeen a `Lúdilo` con slug `ludilo`.

### B. Reparación Determinista en `CatalogDataSanitizer`
En `EnsureKnownPriorityGamesRepairedAsync`:
```csharp
var gotFive = await context.Games.FirstOrDefaultAsync(g => g.BggId == 453526, ct);
if (gotFive != null)
{
    bool updated = false;
    if (gotFive.SpanishTitle != "Código 5")
    {
        gotFive.UpdateSpanishTitle("Código 5");
        updated = true;
    }
    if (gotFive.SpanishPublisher != "Lúdilo")
    {
        gotFive.UpdateSpanishPublisher("Lúdilo");
        updated = true;
    }
    if (updated)
    {
        await context.SaveChangesAsync(ct);
        logger.LogInformation("Saneamiento prioritario: Juego BggId 453526 actualizado a 'Código 5' (Lúdilo).");
    }
}
```

### C. Consulta de Refresco en `IBggRawSnapshotRepository`
Firma:
```csharp
Task<IReadOnlyList<int>> GetBggIdsNeedingVersionRefreshAsync(int minYear = 2025, int limit = 50, CancellationToken ct = default);
```
En SQLite / Npgsql:
Selecciona snapshots donde `versions` existe pero no contiene ningún enlace de idioma español (`"language"` con valor `"Spanish"`, `"Español"` o `"Castellano"`), limitando a juegos recientes donde el año de publicación sea `>= minYear`.

## 3. Estrategia de Pruebas
1. `RegionalPublisherMatcherTests`: Verificar que `"Lúdilo"` y `"Ludilo"` se resuelven a `OfficialName = "Lúdilo"` y `Slug = "ludilo"`.
2. `CatalogDataSanitizerTests`: Verificar que un juego con `BggId = 453526`, `SpanishTitle = "Got Five!"` y `SpanishPublisher = "Asmodee Ibérica"` es saneado a `"Código 5"` y `"Lúdilo"`.
3. `BggRawSnapshotParserVersionsTests`: Verificar la extracción de `BggSpanishVersionInfoDto` para `"Código 5 - Spanish edition (2026)"` con editorial `"Lúdilo"`.
