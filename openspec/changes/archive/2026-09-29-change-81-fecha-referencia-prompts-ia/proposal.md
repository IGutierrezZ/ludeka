# Propuesta: Inyección de Fecha de Referencia y Directivas Temporales en Prompts de Gemini AI

## Motivación
Al analizar publicaciones en redes donde la fecha límite se expresa como «hasta el 2 de octubre» (omitiendo el año), la API de Gemini asignaba el año 2024 (su ancla de preentrenamiento) por falta de contexto temporal en el prompt, provocando que la interfaz de moderación marcara la fecha como pasada y bloqueara la aprobación.

## Alcance
1. Inyectar en los prompts de texto y visión de `GeminiSocialAnalysisService` la fecha actual de referencia (`DateTime.UtcNow:yyyy-MM-dd`) y el año en curso (`DateTime.UtcNow.Year`).
2. Añadir directivas explícitas para resolver fechas sin año hacia el año en curso o el próximo (si ya venció), prohibiendo la inferencia de años pasados.
3. Preservar intacta la validación de fecha en moderación para mantener el control y supervisión del moderador.
4. Cobertura con pruebas unitarias sobre las peticiones HTTP generadas.
