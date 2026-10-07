# Tareas de Implementación: INC-123 — Rediseño de Ficha de Juego Editorial

## Tareas

- [ ] **Tarea 1: Estructura del Hero Editorial Terracota y Botonera Dividida**
  - Implementar la banda envolvente `bg-[var(--brand)] text-[var(--on-brand)]` a todo el ancho.
  - Añadir la barra de navegación superior con `← Volver al catálogo` y `Reportar errata`.
  - Diseñar la columna de metadatos con eyebrow mostaza, titular gigante, autoría, fila de píldoras compactas y recuadro de veredicto.
  - Implementar la botonera dividida (`[ Añadir a mi ludoteca | ▾ ]`) con menú desplegable accesible para «En mi ludoteca», «Lo quiero» y «Jugado».
  - Añadir botones contorneados para «Registrar partida» y compartir `↗`.
  - Retirar «Prestar» de la botonera principal.

- [ ] **Tarea 2: Carrusel Polaroid Físico Inclinado y Adaptación Móvil del Hero**
  - Construir el marco físico polaroid inclinado a 2° con carrusel de fotografías (portada, trasera, mesa), flechas superpuestas, contador numérico y enlace a BGG.
  - Añadir la fila inferior de miniaturas fotográficas y la pegatina flotante de precio («Desde XX €») girada a -6°.
  - Diseñar la versión móvil (<760px) del hero con fotografía 4:3 interactiva, paginación por puntos, titular a 40px y botonera compacta al pulgar.

- [ ] **Tarea 3: Barra Pegajosa de Pestañas y Vistas de Contenido**
  - Implementar la barra `nav` fija (`sticky top-0`) con las 5 pestañas (`Resumen`, `Vídeos`, `Dudas`, `Fundas`, `Expansiones`) y regla indicadora de 3px.
  - Pestaña `Resumen`: Integrar `¿A cuántos se disfruta?` (escala 1-7+), `ADN lúdico` con conmutador de ficha técnica/mecánicas, y bloque de `Vídeos destacados`.
  - Pestaña `Vídeos`: Integrar el reproductor/vídeo destacado grande y la galería de `MultimediaHub`.
  - Pestaña `Dudas`: Integrar el consultorio de dudas y FAQs de `RuleQuestionsSection`.
  - Pestaña `Fundas`: Integrar la especificación de fundas de `SleeveGuideCard`.
  - Pestaña `Expansiones`: Integrar las tarjetas de expansiones y estado vacío si aplica.

- [ ] **Tarea 4: Columna Lateral Fija y Panel Deslizable de Veredicto (Slide-over)**
  - Mantener en la columna lateral fija de escritorio «Dónde comprar» (`StoreOffersCard`) y «Tu valoración» (`UserReviewCard` / estrellas).
  - Implementar el panel lateral deslizable (slide-over modal / bottom-sheet) para el veredicto completo de la Mesa Fundadora, pros, contras, recomendaciones y controles de moderación/IA.
  - Reubicar el botón «Cartel para Redes» en el panel de herramientas de staff (`GameStaffToolsPanel`).

- [ ] **Tarea 5: Verificación Integral, Pruebas y Compilación**
  - Ejecutar la suite completa de pruebas unitarias (`dotnet test`).
  - Verificar la responsividad móvil y de escritorio de la ficha.
  - Asegurar cero regresiones en la gestión de colecciones, registros de partida y reportes de erratas.
