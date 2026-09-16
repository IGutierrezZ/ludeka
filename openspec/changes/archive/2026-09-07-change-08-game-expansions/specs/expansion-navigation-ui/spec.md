# Especificación: expansion-navigation-ui (Navegación Transversal, Fichas Blazor y Filtros)

## 1. Contexto y Propósito
Define la experiencia de navegación editorial, anti-AI slop y accesible para el ecosistema de expansiones: visualización de expansiones en el juego base, banner y tarjeta de aporte en la ficha de la expansión, navegación a expansiones hermanas y filtrado en el catálogo.

---

## 2. Requerimientos y Criterios de Aceptación (Gherkin)

### Escenario 1: Sección de Ecosistema en la ficha del Juego Base
**Dado** un usuario navegando en la ficha de un juego base (ej: `/juegos/wingspan`)  
**Cuando** se renderiza la página  
**Entonces** se muestra la sección "Expansiones Oficiales y Compatibilidad" con 3 pestañas:
  1. *Expansiones Disponibles*: tarjetas visuales con carátula, año, badge de veredicto (ej. "🟢 Imprescindible, mejora el juego base"), chips de aporte ("+1 Jugador", "Modo Solitario"), nota y botón "Ver ficha completa".
  2. *Mezclador de Mesa*: selector de casillas para marcar expansiones y ver el diagnóstico de mesa en vivo.
  3. *Recetas Recomendadas*: listado de combinaciones prediseñadas con botón "Cargar en el mezclador".

### Escenario 2: Banner del Juego Base en la ficha de la Expansión
**Dado** un usuario navegando en la ficha de una expansión (ej: `/juegos/wingspan-expansion-europa`)  
**Cuando** se renderiza la cabecera  
**Entonces** se muestra el distintivo visual `🧩 EXPANSIÓN OFICIAL`  
**Y** un banner interactivo con la información del juego base nodriza ("Requiere el juego base Wingspan"), carátula miniatura, nota y enlace directo para ir a la ficha del juego base.

### Escenario 3: Tarjeta de Aporte y Expansiones Hermanas
**Dado** la ficha de una expansión  
**Cuando** el usuario desciende en la página  
**Entonces** visualiza la tarjeta editorial "¿Qué aporta al Juego Base?" con:
  - El badge de veredicto de necesidad.
  - Las píldoras de aporte destacadas.
  - El texto explicativo de la alteración en mesa.
  - El impacto en número de jugadores y tiempo de partida.  
**Y** visualiza la lista de "Otras Expansiones del Ecosistema" con el indicador directo de compatibilidad respecto a la expansión actual (🟢 Óptima, 🟡 Con reservas, 🔴 Incompatible) y enlace a cada una.

### Escenario 4: Distintivo y Filtrado en el Catálogo Principal
**Dado** la vista del catálogo general (`/`)  
**Cuando** se listan los títulos  
**Entonces** las expansiones muestran un badge `🧩 Expansión` en la tarjeta  
**Y** el usuario puede utilizar el selector de tipo de juego para filtrar por `Todos`, `Juegos Base` o `Expansiones`.
