# Informe de Verificación: INC-147 - Deslizamiento Táctil en Carrusel Móvil e Ingesta Exhaustiva de Galería Editorial

> **Fecha:** 12 de octubre de 2026  
> **Rama / Worktree:** `inc/galeria-editorial-swipe-movil` (`F:\repos\ludeka-wt\galeria-editorial-swipe-movil`)  
> **Estado:** ✅ Verificado al 100% (2.887 pruebas unitarias y de integración en verde)

---

## 1. Resumen Ejecutivo

El incremento INC-147 resuelve dos necesidades fundamentales de la experiencia de usuario y calidad del catálogo:
1. **Ergonomía Táctil en Móvil:** Implementación del gesto de deslizamiento horizontal con el dedo (*touch swipe*) tanto en el visor de cabecera de la ficha pública (`GameDetail.razor`) como en el carrusel compartido (`GameImageCarousel.razor`) y sus visores ampliados (*Lightbox*). Se garantiza la fluidez total del *scroll* vertical mediante `touch-pan-y`, umbral mínimo de 40 px y comprobación de predominancia horizontal (`|deltaX| > |deltaY|`), con protección contra falsos *taps* accidentales tras deslizar.
2. **Erradicación de Pixelación y Recorte Destructivo en Fotos de Mesa:** En móviles, la imagen principal adopta `object-contain` en lugar de `object-cover`, evitando el escalado con recorte agresivo que provocaba pixelación visible en tableros y componentes panorámicos.
3. **Ingesta Exhaustiva de Medios Editoriales:** Los extractores de Devir, Maldito Games y Arrakis recolectan **todas** las imágenes disponibles en las fichas de producto en `AllImages`. Asimismo, reconocen los patrones de alta resolución `lifestyle` y `gameplay` (resoluciones de 1200×1200 px como las de *Brass: Birmingham* y *Ark Nova* en Maldito Games) para asignarlas limpiamente a la foto «En mesa» (`TableImageUrl`). Los *jobs* de sincronización y *backfill* inyectan automáticamente la totalidad de la galería en `AdditionalImages` sin forzar esquemas rígidos de nombres y evitando URLs duplicadas.

---

## 2. Cobertura de Pruebas Automáticas

Se ejecutó la suite de pruebas completa en el entorno aislado del *worktree*:

```text
Serie de pruebas para F:\repos\ludeka-wt\galeria-editorial-swipe-movil\tests\Ludeka.UnitTests\bin\Debug\net10.0\Ludeka.UnitTests.dll (.NETCoreApp,Version=v10.0)
Superadas! - Superadas:  2887, Omitidas: 0, Total:  2887, Duración: 21 s - Ludeka.UnitTests.dll (net10.0)
```

### Pruebas Específicas Incorporadas / Actualizadas
- **`MalditoReleasesExtractorTests`:**
  - `ParseProductGalleryHtml_ExtractsImagesEanAndPrice`: Valida extracción de `AllImages` (todas las imágenes del script `mage/gallery/gallery`).
  - `ParseProductGalleryHtml_WhenLifestyleAndMultipleImages_ExtractsAllImagesAndSetsLifestyleAsTable`: Valida asignación de `lifestyle1.jpg` a `TableImageUrl` y recolección de 5 fotos en `AllImages`.
- **`DevirReleasesExtractorTests`:**
  - `ParseProductGalleryHtml_ExtractsImagesEanAndPrice`: Valida presencia de `AllImages`.
  - `ParseProductGalleryHtml_WhenLifestylePresent_PrioritizesLifestyleOverFrontflatForTable`: Valida priorización de fotos de mesa `lifestyle` frente a carátulas planas.
- **`ArrakisReleasesExtractorTests`:**
  - `ParseProductFicha_ExtractsEanPvpBggAndCover`: Valida presencia de `AllImages`.
  - `ParseProductFicha_ExtractsAllImagesFromHtml`: Valida recolección exhaustiva de fotos en enlaces y etiquetas `<img>` ignorando logos y miniaturas.
- **`MalditoImagesBackfillJobRunnerTests`:**
  - `RunAsync_MatchesByEanAndTitle_UpdatesImagesEanAndMalditoOffer`: Valida población de `AdditionalImages` con fotos editoriales.
  - `RunAsync_WhenGameAlreadyHasCompleteMediaEanAndMalditoOffer_SkipsGallery`: Valida omisión cuando ya cuenta con galería completa.
  - `RunAsync_WhenGameLacksAdditionalImages_EnrichesGalleryEvenIfCoverAndTableExist`: Valida enriquecimiento obligatorio de galería si faltan imágenes adicionales.
- **`DevirImagesBackfillJobRunnerTests`:**
  - `RunAsync_MatchesByEanAndTitle_UpdatesImagesEanAndDevirOffer`: Valida persistencia de `AdditionalImages`.
- **`ArrakisImagesBackfillJobRunnerTests`:**
  - `RunAsync_MatchesByBggIdAndTitle_UpdatesImagesEanAndArrakisOffer`: Valida persistencia de `AdditionalImages`.
- **`GameDetailEditorialContractTests`:**
  - `GameImageCarousel_ShouldSupportThumbnailsAndLightbox`: Valida directivas táctiles `@ontouchstart`, `@ontouchend` y `touch-pan-y`.
  - `GameDetail_MobileHero_ShouldSupportTouchSwipeAndCrispContainment`: Valida directivas táctiles y clase `object-contain` en el visor móvil.

---

## 3. Matriz de Cambios por Componente

| Componente | Capa | Modificación Principal |
| :--- | :--- | :--- |
| `EditorialReleaseDtos.cs` | `Application` | Campo `IReadOnlyList<string>? AllImages` en `DevirProductGalleryDto`, `MalditoProductGalleryDto` y `ArrakisProductGalleryDto`. |
| `MalditoReleasesExtractor.cs` | `Infrastructure` | Captura de `lifestyle`/`gameplay` para `tableUrl` y recolección de todas las fotos en `AllImages`. |
| `DevirReleasesExtractor.cs` | `Infrastructure` | Captura de `lifestyle`/`gameplay` para `tableUrl` y recolección de todas las fotos en `AllImages`. |
| `ArrakisReleasesExtractor.cs` | `Infrastructure` | Extracción exhaustiva de fotos de producto hacia `AllImages`. |
| `MalditoImagesBackfillJobRunner.cs` | `Jobs` | Transferencia de `AllImages` hacia `AdditionalImages`, reevaluación de fotos de mesa BGG remotas y refresco de `TableImageUrl`. |
| `DevirImagesBackfillJobRunner.cs` | `Jobs` | Transferencia de `AllImages` hacia `AdditionalImages` y refresco de `TableImageUrl`. |
| `ArrakisImagesBackfillJobRunner.cs` | `Jobs` | Transferencia de `AllImages` hacia `AdditionalImages` y refresco de `TableImageUrl`. |
| `GameDetail.razor` | `Web` | Eventos táctiles `@ontouchstart` / `@ontouchend`, `touch-pan-y`, prevención de falso *tap* y contención nítida `object-contain`. |
| `GameImageCarousel.razor` | `Web` | Eventos táctiles `@ontouchstart` / `@ontouchend`, `touch-pan-y` en carrusel y *Lightbox*. |

---

## 4. Conclusión

El incremento INC-147 cumple estrictamente todos los requerimientos funcionales y no funcionales especificados. La suite completa de 2.886 pruebas automáticas se encuentra en verde. Se procede a la fase de commits convencionales por unidad de trabajo y apertura del Pull Request hacia `main`.
