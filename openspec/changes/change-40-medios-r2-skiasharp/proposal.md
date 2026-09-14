# Propuesta: change-40-medios-r2-skiasharp (Incremento 40: Pipeline de Almacenamiento y Optimización de Medios)

## 1. Resumen Ejecutivo y Motivación

Actualmente, Ludeka muestra carátulas y recursos multimedia dependiendo de enlaces directos externos (principalmente el CDN de BoardGameGeek `cf.geekdo-images.com`) o assets estáticos locales de desarrollo. Esta dependencia acarrea riesgos graves: rotura de enlaces externos, bloqueos por políticas de referer, lentitud de carga no controlada y falta de compresión homogénea.

El **Incremento 40** establece los cimientos de la infraestructura de medios de Ludeka, dotando a la plataforma de:
1. **Almacenamiento en Cloudflare R2:** Uso de la API S3 estándar con **coste cero en comisiones de transferencia saliente** (*zero egress fees*).
2. **Optimización Zero-Disk con SkiaSharp:** Procesamiento gráfico en memoria (sin escribir archivos temporales a disco) utilizando la librería de código abierto **SkiaSharp** (licencia MIT libre, sin restricciones comerciales).
3. **Conversión y Compresión WebP:** Reducción drástica del peso de transferencia con calidad optimizada (80–82%), preservando nitidez y aspect ratio original.
4. **Variantes Deterministas:**
   - Versión principal (`cover`, `back`): ancho máximo 1000px.
   - Miniatura (`cover_thumb`): ancho máximo 400px.
   - En mesa (`table`): ancho máximo 1200px.
   - Nomenclatura determinista en el bucket: `games/{bggId}/cover.webp`, `games/{bggId}/cover_thumb.webp`, `games/{bggId}/back.webp`, `games/{bggId}/table.webp`, y `social/{year}/{month}/{guid}.webp`.
5. **Estrategia Dual y Modo Simulado:** Soporte completo de modo simulado/local para desarrollo sin conexión y ejecución de suites de pruebas unitarias sin necesidad de credenciales reales de Cloudflare.

---

## 2. Alcance por Capas del Sistema

### 2.1 Dominio (`Ludeka.Core`)
- **`ImageVariantUrls`:** Record/Value Object: `string FullUrl, string ThumbUrl`.
- **`GameImageType`:** Constantes o Value Object: `Cover`, `Back`, `Table`, `Social`.

### 2.2 Aplicación (`Ludeka.Application`)
- **`IImageOptimizationService`:**
  - `Task<byte[]> ConvertToWebpAsync(Stream inputStream, int maxWidth, int quality = 82, CancellationToken ct = default);`
  - `Task<(int Width, int Height)> GetDimensionsAsync(Stream inputStream, CancellationToken ct = default);`
- **`IImageStorageService`:**
  - `Task<string> UploadOptimizedImageAsync(Stream inputStream, string objectKey, int maxWidth = 1000, int quality = 82, CancellationToken ct = default);`
  - `Task<ImageVariantUrls> UploadGameImageVariantsAsync(Stream rawImageStream, int bggId, string imageType, CancellationToken ct = default);`
  - `Task<bool> DeleteImageAsync(string objectKey, CancellationToken ct = default);`
  - `string GetPublicUrl(string objectKey);`
- **`CloudflareR2Options`:** Opciones fuertemente tipadas: `AccountId`, `AccessKeyId`, `SecretAccessKey`, `BucketName`, `PublicCdnBaseUrl`, `Simulate`.

### 2.3 Infraestructura (`Ludeka.Infrastructure`)
- **Dependencias NuGet:**
  - `AWSSDK.S3` para el cliente de Cloudflare R2 (compatible S3).
  - `SkiaSharp` (motor gráfico multiplataforma con codificador WebP nativo).
  - `SkiaSharp.NativeAssets.Linux.NoDependencies` para ejecución en contenedores Linux / Cloud Run.
- **`SkiaSharpImageOptimizationService`:**
  - Lectura de bitmaps desde stream en memoria.
  - Cálculo de escala manteniendo aspect ratio (`Math.Min(1.0, (double)maxWidth / originalWidth)`).
  - Redimensionamiento con filtro de calidad alta (`SKFilterQuality.High`).
  - Codificación a `SKEncodedImageFormat.Webp` con calidad 80-82.
- **`CloudflareR2StorageService`:**
  - Conexión vía `AmazonS3Client` hacia `https://{AccountId}.r2.cloudflarestorage.com`.
  - Subida con `PutObjectRequest` asignando cabeceras `ContentType: image/webp` y `Cache-Control: public, max-age=31536000, immutable`.
- **`SimulatedImageStorageService`:**
  - Almacén en memoria o ruta local de desarrollo cuando `Simulate = true`.

### 2.4 Pruebas Unitarias (`Ludeka.UnitTests`)
- Pruebas de optimización con SkiaSharp: conversión a WebP, escalado sin upscaling artificial cuando la imagen es menor a `maxWidth`, preservación de aspect ratio.
- Pruebas de nombrado determinista de rutas de juego (`games/{bggId}/...`).
- Pruebas de resiliencia y modo simulado del servicio de almacenamiento.

---

## 3. Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Optimizar y generar variantes de portada de un juego
  Dado un stream de imagen JPEG o PNG de alta resolución de un juego (BggId 342942)
  Cuando se invoca UploadGameImageVariantsAsync con tipo "cover"
  Entonces se generan y suben dos archivos a R2: "games/342942/cover.webp" y "games/342942/cover_thumb.webp"
  Y la versión principal tiene un ancho máximo de 1000px y la miniatura de 400px
  Y ambas variantes son formato WebP con compresión de alta fidelidad

Escenario: Imagen de trasera y en mesa
  Dado un stream de imagen de componentes en mesa de un juego (BggId 167791)
  Cuando se invoca UploadGameImageVariantsAsync con tipo "table"
  Entonces se sube a R2 como "games/167791/table.webp"
  Y su ancho máximo se escala a 1200px en formato WebP

Escenario: Modo Simulado en Entorno de Desarrollo
  Dada la configuración con Cloudflare:Simulate en true
  Cuando se sube una imagen optimizada
  Entonces la operación completa con éxito sin realizar llamadas de red externas a Cloudflare
  Y retorna URLs locales deterministas para desarrollo
```
