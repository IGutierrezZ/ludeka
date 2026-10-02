# Tareas de Implementación — INC-102: Auto-vinculación Inmediata de Expansiones y Síntesis Asistida de Aporte con IA

## Fase 1: Dominio y Contratos
- [ ] 1.1 Añadir método `SetExpansionAporte` en `Game.cs` (`Ludeka.Core`).
- [ ] 1.2 Definir `ExpansionAporteAiDto` en `ExpansionDtos.cs` (`Ludeka.Application`).
- [ ] 1.3 Extender `IAiGameSummaryService` con `GenerateExpansionAporteAsync` y `EnsureExpansionAporteAsync`.

## Fase 2: Infraestructura y Síntesis Asistida con IA
- [ ] 2.1 Implementar `HeuristicExpansionAporteGenerator` con análisis determinista de mecánicas, necesidad, impacto y deltas.
- [ ] 2.2 Implementar `GenerateExpansionAporteAsync` y `EnsureExpansionAporteAsync` en `GeminiGameSummaryService` con Structured Output de Gemini Flash y fallback heurístico.

## Fase 3: Auto-Vinculación Bidireccional en Búsqueda Asistida
- [ ] 3.1 Inyectar `IBggRawSnapshotRepository` en `BggSearchAssistedService`.
- [ ] 3.2 Implementar auto-vinculación de expansiones entrantes (`inbound`) a su juego base en catálogo.
- [ ] 3.3 Implementar auto-vinculación de expansiones huérfanas salientes (`outbound`) al ingresar un juego base.
- [ ] 3.4 Disparar la generación automática del aporte al añadir una nueva expansión a la colección.

## Fase 4: Interfaz de Usuario y Estado Vacío
- [ ] 4.1 Diseñar el estado vacío (*empty state*) editorial en `ExpansionAporteCard.razor`.
- [ ] 4.2 Añadir botón interactivo «✨ Generar Aporte con IA» con feedback de carga en `ExpansionAporteCard.razor`.
- [ ] 4.3 Cablear el evento en `GameDetail.razor` para refrescar la ficha tras la generación sin recarga de página.

## Fase 5: Pruebas Unitarias y Verificación
- [ ] 5.1 Pruebas unitarias para `BggSearchAssistedService` validando vinculación bidireccional y resiliencia.
- [ ] 5.2 Pruebas unitarias para `HeuristicExpansionAporteGenerator` y `GeminiGameSummaryService`.
- [ ] 5.3 Pruebas de componente para `ExpansionAporteCard` (estado con datos, estado vacío y botón IA).
- [ ] 5.4 Ejecución de la suite completa de pruebas unitarias (`dotnet test tests/Ludeka.UnitTests`) asegurando 100% en verde.
