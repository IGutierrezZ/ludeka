# Propuesta: INC-87 Fichas Inteligentes de Novedades y Eventos, Rediseño Compacto a Proporción de Catálogo y Edición Editorial

## Motivación
Tras la implementación de INC-79 en sorteos, los módulos de Novedades Editoriales (`/novedades`) y Eventos/Ferias (`/eventos`) mantienen tarjetas horizontales gigantes (1 a 3 columnas, `h-44`/`h-48`) y formatos de carril de portada desalineados (`rail-cover--wide` y `rail-cover--banner`). Además, ninguno de los dos módulos dispone de ficha de detalle pública (`/novedades/{id}` o `/eventos/{id}`), la entidad `WeeklyRelease` carece de persistencia de URL de origen (`SourceUrl`), y los moderadores no pueden editar los datos ni sustituir carátulas desde la vista de detalle.

## Alcance
1. **Dominio y Contratos:**
   - Incorporar `SourceUrl` en `WeeklyRelease`, constructor, método `Update` y mapeo EF Core en SQLite (`LudekaDbContext`).
   - Ampliar `WeeklyReleaseDto`, `CreateWeeklyReleaseRequest` y añadir `UpdateWeeklyReleaseRequest`.
   - Incorporar `GetByIdAsync`, `UpdateReleaseAsync` y `DeleteReleaseAsync` en `IWeeklyReleaseService` y `WeeklyReleaseService` con guardas de permisos de moderación (`CanApproveMedia`).
2. **Fichas de Detalle Inteligentes:**
   - Crear `ReleaseDetail.razor` (`/novedades/{id}`, `/novedad/{id}`) con botón CTA al origen (`SourceUrl`), datos editoriales, vinculación a juego y panel modal de edición y subida WebP a Cloudflare R2 (`releases/{id:N}/cover.webp`).
   - Crear `EventDetail.razor` (`/eventos/{id}`, `/evento/{id}`) con botón CTA a la web oficial (`WebsiteUrl`), metadatos territoriales, fechas y panel modal de edición y subida WebP a R2 (`events/{id:N}/cover.webp`).
3. **Rediseño Compacto de Catálogo:**
   - Rediseñar `News.razor` y `Events.razor` a rejilla unificada de 2 a 6 columnas con tarjetas cuadradas `rail-cover--square` (240x240) y enlaces a sus fichas.
   - Adaptar `HomeReleaseCard.razor` y `HomeEventCard.razor` a formato compacto de catálogo (`w-[28vw] min-w-[105px] max-w-[125px] sm:w-44 md:w-48` y `rail-cover--square` de 192x192) con enlaces directos a sus fichas de detalle.
   - Sincronizar las suites de pruebas de contratos de marcado y optimización de imágenes (`WebMarkupContractTests` y `CatalogImageOptimizationContractTests`).
4. **Verificación Automatizada:**
   - Pruebas unitarias de dominio y servicio bajo Strict TDD.
   - Pruebas de contrato de marcado y componentes para `ReleaseDetail` y `EventDetail`.
