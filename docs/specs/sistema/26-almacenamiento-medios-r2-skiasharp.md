# 26. Pipeline de Almacenamiento y Optimización de Medios (Cloudflare R2 + SkiaSharp + WebP)

> **Incremento Asociado:** INC-40 (`change-40-medios-r2-skiasharp`)  
> **Estado:** Implementado, Verificado y Documentado  
> **Módulo:** Infraestructura, Medios y Rendimiento Web  

---

## 1. Visión General y Propósito

El módulo de **Almacenamiento y Optimización de Medios** dota a Ludeka de soberanía sobre sus recursos visuales (carátulas de juegos, traseras, fotos en mesa y capturas sociales), eliminando la dependencia de enlaces directos externos a terceros (como el CDN de BoardGameGeek) y garantizando **0 € en costes de ancho de banda saliente** (*zero egress fees*) mediante Cloudflare R2 y la API compatible S3.

Todo el procesamiento gráfico se realiza estrictamente en memoria (*zero-disk*) mediante **SkiaSharp** (motor gráfico multiplataforma de Google con licencia MIT libre sin costes ni limitaciones comerciales), convirtiendo todas las imágenes entrantes a formato WebP optimizado (calidad 80–82%) y generando variantes proporcionales con nomenclatura determinista.

---

## 2. Modelo de Dominio (`Ludeka.Core`)

### 2.1. Value Object `ImageVariantUrls`
Ubicación: `Ludeka.Core.ValueObjects.ImageVariantUrls`
Representa el par de URLs públicas correspondientes a la versión principal y a la miniatura optimizadas:
```csharp
public record ImageVariantUrls(string FullUrl, string ThumbUrl);
```

### 2.2. Enum `GameImageType`
Ubicación: `Ludeka.Core.Enums.GameImageType`
Define las tipologías canónicas de imágenes asociadas a juegos y comunidad:
- `Cover = 1`: Carátula frontal oficial (`boxartfront`).
- `Back = 2`: Trasera de la caja (`boxartback`).
- `Table = 3`: Fotografía de componentes y despliegue en mesa (`gameplay` / `creative`).
- `Social = 4`: Imágenes sociales para sorteos, novedades o eventos.

---

## 3. Arquitectura de Casos de Uso y Contratos (`Ludeka.Application`)

### 3.1. Servicio de Optimización de Imágenes (`IImageOptimizationService`)
- `Task<byte[]> ConvertToWebpAsync(Stream inputStream, int maxWidth, int quality = 82, CancellationToken ct = default)`:
  - Decodifica el stream en memoria.
  - Comprueba dimensiones originales y aplica factor de escala proporcional solo si el ancho original supera `maxWidth` (evita upscaling artificial).
  - Codifica en WebP de alta fidelidad.
- `Task<ImageDimensions> GetDimensionsAsync(Stream inputStream, CancellationToken ct = default)`:
  - Inspecciona las cabeceras del codec sin necesidad de volcar a disco.

### 3.2. Contrato de Almacenamiento de Medios (`IImageStorageService`)
- `UploadOptimizedImageAsync(Stream inputStream, string objectKey, int maxWidth = 1000, int quality = 82, CancellationToken ct = default)`:
  - Optimiza a WebP y sube al bucket R2 o almacén configurado.
- `UploadGameImageVariantsAsync(Stream rawImageStream, int bggId, string imageType, CancellationToken ct = default)`:
  - Genera y sube las variantes deterministas canónicas:
    - `cover`: `games/{bggId}/cover.webp` (máx. 1000px) y `games/{bggId}/cover_thumb.webp` (máx. 400px).
    - `back`: `games/{bggId}/back.webp` (máx. 1000px).
    - `table`: `games/{bggId}/table.webp` (máx. 1200px).
- `DeleteImageAsync(string objectKey, CancellationToken ct = default)`:
  - Elimina el objeto del bucket.
- `GetPublicUrl(string objectKey)`:
  - Resuelve la URL pública canónica en base a `PublicCdnBaseUrl`.
- **Compatibilidad Retenida:** Mantiene la compatibilidad total con `SaveGameCoverAsync`, `SaveEventPosterAsync`, `SaveCommunityImageAsync` y `ValidateCoverUrlAsync`.

### 3.3. Configuración (`CloudflareR2Options`)
- Sección de configuración: `"Cloudflare"`.
- Propiedades: `AccountId`, `AccessKeyId`, `SecretAccessKey`, `BucketName`, `PublicCdnBaseUrl`, `Simulate`.
- `HasValidCredentials`: Propiedad calculada que valida si las credenciales mínimas de R2 están presentes y no está en modo simulado.

---

## 4. Implementación de Infraestructura (`Ludeka.Infrastructure`)

- **`SkiaSharpImageOptimizationService`:**
  - Implementación con `SKBitmap`, `SKImageInfo`, `SKSamplingOptions.Default` y `SKEncodedImageFormat.Webp`.
  - Zero-disk, no genera archivos temporales en disco.
- **`CloudflareR2StorageService`:**
  - Cliente S3 mediante `AmazonS3Client` apuntando a `https://{AccountId}.r2.cloudflarestorage.com`.
  - Asignación de cabeceras HTTP de alto rendimiento:
    - `ContentType: image/webp`
    - `Cache-Control: public, max-age=31536000, immutable`
- **`SimulatedImageStorageService`:**
  - Almacén en memoria concurrente (`ConcurrentDictionary<string, byte[]>`) para desarrollo local y tests sin conexión.
  - Ejecuta el pipeline real de SkiaSharp para verificar la integridad de la optimización sin llamadas de red a Cloudflare.
- **Inyección de Dependencias en `Program.cs`:**
  - Si `HasValidCredentials` es true, inyecta `CloudflareR2StorageService`.
  - En caso contrario (desarrollo o testing), conmuta de forma segura a `SimulatedImageStorageService`.

---

## 5. Optimización de Renderizado en Catálogo y Portada (INC-57)

A raíz del diagnóstico empírico documentado en [`docs/specs/diagnostico-imagenes-catalogo.md`](file:///c:/repos/Ludeka/docs/specs/diagnostico-imagenes-catalogo.md), se identificaron e implantaron las siguientes mejoras en la capa de interfaz:
- **Priorización de `ThumbnailUrl` (400px WebP):**
  - Componentes [`GameCard.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/GameCard.razor) y [`HomeGameCard.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Home/HomeGameCard.razor) priorizan la variante `ThumbnailUrl` sobre la carátula completa (`CoverImageUrl`), reduciendo el payload descargado en listados en más de un 81%.
- **Erradicación de CLS (Cumulative Layout Shift):**
  - Incorporación sistemática de dimensiones intrínsecas (`width` y `height`) en [`HomeGiveawayCard.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Home/HomeGiveawayCard.razor) y [`HomeReleaseCard.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Home/HomeReleaseCard.razor) (`width="320" height="180"`), así como en [`ExpansionSisterList.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/ExpansionSisterList.razor) (`56x56`) y [`ExpansionEcosystemSection.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/ExpansionEcosystemSection.razor) (`64x64`).
  - Atributos `loading="lazy"` y `decoding="async"` universales en recursos fuera del viewport inicial.
- **Optimización de Assets Estáticos:**
  - Reducción drástica del asset no comprimido `patchwork.png` (de 1,9 MB a 323 KB en PNG y 49 KB en WebP).
  - Generación de miniaturas WebP a 400px para la totalidad de los 31 juegos base de semilla en `seed-games.json`.

---

## 6. Pruebas y Verificación

- **`SkiaSharpImageOptimizationTests`:**
  - Verificación de redimensionado proporcional a 1000px en imágenes de 1600x1200.
  - Verificación de no-upscaling en imágenes pequeñas (400x300).
  - Verificación de cabeceras mágicas `RIFF` y `WEBP`.
  - Lectura de dimensiones y control de excepciones.
- **`ImageStorageNamingTests`:**
  - Verificación de las rutas deterministas `games/{bggId}/...`.
  - Sanitización de URLs con barras diagonales.
- **`SimulatedImageStorageServiceTests`:**
  - Validación del ciclo de subida, almacenamiento en memoria y eliminación.
- **`CatalogImageOptimizationContractTests` (INC-57):**
  - Verificación de selección de `ThumbnailUrl` en `GameCard` y `HomeGameCard`.
  - Verificación de dimensiones intrínsecas anti-CLS en tarjetas de carril y expansión.
  - Verificación de pesos inferiores a 100 KB en miniaturas WebP y compresión de `patchwork.png`.
- **Suite completa de pruebas:** 1.663 pruebas automatizadas (1.653 unitarias + 10 de integración) pasando al 100% en verde.
