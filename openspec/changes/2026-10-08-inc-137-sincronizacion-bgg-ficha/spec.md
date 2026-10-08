# Especificación: INC-137 Forzar Sincronización BGG desde Ficha de Juego

## 1. Requisitos Funcionales

### RF-01: Permisos de Seguridad
- La operación de forzar sincronización desde BGG exige permisos editoriales: `CurrentUserService.IsFoundingTeam` o rol `Moderator` con permiso `CanEditGames`.
- Usuarios sin los permisos requeridos reciben `UnauthorizedAccessException`.

### RF-02: Validación de Identificador BGG
- Si el juego objetivo no tiene `BggId` asignado o su valor es menor o igual a 0, la operación rechaza la ejecución con `InvalidOperationException` informando de que debe asociarse primero un BGG ID válido.
- En la interfaz web, el botón muestra el `BggId` asociado (ej. «Sincronizar BGG (#453526)») o se deshabilita si no cuenta con él.

### RF-03: Consulta Externa y Actualización del Snapshot Crudo
- Se solicita el XML de BGG mediante `IBggClient.FetchRawThingXmlAsync(bggId, includeVersions: true)`.
- Si BGG responde correctamente, el XML se convierte a JSON estructurado y se almacena/sobrescribe en la tabla `BggRawSnapshots` con `apiVersion = 2` y `fetchedAt = UtfNow` mediante `IBggRawSnapshotRepository.UpsertAsync`.

### RF-04: Extracción de Datos y Sincronización de Catálogo
- Utilizando `BggRawSnapshotParser`, se extrae:
  1. Versión en español (`ExtractSpanishVersionInfoFromJson`):
     - `Title`: si no es genérico ni contiene acrónimos extranjeros, se actualiza `SpanishTitle` si difiere del actual.
     - `Publisher`: si no es nulo, se resuelve mediante `RegionalPublisherMatcher` o el nombre oficial y se actualiza `SpanishPublisher` si difiere o si el actual era genérico/nulo/erróneo.
     - `Ean`: si aporta un EAN válido (longitud 13 dígitos) y difiere, se actualiza.
     - `CoverImageUrl` y `ThumbnailUrl`: si la versión española aporta imagen o si el juego no tenía carátula, se actualizan las URLs de medios.
  2. Metadatos de raíz (`ExtractRootImagesFromJson`, estadísticas):
     - Si el juego carece de carátula o miniatura, se rellenan con las imágenes raíz de BGG.
     - Si el snapshot contiene estadísticas y el juego carece de peso o metadatos básicos, se actualizan.

### RF-05: Auditoría, Caché y Respuesta
- Si se modifican campos en `Game`, se invoca `_gameRepository.UpdateAsync(game, ct)`.
- Se registra un `GameEditLog` con resumen detallado (ej. `Sincronización forzada desde BGG #453526: Título 'Got Five!' -> 'Código 5', Editorial -> 'Lúdilo'`).
- Se registra en `_auditService.RecordChangeAsync`.
- Se invalida la caché de catálogo (`_catalogService.Invalidate(game.Slug)`).
- Se retorna un `GameBggSyncResultDto` con el listado de campos actualizados y el mensaje descriptivo.

### RF-06: Experiencia de Usuario en la Ficha
- Botón accesible en `GameStaffToolsPanel.razor` y en la sección editorial de `GameDetail.razor`.
- Estado interactivo con deshabilitación y spinner (`Sincronizando con BGG...`).
- Actualización reactiva de la ficha (título, editorial, imágenes) sin recarga forzada del navegador.
- Notificación visual (toast o banner de estado) confirmando el resultado de la sincronización.
