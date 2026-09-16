# Exploración: change-17-community-error-reports (Incremento 17: Sistema Comunitario de Reporte de Errores y Bandeja de Moderación de Fichas)

## 1. Estado Actual de la Solución y Análisis de Brecha (Gap Analysis)

### 1.1 Situación Actual del Catálogo y la Ficha de Juego
- **Ficha Inteligente (`GameDetail.razor`):**
  - Actualmente, la ficha presenta información rica: metadatos del juego, carátula BGG/local, semáforo de escalabilidad, guía de fundas, enlaces de compra a tiendas afiliadas, veredicto de la Mesa Fundadora, expansiones oficiales y síntesis con IA.
  - La barra superior y de acciones contiene botones para *"🎨 Cartel para Redes"*, *"🛒 Comprar"*, *"🤖 Generar con IA"* (moderadores) y *"🛡️ Gestionar Veredicto Fundador"*.
  - **Brecha:** No existe ningún mecanismo para que un usuario de la comunidad o visitante anónimo reporte erratas en los datos (edad errónea, número de jugadores desajustado, sinopsis desfasada, enlace de afiliado caído o sin stock permanente, carátula de baja resolución o de edición equivocada). Si un usuario encuentra un error, debe contactar por canales externos o no puede avisar.

### 1.2 Situación Actual de la Moderación en Ludeka
- **Panel de Moderación (`MediaModeration.razor`):**
  - Ubicado en `/moderacion-media` y `/moderacion/multimedia`.
  - Gestiona actualmente la aprobación y rechazo en 1 clic de elementos multimedia (vídeos de YouTube y posts de Instagram), asignación de vídeos huérfanos a juegos del catálogo, detector automatizado de enlaces rotos y búsqueda quirúrgica en YouTube Data API v3.
  - El acceso está protegido mediante `CurrentUserService.IsFoundingTeam || CurrentUserService.IsInRole("Moderator")`.
  - **Brecha:** No existe una bandeja de entrada donde los moderadores reciban, filtren, inspeccionen y resuelvan los reportes de calidad y erratas enviados por los usuarios. Los reportes comunitarios carecen de modelo de dominio, repositorio, servicio de aplicación y vista dedicada.

---

## 2. Alternativas Técnicas y Decisiones de Diseño

### 2.1 Modelo de Dominio y Estados del Reporte (`Ludeka.Core`)
El reporte de una incidencia en una ficha de juego debe estructurarse como una entidad de dominio rica y autocontenida:
- **Entidad `GameIssueReport`:**
  - `Id`: `Guid` único identificador.
  - `GameId`: `Guid` del juego asociado.
  - `GameSlug`: Slug del juego para navegación y visualización directa.
  - `GameTitle`: Título del juego en español para reconocimiento inmediato en la bandeja de moderación sin necesidad de joins pesados.
  - `IssueType`: Enumerado tipificado `GameIssueType` que categoriza el problema:
    - `WrongImage`: Imagen incorrecta, de baja calidad o perteneciente a otra edición.
    - `BrokenImage`: Imagen no carga o enlace caído.
    - `IncorrectPlayerCount`: Número de jugadores o semáforo de escalabilidad incorrecto.
    - `IncorrectDuration`: Duración de partida errónea.
    - `IncorrectAge`: Edad mínima recomendada incorrecta.
    - `ErroneousMetadata`: Título, diseñador, editorial, año o sinopsis con erratas.
    - `BrokenPurchaseLink`: Enlace de compra o afiliado roto o sin stock permanente.
    - `Other`: Otro problema o sugerencia libre.
  - `Details`: Texto con la explicación o sugerencia aportada por el usuario (máx. 1.000 caracteres).
  - `ReportedByUserId`: Identificador del usuario si está registrado/autenticado.
  - `ReporterNameOrAlias`: Nombre/alias del reportador o *"Comunidad abierta"* si es anónimo.
  - `Status`: Enumerado `GameReportStatus`:
    - `Pending`: Recibido y a la espera de triaje por moderación.
    - `InReview`: En revisión activa por un moderador específico.
    - `Resolved`: Incidencia solventada o corregida.
    - `Dismissed`: Descartado (reporte inválido, falso positivo o duplicado).
  - `ModeratorNotes`: Explicación o motivo registrado por el moderador al resolver o descartar.
  - `ResolvedByUserId`: Identificador del moderador que tomó la acción final.
  - `CreatedAt`, `UpdatedAt`, `ResolvedAt`: Marcas temporales UTC para auditoría y métricas.

### 2.2 Experiencia de Usuario (UX): Reporte en 2 Clics
- **Acceso Directo:** Botón accesible y visible en la cabecera de `GameDetail.razor`: `[ 🚩 Reportar problema ]`.
- **Modal Ligero (`GameReportModal.razor`):**
  - Clic 1: Pulsar en `Reportar problema` despliega el modal interactivo.
  - Selección rápida de motivo mediante tarjetas/botones o select estilizado.
  - Campo de texto opcional para sugerir la corrección o aportar detalles.
  - Si el usuario no ha iniciado sesión, permite indicar su nombre o alias opcionalmente.
  - Clic 2: Pulsar `Enviar reporte`.
  - Confirmación inmediata con microtexto de agradecimiento lúdico y cierre automático tras confirmación.

### 2.3 Bandeja de Entrada de Moderación
- **Rutas y Acceso:**
  - Pestaña integrada en el panel existente `/moderacion-media` (para que el moderador tenga un centro neurálgico unificado) y ruta directa `/moderacion/reportes` o `/admin/reportes`.
  - Solo accesible para `IsFoundingTeam` o rol `Moderator`.
- **Filtros e Inspección Rápida:**
  - Filtro por estado: `Pendientes` (con badge contador en tiempo real), `En Revisión`, `Resueltos`, `Descartados`.
  - Filtro por tipo de incidencia (`GameIssueType`).
  - Enlace directo a la ficha del juego en nueva pestaña (`/juegos/{slug}`).
- **Acciones de Moderación:**
  - *Tomar en revisión (`InReview`):* Asigna el reporte al moderador actual para evitar trabajo duplicado.
  - *Descartar (`Dismissed`):* Requiere o permite un motivo breve explicativo.
  - *Resolver (`Resolved`):* Marca el reporte como resuelto e introduce notas de resolución. En INC-18 se enlazará con el editor editorial de la ficha.

---

## 3. Impacto en la Arquitectura y Archivos Involucrados

- **`Ludeka.Core`:**
  - [NEW] `src/Ludeka.Core/Enums/GameIssueType.cs`
  - [NEW] `src/Ludeka.Core/Enums/GameReportStatus.cs`
  - [NEW] `src/Ludeka.Core/Entities/GameIssueReport.cs`
- **`Ludeka.Application`:**
  - [NEW] `src/Ludeka.Application/Contracts/IGameIssueReportRepository.cs`
  - [NEW] `src/Ludeka.Application/Contracts/IGameIssueReportService.cs`
  - [NEW] `src/Ludeka.Application/DTOs/GameIssueReportDtos.cs` (`GameIssueReportDto`, `CreateGameReportCommand`, `UpdateGameReportStatusCommand`, `GameReportFilter`, `GameIssueReportSummaryDto`)
  - [NEW] `src/Ludeka.Application/Features/Reports/GameIssueReportService.cs`
- **`Ludeka.Infrastructure`:**
  - [NEW] `src/Ludeka.Infrastructure/Repositories/SqliteGameIssueReportRepository.cs`
  - [MODIFY] `src/Ludeka.Infrastructure/Data/LudekaDbContext.cs` (incorporación de `DbSet<GameIssueReport>` y configuración en `OnModelCreating`)
- **`Ludeka.Web`:**
  - [NEW] `src/Ludeka.Web/Components/Shared/GameReportModal.razor`
  - [NEW] `src/Ludeka.Web/Components/Pages/GameReportsModeration.razor` (o pestaña integrada en `MediaModeration.razor`)
  - [MODIFY] `src/Ludeka.Web/Components/Pages/GameDetail.razor` (botón de reporte e invocación de modal)
  - [MODIFY] `src/Ludeka.Web/Components/Layout/MainLayout.razor` (enlace al centro de moderación de reportes)
  - [MODIFY] `src/Ludeka.Web/Program.cs` (registro de dependencias DI)
- **`Ludeka.UnitTests`:**
  - [NEW] `tests/Ludeka.UnitTests/Core/GameIssueReportTests.cs` (pruebas de invariantes de dominio)
  - [NEW] `tests/Ludeka.UnitTests/Application/GameIssueReportServiceTests.cs` (pruebas de casos de uso y validación)
  - [NEW] `tests/Ludeka.UnitTests/Infrastructure/SqliteGameIssueReportRepositoryTests.cs` (pruebas con SQLite InMemory)
