# Diseño Técnico: change-70-ingesta-multimodal-sorteos

## 1. Visión General de Arquitectura

El Incremento 70 extiende la arquitectura del **Hub de Ingesta Social** (`Ludeka.Application`, `Ludeka.Infrastructure`, `Ludeka.Web`) para admitir un pipeline de **ingesta multimodal asistida por IA**.

```
[Usuario / Moderador]
       │
       ├─ URL de Instagram (post / reel)
       ├─ Foto de portada (captura de post / cartel)  ──┐
       └─ Bases del sorteo (captura OCR o texto plano) ─┤
                                                        │
                                                        ▼
                                       ┌───────────────────────────────────┐
                                       │   SocialExpressIngestModal.razor  │
                                       └─────────────────┬─────────────────┘
                                                         │ SocialExpressMultimodalInputDto
                                                         ▼
                                       ┌───────────────────────────────────┐
                                       │       SocialIngestionService      │
                                       └─────────┬───────────────┬─────────┘
                                                 │               │
                     (Imagen bases + prompt)    │               │ (Imagen portada)
                                                 ▼               ▼
                        ┌───────────────────────────────┐  ┌───────────────────────────────────┐
                        │   GeminiSocialAnalysisService │  │        GiveawayCoverComposer       │
                        │    (Gemini Vision Multimodal) │  │          (SkiaSharp 16:9)          │
                        └───────────────┬───────────────┘  └─────────────────┬─────────────────┘
                                        │                                    │
                                        │ Datos estructurados                │ WebP 16:9 compuesto
                                        │ + cropBoundingBox                  │
                                        ▼                                    ▼
                               ┌───────────────────────────────────────────────────┐
                               │           IImageStorageService (R2)               │
                               │           + ISocialInboxRepository                │
                               │        --> SocialInboxItem (PendingReview)        │
                               └───────────────────────────────────────────────────┘
```

---

## 2. Contratos y DTOs (`Ludeka.Application`)

### 2.1. Bounding Box Normalizado y DTO de Ingesta Multimodal
Ubicación: `Ludeka.Application.DTOs.SocialInboxDtos.cs`

```csharp
public record NormalizedBoundingBoxDto(int YMin, int XMin, int YMax, int XMax)
{
    public bool IsValid => YMin >= 0 && XMin >= 0 && YMax <= 1000 && XMax <= 1000 && YMax > YMin && XMax > XMin;
}

public record SocialExpressMultimodalInputDto(
    string SourceUrl,
    string? ManualCaption = null,
    byte[]? CoverImageBytes = null,
    string? CoverImageFileName = null,
    string? CoverImageMimeType = null,
    byte[]? BasesImageBytes = null,
    string? BasesImageFileName = null,
    string? BasesImageMimeType = null);
```

### 2.2. Ampliación de `ISocialAiAnalysisService`
Ubicación: `Ludeka.Application.Contracts.ISocialAiAnalysisService.cs`

```csharp
public interface ISocialAiAnalysisService
{
    Task<SocialAiAnalysisResultDto> AnalyzeTextAsync(string text, string? authorOrChannel = null, CancellationToken ct = default);

    Task<SocialAiAnalysisResultDto> AnalyzeMultimodalAsync(
        string? text,
        byte[]? basesImageBytes,
        string? basesImageMimeType,
        byte[]? coverImageBytes = null,
        string? coverImageMimeType = null,
        string? authorOrChannel = null,
        CancellationToken ct = default);
}
```

### 2.3. Ampliación de `SocialAiAnalysisResultDto`
Se añaden las propiedades:
- `NormalizedBoundingBoxDto? CropBoundingBox`
- `string? TerritorialScope` (ej. "Península", "España", "Internacional")

### 2.4. Servicio Compositor de Portadas de Sorteos (`IGiveawayCoverComposer`)
Ubicación: `Ludeka.Application.Contracts.IGiveawayCoverComposer.cs`

```csharp
public interface IGiveawayCoverComposer
{
    byte[] ComposeHorizontalCover(
        byte[] originalImageBytes,
        NormalizedBoundingBoxDto? cropBox = null,
        int targetWidth = 1280,
        int targetHeight = 720);
}
```

---

## 3. Implementación de Infraestructura (`Ludeka.Infrastructure`)

### 3.1. `GeminiSocialAnalysisService` (Soporte Multimodal Vision)
- Formato del payload a `models/gemini-2.5-flash:generateContent`:
  ```json
  {
    "contents": [{
      "parts": [
        { "text": "<PROMPT ESTRUCTURADO CON INSTRUCCIONES EN ESPAÑOL>" },
        { "inlineData": { "mimeType": "image/jpeg", "data": "<BASE64>" } }
      ]
    }],
    "generationConfig": {
      "responseMimeType": "application/json",
      "temperature": 0.1
    }
  }
  ```
- En modo simulado (`options.ShouldSimulate` o sin ApiKey):
  - Retorna un resultado determinista simulado que extrae datos de prueba o parsea texto con expresiones regulares, y provee un `cropBoundingBox` por defecto.

### 3.2. `SkiaSharpGiveawayCoverComposer`
Implementa `IGiveawayCoverComposer` usando SkiaSharp:
1. Decodifica el bitmap de entrada (`SKBitmap.Decode`).
2. **Recorte Inteligente:**
   - Si se proporciona `cropBox` válido, extrae el rectángulo de la imagen:
     `x = (xmin * width) / 1000`, `y = (ymin * height) / 1000`, etc.
   - Si no se provee `cropBox` y la imagen tiene proporción de teléfono vertical (`height / width > 1.85`), descarta el 4.5% superior (barra de notificaciones) y el 5.5% inferior (barra de navegación de Android/iOS).
3. **Lienzo 16:9 (`1280x720`):**
   - **Capa 1 (Fondo desenfocado):** Escala la imagen original recortada para cubrir todo el lienzo (`AspectFill`), aplica tinte oscuro (`#0B0F17` al 65%) y filtro de desenfoque gaussiano (`SKImageFilter.CreateBlur(30, 30)`).
   - **Capa 2 (Imagen central nítida):** Escala la imagen recortada para encajar verticalmente (`height = 680px`), centrada horizontalmente. Se aplica una máscara con bordes redondeados (`rx=20, ry=20`) y sombra proyectada difusa (`SKImageFilter.CreateDropShadow`).
   - **Capa 3 (Marca y badge editorial):** Pequeño sello elegante en esquina con tipografía del sistema Ludeka.
4. Codifica el resultado final a WebP con calidad 85%.

### 3.3. Blindaje de `OpenGraphSocialMetadataExtractor`
- Comprueba si el HTML devuelto por Instagram contiene únicamente `<title>Instagram</title>` y ninguna etiqueta `og:image` ni `og:description`.
- En tal caso, no marca `IsVideo = true` por defecto ni inventa títulos: devuelve `Title = null`, `Description = null`, `ImageUrl = null`, permitiendo a `SocialIngestionService` detectar la ausencia de datos y guiar pedagógicamente al moderador.

---

## 4. Capa de Presentación (`Ludeka.Web`)

### 4.1. `SocialExpressIngestModal.razor`
- Rediseño con pestañas claras y UX mobile-first:
  - **URL:** Campo de enlace obligatorio.
  - **Imagen de Portada (Reel / Foto del Sorteo):**
    - Zona interactiva con soporte `@onpaste` (permite pegar directamente una captura con `Ctrl+V` en PC).
    - Selector de archivo estándar de Blazor (`<InputFile>`) para subir desde la galería del móvil.
    - Miniatura de previsualización con botón de borrado.
  - **Bases del Sorteo (Selector de pestaña o acordeón):**
    - Pestaña "Subir captura de bases" (óptimo para móvil).
    - Pestaña "Pegar texto" (óptimo para PC).
  - Botón de envío con estado de carga interactivo ("Analizando bases con IA...", "Componiendo portada 16:9...", "Guardando en bandeja...").

---

## 5. Estrategia de Pruebas Unitarias
- `GeminiSocialAnalysisServiceTests`:
  - Prueba de llamada multimodal con imagen simulada.
  - Verificación de serialización correcta de `inlineData`.
  - Fallback defensivo cuando la imagen está corrupta o la API falla.
- `SkiaSharpGiveawayCoverComposerTests`:
  - Verificación de generación de lienzo 16:9 a partir de imágenes verticales (9:16) y cuadradas (1:1).
  - Verificación de recorte exacto por `cropBoundingBox`.
  - Verificación de descarte de barras de estado para proporciones móviles.
- `SocialIngestionServiceTests`:
  - Prueba de `IngestMultimodalAsync` creando `SocialInboxItem` en estado `PendingReview` con tipo `Giveaway`.
  - Rechazo con excepción controlada si no hay datos ni texto ni imagen.
- `OpenGraphSocialMetadataExtractorTests`:
  - Verificación del blindaje ante respuestas vacías de Instagram.
