# INC-70: Ingesta Multimodal Asistida con Gemini Vision y Generación de Portadas de Sorteos

- **Incremento:** INC-70
- **Slug / Rama:** `ingesta-multimodal-sorteos` (`inc/ingesta-multimodal-sorteos`)
- **Estado:** ⏳ En progreso (Fase SDD: `sdd-propose`)
- **Módulo:** Radar Comunitario, Ingesta Social y Moderación Editorial (`src/Ludeka.Web`, `src/Ludeka.Application`, `src/Ludeka.Infrastructure`)

---

## 1. Contexto y Justificación

El Radar de Sorteos de Ludeka (`/sorteos`) se nutre de sorteos comunitarios de editoriales y creadores, principalmente desde Instagram.

Debido al bloqueo anti-scraping de Meta (que exige verificación societaria comercial para su API de oEmbed), las peticiones HTTP anónimas desde el servidor devuelven páginas vacías sin metadatos OpenGraph ni descripción. Esto generaba entradas fantasma clasificadas como vídeos o tutoriales vacíos en la bandeja de moderación. Asimismo, la app móvil de Instagram no permite copiar el texto de las publicaciones, forzando a los moderadores a recurrir a capturas de pantalla.

Este incremento introduce el flujo de **Alta Exprés Multimodal**:
1. Carga de la URL de Instagram (enlace para participar).
2. Carga de la foto/portada del sorteo (adaptada automáticamente con plantilla editorial de Ludeka para cabeceras 16:9).
3. Carga de las bases del sorteo mediante texto directo o captura de pantalla, procesada con Gemini Flash Vision (OCR + estructuración de organizador, colaborador, premio/juego, fecha límite y ámbito territorial).
4. Blindaje del extractor para no generar borradores erróneos.

---

## 2. Componentes Impactados
- `src/Ludeka.Application/Contracts/ISocialAiAnalysisService.cs`: Firma multimodal.
- `src/Ludeka.Application/Contracts/ISocialIngestionService.cs`: Flujo de ingesta multimodal.
- `src/Ludeka.Application/DTOs/SocialInboxDtos.cs`: DTOs de entrada y salida multimodal.
- `src/Ludeka.Application/Features/Community/SocialIngestionService.cs`: Orquestación multimodal.
- `src/Ludeka.Infrastructure/Services/GeminiSocialAnalysisService.cs`: Envío de imagen inlineData a Gemini API y fallback.
- `src/Ludeka.Infrastructure/Services/OpenGraphSocialMetadataExtractor.cs`: Blindaje de respuestas vacías de Instagram.
- `src/Ludeka.Application/Features/Community/SocialCardService.cs`: Composición de plantilla de sorteo con fondo difuminado.
- `src/Ludeka.Web/Components/Shared/SocialExpressIngestModal.razor`: Interfaz interactiva multimodal con soporte `@onpaste` y carga de imágenes.
- Pruebas unitarias en `tests/Ludeka.UnitTests/`.

---

## 3. Criterios de Aceptación
1. Ingesta fluida desde móvil (subiendo captura de bases) y desde PC (pegando texto o captura).
2. Extracción correcta de premio, organizador, colaboradores, fecha fin y país.
3. Carátula resultante adaptada al formato de tarjeta de Ludeka sin cortes indeseados.
4. Cobertura de pruebas unitarias al 100% en los nuevos componentes y servicios.
