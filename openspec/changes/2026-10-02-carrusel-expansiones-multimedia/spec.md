# Especificación: INC-99 Carruseles Horizontales y Rediseño Compacto en Expansiones y Hub Multimedia

## Requerimientos del Sistema

### REQ-1: Carrusel Horizontal de Expansiones
- **REQ-1.1:** El listado de expansiones en `ExpansionEcosystemSection.razor` (pestaña `_activeTab == "list"`) DEBE renderizarse en un contenedor horizontal con desplazamiento (`overflow-x-auto snap-x snap-mandatory scrollbar-none`), eliminando la cuadrícula vertical de 2 columnas.
- **REQ-1.2:** Cada tarjeta de expansión DEBE tener dimensiones compactas consistentes (`w-[280px]` a `w-[310px]` en pantallas SM/MD/LG, snap-start), mostrando carátula cuadrada (64x64), rating, año, título (máximo 2 líneas truncadas), badge de necesidad, chips de impacto compactos y botón de navegación a la ficha.
- **REQ-1.3:** La cabecera del carril DEBE incluir botones de navegación interactivos (Anterior y Siguiente) con iconos `Icon Name="chevron-left"` y `chevron-right`, accesibles por teclado y clic, que desplazan suavemente el carril horizontal mediante `window.ludekaScrollRail`.

### REQ-2: Mezclador de Mesa Compacto sin Scroll Kilométrico
- **REQ-2.1:** El Mezclador de Mesa (pestaña `_activeTab == "mixer"`) DEBE estructurarse en un diseño a dos columnas responsivo (`grid grid-cols-1 lg:grid-cols-12 gap-5`):
  - Columna izquierda (`lg:col-span-5`): lista vertical compacta de expansiones con altura máxima delimitada (`max-h-[360px] overflow-y-auto pr-1`) y botones compactos tipo list-item de una sola fila con checkbox, título, año y jugadores adicionales.
  - Columna derecha (`lg:col-span-7`): panel de diagnóstico de compatibilidad y sinergias (`_evaluation`) en tiempo real, alineado visualmente para evitar scroll vertical innecesario.
- **REQ-2.2:** En pantallas móviles, el layout se apila fluidamente manteniendo el formato compacto de las filas de expansión.

### REQ-3: Barra Segmentada Profesional en Hub Multimedia
- **REQ-3.1:** El selector de formato de vídeos en `MultimediaHub.razor` DEBE presentarse como una barra segmentada estilizada (*Segmented Control* / pills editoriales) en una sola fila compacta con scroll horizontal sutil (`bg-[var(--bg-surface-elevated)] p-1 rounded-xl border border-[var(--border-subtle)] inline-flex items-center gap-1 overflow-x-auto max-w-full scrollbar-none`).
- **REQ-3.2:** Cada botón de categoría (`Todos`, `Tutoriales`, `Cómo Funciona`, `Partidas Completas`, `Opiniones y Redes`) DEBE mostrar su icono Lucide y un badge numérico limpio con el total de contenidos disponibles.
- **REQ-3.3:** El estado activo DEBE destacarse con la tonalidad de marca `bg-[var(--brand-primary)] text-white shadow-sm font-bold rounded-lg`, mientras que los inactivos mantienen fondo transparente o sutil, eliminando los colores estridentes y descoordinados previos.
- **REQ-3.4:** Los botones de moderador (`Buscar en YouTube` e `Ingesta Flash`) DEBEN situarse como acciones secundarias compactas a la derecha, preservando íntegramente sus funciones y modales.

### REQ-4: Carrusel Horizontal de Vídeos Multimedia
- **REQ-4.1:** Los vídeos filtrados DEBEN presentarse en un carril horizontal (`flex gap-3 sm:gap-4 overflow-x-auto snap-x snap-mandatory scrollbar-none pb-2 pt-1`), sustituyendo la cuadrícula vertical de 2 columnas gigantescas.
- **REQ-4.2:** El ancho de cada tarjeta de vídeo DEBE calibrarse a `w-[270px] sm:w-[300px] md:w-[320px]`, permitiendo ver 2 o 3 vídeos simultáneos en el ancho de la columna principal.
- **REQ-4.3:** Cada tarjeta DEBE mostrar su miniatura 16:9 con badge de tipo translúcido arriba a la izquierda, duración abajo a la derecha, botón play sutil al hover, creador y botón de «me gusta» compacto, y título truncado a 2 líneas.
- **REQ-4.4:** Si el usuario es moderador, los controles de moderación directa (`Cambiar Categoría`, `Eliminar`) DEBEN situarse de forma compacta en el pie de la tarjeta, manteniendo todos los métodos y eventos existentes.
- **REQ-4.5:** La cabecera o laterales del carril DEBEN incluir controles prev/next con desplazamiento suave mediante `window.ludekaScrollRail`.
- **REQ-4.6:** Se DEBE retirar el contenedor exterior redundante con borde y padding pesado (`bg-[var(--bg-card)] border p-6`) para integrarse limpiamente en la pestaña de `GameDetail.razor`.

### REQ-5: Interoperabilidad de Desplazamiento Suave
- **REQ-5.1:** `rail-scroll.js` DEBE exponer `window.ludekaScrollRail(elementId, distance)` para realizar `scrollBy({ left: distance, behavior: 'smooth' })` de forma segura.

## Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Visualización de expansiones en carrusel horizontal
  Dado que un juego base tiene 7 expansiones asociadas
  Cuando el usuario consulta la pestaña "Expansiones"
  Entonces las expansiones se presentan en un carril horizontal con desplazamiento táctil y snap
  Y cada tarjeta tiene ancho fijo compacto (entre 280px y 310px)
  Y no se genera una cuadrícula vertical expansiva
  Y existen botones de navegación prev/next para deslizar el carril

Escenario: Mezclador de mesa compacto a dos columnas
  Dado que el usuario accede a la pestaña "Mezclador de Mesa"
  Cuando la pantalla tiene resolución de escritorio (lg+)
  Entonces el selector de expansiones se muestra en la columna izquierda con scroll interno máximo de 360px
  Y el diagnóstico en tiempo real se sitúa en la columna derecha contigua sin duplicar el scroll vertical de la página

Escenario: Filtrado multimedia con barra segmentada profesional
  Dado que un juego dispone de vídeos en varias categorías
  Cuando el usuario visualiza el Hub Multimedia
  Entonces las opciones de filtrado se presentan en una barra segmentada única y compacta
  Y cada opción muestra su nombre, icono y contador numérico
  Y el elemento seleccionado resalta con estilo primario de la marca
  Y las acciones de moderador se muestran discretamente a la derecha

Escenario: Visualización de vídeos en carrusel horizontal
  Dado que se muestran vídeos en el Hub Multimedia
  Cuando se renderiza el listado
  Entonces los vídeos aparecen en un carril horizontal deslizante
  Y cada tarjeta de vídeo tiene proporción compacta que permite ver 2 o 3 elementos simultáneos
  Y al pulsar sobre la tarjeta o presionar Enter se abre el reproductor embebido
```
