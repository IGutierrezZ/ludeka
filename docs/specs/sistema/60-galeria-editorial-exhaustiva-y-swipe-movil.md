# 60. Ingesta Exhaustiva de Galerías Editoriales y Deslizamiento Táctil en Carruseles Móviles

> **Estado:** ✅ Archivado (INC-147)  
> **Pruebas automáticas:** 2.887 pruebas unitarias y de integración verificadas al 100%.  
> **Componentes impactados:** `src/Ludeka.Application/DTOs/EditorialReleaseDtos.cs`, `src/Ludeka.Infrastructure/Extractors/MalditoReleasesExtractor.cs`, `src/Ludeka.Infrastructure/Extractors/DevirReleasesExtractor.cs`, `src/Ludeka.Infrastructure/Extractors/ArrakisReleasesExtractor.cs`, `src/Ludeka.Jobs/Runners/MalditoImagesBackfillJobRunner.cs`, `src/Ludeka.Jobs/Runners/DevirImagesBackfillJobRunner.cs`, `src/Ludeka.Jobs/Runners/ArrakisImagesBackfillJobRunner.cs`, `src/Ludeka.Web/Components/Pages/GameDetail.razor`, `src/Ludeka.Web/Components/Shared/GameImageCarousel.razor`.

---

## 1. Contexto y Objetivos

Hasta este incremento, la importación de fotografías desde editoriales oficiales (Devir, Maldito Games, Arrakis) se limitaba a buscar tres patrones concretos: carátula 3D (`face3d`), contraportada (`backflat`) y despliegue en mesa (`components`/`mesa`). Esto causaba dos problemas graves:
1. **Pérdida de fotografías oficiales:** Muchas editoriales proporcionan hasta 10+ fotos por ficha de juego (fotografías de estudio con producción real, miniaturas pintadas, tableros modulares y ejemplos de partidas). Al no coincidir con las tres palabras clave fijas, se ignoraban completamente.
2. **Pixelación destructiva en fotos de mesa en móviles:** En Maldito Games (*Brass: Birmingham*, *Ark Nova*), las fotos de estudio de mesa se nombran `*-1200-lifestyle*.jpg` (1200×1200 px). Al no ser reconocidas, la ficha mantenía la foto lejana panorámica de BGG (`table.webp`). Dicha foto, encuadrada con `object-cover` en un contenedor móvil 4:3, sufría un recorte y ampliación destructivos que producían un efecto pixelado evidente.
3. **Falta de gestos táctiles (*touch swipe*):** En dispositivos móviles, los usuarios debían pulsar pequeños botones transparentes o zonas laterales para cambiar de fotografía, sin soporte para el gesto intuitivo de deslizar horizontalmente con el dedo.

---

## 2. Arquitectura de Extracción Exhaustiva

### 2.1 Modelo de Datos en Aplicación
Se incorpora la colección completa de URLs de la ficha editorial en los DTOs de galería de `EditorialReleaseDtos.cs`:
- `MalditoProductGalleryDto`: propiedad `IReadOnlyList<string>? AllImages`.
- `DevirProductGalleryDto`: propiedad `IReadOnlyList<string>? AllImages`.
- `ArrakisProductGalleryDto`: propiedad `IReadOnlyList<string>? AllImages`.

### 2.2 Extractores de Infraestructura
- **Maldito Games (`MalditoReleasesExtractor`):**
  - Recolecta todas las imágenes declaradas en el array `data` del script JSON de Magento (`mage/gallery/gallery`), agregando tanto `img` como `full` de forma unívoca a `AllImages`.
  - Amplía el reconocimiento de `tableUrl` priorizando URLs que contengan `lifestyle`, `gameplay`, `mesa`, `components` o `contenido`. Esto garantiza que fotos de estudio de 1200×1200 px se asignen directamente a `TableImageUrl`.
- **Devir (`DevirReleasesExtractor`):**
  - Recolecta todas las imágenes del script `mage/gallery/gallery` en `AllImages`.
  - Reconoce fotos de mesa bajo los patrones `components`, `lifestyle`, `gameplay` y `mesa`.
- **Arrakis Games (`ArrakisReleasesExtractor`):**
  - Recolecta todas las imágenes asociadas al producto bajo `wp-content/uploads/` excluyendo logos y miniaturas de baja resolución (`-150x150`, `-100x100`).

---

## 3. Inyección en Catálogo y Jobs de Backfill

En `MalditoImagesBackfillJobRunner`, `DevirImagesBackfillJobRunner` y `ArrakisImagesBackfillJobRunner`:
1. **Población en `Game.AdditionalImages`:** Todas las URLs presentes en `gallery.AllImages` se incorporan a la colección del juego mediante `game.AddGalleryImage(url, title: null)`. Se omiten URLs que ya existan como portada, trasera, mesa o foto adicional previa.
2. **Reevaluación de fotos de mesa BGG remotas:** Si el juego tiene una foto de mesa que apunta a la captura lejana de BGG (`.r2.dev/games/`) y la editorial ofrece una foto oficial (`lifestyle` o `components`), se actualiza inmediatamente `TableImageUrl`.
3. **Omisión inteligente:** Un juego sólo se omite del barrido si ya cuenta con portada 3D, contraportada, mesa editorial no remota, EAN, oferta de la editorial y al menos una fotografía complementaria en `AdditionalImages`.

---

## 4. Experiencia de Usuario: Gesto Táctil (Swipe) y Contención Nítida

### 4.1 Soporte Táctil en Blazor
Tanto `GameDetail.razor` (visor móvil `<lg`) como `GameImageCarousel.razor` (visor principal y modal *Lightbox*) implementan:
- `@ontouchstart="HandleTouchStart"` y `@ontouchend="HandleTouchEnd"`.
- Umbral mínimo de activación de 40 px: `Math.Abs(deltaX) > 40`.
- Predominancia horizontal: `Math.Abs(deltaX) > Math.Abs(deltaY)` para no interferir jamás con el desplazamiento vertical (*scroll*) de la página.
- Estilo CSS `touch-pan-y` en los contenedores táctiles.
- Bandera `_didSwipe` que neutraliza el evento `onclick` inmediatamente posterior al deslizamiento, evitando que se abra involuntariamente el modal de ampliación al deslizar fotos.

### 4.2 Erradicación de la Pixelación por Recorte
En la vista móvil de `GameDetail.razor`, la imagen pasa de `object-cover` a `object-contain p-1 drop-shadow-md`. Esto garantiza que los tableros de mesa, componentes y carátulas apaisadas se muestren con su relación de aspecto natural, sin zoom artificial y aprovechando la resolución original de 1200 px de la editorial.

---

## 5. Verificación y Regresión

- Suite completa de **2.887 pruebas automáticas** ejecutada y aprobada al 100% (2.886 unitarias + 10 de integración en solución).
- Cobertura de pruebas de contrato en `GameDetailEditorialContractTests` verificando directivas táctiles y estilos de contención.
- Pruebas unitarias en `MalditoReleasesExtractorTests`, `DevirReleasesExtractorTests`, `ArrakisReleasesExtractorTests`, `MalditoImagesBackfillJobRunnerTests`, `DevirImagesBackfillJobRunnerTests` y `ArrakisImagesBackfillJobRunnerTests`.
