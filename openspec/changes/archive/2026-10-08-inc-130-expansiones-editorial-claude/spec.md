# Especificación: INC-130 Pestaña de Expansiones Editorial Limpia

## Requisitos del Sistema

### REQ-1: Cabecera y Estructura en GameDetail.razor
- **REQ-1.1:** La pestaña de expansiones (`_activeTab == "expansiones"`) DEBE mostrar como título visible `<h2 class="text-xl sm:text-2xl font-black text-[var(--ink)] tracking-tight">Expansiones</h2>`.
- **REQ-1.2:** Queda eliminado el identificador de bloque grande `04` y el sufijo `& Dónde Comprar` del encabezado visual, manteniendo `id="bloque-04-expansiones-tiendas"` y `aria-label` para compatibilidad de accesibilidad y tests.
- **REQ-1.3:** El contador de expansiones en cabecera y en el botón del sticky nav DEBE reflejar el número de expansiones disponibles o hermanas sin prefijos redundantes.

### REQ-2: Rediseño Editorial de ExpansionEcosystemSection.razor
- **REQ-2.1:** Quedan eliminados el mezclador de mesa (`Mezclador de Mesa`), el evaluador de combinaciones (`EvaluateMixerCombinationAsync`), las recetas (`Recetas Recomendadas`), las subpestañas internas (`_activeTab = "list"|"mixer"|"recipes"`) y los botones de desplazamiento de carril por JavaScript (`ludekaScrollRail`).
- **REQ-2.2:** Las expansiones DEBEN mostrarse en una cuadrícula responsive limpia (`grid grid-cols-1 md:grid-cols-2 gap-4`).
- **REQ-2.3:** Cada tarjeta de expansión DEBE contener:
  - Portada con dimensiones fijas (`width="64"` y `height="64"` o dimensiones contenidas con `loading="lazy"` y `decoding="async"`).
  - Etiqueta de necesidad simplificada («Opcional», «Imprescindible», «Muy recomendada», «Para completistas», «Prescindible»).
  - Título con enlace directo a la ficha `/juegos/{slug}`.
  - Metadatos con año y jugadores (e.g. `2023 · hasta 6 jugadores`).
  - Botón de acción «+ A mi ludoteca» / «En mi ludoteca» (con icono Lucide `check` si está añadida) que interactúa con `IUserLibraryService`.
- **REQ-2.4:** Si el juego no dispone de expansiones, DEBE mostrar un estado vacío sobrio sin romper la cuadrícula.
