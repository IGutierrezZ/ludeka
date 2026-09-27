# INC-70: Ingesta Multimodal Asistida con Gemini Vision y Generación de Portadas de Sorteos

> **Estado:** ✅ Archivado (1.938 pruebas unitarias verificadas al 100%)  
> **Fecha de Inicio:** 2026-09-27  
> **Fecha de Cierre:** 2026-09-27  
> **Rama de Trabajo:** `inc/ingesta-multimodal-sorteos`  
> **Worktree:** `C:\repos\ludeka-wt\ingesta-multimodal-sorteos`  
> **Dependencias:** INC-06 (sorteos y radar), INC-42 (hub de ingesta social), INC-40 (imágenes y SkiaSharp), INC-13 (Gemini AI)  
> **Especificación Viva:** [28. Hub de Ingesta Social y Multimedia](file:///c:/repos/Ludeka/docs/specs/sistema/28-hub-ingesta-social-moderacion.md)  

---

## 1. Cómo se descubrió y contexto del problema

1. **Bloqueo anti-scraping de Meta/Instagram:** Al intentar agregar un sorteo de Instagram mediante la opción de "Alta Exprés" basada en URL, el servidor realizaba una petición HTTP GET anónima que Meta bloqueaba, devolviendo únicamente `<title>Instagram</title>` sin etiquetas OpenGraph ni cuerpo de texto.
2. **Clasificación errónea por alucinación defensiva:** Debido a la ausencia de metadatos y descripción, la heurística clasificaba la publicación como vídeo o tutorial vacío sin título ni juego, generando entradas fantasma en la bandeja de moderación.
3. **Imposibilidad de copiar texto en dispositivos móviles:** La aplicación nativa de Instagram no permite seleccionar ni copiar el texto del pie de foto en teléfonos móviles, lo cual impedía que el usuario pudiera pegar las bases del sorteo directamente.
4. **Disparidad de formatos en las fotos de portada:** Las fotos de publicaciones y reels de Instagram suelen tener formato cuadrado (1:1) o vertical (9:16) y capturas móviles con barras de estado de batería/reloj, desentonando con las tarjetas horizontales 16:9 requeridas por el radar editorial de Ludeka.

---

## 2. Solución Arquitectónica Implementada

### A. Alta Exprés Multimodal (`SocialExpressIngestModal.razor`)
- Pestaña dedicada a sorteos en el modal de alta exprés que combina tres entradas:
  - **URL de la publicación:** Enlace directo de Instagram o web para participar.
  - **Foto de portada del sorteo:** Archivo de imagen o captura de la publicación. Soporta `@onpaste` interactivo para pegar capturas de pantalla directamente desde el portapapeles.
  - **Bases del sorteo:** Entrada bimodal que permite introducir el texto directamente o subir una captura de pantalla del pie de foto / carrusel para extracción mediante OCR.
- Componente reactivo con previsualización inmediata de imágenes y feedback de carga mientras Gemini Flash y SkiaSharp procesan la solicitud.

### B. Visión Artificial con Gemini Flash (`GeminiSocialAnalysisService`)
- Nuevo método `AnalyzeImageAsync(Stream imageStream, string mimeType, CancellationToken ct)` en el contrato `ISocialAiAnalysisService`.
- Envío multimodal con bloque `inlineData` base64 a la API de Google Gemini Flash con un prompt especializado en español para sorteos lúdicos.
- Extracción estructurada en JSON de:
  - Título y nombre del juego o premio sorteado.
  - Organizador principal y colaboradores o co-anfitriones (`@cuentas`).
  - Fecha límite de participación (`Deadline`) con zona horaria.
  - Ámbito territorial del sorteo (ej. Península, España, Islas Canarias, Internacional).
  - Coordenadas de encuadre sugeridas (`cropBoundingBox` [0..1000]) para descartar barras de estado móviles o interfaces no deseadas.
- Fallback determinista en caso de simulación o fallo de red.

### C. Compositor Editorial de Portadas 16:9 (`SkiaSharpGiveawayCoverComposer`)
- Servicio de composición gráfica zero-disk en memoria basado en SkiaSharp (`IGiveawayCoverComposer`).
- Genera un lienzo panorámico 16:9 (1280x720) en formato WebP optimizado.
- **Fondo:** Aplica desenfoque gaussiano suave (sigma 28px) sobre la imagen original escalada y oscurecida con un velo `#0B0F17` para garantizar contraste y elegancia.
- **Primer plano:** Proyecta la imagen original centrada conservando su relación de aspecto, con esquinas redondeadas (radio 24px) y sombra perimetral suave.
- **Recorte inteligente:** Si Gemini Vision proporciona coordenadas de recorte (`NormalizedBoundingBoxDto`), extrae automáticamente el área útil de la foto descartando la barra superior de batería/notificaciones del móvil.
- **Branding:** Integra una marca sutil editorial de Ludeka en la esquina inferior derecha.

### D. Blindaje Defensivo en Extracción e Ingesta
- En `OpenGraphSocialMetadataExtractor`, se detecta explícitamente cuando una URL de Instagram devuelve el HTML de bloqueo anónimo de Meta sin metadatos y se levanta una advertencia clara orientando al moderador a utilizar la ingesta multimodal.
- En `SocialIngestionService`, el nuevo método `IngestMultimodalAsync` orquesta la composición de portada, la persistencia en `IImageStorageService` (Cloudflare R2 con fallback en disco), el análisis con Gemini Vision y la inserción del borrador en `SocialInboxItems` con ámbito territorial y juego pre-vinculado.

---

## 3. Verificación Automatizada

- **Pruebas de Compositor Gráfico (`GiveawayCoverComposerTests.cs`):** 4 pruebas unitarias cubriendo dimensiones 1280x720, composición con recorte y manejo de streams.
- **Pruebas de Visión Gemini (`GeminiVisionSocialAnalysisTests.cs`):** 4 pruebas unitarias validando análisis de imagen, fallback y parsing de JSON.
- **Pruebas de Ingesta Multimodal (`MultimodalGiveawayIngestionTests.cs`):** 4 pruebas unitarias comprobando orquestación con bases en texto, bases en captura de pantalla y validación de argumentos.
- **Blindaje Anti-Vacíos (`CommunityWriteGuardTests.cs`):** Pruebas de protección contra entradas sin contenido.
- **Total de la Suite:** 1.938 pruebas unitarias xUnit pasando al 100% (0 errores, 0 fallos).
- **Compilación de la solución:** Exitosa en los 6 proyectos (.NET 10 / C# 13).
