# Documento de Diseño Técnico: change-30-played-independent-status

## 1. Arquitectura General y Capas

Este diseño implementa el desacoplamiento de la experiencia de juego (*"Jugado"*), la supresión del estado ambiguo *"Deseado"* (fusionándolo en *"Comprar"* con radar de notificaciones), el blindaje de las valoraciones condicionadas a partidas jugadas, y el nuevo subsistema de **Diario y Registro de Partidas** (`GamePlayLog`).

---

## 2. Capa de Dominio (`Ludeka.Core`)

### 2.1 Enumerado `CollectionStatus`
```csharp
namespace Ludeka.Core.Enums;

public enum CollectionStatus
{
    InCollection = 1, // 🟢 En mi ludoteca (físico propio)
    Played = 2,       // 🔵 Jugado (mantenido por compatibilidad histórica)
    Wishlist = 3,     // [Obsoleto / Fusionado a WantToBuy en migración]
    WantToBuy = 4     // 🛒 Comprar / Deseo de compra (radar de sorteos, descuentos y reimpresiones)
}
```

### 2.2 Entidad `UserCollectionItem`
```csharp
namespace Ludeka.Core.Entities;

public class UserCollectionItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string UserId { get; private set; } = string.Empty;
    public Guid? GameId { get; private set; }
    public CollectionStatus? Status { get; private set; }
    public bool IsPlayed { get; private set; }
    public DateTimeOffset AddedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; private set; }

    // BGG queue properties
    public int? BggId { get; private set; }
    public string? PendingTitle { get; private set; }
    public string? PendingThumbnailUrl { get; private set; }
    public int? PendingYearPublished { get; private set; }

    public bool IsPendingCataloging => GameId == null;
    public Game? Game { get; private set; }

    private UserCollectionItem() { }

    public UserCollectionItem(string userId, Guid gameId, CollectionStatus? status, bool isPlayed = false)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("El identificador de usuario no puede estar vacío.", nameof(userId));
        if (gameId == Guid.Empty)
            throw new ArgumentException("El identificador del juego no puede ser un GUID vacío.", nameof(gameId));
        if (!status.HasValue && !isPlayed)
            throw new InvalidOperationException("Un ítem de colección debe tener un estado de posesión o estar marcado como jugado.");

        UserId = userId.Trim();
        GameId = gameId;
        Status = status;
        IsPlayed = isPlayed;
        AddedAt = DateTimeOffset.UtcNow;
    }

    public void ChangeStatus(CollectionStatus? newStatus)
    {
        Status = newStatus;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetPlayed(bool isPlayed)
    {
        IsPlayed = isPlayed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void TogglePlayed()
    {
        IsPlayed = !IsPlayed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
```

### 2.3 Nueva Entidad `GamePlayLog`
```csharp
namespace Ludeka.Core.Entities;

public class GamePlayLog
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string UserId { get; private set; } = string.Empty;
    public Guid GameId { get; private set; }
    public DateTimeOffset PlayDate { get; private set; }
    public string Location { get; private set; } = string.Empty;
    public int PlayerCount { get; private set; }
    public int? DurationMinutes { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public Game? Game { get; private set; }

    private GamePlayLog() { }

    public GamePlayLog(string userId, Guid gameId, DateTimeOffset playDate, string location, int playerCount, int? durationMinutes = null, string? comment = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("El usuario no puede estar vacío.", nameof(userId));
        if (gameId == Guid.Empty)
            throw new ArgumentException("El ID del juego no puede ser un GUID vacío.", nameof(gameId));
        if (playerCount < 1)
            throw new ArgumentException("El número de jugadores debe ser al menos 1.", nameof(playerCount));

        UserId = userId.Trim();
        GameId = gameId;
        PlayDate = playDate;
        Location = string.IsNullOrWhiteSpace(location) ? "En casa" : location.Trim();
        PlayerCount = playerCount;
        DurationMinutes = durationMinutes;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
```

---

## 3. Capa de Aplicación (`Ludeka.Application`)

### 3.1 DTOs
```csharp
public record UserCollectionItemDto(
    Guid Id,
    Guid? GameId,
    string GameTitle,
    string? GameCoverUrl,
    string GameSlug,
    CollectionStatus? Status,
    bool IsPlayed,
    DateTimeOffset AddedAt,
    bool IsCurrentlyLoaned,
    int? BggId = null,
    bool IsPendingCataloging = false,
    bool IsExpansion = false
);

public record GamePlayLogDto(
    Guid Id,
    Guid GameId,
    string GameTitle,
    string? GameCoverUrl,
    string GameSlug,
    DateTimeOffset PlayDate,
    string Location,
    int PlayerCount,
    int? DurationMinutes,
    string? Comment,
    DateTimeOffset CreatedAt
);

public record RecordPlayRequest(
    Guid GameId,
    DateTimeOffset PlayDate,
    string Location,
    int PlayerCount,
    int? DurationMinutes = null,
    string? Comment = null
);

public record UserPlaysStatsDto(
    int TotalPlays,
    string? MostPlayedGameTitle,
    int MostPlayedGameCount,
    string? FavoriteLocation,
    int? MostCommonPlayerCount,
    int PlaysThisMonth
);
```

### 3.2 Contratos de Repositorio y Servicio
- `IGamePlayLogRepository`:
  - `Task AddAsync(GamePlayLog play, CancellationToken ct = default);`
  - `Task DeleteAsync(Guid id, CancellationToken ct = default);`
  - `Task<List<GamePlayLog>> GetByUserIdAsync(string userId, CancellationToken ct = default);`
  - `Task<List<GamePlayLog>> GetByUserAndGameAsync(string userId, Guid gameId, CancellationToken ct = default);`
  - `Task<int> GetCountByUserAsync(string userId, CancellationToken ct = default);`
  - `Task<int> GetCountByUserAndGameAsync(string userId, Guid gameId, CancellationToken ct = default);`
- `IGamePlayLogService`:
  - `Task<GamePlayLogDto> RecordPlayAsync(RecordPlayRequest request, CancellationToken ct = default);`
  - `Task<List<GamePlayLogDto>> GetUserPlaysAsync(string? userId = null, CancellationToken ct = default);`
  - `Task<List<GamePlayLogDto>> GetGamePlaysAsync(Guid gameId, string? userId = null, CancellationToken ct = default);`
  - `Task<UserPlaysStatsDto> GetUserPlaysStatsAsync(string? userId = null, CancellationToken ct = default);`
  - `Task DeletePlayAsync(Guid id, CancellationToken ct = default);`

### 3.3 Regla de Valoración en `UserLibraryService`
```csharp
public async Task<UserReviewDto> SubmitReviewAsync(SubmitReviewRequest request, CancellationToken ct = default)
{
    string userId = _currentUserService.UserId;
    var collectionItem = await _collectionRepo.GetByUserAndGameAsync(userId, request.GameId, ct);

    if (collectionItem == null || !collectionItem.IsPlayed)
    {
        throw new InvalidOperationException("Solo puedes valorar juegos que hayas jugado («Jugado»).");
    }
    // ... resto de la lógica de guardado y recálculo
}
```

---

## 4. Persistencia y Migraciones (`Ludeka.Infrastructure`)

### 4.1 Reconciliación en `SqliteSchemaMigrator`
```sql
-- 1. Añadir columna IsPlayed a UserCollectionItems si no existe
ALTER TABLE "UserCollectionItems" ADD COLUMN "IsPlayed" INTEGER NOT NULL DEFAULT 0;

-- 2. Migrar registros históricos donde Status era 2 (Played)
UPDATE "UserCollectionItems" SET "IsPlayed" = 1 WHERE "Status" = 2;
UPDATE "UserCollectionItems" SET "Status" = NULL WHERE "Status" = 2;

-- 3. Migrar registros históricos con Wishlist (3) a WantToBuy (4)
UPDATE "UserCollectionItems" SET "Status" = 4 WHERE "Status" = 3;

-- 4. Crear tabla GamePlayLogs si no existe
CREATE TABLE IF NOT EXISTS "GamePlayLogs" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_GamePlayLogs" PRIMARY KEY,
    "UserId" TEXT NOT NULL,
    "GameId" TEXT NOT NULL,
    "PlayDate" TEXT NOT NULL,
    "Location" TEXT NOT NULL,
    "PlayerCount" INTEGER NOT NULL,
    "DurationMinutes" INTEGER NULL,
    "Comment" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_GamePlayLogs_Games_GameId" FOREIGN KEY ("GameId") REFERENCES "Games" ("Id") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS "IX_GamePlayLogs_UserId_PlayDate" ON "GamePlayLogs" ("UserId", "PlayDate");
CREATE INDEX IF NOT EXISTS "IX_GamePlayLogs_GameId" ON "GamePlayLogs" ("GameId");
```

---

## 5. Componentes de Interfaz de Usuario (`Ludeka.Web`)

1. **`CollectionActionBar.razor`:**
   - 3 botones principales:
     - `📚 En mi ludoteca` (toggle de posesión)
     - `🎲 Jugado` (toggle ortogonal de partida jugada)
     - `🛒 Comprar` (toggle de compra con aviso de radar de sorteos/ofertas)
   - Botón contextual secundario: `[ ➕ Registrar Partida ]`
   - Si `WantToBuy` está activo, se muestra la píldora informativa: *"🔔 Radar activo: te avisaremos de sorteos, ofertas y reimpresiones"*.
2. **`RecordPlayModal.razor`:**
   - Modal táctil para registrar partida: fecha (selector rápido: hoy / ayer / otra), lugar (chips: Casa, Club, Bar, BGA, Otro), número de jugadores (selector 1 a 8+), duración y comentarios.
3. **`GameDetail.razor`:**
   - Visualización de partidas registradas del usuario.
   - Bloqueo visual con tooltip en el botón de valorar si el juego no está jugado.
4. **`MyLibrary.razor`:**
   - Nueva pestaña `📝 Diario de Partidas` con estadísticas y timeline de partidas.
   - Pestaña `🛒 Comprar` consolidada sin la redundancia de deseos.
