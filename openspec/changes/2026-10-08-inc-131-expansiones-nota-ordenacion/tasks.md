# Tareas: INC-131 Calificación en Tarjetas de Expansión y Ordenación por Nota

- [x] **Tarea 1: Ordenación por Calificación en `ExpansionService.cs`**
  - Ordenar las expansiones en `GetExpansionsForBaseGameAsync` de mayor a menor por nota efectiva (`LudistRating` / `BggRating`).
  - Añadir pruebas unitarias verificando la ordenación en `ExpansionServiceTests`.

- [x] **Tarea 2: Renderizado de Calificación y Ordenación en `ExpansionEcosystemSection.razor`**
  - Añadir badge de calificación con icono de estrella y valor numérico (`0.0`).
  - Asegurar ordenación reactiva en el componente con `SortedExpansions`.

- [x] **Tarea 3: Verificación y Pruebas de Contrato**
  - Actualizar pruebas de contrato en `ExpansionAndMediaCarouselUiContractTests.cs` y `WebMarkupContractTests.cs`.
  - Ejecutar suite completa de pruebas unitarias.
