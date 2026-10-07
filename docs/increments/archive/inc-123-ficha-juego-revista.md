# Incremento 123: Rediseño Editorial de Ficha de Juego (Escritorio y Móvil - Revista Lúdica)

- **ID del Incremento:** `INC-123`
- **Slug:** `ficha-juego-revista`
- **Rama:** `inc/ficha-juego-revista`
- **Fecha:** 2026-10-07
- **Estado:** ✅ Verificado (2.617 pruebas unitarias en verde)
- **Épica / Contexto:** Revista Lúdica (Alineación visual con el prototipo interactivo `Ludeka Final.dc.html` y `Ludeka Final Movil.dc.html`).

---

## 1. Objetivos y Alcance
Trasladar fielmente la experiencia visual y editorial de la ficha de juego definida en el diseño de Claude a la aplicación Blazor en `src/Ludeka.Web/Components/Pages/GameDetail.razor`:

1. **Cabecera envolvente terracota (`bg-[var(--brand)] text-[var(--on-brand)]`):**
   - Banda a ancho completo con migas integradas en contraste suave.
   - Marco polaroid físico rotado 2° con carrusel fotográfico (portada, trasera, en mesa), selector numérico `1 / N`, miniaturas navegables y pegatina de mejor precio girada -6°.
   - Título con tipografía nítida y contraste editorial, subtítulo jerarquizado (estilo, año, editorial).
   - Fila compacta de píldoras en escritorio y móvil (`BGG`, `Ludeka`, ranking BGG con icono Lucide `trophy`, jugadores ideales, edad mínima y duración).
   - Bloque de cita destacada del veredicto con botón hacia el panel lateral desplegable.

2. **Botonera de colección y acciones depurada:**
   - Botón split con desplegable («En mi ludoteca», «Lo quiero», «Jugado»).
   - Botón contorneado de «Registrar partida» con contador dinámico de partidas registradas.
   - Botón circular de compartir mediante enlace en portapapeles.
   - **Decisiones de negocio explícitas:**
     - Se retira «Avisar si baja de precio» (se pospone a un futuro sistema de alertas).
     - Se retira «Prestar» de la ficha de juego (su ubicación canónica es la propia ludoteca del usuario).
     - «Cartel para redes» se reubica exclusivamente en el panel y herramientas de administración/staff.

3. **Cinta de mercado y barra pegajosa de navegación:**
   - Cinta inversa de mercado con mejor precio del día y número de vídeos y fundas.
   - Barra pegajosa (Sticky Nav) con 5 pestañas reactivas: `Resumen`, `Vídeos`, `Dudas`, `Fundas`, `Expansiones`.
   - Distribución asimétrica de 12 columnas (8 para contenido principal, 4 para columna fija lateral con `StoreOffersCard` y `UserReviewCard`).

4. **Estructura en 5 bloques editoriales secuenciales y panel de veredicto:**
   - Preservación estricta del orden de marcado para los 5 bloques editoriales (`bloque-01-cabecera` a `bloque-05-multimedia-comunidad`).
   - Semáforo de escalabilidad comunitaria de 7 columnas (`ScalabilityTrafficLight`).
   - ADN lúdico y 2 vídeos destacados en el resumen con enlace a la pestaña multimedia.
   - Panel lateral deslizable (slide-over modal / bottom sheet) para el veredicto completo de la Mesa Fundadora, pros, contras, contexto ideal y herramientas de staff.

5. **Adaptación móvil extrema:**
   - Carátula 4:3 con controles táctiles y paginación por puntos (*dots*).
   - Barra fija inferior al alcance del pulgar con mejor precio hoy y botón directo de ludoteca.

---

## 2. Archivos Modificados
- `src/Ludeka.Web/Components/Pages/GameDetail.razor`: Rediseño completo de la ficha, integración de cabecera terracota, marco polaroid con carrusel, botonera de colección, barra pegajosa, panel lateral deslizable y vinculación con herramientas de moderación.
- `tests/Ludeka.UnitTests/Web/GameDetailEditorialBlocksContractTests.cs`: Actualización del contrato de marcado para certificar la retirada de «Prestar» y presencia de «Registrar partida».
- `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md`: Registro y seguimiento del incremento INC-123.

---

## 3. Verificación
- **Compilación C# / Blazor:** `dotnet build src/Ludeka.Web/Ludeka.Web.csproj` completada con 0 errores.
- **Suite de Pruebas Unitarias:** `dotnet test tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj` completada con éxito.
  - **Total:** 2.617 pruebas superadas al 100% (0 errores, 0 omitidos).
  - Verificados al 100% los contratos de `GameDetailEditorialBlocksContractTests`, `GameDetailEditorialContractTests` y `WebMarkupContractTests`.
