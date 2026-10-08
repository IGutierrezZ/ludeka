# Especificación Técnica: INC-132 Saneamiento de Novedades Devir y Maldito, Galería Fotográfica y Job de Barrido

## 1. Requisitos Funcionales

- **RF-01 (Filtrado de fechas pasadas en Devir):** El extractor `DevirReleasesExtractor` no debe retornar ningún lanzamiento perteneciente a meses anteriores al actual (`fecha < primer día del mes actual`).
- **RF-02 (Exclusión de juegos de rol y no juegos de mesa):** Descartar cualquier sección o producto cuyo encabezado, título o descripción identifique un juego de rol ("rol", "juegos de rol", "rpg", "libro básico", "pantalla del director").
- **RF-03 (Erradicación de falsos positivos de títulos):**
  - Descartar cualquier título extraído que empiece o contenga fragmentos de metadatos editoriales: `Autor:`, `Ilustrador:`, `Edad:`, `Jugadores:`, `Duración:`, `Libro básico`.
  - Suprimir la ejecución de `ParseTileCardItems` en bloques de sección que ya hayan generado productos detallados estructurados (`ParseDetailedPriceItems`).
  - Restringir la captura de tarjetas `TileCardRegex` para que opere estrictamente sobre elementos delimitados `mgz-element-inner`, impidiendo capturas greedy entre párrafos intermedios.
- **RF-04 (Captura de Galería Fotográfica Devir):**
  - Al procesar una novedad con `SourceUrl` hacia `devir.es`, o durante el barrido de catálogo, parsear el bloque `<script type="text/x-magento-init">` correspondiente a `[data-gallery-role=gallery-placeholder]` -> `mage/gallery/gallery`.
  - Extraer las URLs de alta resolución de:
    - Caja 3D (`face3d.jpg`) -> Portada (`CoverImageUrl`).
    - Componentes en mesa (`components1.jpg` o componentes) -> Foto en mesa (`TableImageUrl`).
    - Contraportada de la caja (`backflat.jpg`) -> Foto de contraportada (`BackCoverImageUrl`).
    - Portada frontal plana (`frontflat.jpg`) -> Portada 2D alternativa.
  - Enriquecer el DTO `EditorialReleaseItem` con `TableImageUrl` y `BackCoverImageUrl`.
  - En `EditorialReleasesSyncService`, actualizar las propiedades multimedia del juego en `IGameRepository` mediante `game.UpdateMediaUrls(...)` preservando las existentes si son válidas y actualizando si se descubren nuevas imágenes de mayor resolución o de mesa/contraportada.
- **RF-05 (Resiliencia y Desbloqueo de Maldito Games):**
  - En `EditorialReleasesSyncService.cs`, aplicar un timeout estricto de 4 segundos a `SuggestMatchAsync` utilizando un `CancellationTokenSource` vinculado. Si la llamada a Gemini se demora, falla o devuelve error HTTP (429 / 500), recurrir de forma instantánea al emparejador heurístico (`GenerateHeuristicMatch`) sin bloquear la sincronización ni superar el límite de tiempo del WebSocket (error 1006).
  - Gestionar la sincronización de editoriales con aislamiento de excepciones por editorial.
  - Suministrar desglose explícito de resultados por editorial en la respuesta de sincronización (`Devir: N items, Maldito Games: M items`).
- **RF-06 (Job Autónomo de Barrido de Catálogo Devir):**
  - Implementar `DevirImagesBackfillJobRunner` en `src/Ludeka.Jobs/Runners/DevirImagesBackfillJobRunner.cs` bajo la interfaz `IJobRunner`, con nombre de trabajo canónico `devir-images-backfill`.
  - Crawlear la paginación del catálogo de juegos de mesa de Devir (`https://devir.es/juegos-de-mesa`), extraer los enlaces a cada ficha de producto, descargar su galería fotográfica y actualizar los juegos correspondientes en Ludeka (cruzando por EAN o título normalizado).

## 2. Modelos y Contratos

### `EditorialReleaseItem` (Ludeka.Application.DTOs)
Se añaden propiedades opcionales para la galería:
```csharp
public record EditorialReleaseItem(
    string Title,
    string Publisher,
    DateOnly? ReleaseDate,
    string? CoverImageUrl,
    decimal? EstimatedPvp,
    string? Notes = null,
    string? Ean = null,
    string? SourceUrl = null,
    bool IsReprint = false,
    bool IsMonthOnly = false,
    string? TableImageUrl = null,
    string? BackCoverImageUrl = null);
```

### `JobNames` (Ludeka.Application.Features.Jobs)
```csharp
public const string DevirImagesBackfill = "devir-images-backfill";
```

## 3. Invariantes de Dominio
- No se admiten lanzamientos pasados en el feed público de novedades editoriales.
- Ninguna novedad en estado `Published` o `PendingModeration` puede tener por título `"Autor:"`, `"Ilustrador:"` o descriptores de rol.
- Toda operación de red externa contra páginas de producto o APIs de IA debe contar con un timeout acotado para impedir agotamiento de sockets o desconexiones de clientes web.
