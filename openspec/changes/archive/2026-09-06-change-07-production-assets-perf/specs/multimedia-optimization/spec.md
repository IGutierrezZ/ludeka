# Especificación: multimedia-optimization (Optimización de Carga y Entrega Multimedia)

## 1. Propósito y Contexto
Esta especificación define el estándar de carga y maquetación para todas las imágenes y contenidos embebidos en Ludeka (carátulas de BGG, fotos de mesa real, miniaturas de YouTube y portadas de Instagram). Garantiza que ninguna imagen secundaria consuma ancho de banda móvil innecesario antes de entrar en el viewport y que los fallos de red se gestionen de manera defensiva.

---

## 2. Requerimientos de Comportamiento (RFC 2119)

### REQ-MEDIA-01: Lazy Loading y Asynchronous Decoding
- Todas las imágenes fuera del viewport inicial **DEBEN** incluir los atributos:
  `loading="lazy"` y `decoding="async"`.
- Los componentes afectados incluyen:
  - `GameCard.razor`
  - `MultimediaHub.razor` (miniaturas de tutoriales, partidas, posts de Instagram y reels)
  - `MyLibrary.razor` (carátulas de colecciones y préstamos)
  - `FoundingVerdictCard.razor` (galería de fotos de mesa real)
  - `CatalogQueuePanel.razor`

### REQ-MEDIA-02: Ratios de Aspecto Rígidos
- Cada elemento multimedia **DEBE** estar envuelto en un contenedor con relación de aspecto explícita:
  - Miniaturas 16:9 (YouTube): clase `aspect-video`.
  - Portadas 1:1 (Juegos e Instagram): clase `aspect-square` o `.cover-wrapper`.
  - Reels / Shorts 9:16: clase `aspect-[9/16]`.

### REQ-MEDIA-03: Fallback Defensivo ante Errores de Carga
- Si una URL remota de carátula o miniatura falla al cargar (error 404 o timeout), el sistema **DEBE** renderizar un estado alternativo limpio (placeholder visual con icono o gradiente neutro) sin romper el layout ni mostrar iconos de imagen rota del navegador.

---

## 3. Criterios de Aceptación (Escenarios Gherkin)

### Escenario 1: Lazy loading en miniaturas del Hub Multimedia
```gherkin
Given un juego con tutoriales y partidas en el Hub Multimedia
When el componente MultimediaHub es renderizado
Then todas las etiquetas <img> de miniaturas contienen loading="lazy" y decoding="async"
And los contenedores de tutoriales tienen la clase aspect-video
```

### Escenario 2: Aspect ratio fijo en fotos de veredicto
```gherkin
Given un veredicto de la Mesa Fundadora con fotos de mesa real
When se visualiza la galería fotográfica
Then cada imagen se encuentra dentro de un contenedor con ratio de aspecto fijo
And la página no experimenta desplazamiento de contenido al completarse la carga
```
