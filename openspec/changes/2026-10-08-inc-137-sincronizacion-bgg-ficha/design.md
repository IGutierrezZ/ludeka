# Diseño Técnico: INC-137 Forzar Sincronización BGG desde Ficha de Juego

## 1. Arquitectura y Componentes Afectados

```
[ GameStaffToolsPanel / GameDetail.razor ]
                  │
                  ▼ (Invoca método Blazor)
         [ IGameEditorService ]
                  │
      ┌───────────┴──────────────┬────────────────────────┐
      ▼                          ▼                        ▼
[ IBggClient ]       [ IBggRawSnapshotRepository ]  [ IGameRepository ]
(Fetch XML thing     (Upsert snapshot fresco)       (UpdateAsync entidad)
 con &versions=1)                 │                        │
                                  ▼                        ▼
                       [ BggRawSnapshotParser ]    [ IAuditService / Log ]
                       (SpanishVersionInfo,        (Auditoría y caché)
                        RootImages, EAN, Matcher)
```

## 2. Contratos y DTOs

### `GameBggSyncResultDto`
```csharp
namespace Ludeka.Application.DTOs;

public record GameBggSyncResultDto(
    bool Success,
    int BggId,
    string? OldSpanishTitle,
    string? NewSpanishTitle,
    string? OldSpanishPublisher,
    string? NewSpanishPublisher,
    string? OldEan,
    string? NewEan,
    string? CoverImageUrl,
    string? ThumbnailUrl,
    IReadOnlyList<string> UpdatedFields,
    string Message
);
```

### Extensión en `IGameEditorService`
```csharp
Task<GameBggSyncResultDto> ForceSyncFromBggAsync(
    Guid gameId,
    CancellationToken ct = default);
```

## 3. Implementación en `GameEditorService`
- Inyección de dependencias ya existentes o requeridas:
  - `IBggClient _bggClient`
  - `IBggRawSnapshotRepository _snapshotRepo`
- Flujo:
  1. Comprobación de seguridad mediante `SessionIdentity.Require(_currentUserService)` y validación de `IsFoundingTeam` o `CanEditGames`.
  2. Carga de `Game` desde `_gameRepository.GetByIdAsync(gameId, ct)`.
  3. Comprobación de `game.BggId > 0`.
  4. Descarga `var xml = await _bggClient.FetchRawThingXmlAsync(game.BggId, includeVersions: true, ct)`.
  5. Parseo XML -> JSON y upsert en `_snapshotRepo`.
  6. Análisis de `SpanishVersionInfo` mediante `BggRawSnapshotParser`:
     - Título: actualización si `vInfo.Title` es válido y difiere.
     - Editorial: resolución mediante `RegionalPublisherMatcher.Match(vInfo.Publisher)` o fallback del nombre oficial de la versión; actualización si difiere.
     - EAN: actualización si `vInfo.Ean` es válido y difiere.
     - Carátula: si `vInfo.CoverImageUrl` o la raíz de BGG aportan imagen y la actual es nula o vacía (o si la versión española trae portada dedicada).
  7. Si hubo modificaciones:
     - `await _gameRepository.UpdateAsync(game, ct)`.
     - Creación de `GameEditLog` con resumen legible.
     - `_auditService.RecordChangeAsync(...)`.
     - Invalidation en `_catalogService`.
  8. Devolución de `GameBggSyncResultDto`.

## 4. Diseño de Interfaz de Usuario
- **Panel Flotante `GameStaffToolsPanel.razor`:**
  - Botón:
    ```razor
    <button type="button"
            @onclick="HandleForceSyncBgg"
            disabled="@(IsSyncingBgg || Game.BggId <= 0)"
            class="w-full h-9 px-3 rounded-xl border border-[var(--line)] bg-[var(--paper-2)] hover:bg-[var(--paper)] text-[var(--ink)] flex items-center gap-2 transition-colors disabled:opacity-50">
        @if (IsSyncingBgg)
        {
            <span class="animate-spin"><Icon Name="loader" Size="14" /></span>
            <span>Sincronizando BGG...</span>
        }
        else
        {
            <Icon Name="refresh-cw" Size="14" />
            <span>Sincronizar BGG (@Game.BggId)</span>
        }
    </button>
    ```
- **Ficha `GameDetail.razor`:**
  - En la botonera staff en línea (junto a "Editar Ficha"), botón complementario con icono `refresh-cw` y texto "Sincronizar BGG".
  - Manejador `HandleForceSyncBgg`:
    - Captura de estado `_isSyncingBgg = true`.
    - Ejecuta `var result = await GameEditorService.ForceSyncFromBggAsync(Game.Id);`.
    - Si `result.Success`:
      - Notificación en pantalla con los campos cambiados (o aviso de que la ficha ya estaba completamente sincronizada).
      - Recarga de `Game` en memoria para refrescar instantáneamente los textos, badges y carátulas de la página.
