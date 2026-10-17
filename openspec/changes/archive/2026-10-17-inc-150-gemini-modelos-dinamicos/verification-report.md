# Informe de Verificación (INC-150: `gemini-modelos-dinamicos`)

## Estado: ✅ VERIFICADO AL 100%

### Resumen de Verificación
- **Suite de Pruebas Unitarias (`Ludeka.UnitTests`):** `2.927 / 2.927` en verde (0 errores, 0 omitidas).
- **Pruebas específicas de Gemini (`FullyQualifiedName~Gemini`):** `59 / 59` en verde.

### Cobertura de Requerimientos (Matriz de Especificación)
1. **R1 — Autodescubrimiento y Caché del Catálogo de Modelos (`GET /v1beta/models`):**
   - Verificado en `GeminiModelResolverTests.GetCandidateModelsAsync_DiscoversFiltersAndOrdersByPriceTierAndVersionDescending` y `GetCandidateModelsAsync_CachesDiscoveryResponseAndDoesNotQueryAgainOnSubsequentCalls`.
2. **R2 — Filtrado de Modelos Obsoletos, No Generativos y de Alto Coste:**
   - Excluye modelos $< 3.5$ (`gemini-2.0-flash`, `gemini-2.5-flash`, `gemini-3.1-flash-lite`), modelos `pro`, `-preview`, `-tts`, `-image`, `-live` y `embedding`.
3. **R3 — Ordenación Determinista por Escalón de Precio (`flash-lite` $\rightarrow$ `flash`):**
   - Orden verificado: `gemini-3.5-flash-lite` (\$0.30/\$2.50) $\rightarrow$ `gemini-3.8-flash` (\$0.75/\$3.75) $\rightarrow$ `gemini-3.7-flash` $\rightarrow$ `gemini-3.6-flash` $\rightarrow$ `gemini-3.5-flash`.
4. **R4 — Enfriamiento (*Cooldown*) y Exclusión en Memoria ante Fallos HTTP:**
   - Exclusión de 6 h ante `404 NotFound` / `400 BadRequest` (`ReportModelFailure_With404NotFound_ExcludesModelFromCandidates`).
   - Relegación al final de la cola durante 5 min ante `429 TooManyRequests` / `503 ServiceUnavailable` (`ReportModelFailure_With429Or503_MovesCooldownModelToEndOfQueue`).
5. **R5 — Unificación de la Cascada en los Tres Servicios de IA:**
   - `GeminiSocialAnalysisService`, `GeminiGameSummaryService` (individual, lote y expansión) y `GeminiReleaseMatcherService` consumen `IGeminiModelResolver` y reportan fallos/éxitos.
