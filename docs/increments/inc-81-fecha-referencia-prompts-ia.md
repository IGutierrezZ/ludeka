# Incremento 81: Inyección de Fecha de Referencia y Directivas Temporales en Prompts de Gemini AI

> **ID:** INC-81  
> **Slug:** `fecha-referencia-prompts-ia`  
> **Rama:** `inc/fecha-referencia-prompts-ia`  
> **Estado:** ⏳ En progreso  
> **Módulos Impactados:** Módulo 28 (`docs/specs/sistema/28-hub-ingesta-social-moderacion.md`), Módulo 13 (`docs/specs/sistema/13-sintesis-ia-resumen.md`)  
> **Dependencias:** INC-80 (Rediseño de moderación social a carteles compactos y reintento IA).

---

## 1. Contexto y Diagnóstico del Problema

1. **Inferencia anacrónica de año en Gemini (defecto 2024):**
   Al procesar publicaciones sociales (Instagram, YouTube o web) con menciones a fechas límites sin año explícito (por ejemplo, *«tienes hasta el 2 de octubre para participar»*), los modelos de Gemini no contaban con la fecha ni el año de referencia actual en el *prompt*.
   Como resultado, la IA asignaba sistemáticamente años de su ventana de entrenamiento previa (como `2024-10-02T23:59:59Z`), provocando que la bandeja de moderación detectara una fecha pasada y bloqueara la aprobación del sorteo.
2. **Ausencia de contexto temporal en las llamadas de texto y visión:**
   Tanto en `CallGeminiApiAsync` como en `CallGeminiVisionApiAsync` de `GeminiSocialAnalysisService`, se solicitaba una fecha ISO (`eventOrReleaseDateIso`) sin suministrar la fecha UTC actual de evaluación (`DateTime.UtcNow`).

---

## 2. Objetivos del Incremento

1. **Inyección de fecha actual de referencia en los prompts de IA:**
   - Inyectar `{DateTime.UtcNow:yyyy-MM-dd}` y `(año en curso: {DateTime.UtcNow.Year})` en la cabecera de las instrucciones de `CallGeminiApiAsync` y `CallGeminiVisionApiAsync`.
2. **Directivas explícitas de resolución de fechas:**
   - Indicar a Gemini que si el texto o imagen menciona día y mes sin año, asuma el año en curso (`DateTime.UtcNow.Year`) o el siguiente (`DateTime.UtcNow.Year + 1`) si la fecha ya venció respecto a la fecha actual.
   - Prohibir taxativamente inferir o asumir años pasados (2024 o anteriores) a menos que figuren textualmente en la publicación original.
   - Preservar la validación y control humano en interfaz para detectar cualquier fecha inválida al intentar aprobar.
3. **Validación automatizada por pruebas:**
   - Pruebas unitarias en `GeminiSocialAnalysisServiceTests` verificando la presencia de las cabeceras temporales y directivas anti-pasado en las peticiones HTTP a Gemini.

---

## 3. Plan de Pruebas y Criterios de Aceptación (TDD)

- **Test 1:** `AnalyzeTextAsync_WhenCallingGeminiApi_InjectsCurrentDateAndYearReferenceIntoPrompt` verifica la inyección de fecha ISO actual, año corriente y directivas temporales en el cuerpo JSON enviado a Gemini.
- **Test 2:** `AnalyzeMultimodalAsync_WhenCallingGeminiApi_InjectsCurrentDateAndYearReferenceIntoPrompt` verifica la inyección de fecha y año de referencia en el análisis multimodal con visión OCR.
- **Test 3:** La suite completa pasa al 100% (2.081 unitarias + 10 integración = 2.091 pruebas en verde).
