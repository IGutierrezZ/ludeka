# Propuesta SDD: Descubrimiento Dinámico de Modelos Gemini por Escalón de Precio (INC-150)

## 1. Intención y Problema

Google AI Studio rota con frecuencia las versiones disponibles de la familia Gemini (`3.5-flash-lite`, `3.8-flash`, etc.), depreca versiones intermedias (`3.1-flash-lite`, `2.0-flash`) y restringe el acceso a familias anteriores (`2.5-flash`). Mantener una lista estática de modelos en código y en `appsettings.json` provoca que:
- Tengamos configurado en `appsettings.json` un modelo más caro (`gemini-3.6-flash`) como primera opción en lugar del más económico (`gemini-3.5-flash-lite`).
- Tengamos modelos de reserva desalineados en coste (`gemini-3.5-flash` cuesta el doble por millón de tokens que `gemini-3.8-flash`) o inaccesibles (`gemini-2.5-flash` cableado en `GeminiGameSummaryService`).
- `GeminiReleaseMatcherService` y `GeminiGameSummaryService` (en lotes y expansiones) no aprovechen la cascada completa.

## 2. Solución Propuesta

Introducir un servicio de resolución dinámica `IGeminiModelResolver` (`GeminiModelResolver`) en `Ludeka.Infrastructure`:
1. **Autodescubrimiento con Caché (`GET /v1beta/models`):** Consulta la lista real de modelos habilitados para la `ApiKey` en Google AI Studio y la guarda en caché durante 12 horas.
2. **Clasificación y Ordenación por Coste y Versión:**
   - Filtra exclusivamente modelos generativos estables de texto/visión de la gama económica: `^gemini-(\d+(?:\.\d+)?)-(flash-lite|flash)$` con versión $\ge 3.5$ y soporte para `generateContent`.
   - Ordena primero por **escalón de precio** (`flash-lite` antes que `flash`) y dentro de cada escalón por **versión descendente** (p. ej., `gemini-3.5-flash-lite` $\rightarrow$ `gemini-3.8-flash` $\rightarrow$ `gemini-3.7-flash` $\rightarrow$ `gemini-3.6-flash` $\rightarrow$ `gemini-3.5-flash`).
3. **Cooldown en Memoria ante Fallos:** Penaliza modelos que respondan `404` (excluidos durante 6 h) o `429`/`503` (relegados al final de la cola durante 5 min) para no repetir llamadas fallidas en cada petición.
4. **Unificación de Cascada:** Conectar `GeminiSocialAnalysisService`, `GeminiGameSummaryService` y `GeminiReleaseMatcherService` a `IGeminiModelResolver`, y establecer `"Model": "auto"` en `appsettings.json`.

## 3. Impacto por Capas

- `src/Ludeka.Infrastructure/Services/IGeminiModelResolver.cs` (nuevo contrato)
- `src/Ludeka.Infrastructure/Services/GeminiModelResolver.cs` (nueva implementación con caché y cooldown)
- `src/Ludeka.Infrastructure/Services/GeminiOptions.cs` (actualización de `DefaultFallbackModels` y normalización de `"auto"`)
- `src/Ludeka.Infrastructure/Services/GeminiSocialAnalysisService.cs` (integración con `IGeminiModelResolver`)
- `src/Ludeka.Infrastructure/Services/GeminiGameSummaryService.cs` (integración en síntesis individual, por lotes y de expansiones; retirada de `gemini-2.5-flash`)
- `src/Ludeka.Infrastructure/Services/GeminiReleaseMatcherService.cs` (incorporación de cascada con `IGeminiModelResolver`)
- `src/Ludeka.Infrastructure/DependencyInjection/LudekaServiceCollectionExtensions.cs` (registro DI)
- `src/Ludeka.Web/appsettings.json` (`"Model": "auto"`)
- `tests/Ludeka.UnitTests/Infrastructure/GeminiModelResolverTests.cs` y pruebas de servicios existentes.
