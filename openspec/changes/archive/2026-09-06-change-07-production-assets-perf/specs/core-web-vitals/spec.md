# Especificación: core-web-vitals (Optimización de Métricas LCP, CLS e INP)

## 1. Propósito y Contexto
Esta especificación establece las restricciones y mejoras necesarias para cumplir con los estándares de Google Core Web Vitals en Ludeka, orientados a una navegación móvil ultra-fluida:
- **Largest Contentful Paint (LCP):** < 1.2s en redes móviles.
- **Cumulative Layout Shift (CLS):** 0.00 (sin saltos de contenido durante la carga).
- **Interaction to Next Paint (INP):** < 100ms con prevención de fugas de memoria en componentes reactivos.

---

## 2. Requerimientos de Comportamiento (RFC 2119)

### REQ-CWV-01: Aceleración de LCP en Fichas y Catálogo
- En `GameDetail.razor`, la imagen de portada principal **DEBE** incluir el atributo `fetchpriority="high"`.
- En `App.razor`, se **DEBE** incluir una etiqueta de preconexión anticipada hacia el CDN de imágenes de BoardGameGeek:
  `<link rel="preconnect" href="https://cf.geekdo-images.com" crossorigin />`.
- En `GameCard.razor`, la imagen de la carátula **DEBE** contar con `decoding="async"`.

### REQ-CWV-02: Erradicación de Cumulative Layout Shift (CLS = 0)
- Todo contenedor de imagen (`cover-wrapper`, miniaturas de vídeos, fotos de veredictos y carátulas de préstamos) **DEBE** declarar un ratio de aspecto CSS explícito (`aspect-square`, `aspect-video`) o dimensiones fijas `width` y `height`.
- En `GameDetail.razor`, el contenedor de la carátula principal **DEBE** mantener una relación de aspecto rígida (`aspect-square`) para evitar el colapso del contenedor antes de la llegada de los bytes de la imagen remota.
- En `MainLayout.razor`, la imagen del logotipo **DEBE** declarar `width="36"` y `height="36"`.

### REQ-CWV-03: Optimización de INP y Disposición de Temporizadores
- En `CatalogSearchBar.razor`, el componente **DEBE** implementar `@implements IDisposable`.
- Al desmontarse el componente o navegar a otra ruta, el temporizador de debounce `System.Threading.Timer` **DEBE** ser cancelado y desechado (`_debounceTimer?.Dispose()`), evitando fugas de memoria e invocaciones huérfanas en el hilo de UI.

---

## 3. Criterios de Aceptación (Escenarios Gherkin)

### Escenario 1: Carátula Hero con alta prioridad y preconexión
```gherkin
Given un usuario navegando a la ficha de un juego /juegos/{slug}
When se renderiza la cabecera del documento y el elemento hero
Then el documento contiene <link rel="preconnect" href="https://cf.geekdo-images.com" crossorigin />
And la imagen de la carátula contiene fetchpriority="high" y decoding="async"
```

### Escenario 2: Estabilidad de maquetación (Zero CLS) en tarjetas de catálogo
```gherkin
Given un listado de juegos en la página principal /
When las imágenes de portada están pendientes de descargar
Then cada tarjeta de juego reserva un contenedor con clase cover-wrapper y aspect-ratio definido
And el renderizado no produce desplazamiento de elementos adyacentes al completar la descarga
```

### Escenario 3: Limpieza de recursos en buscador
```gherkin
Given el componente CatalogSearchBar activo con un temporizador en curso
When el componente es destruido por navegación
Then se ejecuta el método Dispose()
And el temporizador _debounceTimer es liberado sin lanzar excepciones
```
