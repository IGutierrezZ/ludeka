# Propuesta: INC-99 Carruseles Horizontales y Rediseño Compacto en Expansiones y Hub Multimedia

## Motivación
En las fichas de juego (`GameDetail.razor`), las secciones de Expansiones (`ExpansionEcosystemSection.razor`) y Multimedia (`MultimediaHub.razor`) ocupan un espacio vertical desmesurado debido a cuadrículas verticales rígidas (`grid grid-cols-1 md:grid-cols-2`) con tarjetas de gran tamaño.
- Con 7 expansiones o 6 vídeos, la página genera un scroll masivo de miles de píxeles antes de llegar al resto del contenido.
- El mezclador de mesa de expansiones apila un selector de 3 columnas sobre el panel de diagnóstico, desbordando verticalmente.
- El selector de categorías de multimedia agrupa píldoras y botones de moderación en múltiples filas amontonadas con colores heterogéneos y estética poco profesional.

## Alcance
1. **Expansiones (`ExpansionEcosystemSection.razor`):**
   - Carrusel horizontal con desplazamiento suave (`overflow-x-auto snap-x snap-mandatory scrollbar-none`), arrastre con ratón y flechas de navegación prev/next.
   - Tarjetas compactas (`w-[280px]` a `w-[310px]`) que muestran carátula, rating, tags y enlace a ficha, permitiendo ver 2 o 3 simultáneas.
   - Mezclador de Mesa en layout de 2 columnas (`lg:grid-cols-12`): lista vertical compacta de expansiones con scroll interno (`max-h-[360px] overflow-y-auto`) a la izquierda y panel de diagnóstico en tiempo real a la derecha.
2. **Hub Multimedia (`MultimediaHub.razor`):**
   - Barra segmentada estilizada (*Segmented Control* editorial) en fila única para el filtrado de vídeos (`Todos`, `Tutoriales`, `Cómo Funciona`, `Partidas Completas`, `Opiniones y Redes`), con badges limpios y coherencia cromática de Ludeka.
   - Botones de moderador compactos a la derecha sin interferir con el selector.
   - Carrusel horizontal de vídeos (`w-[280px]` a `w-[320px]`) con tarjetas compactas en proporción 16:9, badges translúcidos, controles de desplazamiento y arrastre.
   - Retirada de contenedor doble redundante (`bg-[var(--bg-card)] p-6 rounded-2xl`) dentro de la pestaña de `GameDetail.razor`.
3. **Interoperabilidad (`rail-scroll.js`):**
   - Incorporar `window.ludekaScrollRail(elementId, distance)` para soporte nativo y suave de desplazamiento por clic en flechas.
4. **Pruebas de Contrato y Regresión:**
   - Pruebas de contrato de interfaz en `Ludeka.UnitTests/Web/ExpansionAndMediaCarouselUiContractTests.cs`.
   - Compatibilidad completa con `MultimediaHubUiContractTests` y paso de la suite unitaria.
