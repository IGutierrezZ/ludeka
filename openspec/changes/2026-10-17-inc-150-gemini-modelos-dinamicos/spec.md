# Especificación Funcional y Técnica (INC-150)

## Requerimiento 1: Autodescubrimiento y Filtrado de Modelos en `GET /v1beta/models`

- **Dado** que `GeminiOptions.ShouldSimulate` es `false` y existe una `ApiKey` configurada,
- **Cuando** un servicio de IA solicita la cadena de modelos candidatos a `IGeminiModelResolver.GetCandidateModelsAsync(ct)` y la caché ha expirado o está vacía,
- **Entonces** el resolutor invoca `GET {BaseUrl}/models?key={ApiKey}` y filtra la lista de modelos cumpliendo todas estas reglas:
  1. Si el nodo JSON del modelo incluye `supportedGenerationMethods`, debe contener `"generateContent"`.
  2. El identificador sin el prefijo `"models/"` debe coincidir exactamente con la expresión regular `^gemini-(\d+(?:\.\d+)?)-(flash-lite|flash)$` (insensible a mayúsculas).
  3. El número de versión extraído debe ser $\ge 3.5$ (descartando modelos `2.0`, `2.5` y `3.1` deprecados/restringidos).
  4. Quedan descartados todos los modelos `pro`, `ultra`, `-preview`, `-exp`, `-tts`, `-live`, `-image`, `-transcribe`, `-embedding`, `-robotics` y `-computer-use`.

## Requerimiento 2: Escalado Determinista por Precio y Versión

- **Dado** un conjunto de modelos elegibles descubiertos o configurados por defecto,
- **Cuando** se construye la lista ordenada de candidatos con `Model = "auto"` (o vacío),
- **Entonces**:
  1. Todos los modelos de la gama `flash-lite` (escalón de coste 1, más económico) se sitúan antes que cualquier modelo `flash` (escalón de coste 2).
  2. Dentro del mismo escalón de coste, los modelos se ordenan de versión más reciente a versión más antigua (ej. `gemini-3.8-flash` antes que `gemini-3.7-flash`, `gemini-3.6-flash` y `gemini-3.5-flash`).
  3. Si `GeminiOptions.GetEffectiveModel()` especifica un modelo explícito (por configuración o variable de entorno `Gemini__Model` distinta de `"auto"`), dicho modelo se coloca en la posición 0 sin duplicados.

## Requerimiento 3: Cooldown y Exclusión ante Errores HTTP (`404`, `429`, `503`)

- **Dado** que un intento de invocación a un modelo falla con un código de estado HTTP no exitoso,
- **Cuando** el servicio invoca `IGeminiModelResolver.ReportModelFailure(model, statusCode)`,
- **Entonces**:
  1. Si `statusCode` es `404 NotFound` o `400 BadRequest`, el modelo queda excluido de las siguientes cadenas de candidatos durante 6 horas (salvo que todos los modelos conocidos estén excluidos).
  2. Si `statusCode` es `429 TooManyRequests` o `503 ServiceUnavailable`, el modelo entra en *cooldown* durante 5 minutos y pasa al final de la lista de candidatos para priorizar inmediatamente los modelos sanos en las siguientes llamadas.

## Requerimiento 4: Resiliencia ante Fallo del Endpoint de Descubrimiento y Cascada Unificada

- **Dado** que la llamada a `GET /v1beta/models` falla por error de red, timeout o código HTTP de error,
- **Cuando** `IGeminiModelResolver.GetCandidateModelsAsync(ct)` resuelve la cadena,
- **Entonces** registra una advertencia en el log y retorna de forma transparente la cadena de respaldo `GeminiOptions.GetModelCandidateChain()` (`gemini-3.5-flash-lite`, `gemini-3.8-flash`, `gemini-3.7-flash`, `gemini-3.6-flash`).
- **Y** los tres servicios (`GeminiSocialAnalysisService`, `GeminiGameSummaryService` en sus modos individual, lote y expansión, y `GeminiReleaseMatcherService`) recorren la cadena devuelta por el resolutor.
