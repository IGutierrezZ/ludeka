# INC-99: Rediseño Compacto y Carruseles Horizontales en Expansiones y Hub Multimedia

## Estado
⏳ En progreso

## Rama y Worktree
- **Rama:** `inc/carrusel-expansiones-multimedia`
- **Worktree:** `F:\repos\ludeka-wt\carrusel-expansiones-multimedia`

## Descripción del Problema
En la ficha de detalle de juego (`GameDetail.razor`), las secciones de **Expansiones Oficiales** (`ExpansionEcosystemSection.razor`) y **Hub Multimedia** (`MultimediaHub.razor`) ocupan un espacio vertical desmesurado debido a cuadrículas verticales (`grid grid-cols-1 md:grid-cols-2`) con tarjetas de gran tamaño.
1. En **Expansiones**: títulos con 7 o más expansiones generan un scroll kilométrico; asimismo, el **Mezclador de Mesa** ocupa excesiva altura al mostrar un selector de tres columnas amontonado verticalmente sobre el diagnóstico.
2. En **Hub Multimedia**:
   - El selector de categorías actual (`Todos`, `Cómo Funciona`, `Tutoriales`, `Partidas Completas`, `Opiniones y Redes`) junto con las acciones de moderación genera saltos de línea desordenados y colores discordantes.
   - La cuadrícula de 2 columnas muestra miniaturas gigantescas que fuerzan un scroll masivo para ver pocos vídeos.

## Alcance de la Solución
1. **Carrusel Horizontal de Expansiones (`ExpansionEcosystemSection.razor`):**
   - Sustituir la cuadrícula vertical por un carril horizontal con desplazamiento táctil fluido (`overflow-x-auto snap-x snap-mandatory scrollbar-none scroll-smooth`), arrastre con ratón (`rail-scroll.js`) y controles prev/next.
   - Tarjetas de expansión compactas y optimizadas (`w-[280px]` a `w-[310px]`) que permiten ver 2 o 3 simultáneas.
   - Rediseño del **Mezclador de Mesa** en un layout a dos columnas (`lg:grid-cols-12`) sin scroll excesivo: selector vertical compacto a la izquierda (`max-h-[360px] overflow-y-auto`) y panel de diagnóstico en tiempo real a la derecha.
2. **Barra Segmentada Profesional y Carrusel de Vídeos (`MultimediaHub.razor`):**
   - Sustituir la botonera abigarrada por una **barra segmentada estilizada y unificada** (estilo *Segmented Control* / pills de diseño editorial) en una sola fila compacta y armónica, con badges de conteo y estilos acordes a la paleta de Ludeka.
   - Reubicar las acciones de moderación (`Buscar en YouTube`, `Ingesta Flash`) en botones secundarios compactos y elegantes a la derecha, preservando al 100% sus modales y funciones.
   - Sustituir la cuadrícula de vídeos gigantes por un **carrusel horizontal deslizante** (`w-[280px]` a `w-[320px]`), con tarjetas compactas que permiten ver 2 o 3 vídeos a la vez, con miniaturas 16:9, badges translúcidos, controles prev/next y arrastre fluido con ratón/dedo.
   - Eliminar el doble marco/padding redundante dentro de la pestaña de `GameDetail.razor`.
3. **Soporte JS para Desplazamiento por Botón (`rail-scroll.js`):**
   - Incorporar la función global `window.ludekaScrollRail(elementId, distance)` para permitir desplazamiento suave al accionar los botones de flecha del carrusel.
4. **Verificación y Pruebas:**
   - Crear suite de pruebas de contrato en `Ludeka.UnitTests/Web` para validar la estructura del carrusel, dimensiones compactas, barra segmentada y ausencia de cuadrículas verticales expansivas.
   - Comprobar que todas las pruebas existentes de `MultimediaHubUiContractTests` y la suite global (2.268+ pruebas) sigan en verde.
