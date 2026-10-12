# Especificación: INC-147 - Deslizamiento Táctil en Carrusel Móvil e Ingesta Exhaustiva de Galería Editorial (Devir, Maldito, Arrakis)

## Requisitos Funcionales

### RF-01: Gestos Táctiles en Visores Móviles y Carrusel
- **RF-01.1:** El visor móvil (`lg:hidden`) de `GameDetail.razor` debe capturar el gesto táctil de arrastre horizontal (*swipe*). Deslizar de derecha a izquierda avanzará a la siguiente imagen; deslizar de izquierda a derecha retrocederá a la anterior.
- **RF-01.2:** El gesto debe requerir un desplazamiento horizontal mínimo de al menos 40 píxeles, y el desplazamiento horizontal absoluto debe superar al vertical (`|deltaX| > |deltaY|`) para no interferir con el desplazamiento (*scroll*) vertical de la página.
- **RF-01.3:** El componente `GameImageCarousel.razor` debe incorporar idéntico comportamiento de deslizamiento táctil para garantizar coherencia en todas las vistas donde se integre.
- **RF-01.4:** El encuadre en el visor móvil debe evitar el recorte excesivo y la pixelación de fotografías de despliegue o tableros anchos, empleando contención visual limpia (`object-contain` en lugar de recorte forzado `object-cover` destructivo).

### RF-02: Extracción Exhaustiva de Galerías en Fichas Editoriales
- **RF-02.1:** Los DTOs de galería (`DevirProductGalleryDto`, `MalditoProductGalleryDto` y `ArrakisProductGalleryDto`) deben incluir una colección `IReadOnlyList<string> AllImages` (o propiedad unificada) que contenga la lista completa y ordenada de todas las imágenes únicas encontradas en la ficha.
- **RF-02.2:** En `MalditoReleasesExtractor`, el análisis de la galería JSON Magento debe clasificar como `TableImageUrl` cualquier imagen que contenga `components`, `mesa`, `contenido`, `lifestyle` o `gameplay`. Asimismo, debe guardar todas las URLs en la lista general de imágenes.
- **RF-02.3:** En `DevirReleasesExtractor`, el análisis de la galería JSON Magento debe clasificar como `TableImageUrl` cualquier imagen que contenga `components`, `mesa`, `contenido`, `lifestyle` o `gameplay`, recolectando igualmente todas las imágenes de la ficha en `AllImages`.
- **RF-02.4:** En `ArrakisReleasesExtractor`, se deben recolectar todas las imágenes secundarias del carrusel/galería WooCommerce/Elementor e incorporarlas en `AllImages`.

### RF-03: Alimentación y Persistencia de la Galería del Juego (`AdditionalImages`)
- **RF-03.1:** Al enriquecer o actualizar un juego desde la galería de una editorial, todas las imágenes de `AllImages` que no se hayan asignado a `CoverImageUrl`, `BackCoverImageUrl` o `TableImageUrl` deben incorporarse en la colección `Game.AdditionalImages` como objetos `GameGalleryImage`.
- **RF-03.2:** No se deben generar duplicados por URL al incorporar imágenes a `AdditionalImages`.
- **RF-03.3:** El sistema debe admitir colecciones de 10 o más imágenes sin restricciones artificiales ni exigir nombres rígidos; si una imagen no tiene título descriptivo de la editorial, se mostrará como "Foto N" o "Galería".

### RF-04: Resiliencia y Actualización en Backfills
- **RF-04.1:** Los *runners* `MalditoImagesBackfillJobRunner`, `DevirImagesBackfillJobRunner` y `ArrakisImagesBackfillJobRunner` no deben omitir juegos que ya posean una URL previa si la editorial ofrece nuevas imágenes de galería adicionales o una foto de mesa oficial de alta calidad.
