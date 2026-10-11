# Tareas de Implementación: INC-146 Menú Flotante Global de Moderación y Staff (Retirada de StaffBar y Limpieza Contextual)

- [ ] 1. Pruebas de Contrato y Componente (TDD Rojo)
  - [ ] 1.1 Crear pruebas de marcado en `tests/Ludeka.UnitTests/Web/StaffFloatingMenuContractTests.cs` (verificar ausencia de `StaffBar` en `MainLayout` y presencia de `StaffFloatingMenu` solo para staff).
  - [ ] 1.2 Pruebas de apertura/cierre, accesibilidad WCAG 2.2 AA (diálogo, escape, aria-expanded) y enlace canónico a `/admin`.
  - [ ] 1.3 Pruebas de integración de `IStaffActionService` con publicación reactiva de acciones.

- [ ] 2. Componente Global `StaffFloatingMenu.razor` y Limpieza del Layout
  - [ ] 2.1 Crear `src/Ludeka.Web/Components/Shared/StaffFloatingMenu.razor` con disparador FAB (`fixed bottom-20 right-4 sm:right-6 z-40`), tarjeta flotante, fallback de ruta y modal BGG.
  - [ ] 2.2 Actualizar `MainLayout.razor` retirando `<StaffBar />` e incorporando `<StaffFloatingMenu />`.
  - [ ] 2.3 Retirar `StaffBar.razor` y `StaffActionBar.razor`.

- [ ] 3. Consolidación de la Ficha de Juego (`GameDetail.razor`)
  - [ ] 3.1 Retirar `GameStaffToolsPanel.razor`.
  - [ ] 3.2 Retirar del DOM de `GameDetail.razor` el botón BGG de la foto hero, la caja mostaza de veredicto y los botones incrustados en vídeos ("Hub Multimedia (Moderar)", "Buscar en YouTube").
  - [ ] 3.3 Registrar en `IStaffActionService` las 9 acciones: `Editar Metadatos`, `Síntesis IA`, `Buscar en YouTube`, `Ingesta Flash Vídeo`, `Asociar BGG`, `Sincronizar BGG`, `Veredicto Fundador`, `Cartel Redes`, `Erratas`.
  - [ ] 3.4 Conectar modales (`YouTubeSearchModal`, `GameEditorModal`, etc.) y limpiar en `Dispose()`.

- [ ] 4. Consolidación de Sorteos (`Radar.razor` y `GiveawayDetail.razor`)
  - [ ] 4.1 En `Radar.razor`, registrar en `IStaffActionService`: `+ Alta exprés de sorteo`, `+ Nuevo sorteo manual` y `Bandeja de ingesta social`.
  - [ ] 4.2 Retirar los botones `✎` incrustados en las tarjetas del listado de sorteos.
  - [ ] 4.3 En `GiveawayDetail.razor`, registrar `Editar sorteo` y `Eliminar sorteo` en `IStaffActionService`, retirando el botón incrustado "Cambiar o subir carátula".

- [ ] 5. Consolidación de Eventos (`Events.razor` y `EventDetail.razor`)
  - [ ] 5.1 En `Events.razor`, registrar `+ Alta exprés de evento` y `Gestión de eventos` en `IStaffActionService`, retirando los botones incrustados de cabecera y estado vacío.
  - [ ] 5.2 En `EventDetail.razor`, registrar `Editar evento`, `Eliminar evento` y `Panel de gestión` en `IStaffActionService`, retirando los botones incrustados de cabecera y cartel.

- [ ] 6. Consolidación de Novedades (`News.razor` y `ReleaseDetail.razor`)
  - [ ] 6.1 En `News.razor`, registrar `+ Alta exprés`, `+ Publicar novedad`, `Sincronizar editoriales`, `Bandeja pendientes` e `Ingesta social` en `IStaffActionService`, retirando los botones de cabecera.
  - [ ] 6.2 En `ReleaseDetail.razor`, registrar `Editar novedad`, `Crear post Instagram` y `Eliminar novedad` en `IStaffActionService`, retirando los botones incrustados.

- [ ] 7. Consolidación de Directorios y Fichas de Entidad
  - [ ] 7.1 En `CreatorsDirectory`, `PublishersDirectory` y `StoresDirectory`, registrar `+ Registrar nuevo`, `Sincronizar padrón` y accesos de administración, retirando botones de cabecera y lápices de tarjetas.
  - [ ] 7.2 En `CreatorDetail`, `PublisherDetail` y `StoreDetail`, registrar `Editar ficha` en `IStaffActionService` y retirar el botón incrustado en el hero.

- [ ] 8. Verificación Integral y Cierre
  - [ ] 8.1 Ejecutar toda la suite de pruebas unitarias (`dotnet test`) y verificar cero regresiones.
  - [ ] 8.2 Verificar visualmente en móvil y escritorio que no existen solapamientos y la interfaz editorial queda limpia.
  - [ ] 8.3 Documentar informe de verificación (`verification-report.md`) y actualizar roadmap.
