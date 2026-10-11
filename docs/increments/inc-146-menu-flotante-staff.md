# INC-146: Menú Flotante Global de Moderación y Staff (Retirada de StaffBar y Limpieza Contextual)

**Estado:** ⏳ En propuesta (SDD Fase 2: Propose)  
**Rama:** `inc/menu-flotante-staff`  
**Worktree:** `F:\repos\ludeka-wt\menu-flotante-staff`  
**Pruebas base:** En verificación inicial  

---

## 1. Contexto y Objetivos

Unificar todas las acciones de moderación y administración en un único botón flotante interactivo (FAB) global accesible tanto en escritorio como en dispositivos móviles, eliminando la barra superior fija `StaffBar` y retirando los botones administrativos dispersos en las plantillas y tarjetas.

Objetivos clave:
1. **Retirar la barra superior `StaffBar.razor`:** Despejar la cabecera editorial de la aplicación eliminando la franja superior mostaza de gestión.
2. **Crear el componente global `StaffFloatingMenu.razor`:** Ubicado en `MainLayout.razor` en la esquina inferior derecha (`fixed bottom-20 right-4 sm:right-6 z-40`), visible únicamente para la Mesa Fundadora y moderadores (`IsStaff`), con soporte WCAG 2.2 AA.
3. **Consolidar `GameStaffToolsPanel.razor` en el componente global:** Generalizar la experiencia de la ficha de juego hacia toda la plataforma.
4. **Matriz contextual de acciones por pantalla:** Proveer accesos de alta exprés en listados (sorteos, eventos, novedades, BGG) y de edición en fichas de detalle (juegos, sorteos, eventos, directorios) a través de `IStaffActionService`.
5. **Limpieza de interfaz editorial:** Eliminar botones condicionales dispersos en el DOM que contaminan la experiencia del usuario general.

---

## 2. Artefactos SDD Asociados

- **Propuesta:** `openspec/changes/2026-10-11-inc-146-menu-flotante-staff/proposal.md`
- **Especificación:** Pendiente (`spec.md`)
- **Diseño Técnico:** Pendiente (`design.md`)
- **Plan de Tareas:** Pendiente (`tasks.md`)
