# Especificación: Inyección de Fecha de Referencia y Directivas Temporales en Prompts de Gemini AI

## Requisitos

### R1: Contexto Temporal en Prompt de Texto
`GeminiSocialAnalysisService.CallGeminiApiAsync` debe incluir en el texto del prompt la fecha actual de referencia en formato `YYYY-MM-DD` y el año en curso calculados con `DateTime.UtcNow`.

### R2: Contexto Temporal en Prompt Multimodal con Visión
`GeminiSocialAnalysisService.CallGeminiVisionApiAsync` debe incluir en el texto del prompt la fecha actual de referencia en formato `YYYY-MM-DD` y el año en curso calculados con `DateTime.UtcNow`.

### R3: Directiva Anti-Años Pasados
Ambos prompts deben incluir la directiva explícita de no asumir o inventar años pasados (como 2024 o anteriores) y asumir el año en curso o el próximo si el mes ya venció respecto a la fecha actual.

### R4: Cobertura de Pruebas
Verificación mediante pruebas unitarias en `GeminiSocialAnalysisServiceTests` analizando el payload JSON transmitido a la API.
