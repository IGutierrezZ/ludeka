# Especificación: INC-131 Calificación en Tarjetas de Expansión y Ordenación por Nota

## Requisitos Funcionales

1. **Nota en Tarjeta de Expansión:**
   - Cada tarjeta de expansión en `ExpansionEcosystemSection.razor` debe presentar la nota numérica con 1 decimal (`0.0`).
   - Si la expansión cuenta con `LudistRating > 0`, se utiliza esta puntuación; si no, se emplea `BggRating`.
   - Se muestra un icono de estrella o glifo distintivo junto a la nota.

2. **Ordenación Descendente por Nota:**
   - La lista de expansiones debe entregarse y renderizarse ordenada de mayor a menor según la nota efectiva.
   - En caso de empate en la nota efectiva, se desempata por `BggRating` y por `SpanishTitle`.

3. **Cero Regresiones:**
   - Mantener las dimensiones fijas de imagen (64x64 lazy/async) para preservar CLS cero.
   - Mantener la interactividad de añadir/quitar de la ludoteca.
