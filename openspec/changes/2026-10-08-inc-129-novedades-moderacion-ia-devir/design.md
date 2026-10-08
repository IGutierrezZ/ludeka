# Diseño Arquitectónico y Técnico: INC-129

## 1. Modelo de Dominio y Persistencia

### 1.1 `WeeklyReleaseStatus` y Extensión de `WeeklyRelease`
Ubicación: `src/Ludeka.Core/Enums/WeeklyReleaseStatus.cs` y `src/Ludeka.Core/Entities/WeeklyRelease.cs`

```csharp
namespace Ludeka.Core.Enums;

public enum WeeklyReleaseStatus
{
    Published = 1,
    PendingModeration = 2,
    Rejected = 3
}
```

Nuevas propiedades en `WeeklyRelease`:
- `public WeeklyReleaseStatus Status { get; private set; } = WeeklyReleaseStatus.Published;`
- `public int? AiSuggestedBggId { get; private set; }`
- `public string? AiSuggestedTitle { get; private set; }`
- `public string? AiMatchReasoning { get; private set; }`

Métodos de mutación en `WeeklyRelease`:
- `public void SetPendingModeration(int? suggestedBggId, string? suggestedTitle, string? reasoning)`
- `public void Approve(Guid? gameId = null)`
- `public void Reject()`

### 1.2 Migración Segura en SQLite y PostgreSQL
En `SqliteSchemaMigrator.cs`:
- Añadir columnas:
  - `Status INTEGER NOT NULL DEFAULT 1`
  - `AiSuggestedBggId INTEGER NULL`
  - `AiSuggestedTitle TEXT NULL`
  - `AiMatchReasoning TEXT NULL`

---

## 2. Capa de Aplicación

### 2.1 Contrato del Asistente IA (`IReleaseAiMatcherService`)
Ubicación: `src/Ludeka.Application/Contracts/IReleaseAiMatcherService.cs`

```csharp
public interface IReleaseAiMatcherService
{
    Task<AiReleaseMatchResultDto?> MatchReleaseAsync(
        string rawTitle,
        string publisher,
        decimal? estimatedPvp,
        string? notes,
        CancellationToken ct = default);
}

public record AiReleaseMatchResultDto(
    int? SuggestedBggId,
    string? SuggestedTitle,
    string? Reasoning,
    string? CandidateCoverUrl,
    int? CandidateYearPublished
);
```

### 2.2 Orquestación en `EditorialReleasesSyncService`
1. Extraer elementos de Devir (con filtros de calendario) y Maldito Games.
2. Indexar catálogo local (`barcodeIndex`, `titleIndex`).
3. Para cada elemento extraído:
   - Intentar cruce local por EAN o título.
   - Si cruza: `matchedGame = game`, `status = Published`.
   - Si no cruza de inmediato:
     - No descartar.
     - `status = PendingModeration`.
     - Invocación no bloqueante o desacoplada a `IReleaseAiMatcherService` para obtener la propuesta de IA.
     - Guardar la novedad en el repositorio para que no se pierda ningún lanzamiento oficial.

### 2.3 Servicio de Moderación de Novedades (`IWeeklyReleaseService`)
- Extender `IWeeklyReleaseService`:
  - `Task<IReadOnlyList<WeeklyReleaseDto>> GetPendingModerationReleasesAsync(CancellationToken ct = default);`
  - `Task<WeeklyReleaseDto> ApproveReleaseAsync(Guid id, Guid? linkedGameId, CancellationToken ct = default);`
  - `Task RejectReleaseAsync(Guid id, CancellationToken ct = default);`

---

## 3. Capa de Infraestructura

### 3.1 Refinamiento en `DevirReleasesExtractor`
- Modificar `SectionHeaderRegex` y `ParseSectionDate` para excluir cabeceras que contengan `desarrollo`.
- En `ParseTileCardItems`, buscar el nodo `<a href="...">` anterior o contenedor para extraer la URL canónica del producto en Devir (`https://devir.es/<slug>`).
- Solo aceptar productos que tengan una fecha o mes concreto parseado.

### 3.2 Implementación `GeminiReleaseMatcherService`
- Ubicación: `src/Ludeka.Infrastructure/Services/GeminiReleaseMatcherService.cs`
- Emplea `GeminiOptions` y `HttpClient` hacia Google Gemini API.
- Prompt optimizado para inferir el nombre canónico del juego a partir de marcas y nombres traducidos al español por editoriales españolas.
- Fallback determinista en modo simulación (`Simulate: true`) con diccionario de equivalencias canónicas (*«Crucero Galáctico»* $\rightarrow$ *«Galactic Cruise»*, *«Los 12 trabajos de Hércules»* $\rightarrow$ *«12 Labours of Hercules»*, etc.).

---

## 4. Capa de Presentación (Web / Blazor)

### 4.1 Pestaña de Moderación en `/novedades`
- Si el usuario cuenta con `ModeratorPermission.CanApproveMedia`:
  - Se añade pestaña: *«Pendientes de moderación (N)»*.
  - Vista comparativa entre los datos extraídos de la tienda y la sugerencia de la IA.
  - Botones de acción:
    - `Aprobar con enlace BGG`: activa y vincula el juego.
    - `Aprobar sin enlace`: publica como novedad independiente.
    - `Descartar`: retira el elemento.
