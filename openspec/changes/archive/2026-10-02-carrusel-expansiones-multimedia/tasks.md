# Tareas: INC-99 Carruseles Horizontales y Rediseño Compacto en Expansiones y Hub Multimedia

- [x] **Tarea 1: Interoperabilidad JavaScript para Desplazamiento Suave**
  - [x] Añadir `window.ludekaScrollRail` en `src/Ludeka.Web/wwwroot/js/rail-scroll.js`.

- [x] **Tarea 2: Rediseño de Expansiones y Mezclador (`ExpansionEcosystemSection.razor`)**
  - [x] Implementar carrusel horizontal con snap y scroll táctil en la pestaña `list`.
  - [x] Incorporar botones de flecha prev/next interactivos para desplazamiento del carril de expansiones.
  - [x] Rediseñar las tarjetas de expansión a formato compacto con carátula cuadrada, tags y enlace a ficha.
  - [x] Reestructurar el Mezclador de Mesa en layout a 2 columnas (`lg:grid-cols-12`): selector vertical con scroll interno limitado a la izquierda y panel de diagnóstico a la derecha.
  - [x] Adaptar pestaña de recetas a carril deslizante.

- [x] **Tarea 3: Rediseño de Hub Multimedia (`MultimediaHub.razor`)**
  - [x] Limpiar contenedor redundante de doble marco y padding exterior.
  - [x] Crear barra segmentada profesional (*Segmented Control* / pills editoriales) en una sola fila compacta con badges numéricos.
  - [x] Reubicar botones de moderador (`Buscar en YouTube`, `Ingesta Flash`) a controles secundarios a la derecha.
  - [x] Sustituir la cuadrícula de vídeos por carrusel horizontal (`w-[280px]` a `w-[320px]`) con snap y tarjetas compactas 16:9.
  - [x] Incorporar botones de flecha prev/next para desplazamiento del carril de vídeos.
  - [x] Preservar modales de embebido, moderación directa, ingesta flash y búsqueda de YouTube.

- [x] **Tarea 4: Pruebas Unitarias de Contrato de Interfaz**
  - [x] Crear `Ludeka.UnitTests/Web/ExpansionAndMediaCarouselUiContractTests.cs`.
  - [x] Verificar y actualizar `MultimediaHubUiContractTests.cs` si fuera necesario.
  - [x] Ejecutar la suite completa de pruebas unitarias (`dotnet test`) asegurando 100% en verde (2.277 pruebas superadas).
