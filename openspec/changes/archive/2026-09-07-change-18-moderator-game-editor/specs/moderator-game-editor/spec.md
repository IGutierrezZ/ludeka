# Especificación: Editor Editorial de Fichas de Catálogo y Carga de Imágenes para Moderadores

- **Módulo:** Herramientas Editoriales Internas, Control de Calidad Multimedia y Gestión de Catálogo
- **Incremento:** 18 (`change-18-moderator-game-editor`)
- **Estado:** En Especificación (`sdd-spec`)

---

## 1. Requerimientos Funcionales

### RF-18.1: Control de Acceso y Permisos Editoriales
- La acción de editar fichas de catálogo debe estar restringida exclusivamente a usuarios autenticados que pertenezcan a la Mesa Fundadora o posean el rol de moderador (`ICurrentUserService.IsFoundingTeam || ICurrentUserService.IsInRole("Moderator")`).
- En la ficha de juego (`GameDetail.razor`), la barra superior de acciones debe mostrar el botón `[ ✏️ Editar Ficha ]` únicamente si el usuario actual cumple la condición de rol.
- En la bandeja de reportes de INC-17 (`GameReportsModeration.razor`), cada tarjeta de incidencia pendiente o en revisión debe presentar el botón de acción cruzada `[ ✏️ Corregir Ficha y Resolver ]`.
- Si un usuario no autorizado intenta invocar el servicio de edición `IGameEditorService`, la operación debe denegarse de inmediato arrojando `UnauthorizedAccessException`.

### RF-18.2: Formulario Editorial Completo (`GameEditorModal.razor`)
El diálogo modal debe ser interactivo, responsivo y organizado en 4 pestañas o bloques temáticos limpios:

1. **Pestaña 1: Metadatos Generales:**
   - `SpanishTitle` (Título comercial en español, requerido, no vacío).
   - `OriginalTitle` (Título original en idioma nativo, requerido, no vacío).
   - `Designer` (Autor/es del juego).
   - `Publisher` (Editorial licenciataria en español).
   - `YearPublished` (Año de publicación original, número entero positivo, ej. 1900–2100).

2. **Pestaña 2: Parámetros Técnicos y de Mesa:**
   - **Rango de Jugadores (mínimo y máximo):**
     - Mínimo de 1 a 10 jugadores; Máximo de 1 a 12 jugadores. Invariante: `MinPlayers <= MaxPlayers`.
     - Si el moderador amplía o reduce el rango (ej. de 3–4 a 2–4), la colección `Scalability` de la entidad se ajusta automáticamente para incluir las entradas desde el nuevo mínimo hasta el nuevo máximo, preservando recomendaciones existentes y asignando `Recommended` por defecto a los nuevos puestos incorporados.
   - **Modo Solitario Oficial:** Interruptor booleano (`IsOfficialSolo`).
   - **Duración de Partida:**
     - Duración mínima (minutos, $\ge 1$).
     - Duración máxima (minutos, $\ge \text{mínimo}$).
     - Minutos estimados por jugador ($\ge 1$).
   - **Edad Recomendada:**
     - Edad de caja (`BoxAge`, $\ge 0$).
     - Edad consensuada por la comunidad (`CommunityAge`, $\ge 0$).
   - **ADN Lúdico:**
     - Dependencia del idioma: Selector con valores de `LanguageDependence` (`None`, `Low`, `Medium`, `High`).
     - Estilo de juego: Selector con valores de `GameStyle` (`Euro`, `Ameritrash`, `Party`, `Filler`, `Wargame`, `Abstract`, etc.).
     - Tipo de confrontación: Selector con valores de `ConfrontationType` (`Competitive`, `Cooperative`, `SemiCooperative`, `TeamVsTeam`, `SoloOnly`).
     - Espacio en mesa: Selector con valores de `TableFootprint` (`Small`, `Medium`, `Large`, `Massive`).

3. **Pestaña 3: Sinopsis y Notas:**
   - `Description`: Texto descriptivo en español, depurado y limpio, sin etiquetas HTML inseguras.

4. **Pestaña 4: Carátula e Imágenes:**
   - Selector dual:
     - **Opción A (Subida de Archivo Directo):** Componente `InputFile` para seleccionar un archivo local (`.jpg`, `.jpeg`, `.png`, `.webp`) de hasta 5 MB. Al seleccionar el archivo, se muestra indicador de nombre, peso y botón de procesamiento con vista previa previa a guardar.
     - **Opción B (URL Externa):** Campo para ingresar una URL HTTP/HTTPS válida, con botón `[ Previsualizar ]` para comprobar la carga de la imagen antes de persistir.
   - Previsualización en vivo en recuadro con proporción 1:1, indicando si proviene de archivo local o enlace externo.

### RF-18.3: Almacenamiento Seguro de Imágenes (`IImageStorageService`)
- El servicio de infraestructura `PhysicalFileImageStorageService` debe:
  - Validar extensiones de archivo permitidas (`.jpg`, `.jpeg`, `.png`, `.webp`) y firmas de contenido (Content-Type).
  - Rechazar cualquier archivo que exceda 5 MB ($5 \times 1024 \times 1024$ bytes).
  - Sanitizar el nombre del fichero a partir del slug del juego: `{slug}-cover-{timestamp}.{ext}`.
  - Almacenar el archivo en `wwwroot/images/games/`.
  - Devolver la ruta relativa canónica `/images/games/{filename}` para asignarla a `CoverImageUrl`.
  - Validar sintaxis y esquema (`http`/`https`) en caso de URLs externas.

### RF-18.4: Resolución Integrada de Reportes Comunitarios de INC-17
- Si la edición se activa desde la bandeja de moderación (`GameReportsModeration.razor`) o se suministra un `AssociatedReportId`:
  - El modal muestra un banner informativo: *"Atendiendo reporte #{ReportId}: {IssueType} reportado por {ReporterNameOrAlias}."*
  - El botón principal de guardado cambia su texto a: `[ 💾 Guardar Ficha y Resolver Reporte ]`.
  - Al confirmarse el guardado, además de actualizar el juego, se invoca `IGameIssueReportService.ChangeStatusAsync` para marcar el reporte en estado `Resolved`, registrando como nota de moderador un resumen de los cambios aplicados (o la nota personalizada redactada por el moderador).

### RF-18.5: Invalidación de Caché y Experiencia Zero-FOUC
- Tras persistir los cambios en base de datos:
  - Se invoca `CachedCatalogService.Invalidate(game.Slug)` para purgar la entrada L1 en memoria.
  - La vista Blazor activa (`GameDetail.razor`) actualiza su estado local en memoria y dispara `StateHasChanged()`, mostrando de inmediato los nuevos títulos, carátula, jugadores y badges sin parpadeos ni recargas completas del navegador.

### RF-18.6: Registro de Auditoría Editorial (`GameEditLog`)
- Cada guardado exitoso genera una entrada en la tabla `GameEditLogs`:
  - `Id`: `Guid`.
  - `GameId`: `Guid` del juego modificado.
  - `EditorUserId`: Identificador del usuario moderador.
  - `EditorName`: Nombre o alias del moderador.
  - `SummaryOfChanges`: Resumen explicativo de los atributos actualizados.
  - `AssociatedReportId`: `Guid?` del reporte resuelto simultáneamente (si aplica).
  - `EditedAt`: `DateTimeOffset` UTC.

---

## 2. Requerimientos No Funcionales

- **Accesibilidad (WCAG 2.2 AA):**
  - El componente modal `GameEditorModal.razor` debe implementar `role="dialog"`, `aria-modal="true"`, `aria-labelledby`, soporte de tecla `Escape`, navegación por teclado y contraste de color conforme a los tokens de Ludeka.
- **Rendimiento e Inmediatez:**
  - La actualización de ficha y resolución de reporte debe completarse en $< 150\text{ ms}$ en condiciones normales.
- **Resiliencia:**
  - Si falla el guardado de un archivo o la URL externa es inválida, se debe capturar el error y mostrarlo amigablemente en el modal sin perder los datos ya introducidos en los campos de texto.
