# Propuesta: change-17-community-error-reports (Incremento 17: Sistema Comunitario de Reporte de Errores y Bandeja de Moderación de Fichas)

## 1. Resumen Ejecutivo y Motivación
La calidad y exactitud del catálogo son pilares fundacionales de Ludeka. Por muy riguroso que sea el proceso de ingestión y moderación inicial, los juegos de mesa cuentan con múltiples ediciones en español, erratas en componentes de terceras editoriales, variaciones de reglas en número de comensales o enlaces de afiliados a tiendas que quedan obsoletos.

El **Incremento 17** establece un canal bidireccional y sin fricción entre los jugadores y el equipo editorial:
1. Permite a cualquier usuario o visitante reportar en **2 clics** cualquier anomalía observada directamente desde la ficha del juego (`GameDetail.razor`).
2. Centraliza todas las incidencias en una **Bandeja de Moderación de Reportes** con triaje eficiente por estado y tipología, permitiendo asignar, descartar o resolver reportes con notas explicativas.

---

## 2. Objetivos Principales

1. **Acción de Reporte en Ficha de Juego (`GameDetail.razor`):**
   - Incorporar un botón visible y accesible `[ 🚩 Reportar problema ]` en la barra superior de acciones.
   - Accesible tanto para usuarios autenticados como para invitados sin cuenta activa (comunidad abierta).
2. **Modal Rápido y Tipificado (`GameReportModal.razor`):**
   - Diálogo modal con selección rápida de motivo de reporte (`GameIssueType`):
     - `WrongImage`: Imagen incorrecta, desactualizada o de otra edición.
     - `BrokenImage`: Imagen rota o caída.
     - `IncorrectPlayerCount`: Número de jugadores o semáforo de comensales erróneo.
     - `IncorrectDuration`: Tiempo de partida desajustado.
     - `IncorrectAge`: Edad mínima recomendada incorrecta.
     - `ErroneousMetadata`: Título, diseñador, editorial, año o sinopsis con erratas.
     - `BrokenPurchaseLink`: Enlace a tienda o afiliado roto o sin stock permanente.
     - `Other`: Otras incidencias con campo de texto libre.
   - Campo opcional de sugerencia de corrección y detalles adicionales (hasta 1.000 caracteres).
   - Feedback inmediato con microtexto de agradecimiento ("*¡Gracias por ayudarnos a mantener el catálogo impecable!*").
3. **Entidad de Dominio y Persistencia (`GameIssueReport`):**
   - Atributos: `Id`, `GameId`, `GameSlug`, `GameTitle`, `IssueType`, `Details`, `ReportedByUserId`, `ReporterNameOrAlias`, `Status`, `ModeratorNotes`, `CreatedAt`, `UpdatedAt`, `ResolvedAt`, `ResolvedByUserId`.
   - Estados de ciclo de vida: `Pending`, `InReview`, `Resolved`, `Dismissed`.
   - Persistencia SQLite/EF Core en `LudekaDbContext`.
4. **Bandeja de Moderación de Reportes (`/moderacion/reportes` y pestaña en `/moderacion-media`):**
   - Vista de administración para moderadores y Mesa Fundadora.
   - Métricas y contadores de incidencias pendientes.
   - Filtros combinados por estado y tipo de fallo.
   - Acciones de moderación:
     - *Tomar en revisión:* Pasa a `InReview` y asocia al moderador actual.
     - *Descartar:* Pasa a `Dismissed` con motivo justificativo obligatorio o sugerido.
     - *Resolver:* Pasa a `Resolved` con notas explicativas del cambio realizado.

---

## 3. Criterios de Aceptación (Gherkin)

```gherkin
Escenario: Usuario comunitario reporta una imagen incorrecta desde la ficha
  Dado que un usuario está visualizando la ficha de un juego en "/juegos/catan"
  Cuando pulsa el botón "🚩 Reportar problema"
  Y selecciona el motivo "Imagen incorrecta o rota"
  Y añade el detalle opcional "La carátula corresponde a la edición en inglés de 1995"
  Y pulsa "Enviar reporte"
  Entonces el reporte queda almacenado en estado "Pending"
  Y la interfaz muestra una confirmación visual de agradecimiento lúdico
  Y el modal se cierra limpiamente sin recargar la página

Escenario: Moderador accede a la bandeja y filtra reportes pendientes
  Dado un usuario con rol "Moderator" o "FoundingTeam"
  Cuando navega al panel de moderación de reportes
  Entonces visualiza los contadores de estado y la lista de reportes
  Y al filtrar por estado "Pending" e incidencia "Imagen incorrecta", solo se listan los que coinciden

Escenario: Moderador toma un reporte en revisión y posteriormente lo resuelve
  Dado un reporte en estado "Pending"
  Cuando el moderador pulsa "Tomar en revisión"
  Entonces el reporte pasa a estado "InReview" y queda asignado a dicho moderador
  Y cuando el moderador pulsa "Resolver" e indica "Carátula actualizada a la edición en español de Devir"
  Entonces el estado cambia a "Resolved", se almacena la nota y la fecha de resolución

Escenario: Moderador descarta un reporte inválido o duplicado
  Dado un reporte en estado "Pending"
  Cuando el moderador pulsa "Descartar" indicando "El rango de 3-4 jugadores es el oficial del reglamento"
  Entonces el estado del reporte pasa a "Dismissed"
  Y queda registrada la justificación del descarte
```

---

## 4. Alcance Funcional Detallado

- **Dominio (`Ludeka.Core`):**
  - `GameIssueType.cs`: Tipos estructurados de incidencias.
  - `GameReportStatus.cs`: Estados del ciclo de vida (`Pending`, `InReview`, `Resolved`, `Dismissed`).
  - `GameIssueReport.cs`: Entidad con encapsulación de invariantes y transiciones de estado.
- **Aplicación (`Ludeka.Application`):**
  - `IGameIssueReportRepository.cs` e `IGameIssueReportService.cs`.
  - DTOs y comandos CQRS ligeros: `CreateGameReportCommand`, `UpdateGameReportStatusCommand`, `GameIssueReportDto`, `GameReportFilter`, `GameIssueReportSummaryDto`.
  - `GameIssueReportService`: Lógica de creación con sanitización, validaciones y transiciones de estado.
- **Infraestructura (`Ludeka.Infrastructure`):**
  - `SqliteGameIssueReportRepository.cs` con consultas optimizadas y conteos.
  - Mapeo en `LudekaDbContext.cs` con índices en `GameId`, `Status`, `IssueType` y `CreatedAt`.
- **Presentación Web (`Ludeka.Web`):**
  - `GameReportModal.razor`: Componente modal accesible con foco, tecla Escape, selección rápida y microtextos.
  - `GameReportsModeration.razor`: Página de moderación `/moderacion/reportes` con resumen de métricas, tarjetas editoriales de reporte y modales de acción rápida (revisar, resolver, descartar).
  - Integración en `MediaModeration.razor`: Enlace cruzado o pestaña para acceso unificado desde el panel existente.
  - Integración en `GameDetail.razor`: Botón de reporte `🚩 Reportar problema`.
  - Navegación en `MainLayout.razor`.

---

## 5. No-Alcance (Límites de este Incremento)

- **Edición directa de metadatos de ficha y carga de archivos:** La modificación directa de los campos del juego (subir carátula nueva, editar jugadores o sinopsis) corresponde al **Incremento 18 (`inc-18-moderator-game-editor`)**. En INC-17 se gestiona el reporte, el triaje, la resolución y la trazabilidad.
- **Sistemas automáticos de captcha invasivos:** Para no penalizar la UX comunitaria de 2 clics, se emplea protección por diseño (validación de longitud, deduplicación de envíos rápidos y campos trampa honeypot si fuera necesario) en lugar de captchas intrusivos.
- **Notificaciones externas push a moderadores:** Las notificaciones webhook a Discord/Telegram ya se configuraron en INC-09; en este incremento nos centramos en la experiencia in-app de reporte y gestión.
