# Especificación Técnica: INC-87 — Fichas Inteligentes de Novedades y Eventos, Rediseño Compacto a Proporción de Catálogo y Edición Editorial

## 1. Resumen Ejecutivo
Este incremento homogeniza integralmente las interfaces y contratos de **Novedades Editoriales** (`/novedades`) y **Grandes Citas y Eventos** (`/eventos`) con el estándar establecido en **Sorteos** (INC-79):
1. Incorpora fichas de detalle dedicadas con rutas canónicas y enlaces externos CTA destacados:
   - Novedades: `/novedades/{id}` y `/novedad/{id}`, con botón para acceder a la URL original de la noticia (`SourceUrl`).
   - Eventos: `/eventos/{id}` y `/evento/{id}`, con botón para acceder a la web oficial (`WebsiteUrl`).
2. Proporciona a los moderadores y miembros de la Mesa Fundadora herramientas de edición y mantenimiento directamente desde las fichas públicas de novedad y evento (panel modal con actualización de datos, subida de carátulas/carteles WebP a Cloudflare R2 y eliminación segura).
3. Rediseña las tarjetas en los catálogos principales a formato compacto de catálogo (2 a 6 columnas) con carátulas cuadradas `rail-cover--square` (240x240), y las tarjetas en los carriles de portada (`HomeReleaseCard` y `HomeEventCard`) al formato compacto de 192x192 anti-CLS.

---

## 2. Requerimientos Funcionales y de Dominio

### REQ-1: Persistencia de Fuente Original y Mutación en `WeeklyRelease`
- **Atributo `SourceUrl`:** La entidad `WeeklyRelease` (`Ludeka.Core.Entities`) debe incorporar `public string? SourceUrl { get; private set; }`.
- **Constructor y Mutación:**
  - El constructor principal de `WeeklyRelease` debe admitir el parámetro opcional `string? sourceUrl = null`.
  - Debe incorporarse el método de dominio:
    ```csharp
    public void Update(
        string title,
        string publisher,
        DateOnly releaseDate,
        Guid? gameId,
        string? coverImageUrl,
        decimal? estimatedPvp,
        bool isReprint,
        string? notes,
        string? sourceUrl = null)
    ```
    Validando invariantes (título y editorial obligatorios y limpios).
- **Mapeo EF Core (`LudekaDbContext`):**
  - Mapear la propiedad `SourceUrl` en la tabla `WeeklyReleases` con longitud máxima opcional de 1000 caracteres.

### REQ-2: Contratos de Aplicación y Servicio de Novedades (`IWeeklyReleaseService`)
- **DTOs:**
  - `WeeklyReleaseDto` incluye `string? SourceUrl = null`.
  - `CreateWeeklyReleaseRequest` incluye `string? SourceUrl = null`.
  - Se define `UpdateWeeklyReleaseRequest`:
    ```csharp
    public record UpdateWeeklyReleaseRequest(
        Guid Id,
        string Title,
        string Publisher,
        DateOnly ReleaseDate,
        Guid? GameId = null,
        string? CoverImageUrl = null,
        decimal? EstimatedPvp = null,
        bool IsReprint = false,
        string? Notes = null,
        string? SourceUrl = null);
    ```
- **Métodos en `IWeeklyReleaseService` y `WeeklyReleaseService`:**
  - `Task<WeeklyReleaseDto?> GetByIdAsync(Guid id, CancellationToken ct = default);`
  - `Task<WeeklyReleaseDto> UpdateReleaseAsync(Guid id, UpdateWeeklyReleaseRequest request, CancellationToken ct = default);` (valida permiso `CanApproveMedia`).
  - `Task DeleteReleaseAsync(Guid id, CancellationToken ct = default);` (valida permiso `CanApproveMedia`).

### REQ-3: Ficha Inteligente de Novedad (`ReleaseDetail.razor`)
- **Rutas canónicas:** `@page "/novedades/{Id:guid}"` y `@page "/novedad/{Id:guid}"`.
- **Estructura visual:**
  - Navegación superior con enlace de retorno `/novedades`.
  - Columna izquierda: Carátula en gran formato con fallback anti-CLS `DefaultImageDomain.Novedad`, badges flotantes de novedad/reimpresión y PVP.
  - Botón CTA destacado: «Ir a la noticia oficial &rarr;» apuntando a `SourceUrl` (con `target="_blank"` y `rel="noopener noreferrer"`). Si no hay `SourceUrl`, se muestra aviso editorial amigable o fallback.
  - Columna derecha: Título, editorial, fecha estimada de llegada a tiendas, notas editoriales y ficha de juego enlazado en catálogo si existe (`GameId`).
  - Si el usuario es moderador (`CanApproveMedia` o Mesa Fundadora):
    - Botón «Editar Novedad» que abre modal con edición de todos los campos y selector `<InputFile>` para subir carátula WebP a Cloudflare R2 (`releases/{id:N}/cover.webp`).
    - Botón «Eliminar» con modal de confirmación.

### REQ-4: Ficha Inteligente de Evento (`EventDetail.razor`)
- **Rutas canónicas:** `@page "/eventos/{Id:guid}"` y `@page "/evento/{Id:guid}"`.
- **Estructura visual:**
  - Navegación superior con enlace de retorno `/eventos`.
  - Columna izquierda: Cartel en gran formato con fallback anti-CLS `DefaultImageDomain.Evento`, badges de ámbito (bandera dinámica `CountryCatalog`) y días restantes.
  - Botón CTA destacado: «Visitar web oficial del evento &rarr;» apuntando a `WebsiteUrl` (con `target="_blank"` y `rel="noopener noreferrer"`).
  - Columna derecha: Título, fechas formateadas, ciudad/ubicación, organizador, badge oficial/encuentro y descripción extendida.
  - Si el usuario es moderador (`PermisoGestionarEventos` o Mesa Fundadora):
    - Botón «Editar Evento» que abre modal con edición de todos los campos y selector `<InputFile>` para subir cartel WebP a Cloudflare R2 (`events/{id:N}/cover.webp`).
    - Botón «Eliminar» con modal de confirmación.

### REQ-5: Rediseño Compacto a Proporción de Catálogo
- **`News.razor`:**
  - Rejilla adaptada a `grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-3 sm:gap-4`.
  - Uso de tarjetas compactas `rail-card` con carátula cuadrada `rail-cover--square` (240x240), badges de novedad/reimpresión y fecha, enlace a `/novedades/@release.Id` y botón rápido al origen si existe.
- **`Events.razor`:**
  - Rejilla adaptada a `grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-3 sm:gap-4`.
  - Uso de tarjetas compactas `rail-card` con cartel cuadrado `rail-cover--square` (240x240), badges de país y fechas, enlace a `/eventos/@evt.Id` y botón rápido a web oficial.
- **Portada (`HomeReleaseCard.razor` y `HomeEventCard.razor`):**
  - Adaptadas a anchura compacta de catálogo (`w-[28vw] min-w-[105px] max-w-[125px] sm:w-44 md:w-48`) con carátula cuadrada `rail-cover--square` (192x192).
  - Enlaces a sus respectivas fichas de detalle `/novedades/@Release.Id` y `/eventos/@Event.Id`.
  - Preservación estricta de pruebas de contrato de marcado y atributos anti-CLS (`loading="lazy"`, `decoding="async"`, `width="192"`, `height="192"`, `onerror`).
