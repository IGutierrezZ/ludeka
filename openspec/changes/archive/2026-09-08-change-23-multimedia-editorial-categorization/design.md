# Diseño Técnico y Arquitectura: change-23-multimedia-editorial-categorization

## 1. Diagrama de Arquitectura y Componentes

```mermaid
graph TD
    subgraph UI[Capa de Presentación: Ludeka.Web]
        ModAdmin[MediaModeration.razor (/admin/multimedia)]
        GamePage[GameDetail.razor (/juegos/slug)]
        MediaHubUI[MultimediaHub.razor (Pestañas + [⚙️ Moderar])]
        GamePage --> MediaHubUI
    end

    subgraph App[Capa de Aplicación: Ludeka.Application]
        MediaSvc[IMediaService / MediaService]
        AuditSvc[IAuditService / AuditService]
        AuthSvc[ICurrentUserService (CanApproveMedia)]
        CatalogSvc[ICatalogService (Búsqueda de juegos)]
    end

    subgraph Core[Capa de Dominio: Ludeka.Core]
        MediaItem[MediaItem (Category, ChangeCategory, ReassignGame)]
        MediaCategory[enum MediaCategory (QuickOverview, Tutorial, Gameplay, ReviewOpinion)]
        Classifier[MediaClassifier.Classify]
    end

    subgraph Infra[Capa de Infraestructura: Ludeka.Infrastructure]
        MediaRepo[SqliteMediaRepository]
        DbCtx[LudekaDbContext (MediaItems Table)]
        Migrator[SqliteSchemaMigrator]
        YouTubeSvc[YouTubeSearchService]
    end

    ModAdmin --> MediaSvc
    MediaHubUI --> MediaSvc
    MediaHubUI --> CatalogSvc
    MediaSvc --> AuthSvc
    MediaSvc --> AuditSvc
    MediaSvc --> MediaRepo
    MediaSvc --> MediaItem
    MediaItem --> MediaCategory
    ModAdmin --> Classifier
    YouTubeSvc --> Classifier
    MediaRepo --> DbCtx
    Migrator --> DbCtx
```

---

## 2. Dominio (`Ludeka.Core`)

### 2.1 Enum `MediaCategory`
```csharp
namespace Ludeka.Core.Enums;

public enum MediaCategory
{
    QuickOverview = 0, // "Cómo Funciona" / Vistazo de mecánicas en 2 minutos
    Tutorial = 1,      // Explicación completa de reglas paso a paso
    Gameplay = 2,      // Partida completa o demostrativa con número de comensales
    ReviewOpinion = 3  // Reseña, análisis crítico o primeras impresiones
}
```

### 2.2 Clasificador Heurístico `MediaClassifier`
```csharp
namespace Ludeka.Core.Helpers;

public static class MediaClassifier
{
    public static MediaCategory Classify(string title, string? description = null);
}
```
Patrones evaluados:
1. `QuickOverview`: "cómo funciona", "como funciona", "vistazo rápido", "vistazo rapido", "en 2 minutos", "en 3 minutos", "overview", "quick look", "resumen de mecánicas".
2. `Gameplay`: "partida", "gameplay", "jugando", "playthrough", "let's play", "a 2", "a dos", "duelo", "en solitario".
3. `ReviewOpinion`: "reseña", "resena", "opinión", "opinion", "análisis", "analisis", "primeras impresiones", "vale la pena", "merece la pena", "review", "crítica", "critica", "unboxing", "abriendo".
4. `Tutorial`: "cómo jugar", "como jugar", "tutorial", "aprende a jugar", "reglas", "explicación", "how to play", "rules".
5. Fallback por defecto: `MediaCategory.Tutorial`.

### 2.3 Entidad `MediaItem`
- Propiedad: `public MediaCategory Category { get; private set; }`
- Métodos de mutación:
  ```csharp
  public void ChangeCategory(MediaCategory newCategory)
  {
      Category = newCategory;
      UpdatedAt = DateTimeOffset.UtcNow;
  }

  public void ReassignGame(Guid newGameId)
  {
      if (newGameId == Guid.Empty)
          throw new ArgumentException("El ID del nuevo juego no puede ser vacío.", nameof(newGameId));

      GameId = newGameId;
      UpdatedAt = DateTimeOffset.UtcNow;
  }
  ```
- Compatibilidad en constructores: si `category` no se provee, se mapea automáticamente desde `Type`:
  - `MediaType.QuickOverview` -> `MediaCategory.QuickOverview`
  - `MediaType.Tutorial` -> `MediaCategory.Tutorial`
  - `MediaType.Playthrough` -> `MediaCategory.Gameplay`
  - `MediaType.InstagramPost` o `ShortReel` -> `MediaCategory.ReviewOpinion`

---

## 3. Capa de Aplicación (`Ludeka.Application`)

### 3.1 Contrato `IMediaService`
```csharp
namespace Ludeka.Application.Contracts;

public interface IMediaService
{
    Task<GameMediaHubDto> GetGameMediaAsync(Guid gameId, CancellationToken ct = default);
    Task<IReadOnlyList<MediaItemDto>> GetPendingModerationAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MediaItemDto>> GetOrphansAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MediaItemDto>> GetApprovedAsync(CancellationToken ct = default);
    Task<MediaItemDto?> ApproveMediaAsync(Guid id, MediaCategory? category = null, CancellationToken ct = default);
    Task<MediaItemDto?> RejectMediaAsync(Guid id, CancellationToken ct = default);
    Task<MediaItemDto?> AssignOrphanMediaAsync(Guid id, Guid gameId, CancellationToken ct = default);
    Task<BrokenLinkReportDto> CheckBrokenLinksAsync(CancellationToken ct = default);
    Task<MediaItemDto> CreateMediaItemAsync(MediaItem item, CancellationToken ct = default);
    Task<MediaItemDto?> UpdateMediaCategoryAsync(Guid id, MediaCategory newCategory, CancellationToken ct = default);
    Task<MediaItemDto?> ReassignMediaGameAsync(Guid id, Guid newGameId, CancellationToken ct = default);
    Task<bool> DeleteMediaAsync(Guid id, CancellationToken ct = default);
}
```

### 3.2 DTOs (`MediaDtos.cs`)
```csharp
public record MediaItemDto(
    Guid Id,
    Guid? GameId,
    string? GameTitle,
    MediaType Type,
    MediaCategory Category,
    MediaPlatform Platform,
    string Title,
    string Url,
    string? EmbedUrl,
    string ThumbnailUrl,
    string AuthorChannel,
    int? DurationSeconds,
    string FormattedDuration,
    string? PlayerCountBadge,
    int? LikesCount,
    string? Excerpt,
    ModerationStatus Status,
    bool IsBroken,
    bool IsOrphan,
    DateTimeOffset PublishedAt,
    DateTimeOffset CreatedAt
)
{
    public string CategoryDisplayName => Category switch
    {
        MediaCategory.QuickOverview => "Cómo Funciona",
        MediaCategory.Tutorial => "Tutorial",
        MediaCategory.Gameplay => "Partida Completa",
        MediaCategory.ReviewOpinion => "Reseña u Opinión",
        _ => Category.ToString()
    };
}
```

`GameMediaHubDto`:
```csharp
public record GameMediaHubDto(
    Guid GameId,
    IReadOnlyList<MediaItemDto> Tutorials,
    IReadOnlyList<MediaItemDto> Playthroughs,
    IReadOnlyList<MediaItemDto> InstagramPosts,
    IReadOnlyList<MediaItemDto> ShortReels,
    IReadOnlyList<MediaItemDto>? QuickOverviews = null,
    IReadOnlyList<MediaItemDto>? ReviewsAndOpinions = null
)
```

---

## 4. Capa de Infraestructura (`Ludeka.Infrastructure`)

### 4.1 Reconciliación en `SqliteSchemaMigrator`
```csharp
// Añadir comprobación para la tabla MediaItems
if (!existingColumns.Contains("Category"))
{
    using var alterCmd = connection.CreateCommand();
    alterCmd.CommandText = "ALTER TABLE \"MediaItems\" ADD COLUMN \"Category\" INTEGER NOT NULL DEFAULT 0;";
    await alterCmd.ExecuteNonQueryAsync(ct);
}
```

### 4.2 Configuración EF Core en `LudekaDbContext`
```csharp
media.HasIndex(m => m.Category);
```

---

## 5. Presentación Blazor (`Ludeka.Web`)

### 5.1 Panel de Moderación Central (`MediaModeration.razor`)
- Rutas: `@page "/admin/multimedia"`, `@page "/moderacion-media"`, `@page "/moderacion/multimedia"`.
- Cada tarjeta en la pestaña "Pendientes" incluye:
  - Selector `<select>` con las 4 categorías (`Cómo Funciona`, `Tutorial`, `Partida`, `Opinión/Reseña`).
  - Inicializado automáticamente por `MediaClassifier.Classify(item.Title)`.
  - Botón "Aprobar" que envía la categoría seleccionada a `MediaService.ApproveMediaAsync(item.Id, selectedCategory)`.

### 5.2 Componente `MultimediaHub.razor`
- Botón flotante `[ ⚙️ Moderar ]` en cada tarjeta audiovisual, protegido con:
  ```razor
  @if (CurrentUserService.IsFoundingTeam || (CurrentUserService.IsInRole("Moderator") && CurrentUserService.HasPermission(ModeratorPermission.CanApproveMedia)))
  ```
- Menú modal/desplegable de moderación directa:
  1. **Selector de Categoría:** Desplegable con las 4 opciones de `MediaCategory` y botón "Guardar Cambio". Al guardar:
     - Llama a `MediaService.UpdateMediaCategoryAsync`.
     - Reubica el vídeo localmente entre las listas de `MediaHub` (o refresca el hub).
     - Notifica con un mensaje visual de éxito.
  2. **Reasignar Juego:** Abre modal con buscador reactivo de títulos. Al confirmar:
     - Llama a `MediaService.ReassignMediaGameAsync`.
     - Retira el vídeo de la ficha actual.
  3. **Eliminar de la Ficha:** Diálogo de confirmación. Al confirmar:
     - Llama a `MediaService.DeleteMediaAsync`.
     - Retira el vídeo del hub local.
- EventCallback `OnMediaChanged` hacia `GameDetail.razor` para asegurar sincronización reactiva sin recargar la página.
