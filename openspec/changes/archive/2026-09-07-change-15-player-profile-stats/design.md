# Diseño Técnico: change-15-player-profile-stats (Incremento 15: Estadísticas Avanzadas de Colección y ADN del Jugador)

## 1. Arquitectura de Capas y Componentes

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          src/Ludeka.Web                                 │
│  ┌───────────────────────────┐         ┌─────────────────────────────┐  │
│  │   Pages/MyLibrary.razor   │         │   Pages/PublicProfile.razor │  │
│  │  (Pestaña 🧬 ADN y Stats) │         │    (/u/{id}, /perfil/{id})  │  │
│  └─────────────┬─────────────┘         └──────────────┬──────────────┘  │
│                │                                      │                 │
│                └───────────────────┬──────────────────┘                 │
│                                    ▼                                    │
│             ┌───────────────────────────────────────────────┐           │
│             │ Features/Library/LibraryStatsDashboard.razor  │           │
│             └──────────────────────┬────────────────────────┘           │
└────────────────────────────────────┼────────────────────────────────────┘
                                     │
                                     ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                       src/Ludeka.Application                            │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │  Contracts/IUserLibraryStatsService.cs                            │  │
│  │  DTOs/UserLibraryStatsDtos.cs                                     │  │
│  │  Features/Library/UserLibraryStatsService.cs                      │  │
│  └─────────────────────────────────┬─────────────────────────────────┘  │
└────────────────────────────────────┼────────────────────────────────────┘
                                     │
                                     ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         src/Ludeka.Core                                 │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │  Entities: Game, UserCollectionItem                               │  │
│  │  Value Objects: GameDuration, ScalabilityEntry, SleeveItem        │  │
│  │  Enums: GameStyle, ConfrontationType, CollectionStatus            │  │
│  └───────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Contratos y DTOs (`Ludeka.Application`)

### 2.1 `UserLibraryStatsDtos.cs`

```csharp
namespace Ludeka.Application.DTOs;

public record ShelfTimeStatsDto(
    int TotalMinMinutes,
    int TotalMaxMinutes,
    double TotalMinHours,
    double TotalMaxHours,
    string FormattedShelfHours
);

public record StylePercentageDto(
    GameStyle Style,
    string StyleDisplayName,
    int GameCount,
    double Percentage,
    string ColorHex
);

public record PlayerDnaDistributionDto(
    List<StylePercentageDto> Styles,
    string DominantStyleName,
    double CooperativePercentage,
    int CooperativeGamesCount,
    double SoloReadyPercentage,
    int SoloReadyGamesCount
);

public record ScalabilityCountDto(
    int PlayerCount,
    string DisplayCount,
    int OptimizedGamesCount,
    bool IsSweetSpot
);

public record ScalabilitySweetSpotDto(
    List<ScalabilityCountDto> Curve,
    List<int> SweetSpotPlayerCounts,
    string SweetSpotSummaryText
);

public record TopEntityStatDto(
    string Name,
    int Count,
    double Percentage
);

public record SleeveFormatStatDto(
    string FormatName,
    int CardCount,
    int PacksNeeded
);

public record SleeveProtectionRadarDto(
    int TotalCards,
    int TotalPacksEstimated,
    int GamesRequiringSleevesCount,
    List<SleeveFormatStatDto> TopFormats
);

public record PlayerBadgeDto(
    string RankName,
    int RankLevel,
    string TraitName,
    string TraitDescription,
    string IconEmoji,
    string SummaryTagline
);

public record UserLibraryStatsDto(
    string UserId,
    string UserName,
    int TotalGamesInCollection,
    int TotalPlayed,
    int TotalWishlist,
    int TotalWantToBuy,
    int TotalActiveLoans,
    ShelfTimeStatsDto ShelfTime,
    PlayerDnaDistributionDto DnaDistribution,
    ScalabilitySweetSpotDto Scalability,
    List<TopEntityStatDto> TopDesigners,
    List<TopEntityStatDto> TopPublishers,
    SleeveProtectionRadarDto SleevesRadar,
    PlayerBadgeDto Badge
);

public record PublicUserProfileDto(
    string UserId,
    string UserName,
    UserLibraryStatsDto Stats,
    List<UserCollectionItemDto> ShelfGames
);
```

### 2.2 `IUserLibraryStatsService.cs`

```csharp
namespace Ludeka.Application.Contracts;

public interface IUserLibraryStatsService
{
    Task<UserLibraryStatsDto> GetUserStatsAsync(string? userId = null, CancellationToken ct = default);
    Task<PublicUserProfileDto?> GetPublicProfileAsync(string userId, CancellationToken ct = default);
}
```

---

## 3. Implementación: `UserLibraryStatsService`

1. **Lectura de Datos:**
   - Consulta `_collectionRepo.GetByUserIdAsync(userId, null, ct)`.
   - Consulta `_collectionRepo.GetCountsByStatusAsync(userId, ct)`.
   - Extrae juegos físicos (`Status == CollectionStatus.InCollection` y `item.Game != null`).
2. **Cálculo de Horas:**
   - Suma de `Duration.MinMinutes` y `Duration.MaxMinutes`.
   - Conversión a horas: `TotalMinMinutes / 60.0` y `TotalMaxMinutes / 60.0`.
3. **ADN Lúdico:**
   - Mapeo de `GameStyle` a nombres y colores accesibles:
     - `Eurogame`: "Eurogames", `#3b82f6` (azul)
     - `Ameritrash`: "Temáticos / Ameritrash", `#ef4444` (rojo)
     - `PartyGame`: "Party Games", `#f59e0b` (ámbar)
     - `FillerAbstract`: "Fillers / Abstractos", `#10b981` (verde esmeralda)
     - `NarrativeCampaign`: "Campaña / Narrativos", `#8b5cf6` (púrpura)
   - Conteo de juegos cooperativos (`Confrontation == Cooperative || Confrontation == SemiCooperative`).
   - Conteo de juegos con solitario oficial (`IsOfficialSolo == true`).
4. **Sweet Spot de Comensales:**
   - Tabla para comensales 1..7 (donde 7 representa "7+").
   - Identifica el máximo número de juegos recomendados y extrae los comensales top.
5. **Radar de Fundas:**
   - Agrupa por `FormatName` y suma cartas y paquetes requeridos.
6. **Insignia y Gamificación:**
   - Determina rango por volumen (0 a 4).
   - Determina rasgo distintivo evaluando umbrales porcentuales de estilos.

---

## 4. Componentes y UI en Blazor (`Ludeka.Web`)

1. **`LibraryStatsDashboard.razor`:**
   - Tarjeta principal:
     - Avatar grande del jugador con emoji de insignia y nivel.
     - Título del rasgo distintivo con explicación breve.
     - Píldoras de resumen: *"X horas en estantería"*, *"Y títulos en posesión"*, *"Z cartas por proteger"*, *"Comensales estrella: 2 y 4"*.
   - Sección ADN Lúdico:
     - Barra de espectro continuo con segmentos proporcionales a cada estilo.
     - Leyenda con porcentaje y número de juegos por estilo.
     - Píldoras transversales: % Cooperativos y % Solitario.
   - Sección Rango Dulce de Mesa:
     - Gráfico de barras de frecuencias para comensales (1 a 7+), resaltando con color de marca (`var(--brand-primary)`) los puntos del Sweet Spot.
   - Sección Protección y Fundas:
     - Cartas totales, paquetes requeridos y lista de formatos más comunes.
   - Sección Diseñadores y Editoriales Top:
     - Podio con barras de frecuencia.
2. **Pestaña en `MyLibrary.razor`:**
   - Añade el tab `🧬 ADN y Estadísticas` (`TabType.Stats`).
   - Al seleccionarse, carga e incrusta `<LibraryStatsDashboard Stats="_stats" />`.
3. **Página `PublicProfile.razor`:**
   - Enrutado: `@page "/u/{UserId}"` y `@page "/perfil/{UserId}"`.
   - SSR Interactivo (`@rendermode InteractiveServer`).
   - Muestra el perfil público, estadísticas y vitrina de títulos con búsqueda/filtro de estado.
   - Botón de copiar enlace con feedback temporal.

---

## 5. Pruebas Unitarias (`tests/Ludeka.UnitTests/Application/UserLibraryStatsServiceTests.cs`)

1. `GetUserStatsAsync_EmptyCollection_ReturnsZeroMetricsAndBlankBadge`
2. `GetUserStatsAsync_ComputesCorrectShelfTime`
3. `GetUserStatsAsync_ComputesCorrectDnaDistributionAndDominantStyle`
4. `GetUserStatsAsync_IdentifiesSweetSpotPlayerCounts`
5. `GetUserStatsAsync_ComputesSleevesRadarCorrectly`
6. `GetUserStatsAsync_ParsesMultipleDesignersCorrectly`
7. `GetUserStatsAsync_AssignsCorrectBadgesAndTraitsBasedOnThresholds`
8. `GetPublicProfileAsync_ReturnsFullProfileForExistingUser`
9. `GetPublicProfileAsync_NonExistentUser_ReturnsNull`
