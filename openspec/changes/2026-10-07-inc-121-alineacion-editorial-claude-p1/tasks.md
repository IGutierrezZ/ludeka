# Tareas de Implementación: INC-121 — Alineación Editorial de Claude (Parte 1)

## Tareas

- [x] **Tarea 1: Shell Global (`MainLayout.razor`) y Colores**
  - Retirar campana de webhooks en la cabecera.
  - Añadir soporte de `tendencias` en `GetHeaderColors()` (`var(--brand)`).
  - Retirar frase redundante en el pie de página y ajustar espaciados.
  - Verificar pruebas de marcado existentes y actualizar si procede.

- [x] **Tarea 2: Formato de Cuenta Atrás y Numerales**
  - Actualizar `GiveawayService.CalculateRemainingTime` para retirar "Queda / Quedan".
  - Actualizar pruebas unitarias de `GiveawayServiceTests` reflejando el nuevo formato.
  - Actualizar `GameCard.razor` para liberar el numeral de `overflow-hidden` y aplicar formato `"00"`.

- [x] **Tarea 3: Barra de Gestión Contextual (`StaffBar.razor`) y Portada**
  - Implementar lógica contextual por ruta en `StaffBar.razor` mediante `IStaffActionService`.
  - Configurar 3 destacados diarios en `HomeDashboard.razor` (sorteo inminente, novedad, evento) y modal de selección.

- [x] **Tarea 4: Radar de Sorteos y Ofertas (`Radar.razor`)**
  - Retirar "Mis Alertas" y "Alta Exprés" de la cabecera de sorteos (alta exprés pasa a StaffBar).
  - Cambiar acción del lápiz (✎) para abrir el modal de edición en lugar de promocionar.
  - Implementar paginación completa accesible (10 por página) en Sorteos y en Ofertas.
  - Modernizar modal de sorteo a tokens de Revista Lúdica y unificar creación/edición.

- [x] **Tarea 5: Ficha de Sorteo (`GiveawayDetail.razor`)**
  - Cambiar enlace de retorno a `← Todos los sorteos`.
  - Mover botones Editar y Eliminar a la `StaffBar`.
  - Añadir enlace y modal accesible de "Reportar sorteo".
  - Actualizar modales de edición y eliminación a tokens editoriales de Revista Lúdica.

- [x] **Tarea 6: Verificación y Pruebas Unitarias**
  - Ejecutar suite completa `dotnet test` (2.599 tests unitarios en verde al 100%).
  - Comprobar que no existan errores de compilación ni regresiones de contratos.
