# Propuesta: INC-131 Calificación en Tarjetas de Expansión y Ordenación por Nota

## Motivación
En la ficha de juego, la pestaña de expansiones muestra las expansiones asociadas al juego base pero no refleja su valoración numérica, dificultando al jugador identificar rápidamente cuáles son las expansiones más aclamadas. Además, la ordenación previa era por año cronológico en lugar de destacar las más destacadas por calidad o consenso.

## Propuesta
1. Mostrar la nota (`★ X.X`) en cada tarjeta de expansión en `ExpansionEcosystemSection.razor`.
2. Ordenar las expansiones de mayor a menor según la nota efectiva en `ExpansionService.cs` y en la UI.
3. Blindar el contrato con pruebas automáticas.
