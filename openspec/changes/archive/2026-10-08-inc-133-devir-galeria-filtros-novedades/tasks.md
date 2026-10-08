# Tareas de Implementación: INC-132 Saneamiento de Novedades Devir y Maldito, Galería Fotográfica y Job de Barrido

## Tarea 1: DTOs e Interfaces
- [x] Enriquecer `EditorialReleaseItem` con `TableImageUrl` y `BackCoverImageUrl`.
- [x] Enriquecer `IDevirReleasesExtractor` con soporte para extracción de galería de producto.
- [x] Declarar `JobNames.DevirImagesBackfill = "devir-images-backfill"` en `Ludeka.Application.Features.Jobs.JobNames`.

## Tarea 2: Saneamiento y Galería en `DevirReleasesExtractor.cs`
- [x] Añadir filtro de fecha para ignorar secciones anteriores a `DateOnly(currentYear, currentMonth, 1)`.
- [x] Añadir filtro para ignorar secciones y títulos de juegos de rol.
- [x] Condicionar la ejecución de `ParseTileCardItems` para que no se ejecute si la sección ya aportó productos detallados.
- [x] Reforzar `TileCardRegex` y lista negra de tokens de metadatos (`Autor:`, `Ilustrador:`, `Libro básico`).
- [x] Implementar `ParseProductGalleryHtml` y enriquecimiento opcional de galería para URLs de producto (`SourceUrl`).
- [x] Pruebas unitarias en `DevirReleasesExtractorTests.cs`.

## Tarea 3: Resiliencia y Desbloqueo en `EditorialReleasesSyncService.cs`
- [x] Aplicar timeout defensivo de 4 segundos a `SuggestMatchAsync` con fallback heurístico.
- [x] Actualizar `matchedGame.UpdateMediaUrls(...)` cuando se disponga de `TableImageUrl` o `BackCoverImageUrl`.
- [x] Sincronizar editoriales de forma independiente y tolerante a fallos.
- [x] Pruebas unitarias en `EditorialReleasesSyncServiceTests.cs`.

## Tarea 4: Job Autónomo `DevirImagesBackfillJobRunner.cs`
- [x] Implementar `DevirImagesBackfillJobRunner` en `src/Ludeka.Jobs/Runners/DevirImagesBackfillJobRunner.cs`.
- [x] Registrar el runner en el contenedor de inyección de dependencias de `Ludeka.Jobs`.
- [x] Pruebas unitarias en `Ludeka.UnitTests/Jobs/DevirImagesBackfillJobRunnerTests.cs`.

## Tarea 5: Verificación Integral de la Suite
- [x] Ejecutar la suite completa de pruebas unitarias (`dotnet test`).
- [x] Asegurar cero regresiones y 100% de tests en verde (2.688 pruebas superadas).
