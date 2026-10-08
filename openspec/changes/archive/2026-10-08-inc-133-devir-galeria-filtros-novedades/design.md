# Diseño Técnico: INC-132 Saneamiento de Novedades Devir y Maldito, Galería Fotográfica y Job de Barrido

## 1. Arquitectura y Componentes Afectados

```
                        ┌───────────────────────────────┐
                        │     Ludeka.Infrastructure     │
                        │    (DevirReleasesExtractor)   │
                        └───────────────┬───────────────┘
                                        │ (Extrae galería completa,
                                        │  filtra rol, meses pasados
                                        │  y falsos "Autor:")
                                        ▼
┌─────────────────────────┐     ┌───────────────────────────────┐
│       Ludeka.Web        │     │      Ludeka.Application       │
│  (/novedades & /admin)  │◄────┤ (EditorialReleasesSyncService)│
└─────────────────────────┘     └───────────────┬───────────────┘
                                                │ (Actualiza Game MediaUrls,
                                                │  timeout en IA Gemini)
                                                ▼
                        ┌───────────────────────────────┐
                        │          Ludeka.Jobs          │
                        │  (DevirImagesBackfillJobRunner)
                        └───────────────────────────────┘
```

## 2. Decisiones de Diseño Detalladas

### A. Filtrado y Saneamiento en `DevirReleasesExtractor.cs`
1. **Detección de fechas pasadas:**
   - Se toma `var currentMonthStart = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);`
   - Si la fecha extraída de la sección es menor que `currentMonthStart`, se descarta la sección completa.
2. **Exclusión de rol:**
   - Si el título de sección contiene palabras clave (`"rol"`, `"rpg"`, `"juegos de rol"`), se omite.
   - En la evaluación de cada ítem, si el título contiene términos de rol (`"libro básico"`, `"pantalla del director"`), se descarta.
3. **Erradicación de falsos duplicados por tarjetas:**
   - En `ParseSectionItems`, primero se ejecuta `ParseDetailedPriceItems(content, defaultDate, isMonthOnly, sectionItems)`.
   - Si `sectionItems.Count > 0`, **no se ejecuta `ParseTileCardItems` sobre esa misma sección**.
   - Se refuerza `TileCardRegex` para exigir la clase de envoltorio de tarjeta `mgz-element-inner` de Magezon Page Builder y se añade validación con lista negra (`Autor:`, `Ilustrador:`, `Libro básico`).

### B. Extracción de Galería Fotográfica desde `devir.es`
1. **Método reutilizable:**
   - Se implementa `ExtractDevirProductGalleryAsync(string productUrl, CancellationToken ct)` (o sincrónico `ParseProductGalleryHtml(string html)`) en `DevirReleasesExtractor`:
   - Busca en el HTML del producto:
     ```html
     <script type="text/x-magento-init">
     {
         "[data-gallery-role=gallery-placeholder]": {
             "mage/gallery/gallery": {
                 "data": [
                     { "img": "https://...face3d.jpg", ... },
                     { "img": "https://...components1.jpg", ... },
                     { "img": "https://...backflat.jpg", ... }
                 ]
             }
         }
     }
     </script>
     ```
   - Si el JSON existe, extrae:
     - `face3d`: caja 3D -> `CoverImageUrl`.
     - `components`: componentes en mesa -> `TableImageUrl`.
     - `backflat`: contraportada -> `BackCoverImageUrl`.

### C. Desbloqueo y Resiliencia en `EditorialReleasesSyncService.cs`
1. **Llamadas a IA con Timeout Defensivo:**
   - Al iterar los ítems para consultar `SuggestMatchAsync`:
     ```csharp
     using var aiCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
     aiCts.CancelAfter(TimeSpan.FromSeconds(4));
     aiMatch = await _aiMatcherService.SuggestMatchAsync(..., aiCts.Token);
     ```
   - En caso de cancelación por timeout o excepción en Gemini, se captura y se continúa inmediatamente con el fallback heurístico determinista.
2. **Actualización de Multimedia en `Game`:**
   - Si el juego está enlazado (`matchedGame != null`) y el ítem trae `TableImageUrl` o `BackCoverImageUrl`, se llama a `matchedGame.UpdateMediaUrls(...)` preservando URLs previas si no están vacías, o sustituyendo si la nueva es de alta resolución de Devir.

### D. Nuevo Job Autónomo `DevirImagesBackfillJobRunner`
1. **Registro:**
   - Registrado en `Ludeka.Jobs` y `Program.cs` del contenedor de trabajos.
   - Recorre las páginas de `https://devir.es/juegos-de-mesa?p={page}`.
   - En cada página, obtiene los enlaces a productos.
   - Descarga cada ficha de producto en Devir, obtiene el EAN y título de la ficha y la galería fotográfica (`face3d`, `components1`, `backflat`).
   - Busca el juego en `IGameRepository` (por EAN primero, luego por título).
   - Si existe y tiene imágenes faltantes o reemplazables, actualiza `UpdateImages` y `UpdateMediaUrls` y persiste con `UpdateAsync`.
