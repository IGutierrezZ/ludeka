# Diseño Técnico: INC-99 Carruseles Horizontales y Rediseño Compacto en Expansiones y Hub Multimedia

## 1. Arquitectura de Componentes

### Componente `ExpansionEcosystemSection.razor`
- **Pestaña "list":**
  - Contenedor con `id="expansions-carousel-rail"`.
  - Clases: `flex gap-3 sm:gap-4 overflow-x-auto snap-x snap-mandatory scrollbar-none pb-2 pt-1 scroll-smooth`.
  - Botones prev/next en la cabecera: invoca `ScrollExpansionsRailAsync(-320)` y `ScrollExpansionsRailAsync(320)`.
  - Tarjetas de expansión: `w-[275px] sm:w-[295px] md:w-[310px] shrink-0 snap-start flex flex-col justify-between p-3.5 rounded-xl bg-[var(--bg-surface-elevated)] border border-[var(--border-subtle)] hover:border-[var(--brand-primary)] transition-all shadow-sm`.
- **Pestaña "mixer":**
  - Layout: `grid grid-cols-1 lg:grid-cols-12 gap-5 items-start`.
  - Columna izquierda (`lg:col-span-5`):
    - Cabecera compacta con título y contador de expansiones activas.
    - Lista de expansiones con scroll interno: `max-h-[360px] overflow-y-auto space-y-1.5 pr-1`.
    - Cada fila: botón compacto de una línea con checkbox (`Icon Name="circle-check"` o `Icon Name="square"`), título de la expansión truncado, año y `+NJ` de jugadores.
  - Columna derecha (`lg:col-span-7`):
    - Panel de diagnóstico `_evaluation` con status global, badges de jugadores resultantes y duración, advertencias de sinergia / conflicto en tarjetas de alerta compactas.
- **Pestaña "recipes":**
  - Carril horizontal similar `overflow-x-auto snap-x snap-mandatory` para recetas de mesa recomendadas si existen.

### Componente `MultimediaHub.razor`
- **Contenedor Principal:**
  - Sustituir `p-6 rounded-2xl bg-[var(--bg-card)] border border-[var(--border-subtle)] shadow-xl space-y-6` por un contenedor limpio `space-y-4` sin duplicar el marco exterior ya presente en `GameDetail.razor`.
- **Barra Segmentada de Filtrado:**
  - Contenedor cápsula: `bg-[var(--bg-surface-elevated)] p-1 rounded-xl border border-[var(--border-subtle)] inline-flex items-center gap-1 overflow-x-auto max-w-full scrollbar-none`.
  - Botones con estado activo:
    - Activo: `bg-[var(--brand-primary)] text-white font-bold shadow-sm rounded-lg px-3 py-1.5 text-xs transition-all`.
    - Inactivo: `text-[var(--text-secondary)] hover:text-[var(--text-primary)] hover:bg-[var(--bg-card)] rounded-lg px-3 py-1.5 text-xs font-semibold transition-all`.
  - Píldoras:
    - `Todos` (`Icon Name="tv"`) + `TotalVideosCount`
    - `Tutoriales` (`Icon Name="clapperboard"`) + `MediaHub?.Tutorials.Count`
    - `Cómo Funciona` (`Icon Name="zap"`) + `MediaHub?.QuickOverviews.Count`
    - `Partidas Completas` (`Icon Name="dices"`) + `MediaHub?.Playthroughs.Count`
    - `Opiniones y Redes` (`Icon Name="message-circle"`) + `SocialCount`
- **Acciones de Moderación:**
  - Botones secundarios compactos: `Buscar en YouTube` (`Icon Name="search"`) e `Ingesta Flash` (`Icon Name="zap"`), con estilos discretos sin interferir con la barra de navegación del usuario.
- **Carrusel de Vídeos:**
  - Contenedor con `id="media-carousel-rail"`.
  - Clases: `flex gap-3 sm:gap-4 overflow-x-auto snap-x snap-mandatory scrollbar-none pb-2 pt-1 scroll-smooth`.
  - Tarjetas de vídeo compactas: `w-[260px] sm:w-[290px] md:w-[310px] shrink-0 snap-start flex flex-col justify-between rounded-xl overflow-hidden bg-[var(--bg-surface)] border border-[var(--border-subtle)] hover:border-[var(--brand-primary)] transition-all shadow-sm group cursor-pointer`.
  - Miniatura 16:9 con badge de tipo y duración superpuestos.
  - Título y autor compactos con botón de me gusta.
  - Botones de moderación en el pie de tarjeta (`Cambiar Categoría`, `Eliminar`).
  - Botones de navegación prev/next en la cabecera: invoca `ScrollMediaRailAsync(-320)` y `ScrollMediaRailAsync(320)`.

### Interop JavaScript (`rail-scroll.js`)
- Añadir función helper:
```javascript
window.ludekaScrollRail = function (elementId, distance) {
    var el = document.getElementById(elementId);
    if (el) {
        el.scrollBy({ left: distance, behavior: 'smooth' });
    }
};
```

## 2. Pruebas y Validación
- Pruebas unitarias de contrato de interfaz en `Ludeka.UnitTests/Web`:
  - `ExpansionAndMediaCarouselUiContractTests`: verifica presencia de carriles horizontales (`snap-x`, `overflow-x-auto`), IDs de navegación, ausencia de cuadrículas verticales `grid-cols-2` en el listado de expansiones y vídeos, y presencia de la barra segmentada.
  - `MultimediaHubUiContractTests`: garantizar que todas las pruebas existentes sigan pasando.
