# Plan de Tareas (INC-150: `gemini-modelos-dinamicos`)

## Unidad de Trabajo 1: `GeminiOptions` y `IGeminiModelResolver` / `GeminiModelResolver`
- [ ] Actualizar `GeminiOptions.cs`:
  - Ajustar `DefaultFallbackModels` al orden real por precio y soporte actual (`gemini-3.5-flash-lite`, `gemini-3.8-flash`, `gemini-3.7-flash`, `gemini-3.6-flash`).
  - Añadir método auxiliar `HasExplicitModelOverride(out string explicitModel)` para distinguir cuando el usuario fuerza un modelo concreto frente a `"auto"` o vacío.
  - Actualizar `src/Ludeka.Web/appsettings.json` para que `"Model"` sea `"auto"`.
- [ ] Crear `IGeminiModelResolver.cs` y `GeminiModelResolver.cs` en `src/Ludeka.Infrastructure/Services/`:
  - Implementar consulta a `GET {BaseUrl}/models?key={ApiKey}` con caché en memoria (12 h en éxito, 15 min tras error).
  - Filtrar por `supportedGenerationMethods` (`generateContent`) y patrón `^gemini-(\d+(?:\.\d+)?)-(flash-lite|flash)$` con versión $\ge 3.5$.
  - Ordenar por escalón de precio (`flash-lite` $\rightarrow$ `flash`) y versión descendente.
  - Implementar `ReportModelFailure` con exclusión temporal para `404`/`400` y *cooldown* (relegando al final) para `429`/`503`.
- [ ] Añadir pruebas unitarias en `GeminiOptionsTests.cs` y `GeminiModelResolverTests.cs`.

## Unidad de Trabajo 2: Unificación de Cascada en los Servicios de IA y Registro DI
- [ ] Integrar `IGeminiModelResolver` en `GeminiSocialAnalysisService.cs` (texto y multimodal) reportando códigos HTTP fallidos.
- [ ] Integrar `IGeminiModelResolver` en `GeminiGameSummaryService.cs` (síntesis individual, síntesis en lote `CallGeminiBatchApiAsync` y síntesis de aporte de expansiones `CallGeminiExpansionAporteApiAsync`), eliminando la referencia obsoleta a `gemini-2.5-flash`.
- [ ] Integrar `IGeminiModelResolver` en `GeminiReleaseMatcherService.cs` para recorrer la cascada de modelos candidatos en lugar de un único intento.
- [ ] Registrar `IGeminiModelResolver` en `LudekaServiceCollectionExtensions.cs`.
- [ ] Ampliar pruebas en `GeminiGameSummaryServiceTests.cs`, `GeminiSocialAnalysisServiceTests.cs` y `GeminiReleaseMatcherServiceTests.cs`.
