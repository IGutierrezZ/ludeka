# Propuesta de Cambio: INC-130 Pestaña de Expansiones Editorial Limpia

## Motivación
La pestaña de expansiones en `GameDetail.razor` arrastraba componentes sobrediseñados del antiguo incremento INC-08 e INC-99 (mezclador de mesa, recetas de combinaciones, carrusel con botones de scroll y la cabecera pesada «04 Expansiones & Dónde Comprar»). Estos elementos añadían complejidad innecesaria a la navegación y desentonaban con la línea editorial moderna y minimalista de Claude (`Ludeka Final.dc.html`).

## Propuesta
1. Alinear la pestaña `Expansiones` de `GameDetail.razor` eliminando el título de bloque heredado `04` y mostrando únicamente `Expansiones` con su contador limpio.
2. Refactorizar `ExpansionEcosystemSection.razor` para reemplazar las 3 pestañas y el mezclador por una cuadrícula editorial de tarjetas limpias (`grid grid-cols-1 md:grid-cols-2 gap-4`).
3. Cada tarjeta incorpora: carátula cuadrada con dimensiones anti-CLS, etiqueta de necesidad («Opcional», «Imprescindible», «Muy recomendada»), título con enlace a la ficha, metadatos de año y jugadores («hasta X jugadores»), y botón interactivo «+ A mi ludoteca» / «En mi ludoteca».
4. Actualizar las pruebas de contrato de interfaz para garantizar cero regresiones y blindar el nuevo diseño.
