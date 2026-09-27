# Especificación: multimodal-express-ingestion

Capacidad de ingesta exprés multimodal asistida para sorteos y publicaciones sociales (especialmente Instagram), con extracción de datos mediante Gemini Flash Vision, recorte automático inteligente de capturas móviles y generación de portadas en plantilla apaisada de Ludeka.

---

## 1. Requerimientos Funcionales

### R1.1: Entrada Multimodal en Alta Exprés
- El modal `SocialExpressIngestModal.razor` debe permitir:
  1. **URL de origen:** Enlace canónico (ej. `https://www.instagram.com/reel/...` o `/p/...`).
  2. **Imagen de Portada (Foto del Sorteo):** Subida de archivo o pegado desde portapapeles (`Ctrl+V` / `@onpaste`), admitiendo formatos de captura de pantalla (JPEG, PNG, WebP).
  3. **Bases del Sorteo (Doble Vía):**
     - Vía A (PC / Texto): Campo de texto plano para escribir o pegar las bases copiadas.
     - Vía B (Móvil / Captura): Subida o pegado de una captura de pantalla del pie de foto o bases del post.
- La interfaz debe ofrecer previsualizaciones inmediatas de las imágenes cargadas y permitir eliminarlas o sustituirlas antes de enviar.

### R1.2: Análisis Semántico y OCR con Google Gemini Flash Vision
- `ISocialAiAnalysisService` y `GeminiSocialAnalysisService` deben incorporar capacidad multimodal:
  - Cuando se proporciona captura de bases (o imagen con texto del sorteo), la imagen se envía en el payload JSON a Gemini (`inlineData` en base64 con su MIME type).
  - El prompt en español instruye a Gemini para:
    - Realizar OCR sobre el texto visible de la captura.
    - Extraer organizador principal y colaboradores (`@...`).
    - Identificar el juego o premio sorteado (ej. "Tiny Table", "Cascadia").
    - Extraer la fecha y hora límite de participación en formato ISO universal.
    - Determinar el ámbito territorial del sorteo (ej. "Península", "España", "Internacional").
    - Extraer las coordenadas normalizadas del recorte (`cropBoundingBox: [ymin, xmin, ymax, xmax]`, valores 0-1000) de la imagen principal del premio o cartel, descartando las barras de estado del móvil y controles de la aplicación.
- Si no hay imagen de bases pero sí texto plano, se ejecuta el análisis semántico de texto existente.
- En modo simulado o sin API Key, el servicio proveerá un fallback heurístico y de recorte por defecto.

### R1.3: Recorte Inteligente y Composición de Portada en Plantilla Ludeka
- Si se proporciona una imagen de portada:
  1. Si Gemini devuelve coordenadas `cropBoundingBox`, el backend (SkiaSharp) recorta la imagen a esa región útil.
  2. Si no hay coordenadas o la imagen es una captura de pantalla completa de teléfono móvil (proporción vertical ~9:20), se aplica un recorte defensivo que suprime la barra superior de notificaciones/hora (~4%) y la barra inferior de navegación (~5%).
  3. **Composición de Portada Apaisada (16:9):**
     - Las tarjetas de `/sorteos` requieren proporción horizontal estándar (~16:9 / `h-44`).
     - Para imágenes verticales (9:16) o cuadradas (1:1), el compositor genera un lienzo apaisado donde:
       - El fondo es la propia imagen escalada y con filtro de desenfoque gaussiano oscuro (blur background).
       - En el centro se renderiza la imagen recortada nítida con sombra proyectada y bordes redondeados.
       - Se superpone de forma sutil la marca de Ludeka o badge de sorteo.
  4. La imagen resultante se optimiza a WebP y se persiste en Cloudflare R2 bajo `social-inbox/{id}/thumbnail.webp`.

### R1.4: Blindaje contra Páginas Vacías de Instagram
- `OpenGraphSocialMetadataExtractor`:
  - Si una URL de Instagram devuelve una cáscara vacía (`<title>Instagram</title>` y sin etiquetas OpenGraph de post ni miniatura), el extractor no debe clasificarlo ciegamente como vídeo ni asignarle título engañoso.
- `SocialIngestionService`:
  - Si una ingesta por URL no contiene texto descargado, ni texto manual, ni imagen multimodal, la petición debe rechazarse con un mensaje descriptivo y pedagógico: *"Instagram requiere sesión para leer esta URL. Por favor, adjunta la foto del sorteo o la captura de las bases para procesarlo con IA"*.

---

## 2. Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Ingesta multimodal de sorteo desde móvil con captura de bases y portada
  Dado que el moderador introduce la URL "https://www.instagram.com/reel/C-sorteo/"
  Y adjunta la imagen de portada del Reel
  Y adjunta la captura de pantalla con las bases que mencionan "sorteamos una Tiny Table activo hasta 02/10/2026 en Península"
  Cuando se procesa la ingesta exprés
  Entonces el ítem se registra con DetectedType "Giveaway"
  Y el título incluye "Tiny Table"
  Y el organizador y colaborador se extraen adecuadamente
  Y la fecha límite se fija en "2026-10-02T23:59:00"
  Y la ubicación/ámbito territorial se marca como "Península"
  Y la portada se genera en R2 como un lienzo 16:9 con fondo difuminado sin barras de estado del móvil

Escenario: Ingesta en PC con texto plano pegado e imagen de portada
  Dado que el moderador introduce la URL de Instagram
  Y adjunta la foto del sorteo
  Y pega el texto copiado de las bases en el campo de texto
  Cuando se procesa la ingesta exprés
  Entonces Gemini analiza el texto directamente
  Y la portada se compone en la plantilla editorial de Ludeka

Escenario: Rechazo de URL de Instagram vacía sin datos complementarios
  Dado que el moderador introduce una URL de Instagram bloqueada por Meta
  Y no adjunta ninguna imagen ni escribe texto alguno
  Cuando intenta procesar el alta
  Entonces el sistema devuelve un error de validación claro impidiendo crear un borrador vacío
```
