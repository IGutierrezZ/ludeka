# Especificación de Requerimientos: INC-123 — Ficha de Juego Editorial (Revista Lúdica)

## Requerimientos Funcionales y de Interfaz

### 1. Cabecera Editorial (`#bloque-cabecera`)
- **REQ-01 (Banda Terracota):** La cabecera principal debe cubrir el ancho completo con fondo `var(--brand)` y texto claro `var(--on-brand)`.
- **REQ-02 (Barra de Navegación Superior):**
  - Enlace izquierdo: `← Volver al catálogo` (`href="/catalogo"`).
  - Enlace derecho: `Reportar errata` (disparador del modal `_isReportModalOpen = true`).
  - La acción `Cartel para Redes` no se muestra aquí; se reubica en las herramientas de staff.
- **REQ-03 (Información y Píldoras):**
  - Eyebrow: `[ESTILO] · [AÑO] · [DUREZA]` en color `var(--mustard)`.
  - Título principal `<h1>`: titular de gran escala `clamp(52px,6vw,76px)` con `tracking-[-0.055em]`.
  - Subtítulo de autoría y editorial española enlazada (`/editoriales/{slug}`).
  - Fila de píldoras redondeadas (`rounded-full`):
    - `BGG {Rating}` (fondo `#FFF8EE`, texto `#22180F`)
    - `Ludeka {Rating}` (fondo `#FFC145`, texto `#22180F`)
    - `Ideal a {Jugadores}` (borde `1.5px solid rgba(255,248,238,.6)`)
    - `{Duración}` (borde `1.5px solid rgba(255,248,238,.6)`)
    - `{Dureza} · {Edad}` (borde `1.5px solid rgba(255,248,238,.6)`)
- **REQ-04 (Extracto de Veredicto):**
  - Muestra un contenedor horizontal fino con borde superior e inferior tenue (`rgba(255,248,238,.35)`).
  - Badge mostaza con la etiqueta de veredicto (ej. `IMPRESCINDIBLE`).
  - Frase de resumen y enlace `Leer veredicto` (o `Leer más` en móvil) que activa la apertura del panel lateral `_isVerdictModalOpen = true`.
- **REQ-05 (Botonera de Ludoteca):**
  - Botón dividido (split button) en color inverso (`#22180F` con texto `#FFC145`):
    - Botón primario: Muestra el estado activo («En mi ludoteca», «Lo quiero», «Jugado» o «Añadir a mi ludoteca»).
    - Botón secundario `▾`: Despliega menú flotante accesible con las 3 opciones de estado de colección. Se excluyen «Avisarme si baja de precio» y «Prestar».
  - Botón contorneado blanco: «Registrar partida» (abre `RecordPlayModal` y muestra el contador de partidas si > 0).
  - Botón circular contorneado blanco: «↗» (acción de compartir enlace mediante Clipboard/WebShare).
- **REQ-06 (Carrusel Polaroid Físico en Escritorio):**
  - Marco polaroid `#FFF8EE` con rotación a 2° y sombra profunda (`0 26px 44px rgba(34,24,15,.3)`).
  - Controles de navegación anterior/siguiente superpuestos en la fotografía.
  - Indicador numérico de diapositiva (`1 / N`).
  - Fila de miniaturas inferiores con borde activo para cambio inmediato de diapositiva.
  - Etiqueta de precio girada a -6° en tono inverso (`#22180F` y mostaza): «Desde {MejorPrecio}».
  - Enlace directo a BGG: `Ver en BGG ↗`.
- **REQ-07 (Adaptación Móvil del Hero):**
  - Fotografía a proporción 4:3 con selector táctil y puntos de paginación inferiores.
  - Eyebrow y título a 40px con `tracking-[-0.05em]`.
  - Píldoras compactas adaptables y botonera con iconos de un toque (`+` para registrar partida, `↗` para compartir).

### 2. Navegación Fija por Pestañas (`sticky top-0`)
- **REQ-08 (Barra de Pestañas):**
  - Barra `nav` fija superior con fondo `var(--paper)` y borde inferior `var(--line)`.
  - Pestañas con tipografía en negrita y barra indicadora inferior de 3px para la pestaña activa:
    1. `Resumen` (activa por defecto).
    2. `Vídeos` (incluye conteo de vídeos disponibles).
    3. `Dudas` (incluye conteo de preguntas o FAQ de reglas).
    4. `Fundas` (incluye conteo de formatos de fundas).
    5. `Expansiones` (incluye conteo de expansiones; visible siempre o deshabilitada con texto claro).

### 3. Vistas de Contenido por Pestaña
- **REQ-09 (Pestaña Resumen):**
  - Bloque `¿A cuántos se disfruta?`: Escala de 7 columnas con barras verticales y leyenda de semáforo (`ScalabilityTrafficLight`).
  - Bloque `ADN lúdico`: Rejilla de metadatos (complejidad, huella, etc.) y botón conmutador para desplegar mecánicas completas.
  - Bloque `Vídeos destacados`: 2 accesos directos con miniatura, duración y canal, con enlace para ver la pestaña completa de vídeos.
- **REQ-10 (Pestaña Vídeos):**
  - Vídeo principal destacado o reproductor de gran formato.
  - Rejilla del resto de vídeos clasificados (`MultimediaHub`).
  - Opción de proponer/añadir nuevo vídeo de YouTube.
- **REQ-11 (Pestaña Dudas):**
  - Acordeón interactivo de preguntas y respuestas de reglas (`RuleQuestionsSection`).
  - Enlace de consulta comunitaria.
- **REQ-12 (Pestaña Fundas):**
  - Guía visual de fundas necesarias (`SleeveGuideCard`), dimensiones en milímetros, número de cartas y enlaces a tiendas.
- **REQ-13 (Pestaña Expansiones):**
  - Tarjetas editoriales con miniatura, etiqueta de necesidad («Muy recomendable», «Opcional»), título y botón `+ A mi ludoteca`.
  - Mensaje amigable cuando no existan expansiones registradas.

### 4. Barra Lateral Fija (Escritorio)
- **REQ-14 (Dónde Comprar):**
  - Muestra la lista de tiendas (`StoreOffersCard`), precio destacado en `var(--accent)`, notas de disponibilidad y advertencia de afiliados.
- **REQ-15 (Tu Valoración):**
  - Tarjeta con borde de 2px `var(--rule)` que integra la valoración del usuario mediante estrellas interactivas y acceso a reseña.

### 5. Panel Deslizable de Veredicto (Slide-over Modal)
- **REQ-16 (Panel Lateral de Veredicto):**
  - Diálogo lateral accesible con animación suave y cierre por overlay, botón `✕` y tecla Escape.
  - Cabecera oscura `#22180F` con badge mostaza, título y fuente («Mesa Fundadora»).
  - Contenido: Cita textual, análisis ampliado, bloques «Lo mejor» y «A tener en cuenta», y recomendaciones «Si te gusta, prueba también».
  - Panel para moderadores/staff con botones para generar síntesis IA o editar veredicto.

### 6. Herramientas de Gestión de Staff
- **REQ-17 (Cartel para Redes en Admin):**
  - En el panel de herramientas de staff (`GameStaffToolsPanel` o barra contextual), se incluye el botón para abrir `SocialCardModal`, manteniendo limpio el flujo de usuario regular.
