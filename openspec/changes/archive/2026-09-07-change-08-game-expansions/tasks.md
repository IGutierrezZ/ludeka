# Checklist de Tareas: change-08-game-expansions (Incremento 8: Fichas de Expansión, Ecosistema y Compatibilidad Lúdica)

## Fase 1: Dominio (`Ludeka.Core`)
- [x] 1.1 Crear enums de dominio:
  - [x] `src/Ludeka.Core/Enums/GameType.cs` (`BaseGame`, `Expansion`, `StandaloneExpansion`)
  - [x] `src/Ludeka.Core/Enums/ExpansionNecessity.cs` (`MustHave`, `HighlyRecommended`, `Situational`, `OnlyForFans`, `Dispensable`)
  - [x] `src/Ludeka.Core/Enums/ExpansionImpactTag.cs` (`AddsPlayers`, `ImprovesTwoPlayers`, `FixesBalance`, `AddsSoloMode`, etc.)
  - [x] `src/Ludeka.Core/Enums/ExpansionSynergyLevel.cs` (`PerfectCombo`, `CompatibleWithCaution`, `Incompatible`)
- [x] 1.2 Crear entidades de sinergias y recetas:
  - [x] `src/Ludeka.Core/Entities/ExpansionSynergy.cs` (relación par-a-par con motivo y método `MatchesPair`)
  - [x] `src/Ludeka.Core/Entities/ExpansionRecipe.cs` (packs predefinidos de mesa)
- [x] 1.3 Extender la entidad `Game.cs`:
  - [x] Añadir propiedades `Type`, `BaseGameId`, `BaseGame`, `Expansions`, `ExpansionNecessity`, `ImpactTags`, `WhatItBringsSummary`, `ExtraPlayerCount`, `ExtraDurationMinutes`
  - [x] Actualizar constructores preservando compatibilidad hacia atrás
- [x] 1.4 Pruebas unitarias de Dominio:
  - [x] `tests/Ludeka.UnitTests/Domain/GameExpansionDomainTests.cs` (6 tests en verde)

---

## Fase 2: Aplicación y DTOs (`Ludeka.Application`)
- [x] 2.1 Crear DTOs de expansiones y compatibilidad:
  - [x] `src/Ludeka.Application/DTOs/ExpansionDtos.cs` (`ExpansionSummaryDto`, `ExpansionDetailDto`, `ExpansionSynergyDto`, `ExpansionRecipeDto`, `ExpansionMixerEvaluationDto`)
- [x] 2.2 Extender DTOs existentes:
  - [x] `src/Ludeka.Application/DTOs/GameDetailDto.cs` (soporte para datos de expansión y juego base)
  - [x] `src/Ludeka.Application/DTOs/GameSummaryDto.cs` (soporte para `GameType` y título base)
  - [x] `src/Ludeka.Application/DTOs/GameFilterCriteria.cs` (filtro `GameType? TypeFilter`)
- [x] 2.3 Definir contratos de repositorio y servicio:
  - [x] `src/Ludeka.Application/Contracts/IExpansionRepository.cs`
  - [x] `src/Ludeka.Application/Contracts/IExpansionService.cs`
- [x] 2.4 Implementar servicio de negocio:
  - [x] `src/Ludeka.Application/Features/Expansions/ExpansionService.cs` (cálculo de compatibilidad, evaluador de mezclador de mesa y recetas)
- [x] 2.5 Pruebas unitarias de Aplicación:
  - [x] `tests/Ludeka.UnitTests/Application/ExpansionServiceTests.cs` (3 tests en verde)
  - [x] `tests/Ludeka.UnitTests/Application/CatalogServiceExpansionFilterTests.cs` (1 test en verde)

---

## Fase 3: Infraestructura y Persistencia (`Ludeka.Infrastructure`)
- [x] 3.1 Actualizar `LudekaDbContext.cs`:
  - [x] Registrar `DbSet<ExpansionSynergy>` y `DbSet<ExpansionRecipe>`
  - [x] Configurar relación reflexiva `Game -> BaseGame / Expansions`
  - [x] Configurar mapeos JSON para `ImpactTags` e `IncludedExpansionIds`
  - [x] Crear índices para optimizar consultas de sinergias y expansiones
- [x] 3.2 Implementar `SqliteExpansionRepository.cs`:
  - [x] `src/Ludeka.Infrastructure/Repositories/SqliteExpansionRepository.cs`
- [x] 3.3 Registrar servicios y repositorios en `Program.cs` / DI:
  - [x] Registrar `IExpansionRepository` y `IExpansionService`
- [x] 3.4 Actualizar el Seeder de catálogo (`CatalogSeeder.cs`):
  - [x] Precargar expansiones oficiales reales con carátula, datos y aportes:
    - Wingspan: Europa, Oceanía y Asia
    - Terraforming Mars: Preludio y Hellas & Elysium
    - Carcassonne: Posadas y Catedrales y Constructores y Comerciantes
  - [x] Precargar sinergias par-a-par reales y recetas de mesa
  - [x] Precargar vídeos específicos y valoraciones para las expansiones
- [x] 3.5 Pruebas unitarias de Infraestructura:
  - [x] `tests/Ludeka.UnitTests/Infrastructure/SqliteExpansionRepositoryTests.cs` (3 tests en verde)

---

## Fase 4: Frontend Blazor e Integración UI (`Ludeka.Web`)
- [x] 4.1 Componentes de Ecosistema y Aporte:
  - [x] `src/Ludeka.Web/Components/Shared/ExpansionEcosystemSection.razor` (3 pestañas en la ficha del juego base: Disponibles, Mezclador de Mesa y Recetas)
  - [x] `src/Ludeka.Web/Components/Shared/ExpansionAporteCard.razor` (tarjeta de aporte editorial y métricas en la ficha de la expansión)
  - [x] `src/Ludeka.Web/Components/Shared/ExpansionSisterList.razor` (expansiones hermanas con badges de compatibilidad directa)
  - [x] `src/Ludeka.Web/Components/Shared/ParentGameBanner.razor` (banner superior de regreso al juego base)
- [x] 4.2 Integrar en `GameDetail.razor`:
  - [x] Si es `BaseGame`: mostrar `ExpansionEcosystemSection`
  - [x] Si es `Expansion`: mostrar banner de juego base, tarjeta de aporte, expansiones hermanas, vídeos propios y reviews propias
- [x] 4.3 Integrar en `GameCard.razor` y `Home.razor`:
  - [x] Badge visual `🧩 Expansión` en las tarjetas de juego
  - [x] Filtro rápido en el selector del catálogo: `Todos | 🎲 Juegos Base | 🧩 Expansiones`

---

## Fase 5: Verificación Integral y Cierre
- [x] 5.1 Ejecución de pruebas automatizadas con .NET 10 SDK (`dotnet test src/Ludeka.slnx` -> 170 pruebas superadas al 100%)
- [x] 5.2 Generar reporte de verificación (`verify-report.md`)
- [x] 5.3 Actualizar hoja de ruta (`ROADMAP_MVP_SLICES.md`)
- [x] 5.4 Registrar sesión y aprendizajes en memoria persistente Engram
