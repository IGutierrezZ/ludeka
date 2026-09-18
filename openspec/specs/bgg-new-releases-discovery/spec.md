# Especificación: Auto-Descubrimiento de Novedades y Tendencias BGG

## 1. Propósito
Proveer a Ludeka de un mecanismo autónomo y proactivo para identificar juegos de mesa que son tendencia mundial o nuevos lanzamientos en BoardGameGeek, encolándolos para su catalogación y enriquecimiento editorial en español.

## 2. Requerimientos Funcionales

### RF-01: Extracción del Hotness de BGG
- El sistema debe consultar el endpoint `/xmlapi2/hot?type=boardgame` a través de `IBggClient.FetchTopGamesAsync`.
- Cada elemento devuelto debe incluir: `BggId`, `Title`, `YearPublished`, `BggRank`, `ThumbnailUrl`.

### RF-02: Filtrado por Lanzamientos Recientes
- El servicio de descubrimiento debe permitir distinguir entre:
  1. **Hotness general**: Juegos que están en el top 50 de tendencia sin importar el año (origen `BggHotness`).
  2. **Novedades del año**: Juegos cuyo `YearPublished` sea igual al año actual (`DateTime.UtcNow.Year`) o año inmediatamente anterior (`DateTime.UtcNow.Year - 1`) (origen `BggNewReleases`).

### RF-03: Deduplicación Infalible de Tres Niveles
- Antes de encolar un juego descubierto, el servicio debe verificar de forma secuencial:
  1. Si ya existe en el catálogo principal (`_gameRepo.GetByBggIdAsync` != null) -> Omitir.
  2. Si ya existe en la cola de catalogación (`_pendingRepo.GetByBggIdAsync` != null) -> Omitir o refrescar contador si procede.
  3. Si ya existe en la tabla de staging masivo (`_stagingRepo.GetByBggIdAsync` != null) -> Omitir para evitar doble trabajo.
- Los juegos que superen los tres filtros se considerarán "Nuevos Descubrimientos".

### RF-04: Inserción en Cola de Catalogación
- Los nuevos descubrimientos se insertan en `PendingBggImports` con:
  - `Status = CatalogQueueStatus.Pending`
  - `Origin = CatalogQueueOrigin.BggNewReleases` o `CatalogQueueOrigin.BggHotness`
  - `RequestedCount = 1`
  - `ExtractedTitle = Title`
  - `YearPublished = YearPublished`
  - `ThumbnailUrl = ThumbnailUrl`

## 3. Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Detección y encolado de un nuevo juego del Hotness
  Dado que el endpoint de Hotness de BGG devuelve el juego #412345 "Essen Hit 2026" publicado en 2026
  Y el juego no existe en Games, PendingBggImports ni BggCatalogStaging
  Cuando se ejecuta el servicio de descubrimiento de novedades BGG
  Entonces el juego se guarda en PendingBggImports con origen BggNewReleases
  Y el resultado reporta 1 juego descubierto y encolado

Escenario: Descarte de juegos que ya existen en el catálogo
  Dado que el endpoint de Hotness de BGG devuelve el juego #174430 "Gloomhaven"
  Y el juego #174430 ya existe en el catálogo de Ludeka
  Cuando se ejecuta el servicio de descubrimiento de novedades BGG
  Entonces el juego es omitido y no se genera ningún duplicado en PendingBggImports
  Y el resultado reporta 1 juego ya catalogado
```
