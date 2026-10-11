# Informe de Verificación: INC-146 Menú Flotante Global de Moderación y Staff (Retirada de StaffBar y Limpieza Contextual)

**Fecha:** 2026-10-11  
**Incremento:** INC-146 (`menu-flotante-staff`)  
**Autor:** Antigravity (Principal Systems Architect)  
**Estado:** ✅ APROBADO

---

## 1. Resumen Ejecutivo

El incremento INC-146 rediseña y unifica la experiencia de moderación y administración en toda la plataforma Ludeka, tanto para dispositivos móviles como para escritorio. Anteriormente, las opciones de administración se encontraban fragmentadas entre una barra superior fija (`StaffBar.razor`), paneles locales flotantes redundantes (`GameStaffToolsPanel.razor`), y botones dispersos incrustados en tarjetas, cabeceras y vistas editoriales públicas (`Radar`, `GiveawayDetail`, `Events`, `EventDetail`, `News`, `ReleaseDetail`, directorios de creadores, editoriales y tiendas).

La nueva arquitectura unificada se basa en:
1. **Componente Global `StaffFloatingMenu.razor`:** Un Botón de Acción Flotante (FAB) global en la esquina inferior derecha (`fixed bottom-20 right-4 sm:right-6 z-40`) posicionado cuidadosamente por encima de la barra táctil inferior móvil (`MobileBottomNav`) y del botón de scroll/ayuda.
2. **Servicio Mediador `IStaffActionService`:** Mecanismo reactivo para que cada vista o página registre en su ciclo de vida (`OnInitializedAsync`) y limpie (`Dispose`) sus acciones contextuales específicas (altas exprés, búsquedas en YouTube, ingesta flash, edición de fichas, sincronización BGG, veredicto de fundador, carátulas y erratas), contando con un fallback inteligente basado en la ruta activa y enlace canónico persistente al panel `/admin`.
3. **Limpieza Editorial Completa:** Se eliminaron todos los botones administrativos incrustados y redundantes en el DOM público, manteniendo una experiencia editorial limpia sin contaminación visual ni coste de renderizado para usuarios regulares (cero nodos adicionales en el DOM).
4. **Accesibilidad WCAG 2.2 AA:** Implementación con diálogo semántico (`role="dialog"`, `aria-modal="true"`), cierre automático al pulsar la tecla `Escape`, atajos accesibles y gestión adecuada del foco.

---

## 2. Cobertura de Pruebas Unitarias y de Integración

Se ejecutó la suite completa de pruebas unitarias e integración de la solución:

```
Comando: dotnet test tests/Ludeka.UnitTests/Ludeka.UnitTests.csproj
Resultado: 2.833 pruebas superadas (0 errores, 0 omitidas)
Duración: 21 s - Ludeka.UnitTests.dll (net10.0)
```

### Pruebas Específicas del Incremento:
1. `StaffFloatingMenuContractTests` (5 pruebas en `tests/Ludeka.UnitTests/Web/StaffFloatingMenuContractTests.cs`):
   - `MainLayout_ShouldNotContainStaffBar_AndShouldRenderStaffFloatingMenuOnlyForStaff`: Verificación de que `MainLayout.razor` ya no incluye `StaffBar` ni `StaffActionBar`, y que solo renderiza `StaffFloatingMenu` bajo perfil de staff.
   - `StaffFloatingMenu_RazorFile_ShouldMeetAccessibilityAndContractRequirements`: Verificación de atributos WCAG 2.2 AA (`role="dialog"`, `aria-modal="true"`, `aria-expanded`), atajo `Escape`, y enlace canónico a `/admin`.
   - `StaffActionService_DynamicActions_ShouldNotifyStateChanged`: Registro reactivo y notificación en `IStaffActionService`.
   - `GameDetail_ShouldNotContainRedundantStaffPanels_AndShouldRegisterActions`: Ausencia de `GameStaffToolsPanel`, botones BGG y cajas mostaza incrustadas, y registro correcto de `Editar Ficha` y `Buscar en YouTube`.
2. `WebMarkupContractTests` (7 pruebas adaptadas a la nueva arquitectura):
   - Eliminación de pruebas obsoletas sobre `StaffBar` y validación de ausencia de marcado administrativo en vistas públicas (`Radar.razor`, `News.razor`, etc.).
3. `GameDetailEditorialBlocksContractTests` y `GameEditorWebIntegrationTests`:
   - Verificación de la integración de acciones dinámicas en `GameDetail.razor` (`Editar Ficha`, `HandleRequestAiSummary`, `_isAssociateBggModalOpen`) y del marcado accesible del Hub Multimedia (`<h2 class="sr-only">Hub Multimedia</h2>`).
4. `ReleaseDetailPageContractTests`:
   - Verificación de que las acciones de edición y publicación en Instagram son invocadas contextualmente a través del menú de staff sin botones intrusivos en la cabecera.

---

## 3. Conclusión

El incremento INC-146 ha cumplido el 100% de los requerimientos funcionales, de diseño y de accesibilidad estipulados en la especificación, manteniendo intacta la integridad editorial de Ludeka con las 2.833 pruebas de la plataforma en verde.
