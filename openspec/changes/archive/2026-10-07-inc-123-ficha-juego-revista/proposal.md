# Propuesta: INC-123 — Rediseño Editorial de Ficha de Juego (Escritorio y Móvil - Revista Lúdica)

## Metadatos
- **Fecha:** 2026-10-07
- **Incremento:** INC-123
- **Rama:** `inc/ficha-juego-revista`
- **Slug:** `ficha-juego-revista`
- **Estado:** En curso (ODD / SDD)

## Contexto y Motivación
Tras la entrega de las mejoras de diseño en el prototipo interactivo de Claude (`Ludeka Final.dc.html` y su variante móvil), la ficha de juego (`GameDetail.razor`) requiere alinearse con el lenguaje editorial «Revista Lúdica» (Dirección C):
1. **Cabecera envolvente terracota:** Sustituir la cabecera neutral actual por una banda terracota a todo el ancho (`bg-[var(--brand)] text-[var(--on-brand)]`), integrando las migas, la carátula física inclinada a 2° con carrusel fotográfico polaroid y miniaturas, la fila compacta de píldoras (`BGG`, `Ludeka`, `Ideal a X`, `Duración`, `Dureza/Edad`), y el extracto de veredicto con acceso a panel lateral.
2. **Botonera de ludoteca depurada:** Botón partido en tono inverso (`#22180F` con acento `#FFC145`) con selector de estado («En mi ludoteca», «Lo quiero», «Jugado»), botón contorneado de «Registrar partida» y acción de compartir. Se descartan la alerta de precio (sin soporte backend en esta fase) y la acción de prestar (cuya ubicación canónica es la propia ludoteca del usuario).
3. **Navegación pegajosa por pestañas:** Sustituir el scroll vertical secuencial de 5 bloques por una barra `sticky` con 5 pestañas reactivas (`Resumen`, `Vídeos`, `Dudas`, `Fundas`, `Expansiones`), manteniendo en escritorio la columna lateral fija con «Dónde comprar» y «Tu valoración».
4. **Panel lateral deslizable de veredicto:** Al pulsar «Leer veredicto» en cabecera, se despliega un panel lateral derecho (slide-over modal en escritorio, hoja inferior en móvil) con el desglose completo de la Mesa Fundadora (cita, análisis, pros, contras, recomendaciones y controles de moderación/IA).
5. **Herramientas de administración/staff:** Integrar la generación de «Cartel para Redes» dentro del panel de gestión administrativa (`GameStaffToolsPanel`), preservando las herramientas de sincronización y edición.
6. **Adaptación móvil extrema:** Carátula 4:3 con navegación táctil y paginación por puntos, fila fluida de píldoras, veredicto abreviado y barra de compra inferior fija al alcance del pulgar.

## Objetivos
1. **Banda terracota en cabecera:** Rediseñar la cabecera de `GameDetail.razor` tanto en escritorio como en móvil reflejando la alta fidelidad del prototipo `Ludeka Final`.
2. **Carrusel polaroid físico:** Marco físico inclinado con carrusel integrado (portada, trasera, mesa), selector de miniaturas y pegatina de precio flotante girada a -6°.
3. **Pestañas de contenido:** Implementar la navegación interactiva por pestañas preservando los componentes existentes (`ScalabilityTrafficLight`, `MultimediaHub`, `RuleQuestionsSection`, `SleeveGuideCard`, `ExpansionEcosystemSection`).
4. **Slide-over de veredicto:** Implementar el modal lateral con cierre suave, preservando el contenido enriquecido y los permisos de staff para la síntesis IA.
5. **Reubicación de acciones:** Retirar «Prestar» de la ficha (centralizado en Mi Ludoteca) y alojar «Cartel para redes» en la barra/panel de herramientas de staff.

## No Objetivos (Out of Scope)
- Sistema backend de alertas de bajada de precio (se abordará en un incremento futuro dedicado a notificaciones de precio).
- Modificaciones en la lógica interna del servicio de préstamos (`IUserLibraryService`) ni en la página de Mi Ludoteca.
