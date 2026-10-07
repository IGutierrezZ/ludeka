# Propuesta: INC-121 — Alineación Editorial de Claude (Parte 1: Portada, Tendencias, Catálogo y Sorteos)

## Metadatos
- **Fecha:** 2026-10-07
- **Incremento:** INC-121
- **Rama:** `inc/ajustes-editorial-claude-p1`
- **Slug:** `ajustes-editorial-claude-p1`
- **Estado:** En propuesta (ODD / SDD)

## Contexto y Motivación
Tras la auditoría exhaustiva del prototipo interactivo final de Claude (`Ludeka Final.dc.html`) frente a la base de código de Ludeka en `src/Ludeka.Web`, se han identificado discrepancias funcionales, visuales y de arquitectura de navegación en cinco pantallas clave:
1. **Cabecera global y pie:** La campana de webhooks en la cabecera pública carece de funcionalidad útil y ensucia la barra; en la vista de tendencias el encabezado no cambia a terracota (`var(--brand)`) por omisión de ruta; y el pie de página contiene lemas redundantes con márgenes desajustados.
2. **Barra de Gestión (`StaffBar`):** Actualmente es estática y muestra seis enlaces fijos idénticos en toda la aplicación, en vez de adaptarse contextualmente a la pantalla en curso según el patrón `ADMIN` del prototipo.
3. **Portada:** Se requiere ajustar los elementos destacados a tres tarjetas diarias predeterminadas (sorteo inminente, última novedad y próximo evento) gestionables desde un modal contextual, y retirar el texto "Queda/Quedan" en las cuentas atrás.
4. **Catálogo de Juegos:** Los números ordinales de las carátulas se cortan por `overflow-hidden` y no muestran el formato `01, 02, 03`. La barra de gestión debe incluir el acceso a añadir desde BGG.
5. **Radar de Sorteos y Ofertas:** El botón "Mis alertas" es redundante; "Alta exprés" debe residir en la barra de gestión; ambas pestañas carecen de paginación; y el icono de lápiz ejecuta promoción en lugar de abrir la edición.
6. **Ficha de Sorteo:** Mover edición/eliminación a la barra de gestión; renombrar retorno a `← Todos los sorteos`; añadir el flujo de "Reportar sorteo"; y actualizar el modal de edición a los tokens CSS editoriales.

## Objetivos
1. **Limpieza de cabecera y pie:** Retirar campana de webhooks, añadir color terracota a `/tendencias` y compactar el pie de página con `Ludeka © 2026`.
2. **Barra de gestión contextual:** Convertir `StaffBar` en una barra dinámica dependiente de la URL que exponga las herramientas pertinentes por pantalla.
3. **Destacados de portada:** 3 destacados diarios configurables (sorteo, novedad, evento) con modal de edición que permita seleccionar tendencias.
4. **Legibilidad y formato en catálogo:** Numerales `01, 02...` flotantes sin recorte en `GameCard.razor` y acción BGG en barra de staff.
5. **Radar de sorteos y ofertas optimizado:** Paginación en ambas pestañas, eliminación de "Mis Alertas", botón de lápiz vinculado a edición y cuenta atrás concisa sin "Queda".
6. **Ficha de sorteo pulida:** Navegación `← Todos los sorteos`, botón de reporte comunitario y modal de edición con tokens editoriales.

## No Objetivos (Out of Scope)
- Implementación del centro de notificaciones admin completo con conteo desagregado (previsto para un incremento posterior dedicado).
- Modificaciones en pantallas de Novedades, Eventos, Ficha de juego, Directorios o Cuenta (se abordarán en la Parte 2).
