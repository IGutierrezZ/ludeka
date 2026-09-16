# Checklist de Tareas: change-28-instagram-direct-publisher

## Fase 1: Modelos de Dominio (`Ludeka.Core`)
- [x] 1.1 Crear enums `InstagramPostSourceType` y `InstagramPostDraftStatus` en `Ludeka.Core/Enums`.
- [x] 1.2 Crear entidad `InstagramPostDraft` en `Ludeka.Core/Entities`.
- [x] 1.3 Actualizar `Giveaway.cs` con `InstagramMediaId`, `InstagramPermalink` y método `MarkPublishedOnInstagram`.
- [x] 1.4 Actualizar `WeeklyRelease.cs` con `InstagramMediaId`, `InstagramPermalink` y método `MarkPublishedOnInstagram`.
- [x] 1.5 Añadir `CanPublishInstagram = 1 << 7` en `ModeratorPermission.cs` y actualizar `All`.
- [x] 1.6 Añadir `InstagramPost` en `AuditEntityType.cs`.

## Fase 2: Casos de Uso y Lógica de Aplicación (`Ludeka.Application`)
- [x] 2.1 Crear DTOs y comandos en `Ludeka.Application/DTOs/InstagramDtos.cs`.
- [x] 2.2 Definir interfaces `IInstagramComposerService`, `IInstagramPublisherService` e `IInstagramApiClient` en `Ludeka.Application/Contracts`.
- [x] 2.3 Implementar `InstagramComposerService` en `Ludeka.Application/Features/Instagram/InstagramComposerService.cs` (SVG y copy estructurado con temas Dark/Light para Sorteos, Novedades y Juegos).
- [x] 2.4 Implementar `InstagramPublisherService` en `Ludeka.Application/Features/Instagram/InstagramPublisherService.cs` (orquestación, guardado de borradores, publicación con Meta client, auditoría y actualización de origen).

## Fase 3: Infraestructura, Persistencia y Meta API Client (`Ludeka.Infrastructure`)
- [x] 3.1 Crear `InstagramSettings` en `Ludeka.Infrastructure/Configuration`.
- [x] 3.2 Implementar `InstagramApiClient` en `Ludeka.Infrastructure/ExternalApis/Instagram/InstagramApiClient.cs` con soporte simulado y Meta Graph API v19.0.
- [x] 3.3 Configurar `DbSet<InstagramPostDraft>` en `LudekaDbContext.cs`.
- [x] 3.4 Actualizar `SqliteSchemaMigrator.cs` para crear tabla `InstagramPostDrafts` y columnas en `Giveaways` y `WeeklyReleases`.
- [x] 3.5 Registrar servicios en `DependencyInjection.cs` y configurar sección `Instagram` en `appsettings.json`.

## Fase 4: Capa Web y Componentes Blazor (`Ludeka.Web`)
- [x] 4.1 Añadir endpoint Minimal API `/api/instagram/card/{draftId}.svg` en `Program.cs`.
- [x] 4.2 Crear página de administración `/admin/instagram` (`InstagramModeration.razor`) con mockup interactivo del feed, selector de borradores, editor en vivo y botón de publicación.
- [x] 4.3 Actualizar `GiveawayCard.razor` para mostrar botón `[ 📸 Crear Post de Instagram ]` y badge `[ 📸 Publicado en Instagram ]`.
- [x] 4.4 Actualizar `News.razor` para mostrar botón `[ 📸 Instagram ]` y badge de publicación.
- [x] 4.5 Actualizar `UserPermissionsModal.razor` para conmutar `CanPublishInstagram`.

## Fase 5: Pruebas Unitarias (`Ludeka.UnitTests`)
- [x] 5.1 Crear tests para la entidad `InstagramPostDraft` en `InstagramPostDraftTests.cs`.
- [x] 5.2 Crear tests para `InstagramComposerService` en `InstagramComposerServiceTests.cs` (verificación de SVG y captions para sorteos y novedades).
- [x] 5.3 Crear tests para `InstagramPublisherService` en `InstagramPublisherServiceTests.cs` (ciclo completo de borrador, publicación y auditoría).
- [x] 5.4 Crear tests para `InstagramApiClient` en `InstagramApiClientTests.cs`.
- [x] 5.5 Ejecutar la suite completa `dotnet test` y comprobar que todos los tests estén en verde (681 superados).

## Fase 6: Verificación y Archivo SDD (`sdd-verify` y `sdd-archive`)
- [x] 6.1 Redactar módulo de especificación viva `docs/specs/sistema/21-publicador-directo-instagram.md`.
- [x] 6.2 Actualizar el índice maestro `docs/specs/sistema/README.md`.
- [x] 6.3 Mover `docs/increments/inc-28-instagram-direct-publisher.md` a `docs/increments/archive/`.
- [x] 6.4 Actualizar `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md` marcando INC-28 como `✅ Archivado`.
- [x] 6.5 Trasladar `.openspec/changes/change-28-instagram-direct-publisher` a `.openspec/changes/archive/2026-09-08-change-28-instagram-direct-publisher`.
- [x] 6.6 Resumen final de sesión en Engram (`mem_session_summary`).
