# Propuesta: INC-137 Forzar Sincronización BGG desde Ficha de Juego (Snapshot, Versiones, Imágenes y Datos)

## 1. Motivación y Problema
En el catálogo de Ludeka, títulos recientemente añadidos o novedades de 2025/2026 a menudo se indexan en BoardGameGeek antes de que exista su edición física en castellano o antes de que se cargue la portada localizada y su código de barras EAN-13. Cuando la editorial española publica la versión, los moderadores y la Mesa Fundadora no disponían de un mecanismo directo para forzar la actualización de esa ficha en caliente, viéndose obligados a editar manualmente campo por campo o a esperar a sincronizaciones masivas.

## 2. Solución Propuesta
Dotar a la ficha del juego (tanto en el panel flotante `GameStaffToolsPanel.razor` como en la barra de herramientas de moderación de `GameDetail.razor`) de un botón directo de acción:
**«Sincronizar con BGG» / «Forzar sincronización BGG»**.

Al accionarse:
1. El backend (`IGameEditorService.ForceSyncFromBggAsync`) invoca en tiempo real a BGG XMLAPI2 (`/thing?id={bggId}&versions=1&stats=1`).
2. Se descarga el árbol completo con versiones, imágenes y estadísticas, persistiendo o sobrescribiendo de inmediato el snapshot en `BggRawSnapshots`.
3. Se extrae la mejor versión española (título limpio, editorial identificada mediante `RegionalPublisherMatcher`, EAN e imágenes).
4. Se actualiza la entidad `Game` con los datos corregidos, se registra la traza de auditoría en `GameEditLog` y `_auditService`, y se invalida la caché de catálogo.
5. La interfaz de usuario refleja los cambios de manera reactiva e inmediata, con notificación de éxito detallando los campos sincronizados.

## 3. Impacto y Riesgos
- **Impacto:** Cero fricción operativa para moderadores y Mesa Fundadora; capacidad de reparar fichas en 1 clic.
- **Riesgos:** Errores transitorios de red o timeouts en la API de BGG. Mitigación: captura defensiva de excepciones, notificación de error amigable en la UI y ninguna mutación destructiva si BGG no devuelve un XML válido.
