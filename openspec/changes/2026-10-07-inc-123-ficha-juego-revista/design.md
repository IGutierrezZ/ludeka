# Documento de Diseño: INC-123 — Rediseño de Ficha de Juego Editorial

## Arquitectura de Componentes y Estado

### 1. `GameDetail.razor` como Orquestador de Ficha
`GameDetail.razor` sigue siendo la página principal (`@page "/juegos/{Slug}"`), pero su estructura interna se refactoriza de un flujo de 5 bloques verticales apilados a:
1. **Hero Editorial Terracota (`#hero-terracota`)**:
   - Contenedor con `bg-[var(--brand)] text-[var(--on-brand)]` a todo el ancho.
   - En escritorio: cuadrícula de 2 columnas (`minmax(0,1fr)` y `minmax(320px,380px)`).
   - En móvil: bloque apilado con carátula 4:3 en la parte superior y metadatos debajo.
   - Estado local de selección de imagen (`_selectedImageIndex`), menú de colección desplegable (`_isLibMenuOpen`), y modal de veredicto lateral (`_isVerdictSlideOverOpen`).
2. **Barra Fija de Pestañas (`#ficha-tabs-nav`)**:
   - `nav` con clase `sticky top-0 z-20 bg-[var(--paper)] border-b border-[var(--line)]`.
   - Estado reactivo `_activeTab` con valores: `"resumen"`, `"videos"`, `"dudas"`, `"fundas"`, `"expansiones"`.
3. **Distribución Principal a Dos Columnas (`#ficha-body`)**:
   - En escritorio: columna principal de contenido (`flex-1 min-w-0`) y columna lateral fija (`w-[340px] sticky top-16`).
   - En móvil: columna única de contenido, con barra de compra inferior fija al fondo (`fixed bottom-0`).

### 2. Panel Deslizable Lateral para el Veredicto (`#verdict-slideover`)
En lugar de incrustar `FoundingVerdictCard` y `AiSummaryCard` directamente en el cuerpo en un bloque 02 estático, se crea un panel lateral deslizable (slide-over):
- Disparadores: Enlace «Leer veredicto» en el hero de escritorio, o «Leer más» en móvil.
- Renderizado condicional con fondo atenuado (`fixed inset-0 bg-black/60 z-50`) y panel derecho (`fixed right-0 top-0 bottom-0 w-full sm:max-w-xl bg-[var(--paper)] text-[var(--ink)] shadow-2xl z-51 overflow-y-auto animate-in slide-in-from-right duration-200`).
- En móvil, se presenta como hoja deslizante completa o bottom-sheet.
- Reutiliza la lógica existente de `IFoundingVerdictService`, citas, pros, contras, y herramientas de moderación de IA (`HandleRequestAiSummary`, etc.).

### 3. Carrusel Polaroid Físico
Se adapta o envuelve la lógica de `GameImageCarousel.razor` para reflejar el marco físico del prototipo:
- Marco blanco/crema `#FFF8EE` con rotación a 2° (`rotate-2`) y sombra profunda.
- Flechas prev/next circulares semitransparentes superpuestas sobre la fotografía.
- Píldora inferior de posición (`1 / 3`).
- Tira inferior de miniaturas (`52x52px`) con borde activo.
- Pegatina de precio flotante en negro y mostaza (`#22180F` y `#FFC145`) con rotación a -6° (`-rotate-6`).

### 4. Botonera de Ludoteca Compacta con Menú
- Botón dividido (split button):
  - Botón primario: Muestra el estado actual con icono y texto. Al pulsar cambia de estado o añade a la ludoteca.
  - Botón de flecha: Conmuta `_isLibMenuOpen = !_isLibMenuOpen`.
  - Desplegable con opciones de estado: «En mi ludoteca», «Lo quiero», «Jugado».
- Botón contorneado blanco: «Registrar partida» que abre `_isRecordPlayModalOpen = true`.
- Botón circular contorneado blanco: «↗» para compartir (utilizando API de portapapeles o WebShare).

### 5. Herramientas de Staff y Reubicación de Acciones
- En `GameStaffToolsPanel.razor` (o barra de staff en `GameDetail.razor`):
  - Se añade el botón para abrir `SocialCardModal` («Cartel para Redes»).
- En `GameDetail.razor`:
  - Se retira el botón de «Prestar» (la gestión de préstamos se mantiene en la página de Mi Ludoteca).
