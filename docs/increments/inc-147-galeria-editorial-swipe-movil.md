# INC-147: Deslizamiento Táctil en Carrusel Móvil e Ingesta Exhaustiva de Galería Editorial (Devir, Maldito, Arrakis)

## 1. Contexto y Diagnóstico
En la experiencia móvil actual de la ficha de juego (`GameDetail.razor`, visor `<lg`), la navegación entre imágenes se realizaba exclusivamente mediante pulsaciones en dos zonas transparentes a los extremos de la pantalla o a través de los puntos inferiores de paginación. No existía soporte para gestos táctiles de deslizamiento horizontal (*swipe*), impidiendo pasar de foto de forma fluida con el dedo. Asimismo, el encuadre con `object-cover` en formato 4:3 provocaba recortes severos y pixelaciones notorias al ampliar tomas amplias o no cuadradas.

Por otra parte, los extractores de producto de las editoriales oficiales (`MalditoReleasesExtractor`, `DevirReleasesExtractor` y `ArrakisReleasesExtractor`) filtraban las imágenes de la galería asociándolas únicamente a cadenas fijas como `components`, `mesa` o `contenido`, omitiendo patrones estándar como `lifestyle` (utilizado por Maldito Games en fotos de estudio nítidas de 1200×1200 px). Además, descartaban el resto de fotos de la ficha, privando a los juegos de contar con galerías completas de hasta 10 o más imágenes sin etiquetas rígidas obligatorias. En los *jobs* de sincronización de catálogo, las salvaguardas previas saltaban la actualización si el juego ya contaba con una captura antigua o lejana de BGG, perpetuando imágenes de baja calidad.

## 2. Objetivos y Alcance
1. **Deslizamiento Táctil Móvil (*Swipe Gesture*):**
   - Incorporar detección nativa de gestos táctiles (`ontouchstart`, `ontouchend`) en el visor móvil de `GameDetail.razor` y en el componente compartido `GameImageCarousel.razor`.
   - Garantizar un umbral de desplazamiento horizontal (~40-50 px) con discriminación del eje vertical para permitir el *scroll* natural de la página sin saltos erráticos.
   - Ajustar el visor móvil para evitar la distorsión o el recorte agresivo de fotografías de despliegue mediante contención visual limpia con fondo envolvente neutro.
2. **Ingesta Exhaustiva de Galerías Editoriales:**
   - Ampliar `MalditoReleasesExtractor`, `DevirReleasesExtractor` y `ArrakisReleasesExtractor` para recolectar **todas** las imágenes presentes en la ficha del producto (incluyendo `lifestyle`, `gameplay`, etc.), exponiéndolas en una colección completa `AllImages` en sus respectivos DTOs.
   - Asignar de manera inteligente los roles primarios (`CoverImageUrl`, `BackCoverImageUrl`, `TableImageUrl`), utilizando `lifestyle` como fuente prioritaria de mesa/componentes cuando aplique.
   - Alimentar la galería complementaria `AdditionalImages` del juego (`GameGalleryImage`) con todas las imágenes restantes de la editorial sin exigir etiquetas forzadas, permitiendo que un juego albergue hasta 10+ imágenes.
3. **Actualización Resiliente en Jobs de Catálogo:**
   - Actualizar `MalditoImagesBackfillJobRunner`, `DevirImagesBackfillJobRunner` y `ArrakisImagesBackfillJobRunner` para enriquecer `TableImageUrl` y `AdditionalImages` cuando se disponga de fotos oficiales de la editorial, sin omitir fichas por contar únicamente con imágenes lejanas de BGG.
4. **Pruebas y Verificación:**
   - Pruebas unitarias para la extracción exhaustiva de galerías en cada extractor.
   - Pruebas unitarias para la actualización de galería en los *runners* de *backfill*.
   - Verificación de la suite completa de pruebas unitarias.
