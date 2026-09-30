# Tareas de Implementación: INC-87 — Fichas Inteligentes de Novedades y Eventos, Rediseño Compacto y Edición Editorial

## Fase 1: Dominio, DTOs y Persistencia
- [x] 1.1 Modificar `WeeklyRelease.cs`: añadir `SourceUrl`, actualizar constructor y añadir método `Update`.
- [x] 1.2 Actualizar `CommunityDtos.cs`: extender `WeeklyReleaseDto`, `CreateWeeklyReleaseRequest` y añadir `UpdateWeeklyReleaseRequest`.
- [x] 1.3 Actualizar `LudekaDbContext.cs`: mapear `SourceUrl` en `WeeklyRelease` (`HasMaxLength(1000)`).
- [x] 1.4 Actualizar `SocialIngestionService.cs` para pasar `item.SourceUrl` al crear `WeeklyRelease`.

## Fase 2: Servicios de Aplicación y Pruebas Unitarias TDD
- [x] 2.1 Actualizar `IWeeklyReleaseService` y `WeeklyReleaseService`: implementar `GetByIdAsync`, `UpdateReleaseAsync` y `DeleteReleaseAsync`.
- [x] 2.2 Actualizar fakes y repositorios en tests (`WeeklyReleaseCreationTests.cs`, `SocialIngestionServiceTests.cs`, etc.).
- [x] 2.3 Crear pruebas unitarias dedicadas `WeeklyReleaseServiceTests.cs` verificando operaciones, actualización, borrado y permisos de moderación.

## Fase 3: Fichas Inteligentes de Detalle
- [x] 3.1 Crear `ReleaseDetail.razor` (`/novedades/{id}`, `/novedad/{id}`) con botón CTA a la noticia original, ficha de juego y modal de edición/subida R2 para moderadores.
- [x] 3.2 Crear `EventDetail.razor` (`/eventos/{id}`, `/evento/{id}`) con botón CTA a web oficial y modal de edición/subida R2 para moderadores.
- [x] 3.3 Crear pruebas de contrato de páginas `ReleaseDetailPageContractTests.cs` y `EventDetailPageContractTests.cs`.

## Fase 4: Rediseño de Tarjetas a Proporción de Catálogo
- [x] 4.1 Rediseñar `News.razor` a rejilla de 2 a 6 columnas con tarjetas cuadradas compactas `rail-cover--square` (240x240) y enlace a la ficha.
- [x] 4.2 Rediseñar `Events.razor` a rejilla de 2 a 6 columnas con tarjetas cuadradas compactas `rail-cover--square` (240x240) y enlace a la ficha.
- [x] 4.3 Rediseñar `HomeReleaseCard.razor` a formato vertical compacto (`w-[28vw] min-w-[105px] max-w-[125px] sm:w-44 md:w-48` con `rail-cover--square` 192x192) y enlace a `/novedades/{id}`.
- [x] 4.4 Rediseñar `HomeEventCard.razor` a formato vertical compacto (`w-[28vw] min-w-[105px] max-w-[125px] sm:w-44 md:w-48` con `rail-cover--square` 192x192) y enlace a `/eventos/{id}`.
- [x] 4.5 Actualizar contratos de marcado en `WebMarkupContractTests.cs` y `CatalogImageOptimizationContractTests.cs`.

## Fase 5: Verificación Integral y Cierre
- [x] 5.1 Ejecución completa de la suite de pruebas automáticas en verde.
- [x] 5.2 Commit de la unidad de trabajo y preparación para PR.
