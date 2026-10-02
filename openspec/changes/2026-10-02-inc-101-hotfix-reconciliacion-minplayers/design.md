# Diseño Técnico: Saneamiento Defensivo de Escalabilidad y Blindaje de Reconciliación

## 1. Dominio (`Game.cs`)
```csharp
public void UpdateScalability(IEnumerable<ScalabilityEntry> scalability)
{
    ArgumentNullException.ThrowIfNull(scalability);
    Scalability.Clear();
    Scalability.AddRange(scalability.Where(s => s.PlayerCount > 0));
}
```

## 2. Repositorio (`SqliteGameRepository.cs`)
```csharp
var validPlayerCounts = game.Scalability.Where(s => s.PlayerCount > 0).Select(s => s.PlayerCount).ToList();
int minPlayers = validPlayerCounts.Count > 0 ? Math.Max(1, validPlayerCounts.Min()) : 1;
int maxPlayers = validPlayerCounts.Count > 0 ? Math.Max(minPlayers, validPlayerCounts.Max()) : Math.Max(minPlayers, 4);

existing.UpdateCatalogInformation(..., minPlayers, maxPlayers);
existing.UpdateScalability(game.Scalability.Where(s => s.PlayerCount > 0));
```

## 3. Servicio de Reconciliación (`BggRawSnapshotSyncService.cs`)
```csharp
foreach (var g in gamesToUpdate)
{
    try
    {
        await _gameRepo.UpdateAsync(g, ct);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Error al actualizar juego ID {GameId} ({SpanishTitle}) durante la reconciliación de expansiones. Omitiendo este juego para continuar el proceso.", g.Id, g.SpanishTitle);
    }
}
```
