# Tareas: Inyección de Fecha de Referencia y Directivas Temporales en Prompts de Gemini AI

- [x] Tarea 1: Inyectar fecha de referencia `{DateTime.UtcNow:yyyy-MM-dd}` y año `{DateTime.UtcNow.Year}` con directivas de fecha en `GeminiSocialAnalysisService.CallGeminiApiAsync`.
- [x] Tarea 2: Inyectar fecha de referencia `{DateTime.UtcNow:yyyy-MM-dd}` y año `{DateTime.UtcNow.Year}` con directivas de fecha en `GeminiSocialAnalysisService.CallGeminiVisionApiAsync`.
- [x] Tarea 3: Implementar pruebas unitarias en `GeminiSocialAnalysisServiceTests` validando la presencia del contexto temporal en las llamadas HTTP a Gemini.
- [x] Tarea 4: Ejecutar suite de pruebas completa y verificar 0 regresiones.
