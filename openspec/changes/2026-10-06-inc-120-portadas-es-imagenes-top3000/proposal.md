# Propuesta: INC-120 Extracción de Portadas en Español desde Snapshots y Sincronización de Imágenes Comunitarias Top 3.000 BGG

## 1. Contexto y Problema

1. **Ausencia de imágenes en catálogo (URLs rotas o nulas):**  
   Durante las cargas previas de juegos desde BoardGameGeek en entornos locales o sin credenciales de Cloudflare R2 configuradas, el pipeline de medios delegaba en `SimulatedImageStorageService`. Este servicio guardaba los bytes de imagen en un almacén en memoria volátil (RAM) y generaba URLs simuladas (`https://pub-...r2.dev/games/...`). Al terminar el proceso, los bytes se perdían y las URLs en base de datos quedaron apuntando a objetos inexistentes (HTTP 404), provocando que los componentes Blazor ejecuten el evento `onerror` y muestren de forma recurrente el SVG de marcador de posición (*placeholder*).

2. **Omisión de portadas localizadas en el parser de versiones:**  
   En `BggRawSnapshotParser.cs` y `BggSpanishVersionInfoDto.cs`, la extracción analítica desde los snapshots crudos de BGG XMLAPI2 (`&versions=1`) recupera el título, la editorial, el año y el código EAN de la edición en español, pero **ignora por completo** los elementos `<image>` y `<thumbnail>` presentes en el nodo `<item type="boardgameversion">`. Como consecuencia, juegos que cuentan con una portada oficial localizada en castellano continúan mostrando la portada en inglés o carecen de ella.

3. **Inexistencia de contraportadas y fotos de mesa en el XML básico:**  
   El XML de BGG XMLAPI2 no incluye fotos de la trasera de la caja (`boxartback`) ni de componentes/partida en mesa (`gameplay`/`table`). Estas imágenes residen en la API de galería comunitaria de GeekDo (`https://api.geekdo.com/api/images`), para la cual Ludeka ya dispone del cliente `GeekDoImagesClient`, pero no cuenta con un proceso autónomo de barrido masivo que las asocie a los juegos ya existentes en catálogo.

---

## 2. Alcance Propuesto

1. **Extracción y Priorización de Portadas en Español desde Snapshots Locales:**
   - Ampliar `BggSpanishVersionInfoDto` para incorporar `CoverImageUrl` y `ThumbnailUrl`.
   - Modificar `BggRawSnapshotParser.ParseVersionInfo` para extraer y normalizar las URLs de carátula de la versión española desde el JSON estructurado del snapshot.
   - Enriquecer el barrido de catálogo (`BggRawSnapshotSyncService.SweepCatalogFromVersionsCoreAsync`) para que, cuando el snapshot contenga una portada oficial en español, la asigne prioritariamente a `Game.CoverImageUrl` y `Game.ThumbnailUrl`.

2. **Proceso y Runner Autónomo de Imágenes para el Top 3.000 BGG:**
   - Implementar un servicio de enriquecimiento de medios para el catálogo caliente priorizando los títulos con mayor relevancia comunitaria (`BggRank` no nulo ascendente hasta el Top 3.000).
   - Para cada juego del lote prioritario:
     - Evaluar si dispone de portada localizada en su snapshot crudo; si existe, priorizarla.
     - Consultar la API comunitaria mediante `GeekDoImagesClient` para recuperar la contraportada (`BackCoverImageUrl`) y la fotografía de componentes/mesa (`TableImageUrl`).
     - Si el entorno dispone de credenciales de Cloudflare R2 activas, procesar y optimizar a WebP en R2; si opera en modo directo/desarrollo (*Zero-Cloud*), persistir de forma segura las URLs canónicas de CDN de BGG (`https://cf.geekdo-images.com/...`), erradicando las URLs simuladas que provocan 404.
   - Actualizar atómicamente la entidad `Game` mediante `UpdateMediaUrls(cover, thumb, back, table)`.

3. **Runner CLI y Despliegue en Cloud Run Jobs:**
   - Crear el runner `bgg-images-top3000` (`BggImagesTop3000JobRunner`) en `src/Ludeka.Jobs`.
   - Registrar el nuevo trabajo en `JobNames.cs` y en el contenedor de inyección de dependencias.
   - Exponer telemetría y ejecución en lotes respetando el límite de cortesía hacia BGG (~1.000 ms entre consultas de galería).

---

## 3. Criterios de Aceptación

1. `BggRawSnapshotParser.ExtractSpanishVersionInfoFromJson` extrae correctamente `CoverImageUrl` y `ThumbnailUrl` si el nodo de la versión en español los incluye.
2. `SweepCatalogFromVersionsCoreAsync` promueve la portada en español a `Game.CoverImageUrl` cuando está disponible, sin sobreescribir con valores nulos si la versión carece de imagen.
3. El runner `bgg-images-top3000` recorre los juegos del Top 3.000 BGG, asocia contraportada y foto en mesa desde la galería de GeekDo, y reemplaza URLs rotas/simuladas por URLs accesibles.
4. El 100% de la suite de pruebas unitarias existente (2.572 pruebas) se mantiene en verde, sumando nuevas pruebas para el parser de imágenes de versión y el sincronizador de imágenes.
