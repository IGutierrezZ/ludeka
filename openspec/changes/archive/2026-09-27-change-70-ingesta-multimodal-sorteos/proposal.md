# Propuesta: change-70-ingesta-multimodal-sorteos (Incremento 70: Ingesta Multimodal Asistida con Gemini Vision y Generación de Portadas de Sorteos)

## 1. Resumen Ejecutivo y Motivación

En Ludeka, el **Radar de Sorteos** (`/sorteos`) es una de las áreas de mayor tracción comunitaria. La mayor parte de los sorteos del sector se publican en Instagram (Reels y posts en carrusel de editoriales y creadores).

Sin embargo, debido a las restricciones anti-scraping de Meta y el cierre de su API pública de oEmbed (que ahora requiere verificación societaria comercial con CIF e inspección de aplicación inviable para un proyecto comunitario independiente), la extracción automatizada por simple HTTP GET sobre URLs de Instagram falla sistemáticamente: Meta devuelve una cáscara vacía con `<title>Instagram</title>`, dejando sin imagen ni texto al asistente de catalogación. Esto provocaba que publicaciones de sorteos se catalogasen erróneamente como vídeos o tutoriales vacíos, obligando a rellenar manualmente los datos y perdiendo la miniatura o forzando recortes inadecuados para imágenes verticales (9:16) en cabeceras de tarjeta horizontales (16:9).

Además, en dispositivos móviles, la aplicación oficial de Instagram no permite seleccionar ni copiar el texto de las descripciones, por lo que la captura de pantalla es la única vía ágil que tienen los moderadores para capturar las bases.

El **Incremento 70** resuelve esta fricción implantando un flujo de **Alta Exprés Multimodal Asistida**:
1. **Entrada Multimodal en `SocialExpressIngestModal.razor`:**
   - URL canónica de origen (para el botón «Participar en Instagram»).
   - Subida o pegado directo desde portapapeles (`Ctrl+V` o selector de archivo) de la **imagen del sorteo / Reel** (portada).
   - Doble vía para las bases: caja de texto plano para moderadores en PC **o** subida/pegado de la **captura de pantalla con las bases del sorteo** para moderadores en móvil.
2. **Análisis Semántico Multimodal con Google Gemini Flash Vision (`GeminiSocialAnalysisService`):**
   - Soporte para enviar imágenes (base64 inline) junto con el prompt a la API de Gemini.
   - Si se adjunta captura de bases, Gemini Vision realiza OCR y extracción estructurada de los datos clave: organizador, cuentas colaboradoras (`@...`), premio/juego de mesa, fecha límite de fin de sorteo y ámbito geográfico (Península, España, Baleares/Canarias, Internacional).
3. **Composición de Portada con Plantilla Editorial Ludeka (`SocialCardService` / SkiaSharp / Canvas):**
   - Generación automática de carátula en proporción horizontal estándar (`16:9` / `h-44`), adaptando la imagen vertical centrada sobre un fondo oscuro desenfocado y con el sello editorial de Ludeka, evitando recortes que amputen elementos clave del cartel.
   - Optimización y subida a Cloudflare R2 vía `IImageStorageService`.
4. **Blindaje de la Ingesta por URL:**
   - Si una URL de Instagram no entrega texto ni imagen y el moderador no aporta captura ni bases, el sistema avisa con validación clara en lugar de persistir borradores vacíos clasificados como vídeo o tutorial.

---

## 2. Arquitectura y Alcance por Capas

### 2.1 Dominio (`Ludeka.Core`)
- Enriquecimiento de `SocialInboxItem`:
  - Asegurar la compatibilidad con el ámbito territorial detectado (`Country` o `IsInternational` si aplica para sorteos).
  - Preservación de notas del análisis de visión para auditoría del moderador.

### 2.2 Aplicación (`Ludeka.Application`)
- **`ISocialAiAnalysisService`**:
  - Ampliación del contrato con sobrecarga multimodal:
    `Task<SocialAiAnalysisResultDto> AnalyzeMultimodalAsync(string? text, byte[]? basesImageBytes, string? basesImageMimeType, string? authorOrChannel = null, CancellationToken ct = default)`
- **`ISocialIngestionService`**:
  - Nuevo método de ingesta multimodal asistida:
    `Task<SocialInboxItemDto> IngestMultimodalAsync(SocialExpressMultimodalInputDto input, CancellationToken ct = default)`
- **`ISocialCardService` / `IGiveawayCardComposer`**:
  - Generador de carátula compuesta para sorteos: compone una imagen origen (incluso vertical) en un lienzo apaisado 16:9 con fondo difuminado y marco editorial de Ludeka.

### 2.3 Infraestructura (`Ludeka.Infrastructure`)
- **`GeminiSocialAnalysisService`**:
  - Soporte de `inlineData` con MIME type (`image/jpeg`, `image/png`, `image/webp`) en la estructura JSON enviada a la API de Gemini.
  - Prompt especializado en extracción de sorteos lúdicos a partir de imágenes de bases.
  - Fallback local para tests y desarrollo offline.
- **`OpenGraphSocialMetadataExtractor`**:
  - Detección explícita de páginas vacías de Instagram (título genérico `"Instagram"` sin metadatos) para no marcar falsamente `IsVideo` ni devolver cadenas vacías engañosas.

### 2.4 Interfaz de Usuario (`Ludeka.Web`)
- **`SocialExpressIngestModal.razor`**:
  - Rediseño de la pestaña "Pegar URL y Listo (IA)" a "Alta Exprés Multimodal":
    - Zona de pegado/arrastre de la imagen de portada con previsualización inmediata.
    - Zona de bases: selector entre "Pegar texto" y "Subir captura de bases".
    - Soporte de evento `@onpaste` para pegar capturas directamente con `Ctrl+V`.
    - Indicadores de carga claros durante el análisis visual con Gemini Flash.
- **`SocialInboxModeration.razor` / `SocialInboxEditModal.razor`**:
  - Vista previa de la portada compuesta y de los datos estructurados extraídos por la IA antes de aprobar.

---

## 3. Criterios de Aceptación y Pruebas
1. **Captura Multimodal Exitosa:** Al enviar una URL de Instagram con una captura de bases y una portada, Gemini Vision extrae correctamente el premio ("Tiny Table"), organizador ("pareja_ludica"), colaborador ("mesasparajuegos"), fecha límite ("02/10/2026 23:59") y ámbito ("Península / España").
2. **Plantilla Editorial Generada:** La imagen resultante en Cloudflare R2 tiene relación de aspecto horizontal adecuada para las tarjetas de `/sorteos` sin recortes deformados.
3. **Bandeja de Moderación Limpia:** La publicación entra con tipo `Giveaway`, sin falsos positivos de `MediaItem` ni categoría `Tutorial`.
4. **Resiliencia sin API Key:** Si Gemini no está configurado o falla, el sistema utiliza el generador heurístico o solicita revisión manual sin lanzar un error 500.
5. **Cero Emojis y Accesibilidad:** Cumplimiento de `WebMarkupContractTests` y contraste WCAG 2.2 AA.
