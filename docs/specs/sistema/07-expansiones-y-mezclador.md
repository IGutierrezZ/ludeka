# 07. Expansiones, Ecosistema y Mezclador de Mesa

## 1. Visión General y Propósito
Este módulo dota a las expansiones de ficha propia, metadatos y valoraciones independientes, vinculación bidireccional con el juego base, tarjeta editorial de aportes, matriz de sinergia par-a-par y un Mezclador interactivo de mesa con detección de sobrecarga.

---

## 2. Modelo de Dominio Polimórfico (`Ludeka.Core`)

### 2.1 Atributos de Expansión en `Game`
Ubicación: [`src/Ludeka.Core/Entities/Game.cs`](file:///c:/repos/Ludeka/src/Ludeka.Core/Entities/Game.cs)

- `Type`: Enum `GameType` (`BaseGame`, `Expansion`, `StandaloneExpansion`).
- `BaseGameId`: Guid opcional que referencia al juego base.
- `ExpansionNecessity`: Enum `ExpansionNecessity` (`MustHave`, `Recommended`, `OnlyForCompletionists`, `Avoid`).
- `ImpactTags`: Colección de [`ExpansionImpactTag`](file:///c:/repos/Ludeka/src/Ludeka.Core/Enums/ExpansionImpactTag.cs) (`AddsPlayers`, `FixesBalance`, `AddsSoloMode`, `ModularContent`, `NarrativeCampaign`, `ImprovesTwoPlayers`, `AddsVariability`).
- `WhatItBringsSummary`: Resumen editorial de aportes lúdicos.
- `ExtraPlayerCount`: Incremento en el número máximo de jugadores.
- `ExtraDurationMinutes`: Minutos adicionales aproximados que suma a la partida.

### 2.2 Entidad `ExpansionSynergy` (Matriz Par-a-Par)
Ubicación: [`src/Ludeka.Core/Entities/ExpansionSynergy.cs`](file:///c:/repos/Ludeka/src/Ludeka.Core/Entities/ExpansionSynergy.cs)

- `BaseGameId`, `ExpansionIdA`, `ExpansionIdB`.
- `Level`: Enum `ExpansionSynergyLevel`:
  - `PerfectCombo`: Sinergia excelente y recomendada.
  - `CompatibleWithCaution`: Compatibles con advertencias de reglas o sobrecarga.
  - `IncompatibleRedundant`: Módulos que se solapan o no deben jugarse juntos.
- `Explanation`: Descripción narrativa de la compatibilidad entre ambas.

### 2.3 Entidad `ExpansionRecipe` (Recetas de Mesa)
Ubicación: [`src/Ludeka.Core/Entities/ExpansionRecipe.cs`](file:///c:/repos/Ludeka/src/Ludeka.Core/Entities/ExpansionRecipe.cs)

- Packs prediseñados para configuraciones específicas (ej. *"Duelo Táctico a 2"*, *"El Ecosistema Completo"*).
- `BaseGameId`, `Title`, `Description`, `TargetProfile`, `ExpansionIds`.

---

## 3. Motor de Evaluación y Auto-Vinculación (`Ludeka.Application`)

- **Contrato:** [`IExpansionService`](file:///c:/repos/Ludeka/src/Ludeka.Application/Contracts/IExpansionService.cs) implementado en [`ExpansionService.cs`](file:///c:/repos/Ludeka/src/Ludeka.Application/Features/Expansions/ExpansionService.cs).
- **Evaluación del Mezclador (`EvaluateMixerSelectionAsync`):**
  - Calcula el tiempo total resultante sumando la duración base más los deltas de cada expansión elegida.
  - Detecta sobrecarga si el incremento supera **+45 minutos**.
  - Detecta sobrecarga si se seleccionan más de **2 expansiones de alto impacto**.
  - Evalúa la matriz de sinergias par-a-par para advertir de incompatibilidades entre los módulos seleccionados.
- **Auto-Vinculación Bidireccional Asistida (INC-102):**
  - Ubicación: [`BggSearchAssistedService.cs`](file:///c:/repos/Ludeka/src/Ludeka.Application/Features/Bgg/BggSearchAssistedService.cs).
  - Al incorporar una expansión vía búsqueda asistida de BGG en `/mi-ludoteca`, consulta el snapshot crudo satélite [`BggRawSnapshot`](file:///c:/repos/Ludeka/src/Ludeka.Core/Entities/BggRawSnapshot.cs) y resuelve el enlace entrante (`inbound="true"`, `@type="boardgameexpansion"`), vinculando automáticamente el `BaseGameId` con el juego base existente en catálogo.
  - Si entra un juego base y existen expansiones huérfanas ya importadas, se vinculan retroactivamente de manera inmediata.
  - Genera automáticamente el aporte editorial mediante [`IAiGameSummaryService.GenerateExpansionAporteAsync`](file:///c:/repos/Ludeka/src/Ludeka.Application/Contracts/IAiGameSummaryService.cs).

---

## 4. Componentes UI (`Ludeka.Web`)

- [`ParentGameBanner.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/ParentGameBanner.razor): Banner en la cabecera de la ficha de expansión para navegar al juego base.
- [`ExpansionAporteCard.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/ExpansionAporteCard.razor): Tarjeta de aportes con badges de impacto y necesidad.
  - **Estado Vacío y Acción IA (INC-102):** Si la expansión carece de aportes editoriales cargados, despliega un *empty state* con iconografía `sparkles` y un botón interactivo para generar la síntesis de aporte al instante con feedback visual de carga.
- [`ExpansionSisterList.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/ExpansionSisterList.razor): Carrusel de expansiones hermanas.
- [`ExpansionEcosystemSection.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/ExpansionEcosystemSection.razor): Sección editorial limpia en el juego base (INC-130, alineación Claude Design):
  - **Cuadrícula Editorial de 2 Columnas:** Sustituye el carrusel y las subpestañas por una cuadrícula directa de 2 columnas (`grid grid-cols-1 md:grid-cols-2 gap-4`) con tarjetas contenidas (`rounded-2xl bg-[var(--paper-2)]`).
  - **Tarjetas de Expansión Limpias:**
    - Carátula nítida con dimensiones explícitas anti-CLS (64x64 píxeles, `loading="lazy"`, `decoding="async"`).
    - Etiqueta de necesidad simplificada («Opcional», «Muy recomendada», «Imprescindible», «Para completistas», «Prescindible»).
    - Título editorial enlazado a la ficha de la expansión con efecto hover.
    - Metadatos claros: año de publicación y cálculo del total de jugadores alcanzable con el juego base (`hasta X jugadores`).
    - Botón interactivo de colección: «+ A mi ludoteca» / «En mi ludoteca» (con icono `check` verde), sincronizado de forma reactiva con `IUserLibraryService`.
  - **Retirada del Mezclador en Mesa:** Se elimina la sobrecarga cognitiva del mezclador interactivo, recetas y botones de scroll por JavaScript de la pestaña de consulta para ofrecer una experiencia editorial directa y ligera.
  - **Cabecera Limpia en `GameDetail.razor`:** Título `Expansiones` sin el bloque numérico obsoleto `04` ni el sufijo redundante `& Dónde Comprar` (las tiendas residen en la barra lateral fija). Píldora con contador exacto de expansiones disponibles.
