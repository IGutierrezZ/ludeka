# Propuesta: INC-147 - Deslizamiento Táctil en Carrusel Móvil e Ingesta Exhaustiva de Galería Editorial (Devir, Maldito, Arrakis)

## 1. Motivación y Problema
En el estado actual de la plataforma se identifican dos fricciones esenciales en la experiencia visual:
1. **Navegación Móvil de Imágenes:** En la vista móvil de la ficha de juego (`GameDetail.razor`), el usuario no puede deslizar con el dedo las imágenes; se ve forzado a pulsar botones laterales o miniaturas.
2. **Imágenes de Baja Resolución en Catálogo y Filtrado Parcial:** Editoriales como Maldito Games disponen de fotografías de estudio de alta resolución (1200×1200 px) etiquetadas con el patrón `lifestyle` en su gestor de medios Magento. El extractor actual descartaba estas fotos por buscar solo cadenas como `mesa` o `components`. Adicionalmente, el extractor ignoraba el resto de imágenes de la ficha, impidiendo que el juego almacene la totalidad de la galería oficial (hasta 10 o más fotos) en su colección de `AdditionalImages`.

## 2. Solución Propuesta
1. **Soporte de Gestos Táctiles (Swipe):** Implementar la captura de eventos `ontouchstart` y `ontouchend` en `GameDetail.razor` y `GameImageCarousel.razor`, calculando el vector de desplazamiento horizontal relativo al vertical con umbral mínimo para un paso fluido y natural de imágenes en pantallas táctiles.
2. **Ingesta Exhaustiva en Extractores:** Actualizar `MalditoReleasesExtractor`, `DevirReleasesExtractor` y `ArrakisReleasesExtractor` para extraer la totalidad de las fotos de la ficha, añadiendo la colección `AllImages` a los DTOs de galería. Ampliar el reconocimiento de fotos de mesa a `lifestyle` y `gameplay`.
3. **Población Dinámica de Galería:** Actualizar los *job runners* de *backfill* y el servicio de sincronización para transferir todas las fotos adicionales a `Game.AdditionalImages`, garantizando que convivan tantas imágenes como disponga la editorial sin nombres forzados.

## 3. Impacto y Riesgos
- Cero regresiones en la navegación de escritorio existente (mantiene botones, flechas de teclado y miniaturas).
- Rendimiento preservado: carga diferida (`loading="lazy"`) y formatos ligeros.
- Compatibilidad retroactiva total en persistencia JSONB de `AdditionalImages`.
