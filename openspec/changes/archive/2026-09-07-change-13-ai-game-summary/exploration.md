# Exploración: change-13-ai-game-summary (Incremento 13: Síntesis con IA para Fichas)

## 1. Estado Actual de la Solución y Análisis de Brecha

### 1.1 Funcionalidad Existente de Resúmenes de IA
- **DTO Actual:**
  Existe un DTO preliminar en [`Ludeka.Application.DTOs`](file:///c:/repos/Ludeka/src/Ludeka.Application/DTOs/FoundingVerdictDtos.cs):
  ```csharp
  public record AiGameSummaryDto(
      Guid GameId,
      string GameTitle,
      string ScalabilitySummary,
      string AgeSummary,
      string FootprintSummary,
      string GeneralVerdict
  );
  ```
- **Lógica Heurística en `FoundingVerdictService`:**
  Actualmente, [`FoundingVerdictService.GetAiSummaryForGameAsync`](file:///c:/repos/Ludeka/src/Ludeka.Application/Features/Founding/FoundingVerdictService.cs) calcula una síntesis heurística elemental en memoria a partir de los atributos del juego (`Scalability`, `Age`, `Footprint`, `Confrontation`).
  *Limitaciones detectadas:*
  1. No está desacoplado: el cálculo reside dentro del servicio de Veredictos Fundadores, acoplando dos responsabilidades distintas.
  2. No persiste en base de datos: se recalcula al vuelo en cada visita a la ficha.
  3. No se conecta con un proveedor real de IA (Google Gemini): no analiza la descripción narrativa de BGG ni genera lenguaje natural contextual.
  4. No está integrado en el flujo de auto-catalogación nocturna de [`BggCatalogQueueService`](file:///c:/repos/Ludeka/src/Ludeka.Application/Features/Bgg/BggCatalogQueueService.cs).
  5. La tarjeta [`AiSummaryCard.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/AiSummaryCard.razor) muestra etiquetas genéricas ("📋 Síntesis Editorial") en vez del distintivo oficial `"🤖 Síntesis generada por IA"` con indicación del modelo utilizado.

### 1.2 Cola de Auto-Catalogación BGG
- En [`BggCatalogQueueService.cs`](file:///c:/repos/Ludeka/src/Ludeka.Application/Features/Bgg/BggCatalogQueueService.cs), cuando un juego de la cola comunitaria es procesado (`ProcessPendingQueueBatchAsync`), se descarga de BGG y se añade a la base de datos, pero no se invoca ninguna generación de resumen inteligente para dejar la ficha completamente lista.

### 1.3 Modelo de Dominio y Persistencia
- La entidad [`Game`](file:///c:/repos/Ludeka/src/Ludeka.Core/Entities/Game.cs) carece de una propiedad o Value Object para almacenar la síntesis generada por IA.
- El esquema SQLite en [`LudekaDbContext.cs`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/Data/LudekaDbContext.cs) no tiene mapeada ninguna columna para el resumen de IA.
- [`SqliteSchemaMigrator.cs`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/Data/SqliteSchemaMigrator.cs) es el encargado de reconciliar aditivamente columnas en SQLite sin borrar datos.

---

## 2. Decisiones Técnicas y Opciones de Diseño

### 2.1 Modelo de Dominio: Value Object `AiGameSummary`
Para respetar la arquitectura limpia (Clean Architecture), se modelará un Value Object inmutable en `Ludeka.Core.ValueObjects`:
```csharp
public record AiGameSummary(
    string GeneralVerdict,
    string ScalabilitySummary,
    string AgeSummary,
    string FootprintSummary,
    string Model,
    DateTime GeneratedAt
);
```
En `Game`:
```csharp
public AiGameSummary? AiSummary { get; private set; }
public void SetAiSummary(AiGameSummary summary);
```
En EF Core / SQLite:
Mapeado mediante `game.OwnsOne(g => g.AiSummary, b => b.ToJson());`, lo que almacena el objeto de forma nativa y compacta en una columna `AiSummary TEXT NULL` en la tabla `Games`.

### 2.2 Proveedor Dual: Google Gemini API + Motor Heurístico Resiliente
1. **Configuración (`GeminiOptions` en `Ludeka.Infrastructure`):**
   ```csharp
   public class GeminiOptions
   {
       public const string SectionName = "Gemini";
       public string? ApiKey { get; set; }
       public string Model { get; set; } = "gemini-2.5-flash";
       public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/";
       public bool Simulate { get; set; } = true;
       public bool ShouldSimulate => Simulate || string.IsNullOrWhiteSpace(ApiKey);
   }
   ```
2. **Generación con Gemini REST API:**
   - Emplea el endpoint de generación de contenido estructurado `POST .../models/{model}:generateContent?key={apiKey}`.
   - Pide un esquema JSON estricto (`responseMimeType: "application/json"`) con los campos `GeneralVerdict`, `ScalabilitySummary`, `AgeSummary`, `FootprintSummary`.
   - Prompt de sistema especializado en español neutro editorial para juegos de mesa, respondiendo a los 3 ejes clave: escalabilidad real, accesibilidad/edad familiar y huella en mesa/ritmo.
3. **Generador Heurístico Local (`HeuristicGameSummaryGenerator`):**
   - Utilizado cuando `ShouldSimulate` es `true` o en caso de excepción de red, timeout o cuota excedida en la API de Gemini.
   - Analiza las propiedades ricas del juego (`Scalability`, `Age`, `Footprint`, `Duration`, `Confrontation`, `Style`, `BggRating`, `IsOfficialSolo`) generando descripciones de alta calidad sin depender de internet ni de tokens.

### 2.3 Servicio de Aplicación (`IAiGameSummaryService`)
Contrato en `Ludeka.Application.Contracts`:
```csharp
public interface IAiGameSummaryService
{
    Task<AiGameSummaryDto> GenerateSummaryAsync(Game game, CancellationToken ct = default);
    Task<AiGameSummaryDto> EnsureSummaryForGameAsync(Guid gameId, CancellationToken ct = default);
}
```
- Implementado en `Ludeka.Infrastructure.Services.GeminiGameSummaryService`.
- `EnsureSummaryForGameAsync`: si el juego ya tiene `AiSummary` persistido, lo devuelve; si no, lo genera, lo guarda en `_gameRepo` y lo retorna.

### 2.4 Integración en Cola de Auto-Catalogación (`BggCatalogQueueService`)
- Al procesar cada título en `ProcessPendingQueueBatchAsync`:
  Tras persistir el juego o antes de guardarlo, se invoca `GenerateSummaryAsync` y se asigna con `SetAiSummary`, asegurando que cualquier juego catalogado nazca con su síntesis lista.

### 2.5 Componentes UI (`AiSummaryCard.razor` y `GameDetail.razor`)
- `AiSummaryCard.razor`:
  - Badge principal: `🤖 Resumen generado por IA`.
  - Chip con el modelo (`Gemini 2.5 Flash` / `Heurística Editorial`).
  - Tarjetas visuales de Escalabilidad, Accesibilidad/Edad y Huella/Duración.
  - Veredicto general sintetizado.
  - Nota de transparencia indicando que este resumen provisional se desactiva en cuanto se emita un veredicto de la Mesa Fundadora.
- `GameDetail.razor`:
  - Mantiene la jerarquía estricta:
    1. Si `_foundingVerdict != null` ➔ Muestra exclusivamente `FoundingVerdictCard`.
    2. Si `_foundingVerdict == null` y `_aiSummary != null` ➔ Muestra `AiSummaryCard`.

---

## 3. Matriz de Componentes Afectados

| Componente | Capa | Acción | Detalle |
|---|---|---|---|
| `AiGameSummary` | `Ludeka.Core` | Nuevo | Value Object para representar la síntesis estructurada |
| `Game` | `Ludeka.Core` | Modificar | Propiedad `AiSummary` y método `SetAiSummary` |
| `IAiGameSummaryService` | `Ludeka.Application` | Nuevo | Contrato de generación y aseguramiento de síntesis |
| `AiGameSummaryDto` | `Ludeka.Application` | Modificar | Ampliar con `Model` y `GeneratedAt` |
| `BggCatalogQueueService` | `Ludeka.Application` | Modificar | Integrar generación automática de IA al catalogar |
| `GeminiOptions` | `Ludeka.Infrastructure` | Nuevo | Opciones de configuración para Google Gemini API |
| `GeminiGameSummaryService` | `Ludeka.Infrastructure` | Nuevo | Implementación HTTP con structured JSON y fallback heurístico |
| `SqliteSchemaMigrator` | `Ludeka.Infrastructure` | Modificar | Añadir columna `AiSummary` a tabla `Games` |
| `LudekaDbContext` | `Ludeka.Infrastructure` | Modificar | Mapear `OwnsOne(g => g.AiSummary, b => b.ToJson())` |
| `AiSummaryCard.razor` | `Ludeka.Web` | Modificar | Actualizar interfaz con insignia IA, modelo y estilo editorial |
| `GameDetail.razor` | `Ludeka.Web` | Modificar | Carga y conmutación transparente con prioridad de veredicto fundador |
| `appsettings.json` | `Ludeka.Web` | Modificar | Añadir sección de configuración `Gemini` |
| Pruebas Unitarias | `Ludeka.UnitTests` | Nuevas | Pruebas de GeminiOptions, generación heurística, simulación y fallback |
