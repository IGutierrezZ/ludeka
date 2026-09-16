# Propuesta: change-13-ai-game-summary (Incremento 13: Módulo de Síntesis con IA para Fichas)

## 1. Resumen Ejecutivo y Motivación

En el ciclo editorial de Ludeka ("El Letterboxd de los juegos de mesa en español"), existen dos momentos críticos donde una ficha de juego puede carecer de análisis humano:
1. **Fichas recién catalogadas:** Juegos incorporados automáticamente mediante la cola comunitaria de auto-catalogación (`PendingBggImports`) o sincronizados desde BGG.
2. **Juegos en espera de análisis:** Títulos que todavía no disponen de un Veredicto Oficial emitido por la Mesa Fundadora ni de un volumen suficiente de reseñas comunitarias.

Para evitar fichas desiertas o dependientes exclusivamente de textos promocionales en inglés de las editoriales, el MVP de Ludeka (puntos 4.1 y 8.2) establece la necesidad de un **Módulo de Síntesis Inteligente con IA**. Este módulo genera resúmenes concisos, objetivos y estructurados en español respondiendo a los tres interrogantes cruciales del jugador:
- **Veredicto de Escalabilidad:** Rango óptimo y número de jugadores donde el título realmente destaca sin entreturno excesivo.
- **Accesibilidad & Experiencia Familiar/Infantil:** Comparativa entre la edad legal de caja y la edad real recomendada por la comunidad para partidas fluidas.
- **Espacio y Ritmo en Mesa:** Tamaño del despliegue (mesa de cafetería, comedor estándar o "monstruo de mesa") y duración real estimada por comensal.

Esta propuesta implementa el **Incremento 13** bajo la metodología **Spec-Driven Development (SDD)** con:
- Un proveedor conectado a **Google Gemini API** (`gemini-2.5-flash`) mediante salida estructurada JSON (`responseMimeType: "application/json"`).
- Un motor simulado/heurístico desacoplado para desarrollo offline, entornos de prueba y tolerancia a fallos (zero-crash fallback).
- Persistencia del resumen en el modelo de dominio `Game` como Value Object inmutable mapeado en SQLite.
- Integración automática en la cola comunitaria de auto-catalogación.
- Actualización visual del componente `AiSummaryCard.razor` con distintivo editorial ético y transparente `🤖 Síntesis generada por IA`, indicando el modelo utilizado y cediendo su posición al Veredicto Fundador cuando este exista.

---

## 2. Decisiones de Dominio y Arquitectura

### 2.1 Modelo de Dominio (`Ludeka.Core`)
Se añade el Value Object inmutable `AiGameSummary` en `Ludeka.Core.ValueObjects`:
```csharp
namespace Ludeka.Core.ValueObjects;

public record AiGameSummary(
    string GeneralVerdict,
    string ScalabilitySummary,
    string AgeSummary,
    string FootprintSummary,
    string Model,
    DateTime GeneratedAt
);
```

En `Game.cs`:
```csharp
public AiGameSummary? AiSummary { get; private set; }

public void SetAiSummary(AiGameSummary summary)
{
    ArgumentNullException.ThrowIfNull(summary);
    AiSummary = summary;
}
```

### 2.2 Persistencia EF Core y Reconciliación SQLite (`Ludeka.Infrastructure`)
En `LudekaDbContext.cs`:
```csharp
game.OwnsOne(g => g.AiSummary, b => b.ToJson());
```

En `SqliteSchemaMigrator.cs`:
Se agrega la columna `AiSummary` (`TEXT NULL`) al catálogo de columnas reconciliadas de la tabla `Games`:
```csharp
("AiSummary", "TEXT NULL")
```

### 2.3 Contratos y DTOs (`Ludeka.Application`)
Se crea el contrato del servicio en `Ludeka.Application.Contracts.IAiGameSummaryService`:
```csharp
public interface IAiGameSummaryService
{
    Task<AiGameSummaryDto> GenerateSummaryAsync(Game game, CancellationToken ct = default);
    Task<AiGameSummaryDto> EnsureSummaryForGameAsync(Guid gameId, CancellationToken ct = default);
}
```

Se amplía el DTO `AiGameSummaryDto` en `Ludeka.Application.DTOs`:
```csharp
public record AiGameSummaryDto(
    Guid GameId,
    string GameTitle,
    string ScalabilitySummary,
    string AgeSummary,
    string FootprintSummary,
    string GeneralVerdict,
    string Model = "Heurística Editorial",
    DateTime? GeneratedAt = null
);
```

### 2.4 Configuración y Servicio Gemini (`Ludeka.Infrastructure`)
Se crea `GeminiOptions.cs`:
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

Se implementa `GeminiGameSummaryService.cs` en `Ludeka.Infrastructure.Services`:
- Si `ShouldSimulate == true`: ejecuta el generador heurístico deterministicamente sin peticiones HTTP.
- Si `ShouldSimulate == false`: envía un payload JSON a la API REST de Google Gemini solicitando salida estructurada (`application/json`) con un system prompt editorial en español.
- **Fallback resiliente:** Ante cualquier error HTTP (timeout, cuota 429, API key inválida 400/403, error de servidor), captura la excepción de forma segura, registra una advertencia y devuelve el resumen heurístico con modelo `"Heurística Editorial (Fallback)"`.

### 2.5 Integración en la Cola de Auto-Catalogación (`BggCatalogQueueService`)
En `ProcessPendingQueueBatchAsync`:
Al procesar e incorporar un juego nuevo desde BGG, el servicio invoca automáticamente:
```csharp
var summaryDto = await _aiSummaryService.GenerateSummaryAsync(fetchedGame, ct);
fetchedGame.SetAiSummary(new AiGameSummary(
    summaryDto.GeneralVerdict,
    summaryDto.ScalabilitySummary,
    summaryDto.AgeSummary,
    summaryDto.FootprintSummary,
    summaryDto.Model,
    summaryDto.GeneratedAt ?? DateTime.UtcNow
));
```
Garantizando que cualquier juego importado quede persistido con su síntesis lista desde el primer segundo.

### 2.6 Presentación UI y Prioridad Editorial (`Ludeka.Web`)
1. **Tarjeta `AiSummaryCard.razor`:**
   - Badge principal: `🤖 Síntesis generada por IA`.
   - Chip de procedencia del modelo (`Gemini 2.5 Flash` o `Heurística Editorial`).
   - Fecha de síntesis y estructura visual en tres tarjetas limpias (Escalabilidad, Accesibilidad, Huella/Tiempo).
   - Banner de transparencia explicando que se trata de un análisis provisional hasta la revisión del equipo fundador.
2. **Prioridad en `GameDetail.razor`:**
   - Si existe `_foundingVerdict`, se renderiza `FoundingVerdictCard` prioritariamente y se oculta la tarjeta de IA.
   - Si no existe `_foundingVerdict`, se muestra `AiSummaryCard`.

---

## 3. Criterios de Aceptación (Gherkin)

```gherkin
Característica: Módulo de Síntesis con IA para Fichas de Juego

  Escenario: Generación automática de síntesis en modo simulado o sin API Key
    Dado que el sistema tiene configurado Gemini con Simulate = true o sin ApiKey
    Cuando se solicita la síntesis de un juego
    Entonces se genera un resumen estructurado en español con escalabilidad, edad y huella
    Y el modelo reportado es "Heurística Editorial"

  Escenario: Generación de síntesis con Google Gemini y fallback ante error
    Dado que Gemini está configurado con ApiKey
    Cuando la API de Gemini responde exitosamente con JSON estructurado
    Entonces se almacena la síntesis y el modelo reportado contiene "Gemini"
    Pero si la API de Gemini falla por timeout o cuota
    Entonces el sistema conmuta automáticamente a la síntesis heurística sin interrumpir el flujo

  Escenario: Incorporación automática en la cola comunitaria de BGG
    Dado un juego pendiente de catalogar en la cola comunitaria
    Cuando el worker de la cola procesa el lote
    Entonces el juego se guarda en la base de datos con su AiSummary ya generado

  Escenario: Prioridad jerárquica del Veredicto Fundador en la ficha Blazor
    Dado un juego que dispone de una síntesis generada por IA
    Cuando la Mesa Fundadora publica un Veredicto Oficial
    Entonces la ficha del juego muestra el Veredicto Fundador en la cabecera
    Y la tarjeta de síntesis de IA no se muestra como veredicto principal
```

---

## 4. Plan de Verificación

1. **Pruebas Automatizadas (xUnit):**
   - Validación de lógica condicional en `GeminiOptions.ShouldSimulate`.
   - Pruebas unitarias de `GeminiGameSummaryService` en modo simulado: comprobación de formato, campos obligatorios y textos en español.
   - Pruebas unitarias de `GeminiGameSummaryService` con `HttpMessageHandler` mockeado: verificación de llamada JSON a Gemini y mapeo de respuesta.
   - Pruebas de fallback: simular excepción de red en `HttpClient` y verificar que devuelve el resumen heurístico sin lanzar excepción no controlada.
   - Pruebas en `BggCatalogQueueService`: verificar que al procesar un elemento de la cola se invoca la generación de IA y se asigna a la entidad `Game`.
   - Pruebas de persistencia en SQLite (`SqliteSchemaMigrator` y `LudekaDbContext`).
2. **Pruebas de Componentes y Compilación:**
   - Compilación completa de la solución sin advertencias (`dotnet build`).
   - Ejecución de la suite total de pruebas unitarias (`dotnet test`).
