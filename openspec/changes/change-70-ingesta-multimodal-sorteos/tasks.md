# Tareas de Implementación: change-70-ingesta-multimodal-sorteos (Incremento 70)

## Fase 1: Contratos y DTOs (`Ludeka.Application`)
- [ ] 1.1 Crear `NormalizedBoundingBoxDto` y `SocialExpressMultimodalInputDto` en `Ludeka.Application.DTOs.SocialInboxDtos`.
- [ ] 1.2 Extender `SocialAiAnalysisResultDto` con `CropBoundingBox` y `TerritorialScope`.
- [ ] 1.3 Extender la interfaz `ISocialAiAnalysisService` con la sobrecarga `AnalyzeMultimodalAsync`.
- [ ] 1.4 Crear la interfaz `IGiveawayCoverComposer` en `Ludeka.Application.Contracts`.
- [ ] 1.5 Extender la interfaz `ISocialIngestionService` con `IngestMultimodalAsync`.

## Fase 2: Servicios de Infraestructura e IA (`Ludeka.Infrastructure`)
- [ ] 2.1 Implementar soporte multimodal en `GeminiSocialAnalysisService`:
  - Serialización de `inlineData` (base64) en el payload de Google Gemini Flash.
  - Prompt especializado en OCR de bases de sorteos y detección de `cropBoundingBox`.
  - Fallback heurístico multimodal para modo simulado y pruebas unitarias.
- [ ] 2.2 Implementar `SkiaSharpGiveawayCoverComposer` en `Ludeka.Infrastructure.Services`:
  - Recorte por bounding box o márgenes defensivos de capturas móviles.
  - Composición 16:9 con fondo desenfocado oscuro y sello de Ludeka.
  - Conversión optimizada a WebP.
- [ ] 2.3 Blindar `OpenGraphSocialMetadataExtractor`:
  - Detección de respuesta vacía de Instagram (`<title>Instagram</title>`) sin falsear `IsVideo` ni inventar títulos.
- [ ] 2.4 Registrar `IGiveawayCoverComposer` en la inyección de dependencias (`ServiceCollectionExtensions`).

## Fase 3: Lógica de Aplicación (`Ludeka.Application`)
- [ ] 3.1 Implementar `IngestMultimodalAsync` en `SocialIngestionService`:
  - Validación defensiva: rechazar si no hay URL, ni texto, ni imagen.
  - Procesamiento con `AnalyzeMultimodalAsync`.
  - Composición y subida de portada a Cloudflare R2 vía `IGiveawayCoverComposer` y `IImageStorageService`.
  - Persistencia del ítem en `ISocialInboxRepository` clasificado como `Giveaway` con ámbito territorial y fecha límite.

## Fase 4: Componente de Interfaz Blazor (`Ludeka.Web`)
- [ ] 4.1 Actualizar `SocialExpressIngestModal.razor`:
  - Incorporar selector de imagen / pegado de portada (`@onpaste`).
  - Incorporar doble vía para bases: caja de texto o captura de pantalla.
  - Previsualizaciones interactivas de las imágenes adjuntas.
  - Feedback visual de estados de procesamiento de IA y subida.
- [ ] 4.2 Actualizar `SocialInboxEditModal.razor` para reflejar el ámbito territorial extraído y la portada compuesta.

## Fase 5: Suite de Pruebas Unitarias (`tests/Ludeka.UnitTests`)
- [ ] 5.1 Pruebas para `GeminiSocialAnalysisService` (multimodal y bounding box).
- [ ] 5.2 Pruebas para `SkiaSharpGiveawayCoverComposer` (proporciones 9:16, 1:1, recorte por coordenadas y formato WebP).
- [ ] 5.3 Pruebas para `SocialIngestionService.IngestMultimodalAsync`.
- [ ] 5.4 Pruebas de contrato y blindaje en `OpenGraphSocialMetadataExtractorTests`.
- [ ] 5.5 Ejecutar la suite completa (`dotnet test`) asegurando 0 regresiones.
