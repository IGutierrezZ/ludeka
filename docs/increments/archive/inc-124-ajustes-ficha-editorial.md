# Incremento 124: Ajustes Editoriales de Ficha: Stock en Mejor Precio, Retirada de Pegatina Polaroid, Veredicto en Drawer y Limpieza Visual

- **ID del Incremento:** `INC-124`
- **Slug:** `ajustes-ficha-editorial`
- **Rama:** `inc/ajustes-ficha-editorial`
- **Pull Request:** [#224](https://github.com/IGutierrezZ/ludeka/pull/224) (Mergeado a `main` vía squash en commit `d5e4d16`)
- **Fecha:** 2026-10-07
- **Estado:** ✅ Archivado (2.619 pruebas unitarias en verde al 100%, desplegado en producción en Google Cloud Run)
- **Épica / Contexto:** Revista Lúdica (Alineación con el prototipo de Claude Design `Ludeka Final.dc.html` y `Ludeka Final Movil.dc.html`).

---

## 1. Descripción del Problema
A partir del análisis visual con los pantallazos del usuario y el prototipo interactivo de referencia de Claude (`Ludeka Final.dc.html` y `Ludeka Final Movil.dc.html`), se detectaron varias inconsistencias funcionales y visuales en la ficha editorial del juego (`src/Ludeka.Web/Components/Pages/GameDetail.razor`):
1. **Mejor Precio sin Stock:** La función `GetBestPriceLabel()` seleccionaba la tienda con el precio más bajo sin verificar disponibilidad (`InStock`). En consecuencia, se mostraban como «mejor precio» ofertas agotadas o sin stock disponible.
2. **Pegatina Flotante sobre la Imagen:** La cabecera polaroid incluía una pegatina de precio girada `-6°` encima de la carátula, sobrecargando visualmente la composición fotográfica.
3. **Erratas en Píldora de Jugadores:** La concatenación de cadenas generaba textos duplicados del tipo `Ideal a Ideal: 3-4 jugadores`.
4. **Veredicto en el Cuerpo de la Ficha:** En la pestaña `Resumen`, aparecía un bloque visible gigantesco con el veredicto en medio de la página, duplicando información que debía residir exclusivamente en el slide-over drawer modal al pulsar `Leer veredicto`.
5. **Fallos y Z-Index del Slide-Over Modal:** El panel deslizable de veredicto utilizaba `z-50`/`z-51`, quedando cortado o tapado por cabeceras y elementos fijos de la aplicación.
6. **Márgenes y Cajas Beige Pesadas en Resumen:** Las secciones de escalabilidad, ADN lúdico y vídeos destacados estaban encapsuladas en contenedores beige pesados (`bg-[var(--paper-2)] border p-6`), alejándose del diseño plano, nítido y editorial del prototipo.
7. **Pestaña de Fundas sin Contenido:** La pestaña `Fundas` en la barra pegajosa superior y el contador en la cinta inversa se mostraban incluso cuando el juego no tenía especificaciones de fundas disponibles.

---

## 2. Alcance de la Solución Implementada
1. **Lógica de Mejor Precio con Disponibilidad Real (`GameDetail.razor`):**
   - Actualizado `GetBestPriceLabel()` para filtrar estrictamente `p.InStock && p.Price.HasValue && p.Price.Value > 0`.
   - Si no hay tiendas con stock pero sí con precios registrados, devuelve honestamente `Agotado en tiendas`.
2. **Retirada de la Pegatina Flotante Polaroid:**
   - Eliminado el recuadro con rotación `-6°` sobre la carátula del marco polaroid tanto en escritorio como en móvil.
3. **Limpieza del Prefijo de Jugadores Ideales (`GetCleanIdealPlayerText`):**
   - Incorporado el método auxiliar `GetCleanIdealPlayerText()` que normaliza el texto eliminando prefijos redundantes y garantizando un formato uniforme `Ideal a X jugadores`.
4. **Reubicación de Bloque 02 al Slide-Over Modal:**
   - Eliminado el recuadro duplicado de veredicto del flujo normal de la pestaña `Resumen`.
   - El panel deslizable ahora porta `id="bloque-02-veredicto"` con `aria-label="Veredicto de la Mesa"`, ubicado inmediatamente antes del Bloque 03 para cumplir la secuencia canónica del contrato `GameDetailEditorialBlocksContractTests.cs` (`idx1 < idx2 < idx3 < idx4 < idx5`).
   - Elevado a `z-[100]` con backdrop `z-[90]` y desenfoque suave, eliminando cualquier corte o conflicto de capas.
5. **Maquetación Plana y Espaciado Editorial en Bloque 03:**
   - Retirados los envoltorios beige pesados alrededor del semáforo de jugadores, del ADN lúdico y de los vídeos destacados, adoptando la estructura limpia, con líneas finas de separación (`border-t border-[var(--line)]`) y tipografía nítida de Claude.
6. **Visibilidad Condicional de Fundas:**
   - La pestaña `Fundas` en `#ficha-tabs-nav` y su mención en la cinta de mercado solo se renderizan cuando existen fundas reales asociadas (`Game.Sleeves?.Count > 0`).
7. **Verificación Automatizada:**
   - Actualizado `GameDetailEditorialBlocksContractTests.cs` con la prueba `GameDetail_ShouldFilterStockInBestPriceAndExcludePolaroidSticker`.
   - 2.619 pruebas unitarias pasando al 100% en verde sin regresiones.
