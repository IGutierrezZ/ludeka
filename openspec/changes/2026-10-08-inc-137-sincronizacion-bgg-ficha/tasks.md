# Tareas de Implementación: INC-137 Forzar Sincronización BGG desde Ficha de Juego

## Tareas

- [ ] **Tarea 1 (Contratos y DTOs):**
  - Definir `GameBggSyncResultDto` en `src/Ludeka.Application/DTOs/GameEditorDtos.cs`.
  - Añadir `ForceSyncFromBggAsync` a `src/Ludeka.Application/Contracts/IGameEditorService.cs`.

- [ ] **Tarea 2 (TDD Rojo - Pruebas Unitarias de Aplicación):**
  - Crear pruebas unitarias exhaustivas en `tests/Ludeka.UnitTests/Application/GameEditorServiceTests.cs` cubriendo:
    - Validación de permisos Staff (`UnauthorizedAccessException`).
    - Validación de BGG ID ausente o no positivo (`InvalidOperationException`).
    - Juego no existente (`KeyNotFoundException`).
    - Flujo exitoso: llamada a BGG con versiones, upsert en snapshot repo, extracción de versión en español, actualización de `Game` (título, editorial, EAN, carátula), log de auditoría e invalidación de caché.
    - Flujo sin cambios: BGG responde con los mismos datos actuales; actualiza snapshot y reporta 0 campos modificados de forma limpia.

- [ ] **Tarea 3 (Implementación en Capa de Aplicación - Verde):**
  - Inyectar `IBggClient` e `IBggRawSnapshotRepository` en `src/Ludeka.Application/Features/Catalog/GameEditorService.cs`.
  - Implementar `ForceSyncFromBggAsync` siguiendo el diseño arquitectónico y respetando TDD verde.

- [ ] **Tarea 4 (Capa Web / Interfaz de Usuario):**
  - Añadir el botón y estado `IsSyncingBgg` en `src/Ludeka.Web/Components/Shared/GameStaffToolsPanel.razor`.
  - Añadir el botón en la barra staff de `src/Ludeka.Web/Components/Pages/GameDetail.razor` y cablear el evento `HandleForceSyncBgg` con feedback y actualización reactiva de la ficha en pantalla.

- [ ] **Tarea 5 (Verificación de Suite Completa y Documentación Viva):**
  - Ejecutar la suite completa de pruebas unitarias e integración en el worktree.
  - Actualizar la especificación viva del sistema en `docs/specs/sistema/` y totales en `docs/specs/sistema/README.md`.
  - Generar el informe de verificación `verification-report.md`.
