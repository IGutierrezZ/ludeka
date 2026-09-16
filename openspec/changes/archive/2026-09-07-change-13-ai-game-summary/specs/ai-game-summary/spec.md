# Especificación Técnica: change-13-ai-game-summary (Módulo de Síntesis con IA para Fichas)

## 1. Propósito y Contexto

El objetivo de esta especificación es dotar a Ludeka de un sistema automático de síntesis editorial en español (`🤖 Resumen generado por IA`) para juegos de mesa recién catalogados o pendientes de revisión por la Mesa Fundadora.

El sistema debe operar de forma autónoma, desacoplada y con garantía de disponibilidad (Zero-Crash Fallback), conmutando entre la API oficial de Google Gemini (`gemini-2.5-flash`) y un motor heurístico local cuando la simulación esté activa, la API key no esté configurada o surjan incidencias de conectividad/cuota.

---

## 2. Requerimientos Funcionales

### RF-01: Generación de Síntesis Estructurada
El servicio de síntesis de IA (`IAiGameSummaryService`) debe generar un objeto estructurado que contenga:
1. `GeneralVerdict`: Texto narrativo en español que contextualice el título, su estilo, confrontación y valoración general.
2. `ScalabilitySummary`: Veredicto del número de jugadores ideal basado en el consenso comunitario y análisis de entreturno.
3. `AgeSummary`: Comparativa entre la edad mínima de componentes (caja) y la edad real comunitaria recomendada.
4. `FootprintSummary`: Clasificación del espacio de salón requerido (mesa de café, comedor o monstruo de mesa) y duración estimada por comensal.
5. `Model`: Identificador del motor/modelo que produjo la síntesis (`"Gemini 2.5 Flash"`, `"Heurística Editorial"`, `"Heurística Editorial (Fallback)"`).
6. `GeneratedAt`: Marca de tiempo UTC de generación.

### RF-02: Proveedor Google Gemini con Salida Estructurada JSON
1. Debe interactuar con el endpoint REST de Google Gemini:
   `POST {BaseUrl}models/{Model}:generateContent?key={ApiKey}`
2. Debe solicitar formato estricto `application/json` en `generationConfig` con un schema correspondiente a los 4 campos de texto.
3. El prompt debe suministrar los metadatos relevantes del juego: títulos (español y original), autor, editorial, año, estilos, confrontación, escalabilidad, edades, huella, duración y descripción original.

### RF-03: Conmutación y Resiliencia (Zero-Crash Fallback)
1. Si `Gemini:Simulate` es `true` o si `Gemini:ApiKey` es nulo/vacío (`ShouldSimulate == true`), el servicio ejecuta el motor heurístico local sin peticiones de red.
2. Si la llamada HTTP a Gemini lanza una excepción (timeout, código 400/401/403/429/500), el servicio captura el error, registra una advertencia en los logs y devuelve de inmediato la síntesis heurística etiquetada como `"Heurística Editorial (Fallback)"`.

### RF-04: Persistencia en Dominio (`Game`) y SQLite
1. La entidad `Game` contendrá la propiedad `AiSummary` (`AiGameSummary?`) y el método `SetAiSummary(AiGameSummary summary)`.
2. EF Core persistirá la entidad mediante mapeo JSON nativo (`OwnsOne(..., b => b.ToJson())`).
3. `SqliteSchemaMigrator` asegurará la presencia de la columna `AiSummary` (`TEXT NULL`) en la tabla `Games`.

### RF-05: Integración en la Cola Comunitaria de Auto-Catalogación
1. `BggCatalogQueueService.ProcessPendingQueueBatchAsync` debe invocar la generación del resumen de IA para cada juego nuevo importado y asociarlo antes de guardarlo en el catálogo general.

### RF-06: Aseguramiento Bajo Demanda (`EnsureSummaryForGameAsync`)
1. Si un juego en la base de datos carece de `AiSummary` al ser visitado en la ficha, `EnsureSummaryForGameAsync` generará la síntesis, la persistirá en la base de datos y la retornará para su visualización.

### RF-07: Prioridad Editorial y Presentación Blazor
1. `AiSummaryCard.razor` mostrará un diseño limpio, accesible (WCAG 2.2 AA) y sin estética artificial ("AI slop"), con la insignia `🤖 Síntesis generada por IA`, el chip del modelo y las tres tarjetas temáticas.
2. `GameDetail.razor` mantendrá la jerarquía: si existe `_foundingVerdict`, se muestra de forma prioritaria el Veredicto Fundador y se retira `AiSummaryCard`.

---

## 3. Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Generar síntesis heurística en desarrollo local sin API Key
  Dado que GeminiOptions tiene Simulate = true o ApiKey vacía
  Cuando se llama a GenerateSummaryAsync para un juego válido
  Entonces se devuelve un AiGameSummaryDto completo en español
  Y la propiedad Model es "Heurística Editorial"
  Y no se realiza ninguna petición HTTP externa

Escenario: Generar síntesis con Google Gemini API
  Dado que GeminiOptions tiene Simulate = false y una ApiKey configurada
  Y el endpoint de Gemini responde exitosamente con JSON estructurado
  Entonces se deserializa el resultado en AiGameSummaryDto
  Y la propiedad Model contiene "Gemini"

Escenario: Fallback automático ante caída de la API de Gemini
  Dado que GeminiOptions tiene Simulate = false y ApiKey configurada
  Y el endpoint de Gemini devuelve un error HTTP 429 Too Many Requests o Timeout
  Entonces el servicio no propaga ninguna excepción
  Y devuelve el resumen generado por el motor heurístico
  Y la propiedad Model es "Heurística Editorial (Fallback)"

Escenario: Procesamiento de la cola comunitaria con IA
  Dado un elemento en la cola de PendingBggImports
  Cuando BggCatalogQueueService procesa el lote
  Entonces el juego importado tiene su propiedad AiSummary asignada y guardada en el catálogo

Escenario: Desactivación visual al emitir Veredicto Fundador
  Dado un juego con AiSummary generado
  Cuando un miembro fundador guarda un FoundingVerdict
  Entonces la página de detalle del juego muestra FoundingVerdictCard
  Y AiSummaryCard no se visualiza como veredicto principal
```
