# Especificación Técnica — INC-57: Diagnóstico y Optimización de Imágenes del Catálogo

## 1. Requerimientos Funcionales y de Rendimiento

### REQ-01: Priorización de Miniaturas en Listados
Las tarjetas de juegos renderizadas en vistas de colección, catálogo y carril (`GameCard.razor` y `HomeGameCard.razor`) DEBEN consumir preferentemente la variante `ThumbnailUrl` (400px WebP) cuando esté presente.
- Si `ThumbnailUrl` es nula o vacía, debe recurrir a `CoverImageUrl`.
- Si `CoverImageUrl` es nula o vacía, debe recurrir al SVG placeholder correspondiente según si es juego base o expansión (`/images/game-placeholder.svg` o `/images/expansion-placeholder.svg`).

### REQ-02: Erradicación Universal de CLS (Cumulative Layout Shift)
Todas las etiquetas `<img>` de tarjetas en portada (`HomeGiveawayCard`, `HomeReleaseCard`, `HomeGameCard`, `HomeEventCard`) y vistas asociadas DEBEN declarar atributos intrínsecos de anchura y altura (`width` y `height`), alineados con la relación de aspecto de su contenedor (1:1 o 16:9), además de `loading="lazy"` y `decoding="async"`.

### REQ-03: Optimización y Reducción de Assets Locales
Ningún asset individual empaquetado en `wwwroot/images/games` debe superar los 500 KB de peso. En particular, `patchwork.png` (originalmente 1.898 KB) debe reducirse y disponer de una miniatura WebP de menos de 100 KB.

### REQ-04: Semillas Normalizadas a WebP
El catálogo de semillas (`seed-games.json`) debe contar con rutas `ThumbnailUrl` apuntando a miniaturas WebP (`/images/games/{slug}.webp`), garantizando que la carga inicial de las 31 fichas descargue menos de 1 MB en conjunto.

---

## 2. Criterios de Aceptación Gherkin

### Escenario 1: Renderizado de GameCard con ThumbnailUrl
```gherkin
Dado un GameSummaryDto con ThumbnailUrl="/images/games/catan.webp" y CoverImageUrl="/images/games/catan.png"
Cuando se renderiza el componente GameCard
Entonces la etiqueta <img> tiene src="/images/games/catan.webp"
Y tiene atributos width="240" y height="240"
Y tiene atributos loading="lazy" y decoding="async"
```

### Escenario 2: Renderizado de Tarjetas de Carril en Home
```gherkin
Dado un GiveawayDto en HomeGiveawayCard o un WeeklyReleaseDto en HomeReleaseCard
Cuando se renderiza la tarjeta en el carril
Entonces la imagen tiene atributos explícitos width="320" y height="180"
Y previene cualquier Cumulative Layout Shift (CLS) antes de que finalice la descarga
```

### Escenario 3: Verificación de Integridad de la Suite
```gherkin
Dado el repositorio completo en su worktree inc/imagenes-catalogo
Cuando se ejecuta dotnet test Ludeka.sln
Entonces se ejecutan 1.663 pruebas automatizadas
Y el 100% de las pruebas finalizan en estado superado con 0 fallos
```
