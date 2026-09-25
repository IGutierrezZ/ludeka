# Documento Vivo ODD — INC-67: Gamificación — Hitos y Logros del Jugador

> **Feature:** `hitos-y-logros`  
> **Fichero:** `odd/tasks/inc-67-hitos-y-logros.md` (fuente de verdad operativa)  
> **Incremento:** INC-67  
> **Rama:** `inc/hitos-y-logros`  
> **Worktree:** `C:\repos\ludeka-wt\hitos-y-logros`  
> **Creado:** 2026-09-25 · **Ruta:** rama `inc/hitos-y-logros` → PR a `main`  
> **TDD Mode:** Strict TDD (RED ➔ GREEN ➔ REFACTOR)  
> **Línea Base:** 1.817 pruebas unitarias en verde (0 fallos)  

---

## 1. Objetivo

Reconocer el progreso y la vivencia lúdica del jugador en Ludeka mediante un sistema formal, cálido y no invasivo de **Hitos y Logros**:
1. Diseñar el modelo de dominio para hitos de usuario (`UserMilestone`) con catálogo canónico (`MilestoneCatalog`), categorías lúdicas (Colección, Partidas/Diario, Comunidad) e invariantes rigurosas.
2. Implementar un motor de evaluación y desbloqueo idempotente y fechado (`IMilestoneService`), capaz de conceder logros automáticamente a partir de eventos o estados del usuario (colección, diario de partidas, micro-reseñas, me gusta) preservando la fecha original de obtención.
3. Garantizar persistencia atómica y consultas eficientes tanto en SQLite como en PostgreSQL mediante `IUserMilestoneRepository` y actualización de esquema en `SqliteSchemaMigrator`.
4. Definir por escrito y respetar en el código la convivencia con `ComputePlayerBadge` (INC-15): el badge calcula el Rango y Rasgo global de la estantería, mientras que los hitos son medallas individuales de logros alcanzados.
5. Diseñar la presentación visual en `PublicProfile.razor` y en el área de cuenta (`/cuenta`), con estética editorial moderna y accesible (WCAG 2.2 AA), distinguiendo hitos desbloqueados de pendientes sin recurrir a patrones tóxicos de ludificación (cero rankings competitivos entre usuarios, cero streaks con penalización; frontera estricta con INC-68).

---

## 2. Problema y Diagnóstico Previo

1. **Ausencia de sistema de logros en el repositorio:**
   Solo existe un precursor básico en `UserLibraryStatsService.ComputePlayerBadge`, que infiere un rango estático (Iniciado, Explorador, etc.) y un rasgo por estilo de juego. No existe historial de hitos, ni desbloqueo, ni fecha de consecución, ni retos individuales.
2. **Acciones lúdicas sin reconocimiento persistido:**
   El diario de partidas (INC-30), los estados de colección (Tengo/Jugado/Deseado/Prestar), las micro-reseñas y los «me gusta» comunitarios (INC-65) registran actividad valiosa que nunca se consolida en el progreso personal del jugador.
3. **Frontera necesaria con INC-68:**
   Es imprescindible que INC-67 no derive en comparaciones entre usuarios, clasificaciones públicas ni leaderboards, los cuales corresponden a INC-68 bajo requisitos de privacidad y consentimiento.

---

## 3. Alcance

### Dentro de Alcance:
- **ODD-1 — Dominio (`Ludeka.Core`): Entidad `UserMilestone`, Tipos y Catálogo de Hitos**
  - Entidad inmutable `UserMilestone` con `UserId`, `MilestoneType`, `UnlockedAt` y validación de invariantes.
  - Enumerado `MilestoneType` y categorías `MilestoneCategory` (Colección, Partidas, Comunidad).
  - Catálogo de hitos canónicos `MilestoneCatalog` con metadatos descriptivos (título, descripción, emoji/icono, orden, categoría).
  - Pruebas unitarias en `UserMilestoneTests.cs` bajo Strict TDD.
- **ODD-2 — Persistencia e Infraestructura (`Ludeka.Infrastructure`): Repositorio y Migración**
  - Contrato `IUserMilestoneRepository` en `Ludeka.Application.Contracts`.
  - Implementación `UserMilestoneRepository` en `Ludeka.Infrastructure.Data.Repositories` con `DbSet<UserMilestone>` en `LudekaDbContext`.
  - Mapeo relacional e índices en `LudekaDbContext` e incorporación en `SqliteSchemaMigrator.cs` (`CREATE TABLE IF NOT EXISTS UserMilestones`).
  - Pruebas unitarias en `UserMilestoneRepositoryTests.cs`.
- **ODD-3 — Lógica de Aplicación (`Ludeka.Application`): Servicio de Evaluación e Idempotencia**
  - DTOs `MilestoneDto` y `UserMilestoneProgressDto`.
  - Servicio `MilestoneService` / `IMilestoneService` que evalúa colección, partidas jugadas, préstamos, reseñas y me gusta para conceder hitos de forma idempotente y fechada.
  - Integración o complementariedad con `IUserLibraryStatsService` para exponer los hitos en el perfil de usuario.
  - Pruebas unitarias en `MilestoneServiceTests.cs`.
- **ODD-4 — Presentación Editorial (`Ludeka.Web`): Vitrina de Hitos y Logros**
  - Componente accesible `MilestonesCard.razor` (o `UserMilestonesSection.razor`) con diseño editorial cálido, medallas desbloqueadas fechadas y vista de hitos por descubrir.
  - Integración en `PublicProfile.razor` y en el área de cuenta (`/cuenta`).
  - Pruebas unitarias de componentes web (`MilestonesCardTests.cs`).
- **ODD-5 — Verificación Final, Roadmap y Sincronización Documental**
  - Suite de pruebas unitarias 100% verde (`dotnet test`).
  - Actualización de `ROADMAP.md` y `ROADMAP_MVP_SLICES.md`.
  - Volcado a especificación viva `docs/specs/sistema/` y actualización del índice `README.md`.
  - Traslado de incremento a `docs/increments/archive/inc-67-hitos-y-logros.md`.

### Fuera de Alcance:
- Clasificaciones y comparaciones públicas entre usuarios (reservado para INC-68).
- Retos temporales, recompensas económicas, dinero virtual o rachas de conexión diaria con penalización.

---

## 4. Checklist de Tareas (IDs Estables)

- [x] **ODD-1 — Dominio (`Ludeka.Core`): Entidad `UserMilestone`, Tipos y Catálogo de Hitos**
  - [x] 1.1 Tests en `UserMilestoneTests.cs` (RED): validación de constructor, rechazo de `UserId` vacío, validación de enumerado `MilestoneType`, invariantes de fecha e inmutabilidad.
  - [x] 1.2 Implementar enum `MilestoneType`, `MilestoneCategory` y entidad `UserMilestone` en `Ludeka.Core`.
  - [x] 1.3 Implementar `MilestoneCatalog` con catálogo de hitos canónicos (Primer juego, 10 juegos, Primer juego jugado, Primer préstamo, Primera partida en diario, 5 partidas en diario, Mesa llena con 5+ comensales, Primera micro-reseña, Primer me gusta).
  - [x] 1.4 Verificación en verde (GREEN) y refactorización limpia (REFACTOR). 1.827 pruebas unitarias verdes.
- [x] **ODD-2 — Persistencia e Infraestructura (`Ludeka.Infrastructure`): Repositorio y Esquema**
  - [x] 2.1 Tests en `SqliteUserMilestoneRepositoryTests.cs` (RED): persistencia de hitos, recuperación por usuario, idempotencia al guardar (no duplicar `(UserId, Type)`).
  - [x] 2.2 Contrato `IUserMilestoneRepository` en `Ludeka.Application.Contracts`.
  - [x] 2.3 Implementar `SqliteUserMilestoneRepository` y configurar entidad en `LudekaDbContext`.
  - [x] 2.4 Actualizar `SqliteSchemaMigrator.cs` y `supabase_schema.sql` con la creación de la tabla `UserMilestones` y sus índices.
  - [x] 2.5 Verificación en verde (GREEN) y refactorización (REFACTOR). 1.833 pruebas unitarias verdes.
- [x] **ODD-3 — Lógica de Aplicación (`Ludeka.Application`): Evaluación e Idempotencia**
  - [x] 3.1 Tests en `MilestoneServiceTests.cs` (RED): evaluación de condiciones de desbloqueo, preservación de fecha original en llamadas sucesivas, retorno de progreso ordenado.
  - [x] 3.2 DTOs de transporte (`MilestoneDto`, `UserMilestoneProgressDto`) e interfaz `IMilestoneService`.
  - [x] 3.3 Implementar `MilestoneService`: evaluar reglas cruzando `IUserCollectionRepository`, `IGamePlayLogRepository`, `IUserReviewRepository`, `IUserLikeRepository`, `IGameLoanRepository`.
  - [x] 3.4 Convivencia armoniosa con `UserLibraryStatsService` y DI en `DependencyInjection.cs`.
  - [x] 3.5 Verificación en verde (GREEN) y refactorización (REFACTOR). 1.839 pruebas unitarias verdes.
- [x] **ODD-4 — Presentación Editorial (`Ludeka.Web`): Vitrina de Hitos y Logros**
  - [x] 4.1 Tests en `MilestonesCardTests.cs` (RED): renderizado de hitos desbloqueados con fecha, hitos pendientes con silueta/bloqueo accesible y porcentaje de progreso.
  - [x] 4.2 Crear componente `MilestonesCard.razor` con estilo editorial hogareño de Ludeka (WCAG 2.2 AA).
  - [x] 4.3 Integrar en `PublicProfile.razor` y en `/cuenta` (sección accesible para el propio usuario).
  - [x] 4.4 Verificación en verde (GREEN) y refactorización (REFACTOR). 1.843 pruebas unitarias verdes.
- [ ] **ODD-5 — Verificación Global, Documentación y Cierre**
  - [ ] 5.1 Ejecución completa de suite de pruebas unitarias (`dotnet test`: suite verde, 0 fallos).
  - [ ] 5.2 Actualizar `docs/increments/ROADMAP.md` y `docs/specs/ROADMAP_MVP_SLICES.md`.
  - [ ] 5.3 Volcado a la especificación viva `docs/specs/sistema/` y actualización del índice `README.md`.
  - [ ] 5.4 Mover `docs/increments/inc-67-hitos-y-logros.md` a `docs/increments/archive/inc-67-hitos-y-logros.md`.
  - [ ] 5.5 Commit de cierre y resumen en Engram.
