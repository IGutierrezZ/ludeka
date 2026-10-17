# INC-150: Descubrimiento Dinámico de Modelos Gemini por Escalón de Precio y Unificación de Cascada de Reserva

- **Identificador SDD:** `2026-10-17-inc-150-gemini-modelos-dinamicos`
- **Rama / Worktree:** `inc/gemini-modelos-dinamicos` (`F:\repos\ludeka-wt\gemini-modelos-dinamicos`)
- **Estado:** ⏳ En progreso

---

## 1. Problema y Motivación

Actualmente, la integración con Google Gemini en Ludeka presenta tres fragilidades operativas frente a la rotación constante del catálogo de modelos en Google AI Studio:

1. **Lista estática y precios desactualizados:** `GeminiOptions.DefaultFallbackModels` está cableada a mano (`gemini-3.5-flash-lite`, `gemini-3.5-flash`, `gemini-3.1-flash-lite`, `gemini-3.6-flash`), mientras que en `appsettings.json` se fija `Model: "gemini-3.6-flash"`. En la tarifa oficial vigente de Google, `gemini-3.5-flash` ($1.50/$9.00 por 1M tokens) cuesta el doble que `gemini-3.8-flash` ($0.75/$3.75), y `gemini-3.1-flash-lite` está deprecado con fecha de apagado anunciada.
2. **Servicios desalineados:** Solo `GeminiSocialAnalysisService` recorre la cascada de candidatos; `GeminiGameSummaryService` aún reintenta con el modelo restringido `gemini-2.5-flash` (bloqueado por Google para cuentas nuevas) y en lotes (`CallGeminiBatchApiAsync`) o expansiones no aplica cascada completa; por su parte, `GeminiReleaseMatcherService` solo intenta un único modelo sin reserva.
3. **Ausencia de memoria de fallos (Cooldown):** Si un modelo devuelve `404` (retirado) o `429`/`503` (cuota/saturación), cada petición subsecuente vuelve a chocar contra el mismo modelo fallido antes de saltar al siguiente.

---

## 2. Alcance Funcional y Técnico

1. **Contrato `IGeminiModelResolver` e implementación `GeminiModelResolver` (`Ludeka.Infrastructure`):**
   - Consulta periódicamente el catálogo en vivo de Google AI Studio (`GET {BaseUrl}/models?key={ApiKey}`) y cachea el resultado en memoria (TTL configurable, por defecto 12 horas).
   - **Filtro estricto de elegibilidad:** Admite únicamente modelos que soporten `"generateContent"` y cuyo identificador canónico coincida con `^gemini-(\d+(?:\.\d+)?)-(flash-lite|flash)$` con versión $\ge 3.5$, descartando variantes especializadas (`-tts`, `-live`, `-image`, `-transcribe`, `-embedding`, `-robotics`, `-preview`, `-exp`) y gamas `pro` o legadas (`2.0`, `2.5`, `3.1`).
   - **Ordenación determinista por escalón de precio y versión:**
     1. **Escalón 1 (Más económico):** Familia `*-flash-lite`, ordenada de versión más reciente a más antigua (ej. `gemini-3.5-flash-lite`, y automáticamente cualquier futuro `3.8-flash-lite` o `4.0-flash-lite`).
     2. **Escalón 2 (Siguiente en precio):** Familia `*-flash`, ordenada de versión más reciente a más antigua (`gemini-3.8-flash` $\rightarrow$ `gemini-3.7-flash` $\rightarrow$ `gemini-3.6-flash` $\rightarrow$ `gemini-3.5-flash`).
   - **Penalización temporal (Cooldown / Circuit Breaker en memoria):**
     - `ReportModelFailure(string model, HttpStatusCode? statusCode)`: penaliza con 6 horas los errores `404 NotFound` / `400 BadRequest` y con 5 minutos los errores transitorios `429 TooManyRequests` / `503 ServiceUnavailable`.
     - Los modelos penalizados por `404` se excluyen de la cadena; los modelos en *cooldown* por `429`/`503` se relegan al final de la cola como último recurso para que las peticiones inmediatas entren directamente al siguiente modelo sano más barato.
2. **Actualización de `GeminiOptions` y `appsettings.json`:**
   - Configuración por defecto `Model = "auto"` en `appsettings.json` para que siempre opere la selección dinámica desde el modelo más económico. Si el administrador define explícitamente un modelo en `Gemini__Model` (distinto de `"auto"`), este se respeta en primera posición seguido de la cascada dinámica.
   - Actualización de `DefaultFallbackModels` estático de respaldo (para modo sin red o si falla `GET /models`) a: `["gemini-3.5-flash-lite", "gemini-3.8-flash", "gemini-3.7-flash", "gemini-3.6-flash"]`.
3. **Unificación en los tres servicios de IA:**
   - `GeminiSocialAnalysisService` (texto y multimodal vision), `GeminiGameSummaryService` (individual, lotes y aporte de expansiones) y `GeminiReleaseMatcherService` (inferencia BGG de novedades) consumen `IGeminiModelResolver` y notifican los fallos HTTP para alimentar el *cooldown* compartido.

---

## 3. Criterios de Aceptación y Verificación

- [ ] `GeminiModelResolver` descubre, filtra y ordena los modelos de `GET /v1beta/models` colocando siempre todos los `flash-lite` ($\ge 3.5$, de versión mayor a menor) antes que los `flash` ($\ge 3.5$, de versión mayor a menor).
- [ ] Variantes no deseadas (`gemini-3.8-flash-tts`, `gemini-3.8-live`, `gemini-3.1-flash-image`, `gemini-3.1-pro-preview`, `gemini-2.5-flash`, `gemini-3.1-flash-lite`) quedan excluidas automáticamente.
- [ ] Si un modelo falla con `503` o `429`, `ReportModelFailure` lo mueve al final de la cadena durante la ventana de *cooldown*; si falla con `404`, queda excluido.
- [ ] Si `GET /v1beta/models` falla por red o timeout, el resolutor devuelve sin lanzar excepción la lista de respaldo `GeminiOptions.DefaultFallbackModels`.
- [ ] Los tres servicios (`GeminiSocialAnalysisService`, `GeminiGameSummaryService`, `GeminiReleaseMatcherService`) recorren la cascada dinámica y toda la suite de pruebas unitarias pasa al 100%.
