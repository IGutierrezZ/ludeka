# Tareas de Implementación: INC-146 Menú Flotante Global de Moderación y Staff (Retirada de StaffBar y Limpieza Contextual)

- [ ] 1. Pruebas de Contrato y Componente (TDD Rojo)
  - [ ] 1.1 Crear o actualizar pruebas de marcado en `tests/Ludeka.UnitTests/Web/StaffFloatingMenuContractTests.cs` (verificar ausencia de `StaffBar` y presencia de `StaffFloatingMenu` solo para staff).
  - [ ] 1.2 Verificar el renderizado condicional de acciones personalizadas vs. acciones por defecto basadas en ruta en `StaffFloatingMenu`.
  - [ ] 1.3 Verificar la presencia permanente del enlace hacia `/admin`.

- [ ] 2. Componente Global `StaffFloatingMenu.razor` y Layout
  - [ ] 2.1 Crear `src/Ludeka.Web/Components/Shared/StaffFloatingMenu.razor` con disparador FAB (`fixed bottom-20 right-4 sm:right-6 z-40`), diálogo desplegable accesible, soporte de tecla `Escape` y accesos directos.
  - [ ] 2.2 Actualizar `src/Ludeka.Web/Components/Layout/MainLayout.razor` retirando `<StaffBar />` e incorporando `<StaffFloatingMenu />`.
  - [ ] 2.3 Retirar o sustituir `StaffBar.razor` como componente independiente obsoleto.

- [ ] 3. Consolidación de `GameDetail.razor` y Retirada de `GameStaffToolsPanel.razor`
  - [ ] 3.1 Registrar las 7 herramientas de staff de `GameDetail.razor` en `IStaffActionService` y limpiar en `Dispose()`.
  - [ ] 3.2 Eliminar `<GameStaffToolsPanel ... />` de `GameDetail.razor` y retirar el fichero `GameStaffToolsPanel.razor`.

- [ ] 4. Integración Contextual y Limpieza en Otras Vistas
  - [ ] 4.1 En `GiveawayDetail.razor`, registrar la acción contextual `Editar Sorteo` en `IStaffActionService`.
  - [ ] 4.2 En `Events.razor` y `News.razor`, retirar botones administrativos dispersos en las plantillas.
  - [ ] 4.3 Asegurar que `BggSearchModal` esté disponible o invocable desde el catálogo a través de la acción en el menú flotante.

- [ ] 5. Verificación Completa y Documentación
  - [ ] 5.1 Ejecutar toda la suite de pruebas automáticas (`dotnet test`) y verificar cero regresiones.
  - [ ] 5.2 Comprobar la experiencia responsive en móvil y escritorio.
  - [ ] 5.3 Elaborar el informe de verificación (`verification-report.md`) y actualizar el catálogo de incrementos.
