# Incremento 121: Alineación Editorial de Claude (Parte 1: Portada, Tendencias, Catálogo y Sorteos)

> **Estado:** ⏳ Verificado (Listo para PR y CI/CD)  
> **Rama:** `inc/ajustes-editorial-claude-p1`  
> **Worktree:** `F:\repos\ludeka-wt\ajustes-editorial-claude-p1`  
> **Fecha:** 2026-10-07  
> **Autor:** Antigravity  

---

## 1. Resumen Ejecutivo
Refinamiento visual y de interacción alineando la implementación web con el diseño interactivo final de Claude (`Ludeka Final.dc.html`). Resuelve discrepancias detectadas en Portada, Tendencias, Catálogo de Juegos, Radar de Sorteos/Ofertas y Ficha de Sorteo:
- Retirada de la campana en cabecera pública superior.
- Cabecera terracota (`var(--brand)`) en `/tendencias` y compactación del pie de página.
- Barra de gestión contextual (`StaffBar`) por pantalla mediante mediador `IStaffActionService`.
- 3 destacados en portada diarios con modal de configuración para administradores.
- Numerales libres de recorte con formato `01, 02` en catálogo.
- Cuentas atrás limpias sin "Queda/Quedan".
- Paginación de 10 elementos en Sorteos y Ofertas, enlace de edición directo en tarjetas de sorteos.
- Ficha de sorteo con `← Todos los sorteos`, reporte comunitario y modal con tokens editoriales.

---

## 2. Artefactos del Incremento
- [Propuesta SDD](file:///F:/repos/ludeka-wt/ajustes-editorial-claude-p1/openspec/changes/2026-10-07-inc-121-alineacion-editorial-claude-p1/proposal.md)
- [Especificación SDD](file:///F:/repos/ludeka-wt/ajustes-editorial-claude-p1/openspec/changes/2026-10-07-inc-121-alineacion-editorial-claude-p1/spec.md)
- [Diseño SDD](file:///F:/repos/ludeka-wt/ajustes-editorial-claude-p1/openspec/changes/2026-10-07-inc-121-alineacion-editorial-claude-p1/design.md)
- [Tareas SDD](file:///F:/repos/ludeka-wt/ajustes-editorial-claude-p1/openspec/changes/2026-10-07-inc-121-alineacion-editorial-claude-p1/tasks.md)
- [Informe de Verificación](file:///F:/repos/ludeka-wt/ajustes-editorial-claude-p1/openspec/changes/2026-10-07-inc-121-alineacion-editorial-claude-p1/verification-report.md)

---

## 3. Pruebas y Certificación
- Suite unitaria: **2.599 tests superados (100% verde)**.
- Compilación sin errores.
