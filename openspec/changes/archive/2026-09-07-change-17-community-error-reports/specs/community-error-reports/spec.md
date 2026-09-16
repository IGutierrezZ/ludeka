# Especificación: Sistema Comunitario de Reporte de Errores y Bandeja de Moderación de Fichas

- **Módulo:** Calidad del Catálogo, Feedback Comunitario y Moderación de Fichas
- **Incremento:** 17 (`change-17-community-error-reports`)
- **Estado:** En Especificación (`sdd-spec`)

---

## 1. Requerimientos Funcionales

### RF-17.1: Botón de Reporte Contextual en Ficha de Juego (`GameDetail.razor`)
- La cabecera o barra de acciones superior de la ficha de juego debe incluir un botón claramente visible y accesible: `[ 🚩 Reportar problema ]`.
- Debe estar disponible tanto para usuarios autenticados como para invitados anónimos (comunidad abierta).
- Al hacer clic, debe abrir el componente modal interactivo `GameReportModal.razor` precargando los datos del juego actual (`GameId`, `Slug`, `SpanishTitle`).

### RF-17.2: Modal de Reporte Rápido en 2 Clics (`GameReportModal.razor`)
- Debe renderizarse de forma no intrusiva con backdrop oscurecido, soporte para cierre mediante tecla `Escape`, botón de cierre `✕` o clic fuera del diálogo.
- **Tipología de Incidencias (`GameIssueType`):**
  - Selector intuitivo con opciones claras y descriptivas en español:
    1. `WrongImage`: Imagen incorrecta, desactualizada o de otra edición.
    2. `BrokenImage`: La imagen no carga o presenta enlace caído.
    3. `IncorrectPlayerCount`: Rango de comensales o semáforo de escalabilidad erróneo.
    4. `IncorrectDuration`: Tiempo de partida desajustado.
    5. `IncorrectAge`: Edad legal o recomendada inconsistente.
    6. `ErroneousMetadata`: Título, diseñador, editorial, año o sinopsis con erratas.
    7. `BrokenPurchaseLink`: Enlace a tienda o afiliado defectuoso o fuera de stock.
    8. `Other`: Otra incidencia o sugerencia libre.
- **Detalle y Sugerencia de Corrección:**
  - Campo de texto multilínea opcional (o requerido si el tipo es `Other`) para aportar contexto o la corrección sugerida (límite de 1.000 caracteres).
- **Identificación del Reportador:**
  - Si el usuario está autenticado en la plataforma, se captura automáticamente su identificador y nombre/email.
  - Si no está autenticado, se ofrece un campo de texto opcional *"Tu nombre o alias"* (por defecto: *"Comunidad anónima"*).
- **Confirmación y Agradecimiento:**
  - Tras enviar con éxito, muestra un estado de confirmación lúdico con microtexto: *"¡Muchas gracias por ayudarnos a mantener el catálogo impecable! Un moderador revisará tu reporte."*
  - Cierre automático o botón para volver a la ficha.

### RF-17.3: Entidad de Dominio y Ciclo de Vida del Reporte (`GameIssueReport`)
- La entidad `GameIssueReport` en `Ludeka.Core` debe encapsular sus estados e invariantes:
  - `Id`: `Guid` único.
  - `GameId`: `Guid` del juego referenciado.
  - `GameSlug`: `string` slug canónico del juego.
  - `GameTitle`: `string` título del juego al momento del reporte.
  - `IssueType`: Enumerado `GameIssueType`.
  - `Details`: `string?` detalle proporcionado por el usuario (máx. 1.000 caracteres).
  - `ReportedByUserId`: `string?` identificador de usuario si procede.
  - `ReporterNameOrAlias`: `string` identificador amigable del reportador.
  - `Status`: Enumerado `GameReportStatus` (`Pending`, `InReview`, `Resolved`, `Dismissed`).
  - `ModeratorNotes`: `string?` justificación o notas dejadas por el moderador.
  - `ResolvedByUserId`: `string?` identificador del moderador que resolvió o descartó el reporte.
  - `CreatedAt`: `DateTimeOffset` UTC.
  - `UpdatedAt`: `DateTimeOffset?` UTC.
  - `ResolvedAt`: `DateTimeOffset?` UTC.
- Métodos de dominio para transiciones de estado:
  - `MarkAsInReview(string moderatorId, string? notes = null)`: Pasa a `InReview`.
  - `Resolve(string moderatorId, string? notes = null)`: Pasa a `Resolved` y marca `ResolvedAt`.
  - `Dismiss(string moderatorId, string reason)`: Pasa a `Dismissed`, exige motivo y marca `ResolvedAt`.
  - `Reopen()`: Devuelve el reporte a `Pending` limpiando la resolución.

### RF-17.4: Contratos de Aplicación y Servicio (`Ludeka.Application`)
- **Repositorio (`IGameIssueReportRepository`):**
  - `GetByIdAsync(Guid id, CancellationToken ct = default)`
  - `GetAllAsync(GameReportFilter filter, CancellationToken ct = default)`
  - `GetSummaryAsync(CancellationToken ct = default)`: Devuelve conteo total, pendientes, en revisión y resueltos.
  - `AddAsync(GameIssueReport report, CancellationToken ct = default)`
  - `UpdateAsync(GameIssueReport report, CancellationToken ct = default)`
- **Servicio (`IGameIssueReportService`):**
  - `CreateReportAsync(CreateGameReportCommand command, CancellationToken ct = default)`: Valida datos, crea la entidad y la persiste.
  - `GetReportsAsync(GameReportFilter filter, CancellationToken ct = default)`: Devuelve lista paginada/filtrada de `GameIssueReportDto`.
  - `GetSummaryAsync(CancellationToken ct = default)`: Devuelve resumen métrico.
  - `ChangeStatusAsync(Guid id, UpdateGameReportStatusCommand command, CancellationToken ct = default)`: Ejecuta la transición de estado correspondiente.

### RF-17.5: Persistencia SQLite / EF Core (`Ludeka.Infrastructure`)
- Tabla `GameIssueReports` configurada en `LudekaDbContext`.
- Índices en: `GameId`, `Status`, `IssueType`, `CreatedAt`.
- Repositorio `SqliteGameIssueReportRepository` con consultas asíncronas optimizadas y ordenación cronológica inversa (los más recientes primero).

### RF-17.6: Bandeja de Moderación de Reportes (`GameReportsModeration.razor` y `/moderacion/reportes`)
- Ruta principal: `@page "/moderacion/reportes"` y `@page "/admin/reportes"`.
- Control de acceso: reservado a usuarios con `CurrentUserService.IsFoundingTeam || CurrentUserService.IsInRole("Moderator")`.
- Resumen en cabecera con tarjetas métricas:
  - ⏳ **Pendientes** (con indicador de alerta si > 0).
  - 🔍 **En Revisión**.
  - ✅ **Resueltos**.
  - 📁 **Total de Reportes**.
- Filtros interactivos:
  - Pestañas por estado (`Todos`, `Pendientes`, `En Revisión`, `Resueltos`, `Descartados`).
  - Desplegable de filtro por tipología de fallo (`GameIssueType`).
- Tarjeta editorial de cada reporte:
  - Título del juego con enlace directo a su ficha (`/juegos/{slug}`).
  - Badge visual de la tipología de fallo (color e icono alusivo).
  - Fecha formateada en español y usuario reportador.
  - Detalle o texto explicativo de la errata.
  - Notas de moderador (si ya está en revisión, resuelto o descartado).
- Acciones rápidas de moderador:
  - Botón *"Tomar en revisión"*: Asigna el reporte al moderador actual sin recarga.
  - Botón *"Resolver"*: Abre modal ligero para introducir notas de resolución (ej. *"Carátula actualizada"* o *"Semáforo corregido"*).
  - Botón *"Descartar"*: Abre modal ligero para indicar el motivo del descarte (ej. *"Dato conforme a la edición oficial"*).
  - Botón *"Reabrir"*: Permite reactivar un reporte descartado por error.
- Pestaña de navegación en `MediaModeration.razor` para alternar fluidamente entre moderación multimedia y moderación de incidencias de catálogo.

---

## 2. Requerimientos No Funcionales

- **Rendimiento:** La consulta de la bandeja con filtrado debe ejecutarse en menos de 50 ms sobre SQLite local.
- **Accesibilidad (a11y):** Cumplimiento de WCAG 2.2 AA. Botones con etiquetas `aria-label`, foco accesible en modal con `role="dialog"`, `aria-modal="true"` y contraste cromático adecuado según el sistema de diseño de Ludeka.
- **Seguridad:** Sanitización de textos para prevenir ataques XSS. Control estricto de roles en las operaciones de moderación.
- **Persistencia y Transaccionalidad:** Inserción y actualización atómicas con EF Core.

---

## 3. Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Reporte de incidencia por usuario comunitario desde la ficha
  Dado que un usuario está visualizando la ficha de un juego
  Cuando pulsa el botón "🚩 Reportar problema"
  Y en el modal selecciona el motivo "BrokenPurchaseLink"
  Y escribe "La tienda afiliada ha descatalogado el juego"
  Y pulsa "Enviar reporte"
  Entonces se almacena un registro en GameIssueReports con estado "Pending"
  Y la interfaz muestra el mensaje de agradecimiento sin recargar la página

Escenario: Moderador filtra reportes pendientes en el panel
  Dado un usuario autenticado con rol "Moderator" o "FoundingTeam"
  Cuando accede a "/moderacion/reportes"
  Y selecciona el filtro "Pendientes"
  Entonces solo visualiza reportes cuyo Status sea "Pending"
  Y el contador de pendientes coincide con el número de elementos mostrados

Escenario: Moderador descarta un reporte con justificación
  Dado un reporte en estado "Pending"
  Cuando el moderador pulsa "Descartar" e indica "La duración de 90 min es la oficial"
  Entonces el reporte pasa a estado "Dismissed"
  Y se registra el moderador y la nota de justificación
```
