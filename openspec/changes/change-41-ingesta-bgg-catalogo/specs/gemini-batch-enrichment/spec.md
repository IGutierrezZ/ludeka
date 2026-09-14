# Especificación: Síntesis IA en Lotes y Control de Cuota (gemini-batch-enrichment)

## 1. Contexto y Requerimientos

La capa gratuita de Google Gemini Flash concede 1.500 llamadas por día (`requests per day`). Procesar 8.000 juegos de 1 en 1 requeriría casi 6 días completos y pondría en riesgo el cupo diario para otras funciones de la aplicación.
Enviando **5 a 10 juegos por prompt estructurado JSON**, el catálogo de 8.000 títulos se puede procesar en 800 a 1.600 llamadas en total (cubrible en 1 o 2 días).

### Requerimientos Funcionales
- **RF-01 (Agrupación en Lotes de 5 a 10 juegos):**
  - El orquestador toma entre 5 y 10 registros de staging con `AiStatus = Pending`.
  - Construye un prompt compacto con datos clave de cada juego: `BggId`, título en español/original, autor, editorial, año, mecánicas, escalabilidad, edades, duración y descripción.
  - Solicita un array JSON de salida con objetos vinculados por `bggId`:
    - `bggId` (int)
    - `generalVerdict` (string)
    - `scalabilitySummary` (string)
    - `ageSummary` (string)
    - `footprintSummary` (string)
- **RF-02 (Parseo y Asignación Atómica):**
  - Cada respuesta se valida y se almacena en el campo `AiSummaryJson` del registro de staging correspondiente, cambiando su estado a `Completed`.
- **RF-03 (Detección de Cuota Diaria y Pausa Limpia):**
  - Si Gemini responde con código HTTP 429 (`Resource Exhausted` / `Quota Exceeded`):
    1. Se captura la excepción y se marca un flag `QuotaExhausted = true` en el contexto del job nocturno.
    2. Los juegos del lote actual y posteriores se mantienen en estado `Pending`.
    3. Se registra una entrada de advertencia clara en la bitácora (`NightlyCatalogingExecutionLog`) indicando que la cuota de Gemini del día se ha alcanzado y que el proceso se reanudará automáticamente en el próximo ciclo programado.
    4. El proceso nocturno continúa con las demás fases sin abortar con excepción no controlada.
- **RF-04 (Modo Heurístico / Simulado):**
  - Si Gemini está en modo `Simulate = true` o no hay ApiKey en entorno de test/desarrollo, se emplea el generador heurístico editorial para resolver el lote de forma instantánea y determinista.

---

## 2. Criterios de Aceptación (Gherkin)

```gherkin
Característica: Síntesis IA en lotes y manejo de rate limits

  Escenario: Generación en lote para 8 juegos con Gemini Flash
    Dados 8 juegos en staging sin síntesis de IA
    Cuando se ejecuta el procesamiento por lote
    Entonces se realiza una única petición HTTP a Gemini API
    Y se reciben 8 objetos en la respuesta JSON
    Y los 8 registros de staging se actualizan con su AiSummaryJson y estado Completed

  Escenario: Cuota agotada de Gemini (HTTP 429)
    Dado un lote de juegos enviado a Gemini cuando salta un error 429 Too Many Requests
    Cuando el servicio procesa la respuesta
    Entonces los registros de staging permanecen en AiStatus = Pending
    Y el servicio registra una advertencia de cuota en el log
    Y no se lanzan excepciones que rompan el servicio nocturno
```
