# Checklist de Tareas Técnicas: change-23-multimedia-editorial-categorization

## Fase 1: Capa de Dominio (`Ludeka.Core`)
- [ ] **1.1** Crear `src/Ludeka.Core/Enums/MediaCategory.cs` con `QuickOverview`, `Tutorial`, `Gameplay`, `ReviewOpinion`.
- [ ] **1.2** Crear `src/Ludeka.Core/Helpers/MediaClassifier.cs` con heurística pura de clasificación por título y descripción.
- [ ] **1.3** Actualizar `src/Ludeka.Core/Entities/MediaItem.cs` incorporando la propiedad `Category`, constructores compatibles y métodos de mutación `ChangeCategory(MediaCategory)` y `ReassignGame(Guid)`.

## Fase 2: Capa de Aplicación (`Ludeka.Application`)
- [ ] **2.1** Actualizar `src/Ludeka.Application/DTOs/MediaDtos.cs` con `Category`, `CategoryDisplayName` y soporte en `GameMediaHubDto`.
- [ ] **2.2** Actualizar `src/Ludeka.Application/Contracts/IMediaService.cs` incorporando `ApproveMediaAsync(id, category, ct)`, `UpdateMediaCategoryAsync(id, category, ct)`, `ReassignMediaGameAsync(id, newGameId, ct)`, `DeleteMediaAsync(id, ct)`.
- [ ] **2.3** Implementar en `src/Ludeka.Application/Features/Media/MediaService.cs` los nuevos métodos, validaciones de seguridad con `EnsurePermission()` (`CanApproveMedia` / `IsFoundingTeam`) y trazabilidad estructurada en `IAuditService`.

## Fase 3: Capa de Infraestructura (`Ludeka.Infrastructure`)
- [ ] **3.1** Actualizar `src/Ludeka.Infrastructure/Data/LudekaDbContext.cs` mapeando el índice `Category` en `MediaItems`.
- [ ] **3.2** Actualizar `src/Ludeka.Infrastructure/Data/SqliteSchemaMigrator.cs` con migración defensiva para la columna `Category` en la tabla `MediaItems`.
- [ ] **3.3** Actualizar `src/Ludeka.Infrastructure/YouTube/YouTubeSearchService.cs` conectando `MediaClassifier` en la ingesta.
- [ ] **3.4** Actualizar `src/Ludeka.Infrastructure/Seeding/CatalogSeeder.cs` estableciendo categorías explícitas.

## Fase 4: Capa de Presentación Blazor (`Ludeka.Web`)
- [ ] **4.1** Actualizar `src/Ludeka.Web/Components/Pages/MediaModeration.razor` añadiendo ruta `@page "/admin/multimedia"` y selector interactivo obligatorio de categoría preclasificado por la heurística.
- [ ] **4.2** Actualizar `src/Ludeka.Web/Components/Shared/MultimediaHub.razor` integrando el menú flotante `[ ⚙️ Moderar ]`, cambio rápido de categoría reactivo, modal de reasignación asistida por título y diálogo de eliminación segura.
- [ ] **4.3** Actualizar `src/Ludeka.Web/Components/Pages/GameDetail.razor` con callback de actualización reactiva del hub.

## Fase 5: Pruebas Unitarias y Verificación (`Ludeka.UnitTests`)
- [ ] **5.1** Crear `tests/Ludeka.UnitTests/Core/MediaClassifierTests.cs` cubriendo las 4 categorías con casos reales.
- [ ] **5.2** Crear `tests/Ludeka.UnitTests/Core/MediaItemCategorizationTests.cs` validando invariantes y métodos de mutación.
- [ ] **5.3** Actualizar `tests/Ludeka.UnitTests/Services/MediaServiceTests.cs` cubriendo aprobación con categoría, recategorización, reasignación, eliminación, auditoría de `Media` y rechazo de accesos no autorizados.
- [ ] **5.4** Ejecutar `dotnet test` y verificar que el 100% de las pruebas pasen sin regresiones.
