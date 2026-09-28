# Diseño Técnico: INC-78 — Barrido Completo de Calidad de Catálogo (~10.000 Juegos Promovidos)

## 1. Contratos y DTOs

### `BggQualitySweepBatchResultDto`
```csharp
namespace Ludeka.Application.Features.Bgg;

public sealed record BggQualitySweepBatchResultDto(
    int EvaluatedCount,
    int UpdatedCount,
    int SkippedCount,
    int FailedCount,
    int LastBggIdProcessed,
    bool HasMore,
    string Message
);
```

### Contrato en `IGameRepository`
```csharp
namespace Ludeka.Application.Contracts;

public interface IGameRepository
{
    // Métodos existentes...
    
    Task<IReadOnlyList<Game>> GetGamesCursorPagedAsync(int afterBggId, int limit = 50, CancellationToken ct = default);
    Task<int> GetTotalCatalogCountAsync(CancellationToken ct = default);
}
```

### Contrato en `IBggMassIngestionService`
```csharp
namespace Ludeka.Application.Contracts;

public interface IBggMassIngestionService
{
    // Métodos existentes...

    Task<BggQualitySweepBatchResultDto> SweepCatalogQualityBatchAsync(int afterBggId = 0, int batchSize = 50, CancellationToken ct = default);
    Task<BggQualitySweepBatchResultDto> RunScheduledSweepCatalogQualityBatchAsync(int afterBggId = 0, int batchSize = 50, CancellationToken ct = default);
}
```

---

## 2. Implementación en Repositorio (`SqliteGameRepository`)

```csharp
public async Task<IReadOnlyList<Game>> GetGamesCursorPagedAsync(int afterBggId, int limit = 50, CancellationToken ct = default)
{
    if (limit <= 0) limit = 50;

    await using var scope = await CreateScopeAsync(ct);
    return await scope.Context.Games
        .AsNoTracking()
        .Where(g => g.BggId > afterBggId)
        .OrderBy(g => g.BggId)
        .Take(limit)
        .ToListAsync(ct);
}

public async Task<int> GetTotalCatalogCountAsync(CancellationToken ct = default)
{
    await using var scope = await CreateScopeAsync(ct);
    return await scope.Context.Games.CountAsync(ct);
}
```

---

## 3. Lógica del Barrido en `BggMassIngestionService`

```csharp
private async Task<BggQualitySweepBatchResultDto> ExecuteSweepCatalogQualityBatchAsync(int afterBggId, int batchSize, CancellationToken ct)
{
    var games = await _gameRepo.GetGamesCursorPagedAsync(afterBggId, batchSize, ct);
    if (games.Count == 0)
    {
        return new BggQualitySweepBatchResultDto(0, 0, 0, 0, afterBggId, false, "Barrido finalizado: no hay más títulos.");
    }

    int evaluated = games.Count;
    int updated = 0;
    int skipped = 0;
    int failed = 0;
    int maxBggId = afterBggId;

    foreach (var game in games)
    {
        if (ct.IsCancellationRequested) break;
        if (game.BggId > maxBggId) maxBggId = game.BggId;

        try
        {
            bool modified = await EnrichSingleGameQualityAsync(game, ct);
            if (modified)
            {
                await _gameRepo.UpdateAsync(game, ct);
                updated++;
            }
            else
            {
                skipped++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error en barrido de calidad para #{BggId} ('{Title}'): {Message}", game.BggId, game.SpanishTitle, ex.Message);
            failed++;
        }
    }

    bool hasMore = evaluated == batchSize;
    string msg = $"Lote de barrido completado: {evaluated} evaluados, {updated} actualizados, {skipped} sin cambios, {failed} fallidos. Último BggId: {maxBggId}.";
    return new BggQualitySweepBatchResultDto(evaluated, updated, skipped, failed, maxBggId, hasMore, msg);
}
```

---

## 4. UI en `CatalogQueueAdmin.razor`

- Nuevo botón violeta: `Barrido Completo (~@_totalCatalogCount juegos)`.
- Estado reactivo `_isContinuousSweepActive`, `_continuousSweepMessage`, `_sweepEvaluatedCount`, `_sweepUpdatedCount`, `_sweepSkippedCount`.
- Bucle de fondo con `CancellationTokenSource`.
