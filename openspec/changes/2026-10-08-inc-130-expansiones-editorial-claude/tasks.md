# Tareas: INC-130 Pestaña de Expansiones Editorial Limpia

- [x] **Tarea 1: Rediseño Editorial de `ExpansionEcosystemSection.razor`**
  - Retirar lógica de mezclador, evaluación de combinaciones, recetas y carrusel con scroll JS.
  - Implementar cuadrícula de 2 columnas (`grid grid-cols-1 md:grid-cols-2 gap-4`).
  - Diseñar tarjeta editorial con carátula cuadrada (64x64 lazy/async), etiqueta de necesidad («Opcional», «Imprescindible»), título, metadatos y botón «+ A mi ludoteca».
  - Gestionar persistencia interactiva de colección con `IUserLibraryService`.

- [x] **Tarea 2: Simplificación de Cabecera en `GameDetail.razor`**
  - Eliminar el bloque grande `04` y la etiqueta `& Dónde Comprar` del título visible.
  - Mostrar encabezado limpio `Expansiones` con contador simple.
  - Pasar `BaseGameMaxPlayers` a `ExpansionEcosystemSection`.

- [x] **Tarea 3: Actualización de Pruebas de Contrato y Regresión**
  - Actualizar `GameDetailEditorialBlocksContractTests.cs` para validar la cabecera limpia.
  - Actualizar `ExpansionAndMediaCarouselUiContractTests.cs` para verificar la cuadrícula y la ausencia del mezclador.
  - Actualizar `WebMarkupContractTests.cs` validando iconografía Lucide.
  - Ejecutar suite completa de pruebas unitarias.
