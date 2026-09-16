# Exploración: change-18-moderator-game-editor (Incremento 18: Editor Editorial de Fichas de Catálogo y Carga de Imágenes para Moderadores)

## 1. Estado Actual de la Solución y Análisis de Brecha (Gap Analysis)

### 1.1 Situación Actual del Catálogo y la Ficha de Juego
- **Ficha Inteligente (`GameDetail.razor`):**
  - Actualmente, `GameDetail.razor` presenta la ficha completa con metadatos del juego, carátula BGG/local, semáforo de comensales, guía de fundas, ofertas de tiendas afiliadas, veredicto de la Mesa Fundadora, expansiones oficiales y síntesis con IA.
  - La barra superior incluye acciones para `[ 🚩 Reportar problema ]`, `[ 🎨 Cartel para Redes ]`, `[ 🛒 Comprar ]`, `[ 🤖 Generar con IA ]` y `[ 🛡️ Gestionar Veredicto Fundador ]`.
  - **Brecha:** No existe ningún botón ni mecanismo en la interfaz para que un moderador o fundador edite los datos de la ficha directamente (títulos, autores, año, editorial, duraciones, edades, sinopsis o ADN lúdico) ni para sustituir o subir una carátula nueva. Si hay una errata en el catálogo, actualmente se requiere modificar la base de datos a bajo nivel o resembrar.

### 1.2 Situación Actual de la Bandeja de Reportes (INC-17) y Moderación
- **Bandeja de Reportes (`GameReportsModeration.razor`):**
  - Implementada en el Incremento 17 (`/moderacion/reportes`). Permite a los moderadores revisar reportes enviados por la comunidad (`Pending`, `InReview`, `Resolved`, `Dismissed`).
  - Cada tarjeta de reporte cuenta con botones para `[ 🔍 Tomar en revisión ]`, `[ ✅ Resolver ]` (con modal para ingresar notas) y `[ ✕ Descartar ]`.
  - **Brecha:** El moderador puede resolver el reporte introduciendo un texto, pero **no puede aplicar la corrección a la ficha del juego en el mismo flujo**. Debe abrir la ficha en otra pestaña, carece de formulario de edición y no hay trazabilidad cruzada en un solo clic entre "corregir ficha" y "resolver reporte automáticamente".

### 1.3 Almacenamiento de Imágenes y Gestión Multimedia
- **Directorio de Imágenes (`wwwroot/images/games/`):**
  - Contiene imágenes locales pre-sembradas en formatos `.png`, `.jpg` y `.webp`.
  - **Brecha:** No existe un servicio de almacenamiento de imágenes (`IImageStorageService`) que valide tipos MIME (JPG, PNG, WebP), limite el peso a un máximo seguro (5 MB), higienice nombres de archivo canónicos por slug y guarde en el sistema de ficheros de forma segura para Blazor SSR e interactivo.

### 1.4 Persistencia en Repositorio (`SqliteGameRepository`)
- **Método `UpdateAsync`:**
  - Actualmente en `SqliteGameRepository.cs`, `UpdateAsync` solo actualiza `LudistRating` y `AiSummary` sobre la entidad existente rastreada.
  - **Brecha:** No actualiza los metadatos generales (`SpanishTitle`, `OriginalTitle`, `Designer`, `Publisher`, `YearPublished`, `Description`, `Confrontation`, `Style`, `IsOfficialSolo`, `Age`, `Language`, `Footprint`, `Duration`, `Scalability`, `CoverImageUrl`, `ThumbnailUrl`). Es necesario enriquecer `Game.cs` con métodos de dominio expresivos (`UpdateCatalogInformation` y `UpdateImages`) y asegurar que `SqliteGameRepository` persista todos los campos modificados.

---

## 2. Alternativas Técnicas y Decisiones de Diseño

### 2.1 Identificador y Slug al Modificar el Título
- **Alternativa A (Regenerar slug automáticamente):** Cambiar el slug cuando cambia el título en español.
  - *Riesgo:* Rompe URLs existentes, marcadores del navegador, enlaces de reportes existentes e indexación SEO.
- **Alternativa B (Slug canónico inmutable por defecto, editable solo si se solicita) [DECISIÓN RECOMENDADA]:**
  - Mantener el `Slug` existente del juego intacto durante las ediciones habituales para preservar la integridad referencial y enlaces externos. El slug se mantiene como identificador canónico estable en URLs.

### 2.2 Formato y Almacenamiento Físico de Imágenes
- **Alternativa A (Guardar en Base de Datos como BLOB o Base64):**
  - *Inconveniente:* Degrada el rendimiento de SQLite, hincha las copias de seguridad e incrementa el consumo de memoria en consultas habituales.
- **Alternativa B (Almacenamiento Físico en `wwwroot/images/games/` con URL relativa) [DECISIÓN RECOMENDADA]:**
  - Guardar el archivo subido en `src/Ludeka.Web/wwwroot/images/games/` con nomenclatura canónica higienizada: `{slug}-cover-{timestamp}.{ext}` (o `{slug}-cover.{ext}` con query param para cache busting).
  - Admite URLs remotas válidas (ej. BGG o web editorial) si el moderador prefiere enlazar sin subir archivo físico.
  - Servicio desacoplado `IImageStorageService` con implementación `PhysicalFileImageStorageService`.

### 2.3 Modelo de Auditoría Editorial (`GameEditLog`)
- Registrar cada edición editorial con:
  - `Id`: Identificador único.
  - `GameId`: Juego editado.
  - `EditorUserId`: Usuario moderador/fundador que realizó la modificación.
  - `EditorName`: Nombre/alias del editor.
  - `SummaryOfChanges`: Resumen en lenguaje natural de los campos modificados (ej. "Modificado número de jugadores de 3-4 a 2-4; Actualizada carátula").
  - `AssociatedReportId`: Guid del reporte comunitario de INC-17 resuelto en la misma operación (si aplica).
  - `EditedAt`: Marca temporal UTC.

### 2.4 Invalidación de Caché sin FOUC (Zero Flash of Unstyled Content)
- Al guardar los cambios:
  1. La base de datos SQLite persiste los nuevos valores.
  2. Se invoca `CachedCatalogService.Invalidate(slug)` para purgar la caché L1 en memoria.
  3. En la vista Blazor (`GameDetail.razor`), se refresca el objeto local `Game` o `GameDetailDto` de inmediato mediante notificación de callback, garantizando que el usuario y moderador vean los cambios reflejados al instante sin parpadeos ni necesidad de recargar la página (`NavigationManager.Refresh()`).

---

## 3. Impacto en la Arquitectura y Archivos Involucrados

- **`Ludeka.Core`:**
  - [MODIFY] `src/Ludeka.Core/Entities/Game.cs` (método de dominio `UpdateCatalogInformation(...)`, enriquecimiento de `UpdateImages(...)` y ajuste de comensales/escalabilidad).
  - [NEW] `src/Ludeka.Core/Entities/GameEditLog.cs` (entidad de auditoría editorial).
- **`Ludeka.Application`:**
  - [NEW] `src/Ludeka.Application/Contracts/IGameEditorService.cs`
  - [NEW] `src/Ludeka.Application/Contracts/IImageStorageService.cs`
  - [NEW] `src/Ludeka.Application/DTOs/GameEditorDtos.cs` (`UpdateGameDetailsCommand`, `GameImageUploadResult`, `GameEditLogDto`)
  - [NEW] `src/Ludeka.Application/Features/Catalog/GameEditorService.cs`
- **`Ludeka.Infrastructure`:**
  - [NEW] `src/Ludeka.Infrastructure/Services/PhysicalFileImageStorageService.cs`
  - [MODIFY] `src/Ludeka.Infrastructure/Data/SqliteGameRepository.cs` (actualización completa en `UpdateAsync`)
  - [MODIFY] `src/Ludeka.Infrastructure/Data/LudekaDbContext.cs` (DbSet `GameEditLogs` y mapeo)
  - [MODIFY] `src/Ludeka.Infrastructure/Data/SqliteSchemaMigrator.cs` (tabla `GameEditLogs`)
- **`Ludeka.Web`:**
  - [NEW] `src/Ludeka.Web/Components/Shared/GameEditorModal.razor` (modal interactivo y accesible WCAG 2.2 AA)
  - [MODIFY] `src/Ludeka.Web/Components/Pages/GameDetail.razor` (botón de edición y apertura de modal)
  - [MODIFY] `src/Ludeka.Web/Components/Pages/GameReportsModeration.razor` (botón `Corregir Ficha y Resolver` con modal de edición embebido o redirección inteligente)
  - [MODIFY] `src/Ludeka.Web/Program.cs` (inyección de dependencias)
- **`Ludeka.UnitTests`:**
  - [NEW] `tests/Ludeka.UnitTests/Domain/GameEditorDomainTests.cs` (invariantes de dominio de Game y GameEditLog)
  - [NEW] `tests/Ludeka.UnitTests/Application/GameEditorServiceTests.cs` (casos de uso, validación de rangos, permisos y resolución de reportes)
  - [NEW] `tests/Ludeka.UnitTests/Infrastructure/PhysicalFileImageStorageServiceTests.cs` (validación MIME, tamaño, nombrado higienizado)
  - [NEW] `tests/Ludeka.UnitTests/Web/GameEditorWebIntegrationTests.cs` (verificación de accesibilidad del modal y DI)
