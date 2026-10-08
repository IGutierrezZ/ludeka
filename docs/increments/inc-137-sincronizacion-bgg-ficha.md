# INC-137: Forzar Sincronización BGG desde Ficha de Juego (Snapshot, Versiones, Imágenes y Datos)

## 1. Contexto y Motivación
En ocasiones, novedades y juegos de catálogo se indexan inicialmente en BoardGameGeek antes de que las editoriales españolas publiquen su edición física o suban sus versiones localizadas. Posteriormente, la comunidad o la editorial añaden la edición en castellano (con título traducido, editorial local, código de barras EAN-13 e imágenes de caja).

Aunque Ludeka cuenta con procesos periódicos en segundo plano, el equipo editorial y los moderadores necesitan una **acción inmediata y bajo demanda en la propia ficha del juego** para forzar la consulta en tiempo real a BGG (`XMLAPI2 /thing?id={bggId}&versions=1&stats=1`), descargar y refrescar el snapshot crudo con sus versiones, y sincronizar los datos de la ficha (título en español, editorial, EAN, carátula y miniatura) sin tener que esperar a jobs nocturnos ni introducir manualmente los datos.

---

## 2. Objetivos
1. **Acción bajo demanda en `IGameEditorService`:** Proveer `ForceSyncFromBggAsync(Guid gameId)` con validación de permisos de Staff (`CanEditGames` o Mesa Fundadora).
2. **Descarga y refresco de Snapshot BGG:** Consultar la API oficial de BGG con `includeVersions: true` y estadísticas, persistiendo o sobrescribiendo de forma idempotente el registro en `BggRawSnapshots`.
3. **Saneamiento y enriquecimiento en caliente:** Extraer mediante `BggRawSnapshotParser` y `RegionalPublisherMatcher` los datos de la versión española y actualizar la entidad `Game` si difieren o mejoran los existentes.
4. **Auditoría e invalidación de caché:** Registrar la traza en `GameEditLog` y `_auditService`, e invalidar la caché de catálogo.
5. **Control interactivo en la Ficha del Juego:** Incorporar el botón en `GameStaffToolsPanel.razor` y en la botonera editorial de `GameDetail.razor`, con indicador de progreso (`Sincronizando...`) y actualización reactiva de la ficha en pantalla.

---

## 3. Criterios de Aceptación
- [ ] Moderadores y Mesa Fundadora pueden pulsar «Sincronizar con BGG» en la ficha de cualquier juego con `BggId` válido.
- [ ] Si el juego no tiene `BggId` (o es menor o igual a 0), la acción está deshabilitada o informa de que debe asociarse primero.
- [ ] La operación consulta BGG, actualiza el snapshot crudo en base de datos y extrae versiones en castellano.
- [ ] Si la versión de BGG aporta título en castellano, editorial española, código EAN o imágenes, la ficha se actualiza y la UI refleja los cambios sin recargar el navegador.
- [ ] Se registra auditoría editorial detallando los campos modificados.
- [ ] La suite de pruebas unitarias verifica todos los escenarios (permisos, BGG ID inválido, actualización de campos, idempotencia).
