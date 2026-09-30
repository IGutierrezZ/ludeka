# INC-87: Fichas Inteligentes de Novedades y Eventos, Rediseño Compacto a Proporción de Catálogo y Edición Editorial

> **Estado:** ✅ Archivado  
> **Fecha de Inicio:** 2026-09-30 · **Fecha de Cierre:** 2026-09-30  
> **Rama de Trabajo:** `inc/novedades-eventos-fichas-y-tarjetas`  
> **Worktree:** `F:\repos\ludeka-wt\novedades-eventos-fichas-y-tarjetas`  
> **Dependencias:** INC-22 (Segregación de Sorteos, Novedades y Eventos), INC-79 (Ficha Inteligente de Sorteos y Redimensionado), INC-40 (Almacenamiento Cloudflare R2)  
> **Pruebas Automatizadas Verificadas:** 2.139 superadas (100% en verde)  
> **Especificación Viva:** [`16. Sorteos, Novedades y Eventos`](../specs/sistema/16-sorteos-novedades-y-eventos.md)  
> **Metodología:** Spec-Driven Development (SDD) con Strict TDD y contratos de marcado  

---

## 1. Contexto y Diagnóstico

Tras la exitosa homogeneización de los sorteos en el INC-79, los módulos de **Novedades Editoriales** y **Grandes Citas y Eventos** presentan deficiencias funcionales y de coherencia visual:

1. **Ausencia de Fichas de Detalle Dedicadas:**
   - No existen páginas de detalle públicas ni para novedades (`/novedades/{id}` o `/novedad/{id}`) ni para eventos (`/eventos/{id}` o `/evento/{id}`).
   - En Novedades, la información queda restringida a una tarjeta estática sin posibilidad de profundizar en los detalles editoriales ni acceder al enlace de origen donde se obtuvo la noticia (`SourceUrl`).
   - En Eventos, la tarjeta redirige directamente a la web oficial externa mediante un botón, sin permitir una vista previa completa en la plataforma con fechas, ubicación, organizador y descripción extendida.

2. **Falta de Edición y Moderación Directa desde la Ficha:**
   - Los moderadores y miembros de la Mesa Fundadora no disponen de un acceso contextual para editar una novedad o un evento desde su ficha pública.
   - Para novedades, `IWeeklyReleaseService` y `WeeklyReleaseService` carecen de operaciones `GetByIdAsync`, `UpdateReleaseAsync` y `DeleteReleaseAsync`.
   - Para eventos, aunque existe `/admin/eventos`, no existe acceso directo de edición desde la vista del evento.

3. **Disparidad Visual y Formato Gigante («Tarjetas Desproporcionadas»):**
   - En `News.razor` y `Events.razor`, las tarjetas se disponen en rejillas de 1 a 3 columnas con carteles 16:9 (`h-44` y `h-48`).
   - En portada, `HomeReleaseCard.razor` usa un formato apaisado (`rail-cover--wide`, 320x180) y `HomeEventCard.razor` un banner alargado (`rail-cover--banner`, 320x144).
   - Este esquema rompe el lenguaje visual de `HomeGameCard` y `HomeGiveawayCard` (INC-79), que emplean formato vertical compacto con carátula cuadrada `rail-cover--square` (192x192).

---

## 2. Alcance de la Solución (INC-87)

### Componente 1: Modelo de Dominio y Contratos de Aplicación
- **Entidad `WeeklyRelease` (`Ludeka.Core`):**
  - Añadir propiedad `SourceUrl { get; private set; }` para persistir el enlace original de la noticia.
  - Añadir método de mutación `Update(title, publisher, releaseDate, gameId, coverImageUrl, estimatedPvp, isReprint, notes, sourceUrl)`.
- **DTOs (`Ludeka.Application`):**
  - Actualizar `WeeklyReleaseDto`, `CreateWeeklyReleaseRequest` e incorporar `UpdateWeeklyReleaseRequest` con soporte de `SourceUrl`.
- **Servicios (`IWeeklyReleaseService` / `WeeklyReleaseService`):**
  - Implementar `GetByIdAsync(id, ct)`.
  - Implementar `UpdateReleaseAsync(id, request, ct)` con validación de moderación (`CanApproveMedia`).
  - Implementar `DeleteReleaseAsync(id, ct)` con validación de moderación (`CanApproveMedia`).
- **Persistencia SQLite y EF Core (`Ludeka.Infrastructure`):**
  - Mapear `SourceUrl` en `LudekaDbContext` para `WeeklyReleases` (`HasMaxLength(1000)`).
  - Migración EF Core `20260930060047_AddWeeklyReleaseSourceUrl` y reconciliación defensiva en `SqliteSchemaMigrator`.
  - Canario de esquema PostgreSQL actualizado a 15 migraciones en `PostgresSchemaVerificationTests`.

### Componente 2: Fichas de Detalle Inteligentes
- **`ReleaseDetail.razor`:**
  - Rutas: `@page "/novedades/{Id:guid}"` y `@page "/novedad/{Id:guid}"`.
  - Vista editorial: carátula en alta resolución con fallback anti-CLS `DefaultImageDomain.Novedad`, badges de novedad/reimpresión, fecha de lanzamiento y PVP estimado.
  - Botón CTA prominente hacia la fuente de origen (`SourceUrl`), con apertura en pestaña nueva segura (`target="_blank" rel="noopener noreferrer"`).
  - Vínculo a la ficha del juego si está catalogado en Ludeka.
  - Panel modal de edición para moderadores con modificación de campos, subida de carátula WebP a Cloudflare R2 (`releases/{id:N}/cover.webp`) y modal de borrado seguro.
- **`EventDetail.razor`:**
  - Rutas: `@page "/eventos/{Id:guid}"` y `@page "/evento/{Id:guid}"`.
  - Vista editorial: cartel de la cita en alta resolución con fallback anti-CLS `DefaultImageDomain.Evento`, badges de país/ámbito con bandera dinámica (`CountryCatalog`), fechas exactas, cuenta atrás y badges de oficialidad.
  - Botón CTA prominente hacia la web oficial del evento (`WebsiteUrl`).
  - Panel modal de edición para moderadores con modificación de campos, subida de cartel WebP a Cloudflare R2 (`events/{id:N}/cover.webp`) y modal de borrado seguro.

### Componente 3: Rediseño de Tarjetas a Proporción de Catálogo
- **`ReleaseCard.razor` y `EventCard.razor` (o componentes compactos dedicados):**
  - Formato vertical compacto de catálogo (`rail-card` con `rail-cover--square` de 240x240).
  - Badges flotantes compactos e iconografía oficial Lucide sin emojis.
  - Enlaces directos a las nuevas fichas de detalle (`/novedades/{id}` y `/eventos/{id}`).
- **`News.razor` y `Events.razor`:**
  - Rejilla responsive unificada con Catálogo y Sorteos: `grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-3 sm:gap-4`.
- **`HomeReleaseCard.razor` y `HomeEventCard.razor`:**
  - Adaptadas a anchura compacta de catálogo (`w-[28vw] min-w-[105px] max-w-[125px] sm:w-44 md:w-48`) con carátula cuadrada `rail-cover--square` (192x192).
  - Enlaces clicables directos a las fichas de detalle respectivas.
  - Actualización sincronizada de pruebas de contrato de marcado (`WebMarkupContractTests`) y dimensiones anti-CLS (`CatalogImageOptimizationContractTests`).

---

## 3. Criterios de Aceptación y Verificación

1. **Dominio:** Todas las mutaciones de `WeeklyRelease` y operaciones de `WeeklyReleaseService` cubiertas por pruebas unitarias bajo Strict TDD.
2. **Fichas:** Páginas `ReleaseDetail` y `EventDetail` cubiertas por pruebas de contrato (rutas, interactividad, anti-CLS y permisos de moderación).
3. **Marcado:** `WebMarkupContractTests` y `CatalogImageOptimizationContractTests` en verde con las nuevas dimensiones y clases válidas.
4. **Regresión:** Suite completa de pruebas unitarias en verde.
