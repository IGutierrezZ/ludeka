# INC-130: Pestaña de Expansiones Editorial Limpia (Alineación Claude Design y Retirada del Mezclador)

**Estado:** ⏳ En progreso (Verificado 2.677 tests)  
**Rama:** `inc/expansiones-diseno-claude`  
**Objetivo:** Alinear la pestaña de Expansiones en la ficha de detalle de juego (`GameDetail.razor` y `ExpansionEcosystemSection.razor`) con el prototipo editorial de Claude, eliminando el mezclador en mesa y las recetas no utilizadas, e implementando tarjetas limpias con portada, etiqueta de necesidad («Opcional», «Imprescindible»), título, metadatos y acción directa «+ A mi ludoteca».

---

## 1. Contexto y Justificación

En la versión actual de la ficha de detalle de juego, la pestaña de expansiones conservaba el bloque heredado «04 Expansiones & Dónde Comprar» con subpestañas internas complejas («Expansiones», «Mezclador de Mesa», «Recetas»), carrusel horizontal con flechas y herramientas de simulación de mesa que no aportan valor a la experiencia de consulta editorial y saturan la interfaz.

El nuevo diseño editorial de Claude simplifica radicalmente esta sección:
1. **Cabecera Limpia:** Título directo `Expansiones`, eliminando la numeración obsoleta `04` y la redundancia `& Dónde Comprar` (las ofertas de tiendas ya residen de forma permanente en la barra lateral fija).
2. **Retirada del Mezclador:** Supresión completa del mezclador de combinaciones, recetas y carrusel con scroll por script.
3. **Tarjetas Editoriales:** Cuadrícula de 2 columnas con tarjetas contenidas (`rounded-2xl bg-[var(--paper-2)]`), carátula nítida, píldora de necesidad («Opcional», «Muy recomendada», «Imprescindible»), metadatos de año y jugadores añadidos, y botón interactivo «+ A mi ludoteca» / «En mi ludoteca».
4. **Soporte de Expansiones Hijas:** Mantenimiento de la ficha de aportes y lista de expansiones hermanas cuando la ficha corresponde a una propia expansión.
