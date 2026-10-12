# Diseño Técnico: INC-147 - Deslizamiento Táctil en Carrusel Móvil e Ingesta Exhaustiva de Galería Editorial (Devir, Maldito, Arrakis)

## 1. Arquitectura de Interacción Táctil (UI / Blazor)
En Blazor Web App (interactividad Server/Interactive), la captura de eventos táctiles estándar (`@ontouchstart` y `@ontouchend`) proporciona una solución ligera sin dependencias de librerías externas de JavaScript.

### Parámetros y Estado del Gesto:
```csharp
private double _touchStartX;
private double _touchStartY;

private void OnTouchStart(TouchEventArgs e)
{
    if (e.Touches.Length > 0)
    {
        _touchStartX = e.Touches[0].ClientX;
        _touchStartY = e.Touches[0].ClientY;
    }
}

private async Task OnTouchEnd(TouchEventArgs e)
{
    if (e.ChangedTouches.Length > 0)
    {
        var deltaX = e.ChangedTouches[0].ClientX - _touchStartX;
        var deltaY = e.ChangedTouches[0].ClientY - _touchStartY;

        // Umbral mínimo de 40px y prevalencia del eje horizontal sobre el vertical
        if (Math.Abs(deltaX) > 40 && Math.Abs(deltaX) > Math.Abs(deltaY))
        {
            if (deltaX < 0)
            {
                await NextHeroImage(); // Deslizamiento a la izquierda -> siguiente
            }
            else
            {
                await PreviousHeroImage(); // Deslizamiento a la derecha -> anterior
            }
        }
    }
}
```
En el marcado del contenedor:
- Añadir `style="touch-action: pan-y;"` para garantizar que el navegador permita el desplazamiento vertical continuo sin bloqueo.
- Reemplazar `class="... object-cover ..."` por `class="... object-contain ..."` o contención con fondo oscuro desenfocado suave para que fotos panorámicas o desplegadas en mesa no se recorten en el móvil ni se pixelen al reescalar.

## 2. DTOs y Contratos de Extracción
Actualización de los DTOs de galería en `Ludeka.Application.DTOs.EditorialReleaseDtos.cs`:
- Incorporar `IReadOnlyList<string> AllImages = null` en `DevirProductGalleryDto`, `MalditoProductGalleryDto` y `ArrakisProductGalleryDto`.

## 3. Lógica de Extracción de Galerías
1. `MalditoReleasesExtractor.cs`:
   - Al recorrer `mage/gallery/gallery.data`, recolectar cada `targetUrl` único en `allImagesList`.
   - Si la URL contiene `components`, `mesa`, `contenido`, `lifestyle` o `gameplay`, clasificarla como `tableUrl` si aún no se ha asignado una.
2. `DevirReleasesExtractor.cs`:
   - Idéntica ampliación para incluir `lifestyle` y `gameplay` en `tableUrl`, y poblar `AllImages`.
3. `ArrakisReleasesExtractor.cs`:
   - Extraer todas las imágenes de la galería del producto y acumularlas en `AllImages`.

## 4. Estrategia de Asignación en Fichas de Juego (`Game`)
Método de extensión o lógica auxiliar de enriquecimiento de galería:
- Identificar imágenes ya asignadas como carátula (`CoverImageUrl`), trasera (`BackCoverImageUrl`) y mesa (`TableImageUrl`).
- Para cada URL en `gallery.AllImages`: si no es ninguna de las 3 anteriores y no existe ya en `game.AdditionalImages`, crear un nuevo `GameGalleryImage(url, title: null)`.
- Si `game.TableImageUrl` era nula o provenía de BGG lejana y la editorial proporciona una nueva mesa de calidad (`lifestyle`/`table`), sustituirla por la oficial.

## 5. Jobs de Sincronización
- En `MalditoImagesBackfillJobRunner`, `DevirImagesBackfillJobRunner` y `ArrakisImagesBackfillJobRunner`:
  - Evaluar si la editorial aporta nuevas imágenes para `AdditionalImages` o una mejor imagen para `TableImageUrl`. Si es así, no omitir el juego y persistir los cambios.
