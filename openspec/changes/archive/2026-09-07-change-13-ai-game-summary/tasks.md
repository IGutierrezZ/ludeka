# Tareas de Implementación: change-13-ai-game-summary (Incremento 13)

## Tareas

- [x] **1. Dominio (`Ludeka.Core`)**
  - [x] 1.1 Crear Value Object `AiGameSummary` en `Ludeka.Core/ValueObjects/AiGameSummary.cs`.
  - [x] 1.2 Agregar propiedad `AiSummary` y método `SetAiSummary` a `Game.cs`.

- [x] **2. Aplicación (`Ludeka.Application`)**
  - [x] 2.1 Actualizar DTO `AiGameSummaryDto` en `Ludeka.Application/DTOs/FoundingVerdictDtos.cs` con propiedades de modelo y fecha de generación.
  - [x] 2.2 Crear interfaz de servicio `IAiGameSummaryService` en `Ludeka.Application/Contracts/IAiGameSummaryService.cs`.
  - [x] 2.3 Refactorizar `FoundingVerdictService.GetAiSummaryForGameAsync` para coordinarse con `IAiGameSummaryService`.
  - [x] 2.4 Integrar llamada a `IAiGameSummaryService` dentro de `BggCatalogQueueService.ProcessPendingQueueBatchAsync`.

- [x] **3. Infraestructura (`Ludeka.Infrastructure`)**
  - [x] 3.1 Crear clase de configuración `GeminiOptions` en `Ludeka.Infrastructure/Services/GeminiOptions.cs`.
  - [x] 3.2 Crear generador heurístico local `HeuristicGameSummaryGenerator` en `Ludeka.Infrastructure/Services/HeuristicGameSummaryGenerator.cs`.
  - [x] 3.3 Implementar servicio `GeminiGameSummaryService` en `Ludeka.Infrastructure/Services/GeminiGameSummaryService.cs` con llamadas HTTP estructuradas a Google Gemini REST API y fallback tolerante a fallos.
  - [x] 3.4 Configurar mapeo EF Core en `LudekaDbContext.cs` (`game.OwnsOne(g => g.AiSummary, b => b.ToJson())`).
  - [x] 3.5 Reconciliar columna `"AiSummary"` en `SqliteSchemaMigrator.cs`.

- [x] **4. Interfaz Web y Configuración (`Ludeka.Web`)**
  - [x] 4.1 Añadir sección `"Gemini"` en `src/Ludeka.Web/appsettings.json`.
  - [x] 4.2 Registrar `GeminiOptions`, `HttpClient` tipado y `IAiGameSummaryService` en `Program.cs`.
  - [x] 4.3 Actualizar componente `AiSummaryCard.razor` con insignia ética de IA, modelo usado y bloques informativos.
  - [x] 4.4 Verificar invocación en `GameDetail.razor` asegurando la jerarquía de prioridad del Veredicto Fundador.

- [x] **5. Pruebas Automatizadas y Verificación (`Ludeka.UnitTests`)**
  - [x] 5.1 Crear `GeminiOptionsTests.cs` (evaluación de `ShouldSimulate` con y sin API key).
  - [x] 5.2 Crear `GeminiGameSummaryServiceTests.cs` (pruebas de generación heurística, simulación con JSON de Gemini y fallback ante excepciones HTTP).
  - [x] 5.3 Actualizar pruebas de `BggCatalogQueueService` para validar la asignación del resumen de IA.
  - [x] 5.4 Ejecutar suite completa (`dotnet test`) asegurando 100% de tests aprobados (281 tests pasados).
  - [x] 5.5 Generar informe de verificación `verify-report.md`.
