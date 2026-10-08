# Tareas de Implementación: INC-129

## Tarea 1: Modelo de Dominio y Persistencia (`WeeklyReleaseStatus`)
- [x] 1.1 Crear enum `WeeklyReleaseStatus` (`Published`, `PendingModeration`, `Rejected`) en `src/Ludeka.Core/Enums/WeeklyReleaseStatus.cs`.
- [x] 1.2 Extender la entidad `WeeklyRelease.cs` con `Status`, `AiSuggestedBggId`, `AiSuggestedTitle` y `AiMatchReasoning`, añadiendo métodos `SetPendingModeration`, `Approve` y `Reject`.
- [x] 1.3 Actualizar `SqliteSchemaMigrator.cs` para migrar de forma defensiva las nuevas columnas en SQLite.
- [x] 1.4 Crear pruebas unitarias en `Ludeka.UnitTests` verificando el comportamiento de `WeeklyRelease` y sus nuevos estados.

## Tarea 2: Filtros de Calendario y Enlaces en `DevirReleasesExtractor`
- [x] 2.1 Descartar secciones que contengan «desarrollo» en `DevirReleasesExtractor.cs`.
- [x] 2.2 Exigir que los lanzamientos extraídos tengan un mes cerrado de calendario.
- [x] 2.3 Capturar la URL de ficha de producto (`<a href="https://devir.es/...">`) en lugar del enlace genérico de próximos lanzamientos.
- [x] 2.4 Actualizar y ampliar las pruebas unitarias en `DevirReleasesExtractorTests.cs`.

## Tarea 3: Asistente IA de Enlace (`IReleaseAiMatcherService`)
- [x] 3.1 Definir interfaces y DTOs en `src/Ludeka.Application/Contracts/IReleaseAiMatcherService.cs` y `src/Ludeka.Application/DTOs/`.
- [x] 3.2 Implementar `GeminiReleaseMatcherService.cs` en `src/Ludeka.Infrastructure/Services/` con soporte de simulación determinista para títulos clave en español (*Crucero Galáctico*, *Los 12 trabajos de Hércules*, etc.).
- [x] 3.3 Registrar el servicio en la inyección de dependencias (`LudekaServiceCollectionExtensions.cs`).
- [x] 3.4 Crear pruebas unitarias en `GeminiReleaseMatcherServiceTests.cs`.

## Tarea 4: Orquestación No Bloqueante en `EditorialReleasesSyncService`
- [x] 4.1 Modificar `EditorialReleasesSyncService.cs` para que los elementos que no crucen de inmediato no se descarten, sino que se guarden en `PendingModeration`.
- [x] 4.2 Integrar `IReleaseAiMatcherService` para adjuntar la propuesta de BGG y razonamiento de IA.
- [x] 4.3 Eliminar el bucle masivo bloqueante de llamadas a BGG en la solicitud sincrónica.
- [x] 4.4 Actualizar `EditorialReleasesSyncServiceTests.cs` validando el nuevo flujo sin descartes de Maldito Games.

## Tarea 5: Bandeja de Moderación y Visualización en `/novedades`
- [ ] 5.1 Extender `WeeklyReleaseDto.cs` y `IWeeklyReleaseService.cs` con métodos de aprobación y rechazo.
- [ ] 5.2 Añadir pestaña y tarjetas de moderación comparativa en `News.razor` para usuarios moderadores (`CanApproveMedia`).
- [ ] 5.3 Implementar botones para Aprobar con enlace BGG, Aprobar sin enlace, y Descartar.
- [ ] 5.4 Pruebas de integración y componentes para la vista de moderación.

## Tarea 6: Jerarquía de Imágenes (3D como Principal) y Galería
- [ ] 6.1 Asegurar que `face3d` tenga prioridad como `CoverImageUrl` principal del juego y de la novedad.
- [ ] 6.2 Exponer en la ficha de detalle las imágenes complementarias (portada 2D, contraportada, mesa).
- [ ] 6.3 Ejecutar suite completa `dotnet test` y verificar 100% verde sin regresiones.
