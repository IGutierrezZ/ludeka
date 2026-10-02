# INC-99: Rediseño Compacto y Carruseles Horizontales en Expansiones y Hub Multimedia

## Estado
✅ Archivado (Mergeado en PR #179, verificado con 2.277 pruebas unitarias + 10 de integración en verde, y desplegado a producción en Google Cloud Run el 2026-10-02)

## Rama y Worktree
- **Rama:** `inc/carrusel-expansiones-multimedia` (fusionada y eliminada)
- **Worktree:** `F:\repos\ludeka-wt\carrusel-expansiones-multimedia` (limpiado)

## Descripción del Problema
En la ficha de detalle de juego (`GameDetail.razor`), las secciones de **Expansiones Oficiales** (`ExpansionEcosystemSection.razor`) y **Hub Multimedia** (`MultimediaHub.razor`) ocupaban un espacio vertical desmesurado debido a cuadrículas verticales (`grid grid-cols-1 md:grid-cols-2`) con tarjetas de gran tamaño.
1. En **Expansiones**: títulos con 7 o más expansiones generaban un scroll kilométrico; asimismo, el **Mezclador de Mesa** ocupaba excesiva altura al mostrar un selector de tres columnas amontonado verticalmente sobre el diagnóstico.
2. En **Hub Multimedia**:
   - El selector de categorías previo (`Todos`, `Cómo Funciona`, `Tutoriales`, `Partidas Completas`, `Opiniones y Redes`) junto con las acciones de moderación generaba saltos de línea desordenados y colores discordantes.
   - La cuadrícula de 2 columnas mostraba miniaturas gigantescas que forzaban un scroll masivo para ver pocos vídeos.

## Alcance de la Solución
1. **Carrusel Horizontal de Expansiones (`ExpansionEcosystemSection.razor`):**
   - Sustituida la cuadrícula vertical por un carril horizontal con desplazamiento táctil fluido (`overflow-x-auto snap-x snap-mandatory scrollbar-none scroll-smooth`), arrastre con ratón (`rail-scroll.js`) y controles prev/next asistidos por `window.ludekaScrollRail`.
   - Tarjetas de expansión compactas y optimizadas (`w-[275px]` a `w-[310px]`) que permiten ver 2 o 3 simultáneas.
   - Rediseño del **Mezclador de Mesa** en un layout a dos columnas (`lg:grid-cols-12`) sin scroll excesivo: selector vertical compacto a la izquierda (`max-h-[380px] overflow-y-auto`) y panel de diagnóstico en tiempo real a la derecha.
2. **Barra Segmentada Profesional y Carrusel de Vídeos (`MultimediaHub.razor`):**
   - Sustituida la botonera abigarrada por una **barra segmentada estilizada y unificada** (estilo *Segmented Control* / pills de diseño editorial) en una sola fila compacta y armónica, con badges de conteo y estilos acordes a la paleta de Ludeka.
   - Reubicadas las acciones de moderación (`Buscar en YouTube`, `Ingesta Flash`) en botones secundarios compactos y elegantes a la derecha, preservando al 100% sus modales y funciones.
   - Sustituida la cuadrícula de vídeos gigantes por un **carrusel horizontal deslizante** (`w-[260px]` a `w-[310px]`), con tarjetas compactas que permiten ver 2 o 3 vídeos a la vez, con miniaturas 16:9, badges translúcidos, controles prev/next y arrastre fluido con ratón/dedo.
   - Eliminado el doble marco/padding redundante dentro de la pestaña de `GameDetail.razor`.
3. **Soporte JS para Desplazamiento por Botón (`rail-scroll.js`):**
   - Incorporada la función global `window.ludekaScrollRail(elementId, distance)` para permitir desplazamiento suave al accionar los botones de flecha del carrusel.
4. **Verificación y Pruebas:**
   - Creada suite de pruebas de contrato `ExpansionAndMediaCarouselUiContractTests.cs` (9 pruebas nuevas).
   - Verificadas las 8 pruebas de `MultimediaHubUiContractTests.cs`.
   - Verificada la suite completa de 2.277 pruebas unitarias + 10 de integración en verde al 100%.
   - Pipeline de CI/CD en GitHub Actions y despliegue a Google Cloud Run completados en verde.
