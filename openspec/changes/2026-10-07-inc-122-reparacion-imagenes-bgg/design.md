# Diseño Técnico: INC-122 — Reparación de Calidad y Completitud de Imágenes BGG

## 1. Arquitectura de Cambios

### 1.1 Modelo de Datos y DTOs en `GeekDoImagesClient`
El payload real de `https://api.geekdo.com/api/images` responde con la siguiente estructura:
```csharp
public class GeekDoImageItem
{
    [JsonPropertyName("imageid")]
    public string? ImageId { get; set; }

    [JsonPropertyName("caption")]
    public string? Caption { get; set; }

    [JsonPropertyName("numrecommend")]
    public int NumRecommend { get; set; }

    [JsonPropertyName("imageurl")]
    public string? ImageUrl { get; set; } // 64x64 micro thumbnail

    [JsonPropertyName("imageurl_lg")]
    public string? ImageUrlLg { get; set; } // 1024x1024 high-res version

    [JsonPropertyName("imageurl@2x")]
    public string? ImageUrl2x { get; set; } // 128x128 thumbnail
}
```

La extracción de la mejor URL (`ExtractBestUrl`) prioriza incondicionalmente:
1. `ImageUrlLg` si no es nula ni vacía.
2. `ImageUrl2x` como fallback secundario.
3. Si solo existe `ImageUrl` y contiene `__micro`, se descarta (retorna `null`) para evitar persistir fotos degradadas.

### 1.2 Estrategia de Consulta en `GeekDoImagesClient`
Para obtener las 3 fotos comunitarias con máxima precisión:
- **Llamada 1 (Contraportada):** `tag=BoxBack&sort=hot&showcount=5`. Si hay resultados, la primera imagen más votada es `BackCoverUrl`.
- **Llamada 2 (Galería Hot general / Componentes):** `gallery=all&sort=hot&showcount=15`.
  - La primera imagen que no sea la contraportada ni la portada y que tenga caption alusivo a componentes/mesa (o simplemente la foto más votada de la galería) se asigna a `TableOrGameplayUrl`.
  - Si la llamada 1 no encontró `tag=BoxBack`, se escanea esta galería buscando "back" en el caption.
  - La portada frontal comunitaria (`FrontCoverUrl`) se extrae de la foto más votada con `caption` de caja frontal o primera imagen de la galería.

### 1.3 Lógica de Asignación en `BggImagesSyncService`
```csharp
// 1. Portada frontal:
if (vInfo != null && !string.IsNullOrWhiteSpace(vInfo.CoverImageUrl))
{
    targetCover = vInfo.CoverImageUrl;
    targetThumb = vInfo.ThumbnailUrl ?? targetThumb;
}
else if (snapshot != null)
{
    var (rootCover, rootThumb) = BggRawSnapshotParser.ExtractRootImagesFromJson(snapshot.RawJson);
    if (!string.IsNullOrWhiteSpace(rootCover))
    {
        targetCover = rootCover;
        targetThumb ??= rootThumb;
    }
}
else if (isCorruptedCover || string.IsNullOrWhiteSpace(targetCover))
{
    if (gallery != null && !string.IsNullOrWhiteSpace(gallery.FrontCoverUrl))
    {
        targetCover = gallery.FrontCoverUrl;
    }
}

// 2. Contraportada:
if ((string.IsNullOrWhiteSpace(targetBack) || IsCorruptedCover(targetBack)) && gallery != null && !string.IsNullOrWhiteSpace(gallery.BackCoverUrl))
{
    targetBack = gallery.BackCoverUrl;
}

// 3. Mesa:
if ((string.IsNullOrWhiteSpace(targetTable) || IsCorruptedCover(targetTable)) && gallery != null && !string.IsNullOrWhiteSpace(gallery.TableOrGameplayUrl))
{
    targetTable = gallery.TableOrGameplayUrl;
}
```

Detección de URLs corruptas:
```csharp
private static bool IsCorruptedOrSimulatedCover(string? url)
{
    if (string.IsNullOrWhiteSpace(url)) return true;
    return url.Contains(".r2.dev/games/", StringComparison.OrdinalIgnoreCase) ||
           url.Contains("/images/game-placeholder.svg", StringComparison.OrdinalIgnoreCase) ||
           url.Contains("__micro", StringComparison.OrdinalIgnoreCase) ||
           url.Contains("fit-in/64x64", StringComparison.OrdinalIgnoreCase);
}
```

### 1.4 Rediseño del Visor en `GameImageCarousel.razor`
- Cambiar el contenedor principal de `aspect-[4/3] sm:aspect-[16/10] md:aspect-[16/9]` a `relative w-full aspect-square sm:aspect-[4/3] max-h-[460px]`.
- Fondo elegante con gradiente oscuro suave (`bg-neutral-900/90` o `bg-[#18181b]`) para que las carátulas resalten con drop-shadow pronunciado.
- Retirar `width="320"` y `height="320"`, usando `class="max-w-full max-h-full object-contain drop-shadow-2xl"`.
- Los botones de miniatura inferiores muestran el título claro de las 3 opciones ("Portada", "Trasera", "En mesa") cuando las 3 URLs están disponibles.
