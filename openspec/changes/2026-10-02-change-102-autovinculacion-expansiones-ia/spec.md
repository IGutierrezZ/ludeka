# Especificación Funcional y Técnica — INC-102: Auto-vinculación Inmediata de Expansiones y Síntesis Asistida de Aporte con IA

## 1. Requisitos Funcionales

- **RF-01: Auto-vinculación de expansiones al añadir desde BGG:**  
  Al añadir un juego desde el buscador asistido (`BggSearchAssistedService.AddGameToCollectionAsync`):
  - Si el juego descargado o catalogado es de tipo `GameType.Expansion`, el sistema debe consultar su snapshot satélite para extraer el identificador BGG del juego base (`inboundBaseBggId`). Si el juego base ya existe en el catálogo, debe asignar de inmediato `SetBaseGameId(baseGame.Id)`.
  - Si el juego descargado o catalogado es de tipo `GameType.BaseGame`, el sistema debe consultar los enlaces salientes (`outbound`) de expansiones en su snapshot. Si en el catálogo existen expansiones que referencian a este juego base y tienen `BaseGameId == null`, deben ser vinculadas inmediatamente.
  - La operación debe ser resiliente: si el snapshot no contiene enlaces o el juego base aún no está en catálogo, no debe abortar la ingesta ni la agregación a la colección.

- **RF-02: Generación de síntesis asistida de aporte de expansión con IA (`IAiGameSummaryService`):**  
  - Se debe proveer el método `GenerateExpansionAporteAsync(Game expansion, Game? baseGame = null, CancellationToken ct = default)` en `IAiGameSummaryService`.
  - El DTO resultante `ExpansionAporteAiDto` debe estructurar:
    - `WhatItBringsSummary` (resumen redactado en castellano de 1 a 2 párrafos sobre mecánicas nuevas, dinámicas y valor añadido frente a la caja base).
    - `Necessity` (`ExpansionNecessity`: MustHave, HighlyRecommended, Situational, OnlyForFans, Dispensable).
    - `ImpactTags` (`IReadOnlyList<ExpansionImpactTag>`: AddsPlayers, AddsSoloMode, AddsAsymmetry, ModularContent, FixesBalance, etc.).
    - `ExtraPlayerCount` (incremento de comensales, o null/0).
    - `ExtraDurationMinutes` (incremento de minutos de partida, o null/0).
  - La implementación en `GeminiGameSummaryService` debe utilizar Google Gemini Flash con esquema estructurado estricto cuando esté configurada la API Key y no esté en modo simulación.
  - Debe integrarse un respaldo heurístico determinista (`HeuristicExpansionAporteGenerator`) para modo simulación, pruebas unitarias y tolerancia a fallos de API externa (Zero-Crash Fallback).

- **RF-03: Ingesta automática de aporte para nuevas expansiones:**  
  - Al añadir una expansión en `BggSearchAssistedService`, si el servicio de IA está disponible, debe generarse y asignarse automáticamente el aporte de expansión en la entidad `Game` antes de guardarla.

- **RF-04: Estado vacío (*empty state*) en `ExpansionAporteCard.razor`:**  
  - Si una expansión en catálogo no dispone de datos de aporte (`WhatItBringsSummary` nulo o vacío y sin etiquetas de impacto), el componente debe renderizar un estado vacío informativo y cuidado estéticamente.
  - Se debe indicar con claridad que el aporte al juego base está pendiente de veredicto editorial.
  - Para usuarios con permisos de edición/moderación (o administradores), debe habilitarse un botón interactivo «✨ Generar Aporte con IA» con feedback de carga (*spinner* / estado deshabilitado) que ejecute la síntesis y actualice la tarjeta de forma reactiva sin recargar la página.

## 2. Criterios de Aceptación

1. Al añadir una expansión cuyo juego base ya está en el catálogo a través de `AddGameToCollectionAsync`, la propiedad `BaseGameId` queda establecida con el `Guid` del juego base en base de datos.
2. Al añadir un juego base que tiene expansiones huérfanas en catálogo, estas adquieren automáticamente el `BaseGameId` del nuevo juego base.
3. `GenerateExpansionAporteAsync` genera un veredicto de aporte coherente tanto en modo heurístico como con Gemini API.
4. La ficha de expansión (`/juegos/{slug}`) muestra el estado vacío si no tiene aporte cargado, eliminando la anomalía de la tarjeta vacía con solo la cabecera.
5. El botón de generación asistida en la ficha invoca el servicio, persiste en BD y actualiza el componente visualmente.
6. La suite completa de pruebas unitarias pasa al 100% sin regresiones.
