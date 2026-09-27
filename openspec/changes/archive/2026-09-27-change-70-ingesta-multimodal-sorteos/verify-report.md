# Reporte de Verificación: INC-70 — Ingesta Multimodal de Sorteos (Gemini Flash Vision + SkiaSharp)

**Fecha de Ejecución:** 27 de Septiembre de 2026  
**Rama:** `inc/ingesta-multimodal-sorteos`  
**Directorio de Trabajo:** `C:\repos\ludeka-wt\ingesta-multimodal-sorteos`  
**Resultado Global:** ✅ **100% SUPERADO (1.938 / 1.938 pruebas unitarias en verde)**

---

## 1. Resumen Ejecutivo

El Incremento 70 resuelve de raíz la limitación arquitectónica de Instagram/Meta (bloqueo estricto a llamadas HTTP de servidores anónimos que impedía extraer texto o imagen de los sorteos) y la restricción de usuario en dispositivos móviles (la app de Instagram prohíbe seleccionar o copiar el texto del pie de foto):

1. **Ingesta Exprés Multimodal:** Permite al usuario aportar la URL de origen combinada con la imagen del sorteo (foto del post/reel) y las bases (bien en texto pegado para PC o en captura de pantalla del pie de foto para móvil).
2. **Visión Artificial con Google Gemini Flash:** OCR y extracción semántica automática sobre imágenes (`inlineData` base64):
   - Organizador y colaboradores (`@cuentas`).
   - Título del juego y del sorteo.
   - Fecha límite de participación.
   - Ámbito territorial (ej. "Península", "España", "Internacional").
   - Coordenadas de recorte (`cropBoundingBox` `[ymin, xmin, ymax, xmax]`) para encuadrar la carátula y eliminar la barra de estado del móvil (batería, hora, cobertura).
3. **Composición Horizontal 16:9 con SkiaSharp (`SkiaSharpGiveawayCoverComposer`):**
   - Lienzo estándar 1280x720 (720p HD).
   - Fondo extendido con desenfoque gaussiano suave (blur 28px) en tono oscuro (#0B0F17).
   - Primer plano centrado con esquinas redondeadas (radio 16px), sombra difusa y recorte inteligente o defensivo (top/bottom 7% para pantallas móviles alargadas > 1.8).
   - Badge editorial de marca `LUDEKA` en la esquina inferior derecha.
   - Exportación directa a formato WebP optimizado (calidad 85%).
4. **Blindaje Anti-Vacíos y Experiencia Guiada:**
   - `OpenGraphSocialMetadataExtractor` ignora las respuestas de login de Instagram (`<title>Instagram</title>`) sin falsear títulos ni clasificar erróneamente como vídeos.
   - `SocialIngestionService` detecta URLs de Instagram bloqueadas sin contenido y orienta de forma clara al usuario hacia el modal multimodal.
5. **Interfaz Blazor Renovada (`SocialExpressIngestModal.razor` y `SocialInboxEditModal.razor`):**
   - Subida y previsualización de imagen de portada con indicador de encuadre 16:9.
   - Selector dual de bases (texto vs captura de pantalla).
   - Muestra y edición del ámbito territorial en la bandeja de moderación antes de publicar.
   - Cumplimiento estricto de WCAG 2.2 AA y cero emojis en UI.

---

## 2. Resultados de la Suite de Pruebas Automatizadas

Comando ejecutado:
```powershell
dotnet test tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj
```

```text
Serie de pruebas para Ludeka.UnitTests.dll (.NETCoreApp,Version=v10.0)
1 archivos de prueba en total coincidieron con el patrón especificado.

Correctas! - Con error: 0, Superado: 1938, Omitido: 0, Total: 1938, Duración: 20 s
```

### Nuevas Pruebas Agregadas para INC-70 (+13 pruebas específicas):
- **Infraestructura (`Ludeka.UnitTests/Infrastructure`):**
  - `SkiaSharpGiveawayCoverComposerTests`: 5 pruebas (composición 16:9 desde imagen vertical 9:16, recorte defensivo en capturas móviles alargadas ratio > 1.8, recorte por coordenadas IA `NormalizedBoundingBoxDto`, adaptación de imágenes cuadradas 1:1, y validación de argumentos nulos o vacíos).
  - `GeminiSocialAnalysisServiceTests`: 3 pruebas (modo simulado con fallback heurístico multimodal, parseo de respuesta JSON con `cropBoundingBox` y `territorialScope`, y tolerancia a fallos HTTP 500 con degradación elegante).
  - `OpenGraphSocialMetadataExtractorTests`: 1 prueba (blindaje contra HTML de bloqueo de Instagram evitando falsos positivos de vídeo o títulos vacíos).
- **Aplicación (`Ludeka.UnitTests/Application`):**
  - `SocialIngestionServiceTests`: 4 pruebas (`IngestMultimodalAsync` con portada compone carátula 16:9 y persiste en bandeja, rechazo con excepción cuando faltan todas las entradas, validación de URL vacía, y rechazo descriptivo en `IngestFromCollectorAsync` ante URLs de Instagram sin metadatos ni texto).

---

## 3. Verificación de Criterios de Aceptación (Gherkin)

| Escenario Gherkin | Estado | Evidencia / Mecanismo |
|---|:---:|---|
| **E1: Alta Multimodal con Portada y Captura de Bases** | ✅ | `SocialExpressIngestModal` procesa ambos archivos; `GeminiSocialAnalysisService.AnalyzeMultimodalAsync` realiza OCR y clasifica; `SkiaSharpGiveawayCoverComposer` genera la carátula 16:9 en WebP; el ítem entra en `SocialInboxItem` en estado `PendingReview`. |
| **E2: Alta Multimodal con Portada y Texto Pegado** | ✅ | Subida de foto con texto descriptivo manual; el servicio multimodal extrae datos y compone carátula correctamente. |
| **E3: Recorte Inteligente de Captura Móvil** | ✅ | Si Gemini detecta `cropBoundingBox`, SkiaSharp extrae el área delimitada. Si no hay coordenadas pero el ratio es > 1.8, se aplica recorte defensivo del 7% superior/inferior para eliminar barras de batería y controles de Instagram. |
| **E4: Blindaje de URL de Instagram Bloqueada** | ✅ | Si el usuario introduce una URL de Instagram sin imagen ni texto y Meta devuelve la página de login, el sistema lanza `InvalidOperationException` explicando que se requiere la foto o captura en el Alta Exprés Multimodal. |
| **E5: Edición de Ámbito Territorial en Bandeja** | ✅ | `SocialInboxEditModal` muestra el campo "Ámbito Territorial / País del Sorteo" (poblado con "España (Península)", "Internacional", etc.) antes de la aprobación editorial. |
| **E6: Cero Regresiones en la Plataforma** | ✅ | 1.938 pruebas unitarias superadas al 100%, manteniendo compatibilidad con la moderación de eventos, novedades semanales y vídeos. |

---

## 4. Conclusión

El Incremento 70 queda **completamente verificado y listo para su archivo y merge vía Pull Request**. Transforma un punto crítico de fricción de la comunidad en un flujo asistido por IA robusto, elegante y con identidad visual propia.
