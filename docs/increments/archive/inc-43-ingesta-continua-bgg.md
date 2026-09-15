# INC-43: Ingesta Continua y Auto-Descubrimiento de Novedades BGG en el Lote Nocturno

> **Estado:** ✅ Archivado  
> **Fecha de Inicio:** 2026-09-14  
> **Fecha de Cierre:** 2026-09-14  
> **Rama de Trabajo:** `inc/ingesta-continua-bgg`  
> **Worktree:** `C:\repos\ludeka-wt\ingesta-continua-bgg`  
> **Dependencias:** INC-24 (Lote Nocturno), INC-41 (Ingesta Masiva y Fotos GeekDo)  
> **Especificación Viva del Sistema:** [29. Ingesta Continua y Auto-Descubrimiento de Novedades BGG en el Lote Nocturno](file:///c:/repos/Ludeka/docs/specs/sistema/29-ingesta-continua-novedades-bgg.md)

---

## 1. Motivación y Visión
Ludeka busca ofrecer a la comunidad hispanohablante de juegos de mesa una base de datos viva, contemporánea y exhaustiva. Mientras que INC-41 resolvió la ingesta masiva de los 8.000 juegos históricos más valorados y las fotos de GeekDo/R2, el sistema carecía de un mecanismo desatendido para **detectar activamente los nuevos juegos que se publican o cobran tracción internacional en BoardGameGeek**.

Con este incremento se implementó el motor de auto-descubrimiento de novedades y tendencias BGG, permitiendo que el lote nocturno o una acción administrativa manual escanee el *Hotness* y lanzamientos del año en BGG, filtre los títulos desconocidos y los encole automáticamente para su catalogación y enriquecimiento editorial con IA.

---

## 2. Alcance Implementado y Verificado
1. **Contratos y Dominio**:
   - Nuevos valores en enum `CatalogQueueOrigin`: `BggNewReleases (3)` y `BggHotness (4)`.
   - Propiedad `BggDiscoveryCount` en `NightlyCatalogingExecutionLog` y `NightlyCatalogingResultDto`.
   - Interfaz `IBggDiscoveryService` con métodos `DiscoverAndEnqueueBggTrendsAsync` y `DiscoverRecentReleasesAsync`.
   - DTOs de resultados del descubrimiento (`BggDiscoveryResultDto`).
2. **Servicio de Descubrimiento (`BggDiscoveryService`)**:
   - Consulta a `IBggClient.FetchTopGamesAsync` (`/xmlapi2/hot?type=boardgame`) y filtrado por años actuales (`DateTime.UtcNow.Year` y anterior).
   - Triple verificación anti-duplicados:
     - Catálogo existente en `IGameRepository`.
     - Cola pendiente/en proceso en `IPendingBggImportRepository`.
     - Staging masivo en `IBggCatalogStagingRepository`.
   - Inserción en `PendingBggImport` con el origen correspondiente y prioridad ajustada.
3. **Orquestación en Lote Nocturno (`NightlyCatalogingService`)**:
   - Integración de la Fase 1.5 en el flujo diario.
   - Registro del total de juegos descubiertos en la bitácora (`NightlyCatalogingExecutionLog.BggDiscoveryCount`).
4. **UI de Administración Editorial (`CatalogQueueAdmin.razor`)**:
   - Botón de acción con feedback en vivo: *"Escanear Novedades BGG"*.
   - Filtro de visualización por origen para aislar `BggNewReleases` y `BggHotness`.
   - Badges editoriales accesibles con iconos Lucide (`TrendingUp`, `Sparkles`), cumpliendo contratos de marcado libre de emojis.
5. **Fixtures y Tests**:
   - Suite completa de 968 pruebas unitarias (+8 tests nuevos) pasando al 100% sin dependencias externas de mock.
