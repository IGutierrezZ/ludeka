# Propuesta: change-08-game-expansions (Incremento 8: Fichas de Expansión, Ecosistema y Compatibilidad Lúdica)

## 1. Resumen Ejecutivo y Motivación

En el universo de los juegos de mesa, las expansiones no son meros complementos secundarios: son extensiones críticas que alteran la profundidad, el balance, el número de jugadores e incluso salvan o reinventan un juego. Títulos emblemáticos como *Wingspan*, *Carcassonne* o *Terraforming Mars* cuentan con decenas de expansiones donde los jugadores se enfrentan constantemente a tres grandes dilemas:
1. **¿Qué aporta exactamente esta expansión al juego base y merece la pena comprarla?** (ej: *"¿Añade más jugadores?", "¿Es imprescindible para jugar a 2?", "¿Arregla el ritmo inicial?"*).
2. **¿Cómo interactúan y combinan las expansiones entre sí?** (ej: *"¿Puedo jugar la Expansión A y la B a la vez sin que la partida dure 4 horas o se pisen las reglas?"*).
3. **¿Cuál es la valoración y contenido audiovisual exclusivo de la expansión?** (unboxing, cómo se juega la expansión, micro-reseñas comunitarias independientes de la caja base).

Esta propuesta formaliza el **Incremento 8** de Ludeka bajo la metodología **Spec-Driven Development (SDD)**, dotando a las expansiones de identidad de primer nivel (ficha propia, vídeos y reviews independientes), vinculación bidireccional con su juego base nodriza, veredicto editorial de aporte y una herramienta pionera e interactiva: el **Mezclador de Mesa y Matriz de Compatibilidad entre Expansiones**.

---

## 2. Decisiones de Arquitectura y Modelo de Datos

### 2.1 Extensión de la Entidad `Game` (Herencia Conceptual de Primer Nivel)
Una expansión comparte el 90% de los atributos de un juego: tiene carátula, año, diseñador, editorial, puntuación BGG, puntuación Ludeka, medidas de fundas (`Sleeves`), vídeos de YouTube e Instagram (`MediaItems`), reseñas de usuarios (`UserGameReviews`) y estados de ludoteca (`UserCollectionItems`).

Por ello, en lugar de crear una tabla paralela desconectada, **la expansión es un `Game` con especialización de tipo y relación reflexiva**:
- **Nuevo Enum `GameType`:**
  - `BaseGame` (0): Juego base independiente.
  - `Expansion` (1): Expansión que requiere un juego base para ser jugada.
  - `StandaloneExpansion` (2): Expansión autojugable (funciona por sí sola o se combina con el base).
- **Propiedad `Guid? BaseGameId`:** Clave foránea opcional que apunta al `Game` nodriza (nulo si es `BaseGame`).
- **Navegación EF Core:**
  - `Game? BaseGame`
  - `List<Game> Expansions`
- **Atributos de Aporte y Veredicto de Expansión:**
  - `ExpansionNecessity? Necessity`:
    - `MustHave`: *"Imprescindible, mejora el juego base"*
    - `HighlyRecommended`: *"Muy recomendada"*
    - `Situational`: *"Recomendada según grupo / jugadores"*
    - `OnlyForFans`: *"Solo para completistas / muy cafeteros"*
    - `Dispensable`: *"Prescindible / Aporta poco"*
  - `List<ExpansionImpactTag> ImpactTags`:
    - `AddsPlayers` (+Jugadores)
    - `ImprovesTwoPlayers` (Imprescindible a 2)
    - `FixesBalance` (Corrige balance o ritmo)
    - `AddsSoloMode` (Modo solitario)
    - `AddsAsymmetry` (Asimetría / Facciones)
    - `ModularContent` (Módulos combinables)
    - `NewMapOrFactions` (Nuevos mapas / tableros)
    - `TightensTime` (Acorta o dinamiza la partida)
  - `string? WhatItBringsSummary`: Síntesis editorial concisa del aporte a la mesa.
  - `int? ExtraPlayerCount`: Modificador del número de jugadores respecto al base (ej. `+1` o `+2`).
  - `int? ExtraDurationMinutes`: Modificador del tiempo de partida (ej. `+15 min`).

> **Compatibilidad hacia atrás garantizada:** El constructor de `Game` añade estos parámetros como opcionales con valores por defecto (`gameType = GameType.BaseGame`, `baseGameId = null`), por lo que no se produce ningún breaking change en los 157 tests unitarios ni en los servicios existentes.

---

### 2.2 Entidad `ExpansionSynergy` (Matriz Par-a-Par de Compatibilidad)
Permite modelar con precisión quirúrgica cómo interactúan dos expansiones del mismo juego base:
- `Guid Id`
- `Guid BaseGameId` (FK hacia `Game`)
- `Guid ExpansionAId` (FK hacia `Game`)
- `Guid ExpansionBId` (FK hacia `Game`)
- `ExpansionSynergyLevel Level`:
  - 🟢 `PerfectCombo`: Sinergia excelente / Combo recomendado. Se integran limpiamente y enriquecen la mesa sin fricción.
  - 🟡 `CompatibleWithCaution`: Compatible pero con reservas. Genera sobrecarga de reglas, tiempo excesivo o saturación de espacio.
  - 🔴 `Incompatible`: Incompatibles / Excluyentes. Reglas contradictorias o reemplazo de los mismos componentes físicos.
- `string Reason`: Explicación lúdica de la sinergia o conflicto (ej. *"Ambas añaden módulos limpios que se integran en paralelo"* o *"Conflicto: Ambas intentan sustituir el mazo de objetivos centrales"*).

---

### 2.3 Entidad `ExpansionRecipe` (Recetas de Mesa Recomendadas)
Preajustes y combinaciones prediseñadas recomendadas por la mesa fundadora o la comunidad:
- `Guid Id`
- `Guid BaseGameId`
- `string Name`: Nombre sugerente del combo (ej: *"El Duelo Definitivo a 2"*, *"Setup Ágil de Torneo"*, *"La Gran Campaña Épica"*).
- `string Description`: Por qué funciona tan bien este conjunto.
- `string IdealFor`: Contexto ideal (ej. *"2 jugadores en 45 min"*, *"5-6 jugadores"*).
- `List<Guid> IncludedExpansionIds`: Lista de identificadores de las expansiones que componen la receta.

---

## 3. Capa de Aplicación (`Ludeka.Application`)

### 3.1 Contrato `IExpansionService` y DTOs
- `Task<IReadOnlyList<ExpansionSummaryDto>> GetExpansionsByBaseGameIdAsync(Guid baseGameId, CancellationToken ct = default);`
- `Task<ExpansionDetailDto?> GetExpansionDetailAsync(Guid expansionId, CancellationToken ct = default);`
- `Task<IReadOnlyList<ExpansionSynergyDto>> GetSynergiesForBaseGameAsync(Guid baseGameId, CancellationToken ct = default);`
- `Task<ExpansionMixerEvaluationDto> EvaluateCombinationAsync(Guid baseGameId, IEnumerable<Guid> selectedExpansionIds, CancellationToken ct = default);`
- `Task<IReadOnlyList<ExpansionRecipeDto>> GetRecipesByBaseGameIdAsync(Guid baseGameId, CancellationToken ct = default);`

### 3.2 DTOs Enriquecidos
- `ExpansionSummaryDto`: Resumen para tarjetas compactas (carátula, títulos, año, veredicto de necesidad, badges de aporte, rating propio, enlace a ficha).
- `ExpansionDetailDto`: Datos completos del aporte al juego base, métricas de impacto (+jugadores, +tiempo) y datos del juego base nodriza.
- `ExpansionSynergyDto`: Relación entre par de expansiones con nivel de sinergia y motivo.
- `ExpansionMixerEvaluationDto`: Resultado del diagnóstico en vivo del mezclador de mesa:
  - Nivel global de mesa (`Balanced`, `Caution`, `Conflict`).
  - Jugadores mínimos y máximos resultantes.
  - Tiempo estimado de partida con el conjunto seleccionado.
  - Lista de avisos de sinergia y conflictos detectados.

---

## 4. Experiencia de Usuario y Componentes UI (`Ludeka.Web`)

### 4.1 En la Ficha del Juego Base (`GameDetail.razor` cuando `Type == BaseGame`)
- **Sección Editorial "Expansiones y Ecosistema":**
  - **Pestaña 1: Expansiones Disponibles:**
    - Catálogo en cuadrícula con carátulas oficiales, títulos, badge de necesidad (`"🟢 Imprescindible, mejora el juego base"`), chips de qué aporta (`"+1 Jugador"`, `"Modo Solitario"`), rating propio de la expansión y botón táctil para ir a su ficha completa.
  - **Pestaña 2: Mezclador de Mesa y Diagnóstico:**
    - Selector interactivo táctil donde el usuario marca las expansiones que tiene o quiere jugar.
    - Tarjeta de diagnóstico reactiva en tiempo real:
      - 🟢 *"¡Mesa Perfecta! Esta combinación añade variedad sin entorpecer el ritmo de la partida."*
      - 🟡 *"Atención: Mesa Exigente (+120 min de partida y alta huella en mesa)."*
      - 🔴 *"Conflicto de componentes: Estas dos expansiones no se recomiendan juntas."*
  - **Pestaña 3: Recetas Recomendadas de Mesa:**
    - Listado de combinaciones predefinidas con 1 clic para cargar en el mezclador.

### 4.2 En la Ficha de la Expansión (`GameDetail.razor` cuando `Type == Expansion`)
- **Banner Superior de Ecosistema:**
  - Distintivo visual: `🧩 EXPANSIÓN OFICIAL`.
  - Tarjeta de cabecera con acceso directo al juego base: *"Requiere el juego base [Título del Juego Base]"* con portada miniatura, rating y enlace con un clic.
- **Tarjeta "¿Qué aporta al Juego Base?":**
  - Veredicto de compra destacado (ej. *"🟢 Imprescindible, mejora el juego base"*).
  - Píldoras de aporte lúdico (*"Agrega más jugadores"*, *"Imprescindible para 2 jugadores"*, etc.).
  - Resumen editorial detallado de qué cambia en la mesa.
  - Modificación de métricas: aumento del rango de jugadores y tiempo adicional.
- **Tarjeta "Otras Expansiones del Ecosistema":**
  - Listado de las expansiones hermanas, indicando para cada una su compatibilidad directa respecto a la que se está consultando (🟢 Sinergia Óptima, 🟡 Compatible con reservas, 🔴 Incompatible).
- **Hub Multimedia Propio:** Tutoriales y partidas exclusivas de la expansión.
- **Valoraciones Propias:** Los usuarios puntúan la expansión de 1 a 10 y escriben micro-reseñas sobre su calidad y rejugabilidad.
- **Acciones de Ludoteca:** Registrar si se posee la expansión, si se ha jugado o si está en la lista de deseos.

### 4.3 En el Catálogo General (`Catalog.razor` y `GameCard.razor`)
- Píldora identificativa `🧩 Expansión` en la carátula de las tarjetas.
- Filtro en la barra de búsqueda y filtros: selector para ver `Todos`, `Solo Juegos Base` o `Solo Expansiones`.

---

## 5. Precarga de Datos Reales (Seeding)

Para dotar al sistema de realismo lúdico inmediato en consonancia con la regla "Cero AI Slop", se precargarán expansiones reales con información de calidad:
- **Wingspan (Maldito Games):**
  - *Expansión Europa* (BggId 290448): "Imprescindible, mejora el juego base", nuevos poderes de fin de ronda y aves ibéricas.
  - *Expansión Oceanía* (BggId 300580): "Imprescindible, corrige balance", néctar y tableros de jugador rebalanceados.
  - *Expansión Asia* (BggId 366161): "Imprescindible para 2 jugadores", modo Dúo y bandada 6-7 jugadores.
  - Matriz de sinergia cruzada y recetas ("Duelo Rápido a 2", "El Wingspan Definitivo").
- **Terraforming Mars (Maldito Games):**
  - *Preludio* (BggId 247030): "Imprescindible, corrige ritmo de inicio", reduce 30 min la partida.
  - *Hellas & Elysium* (BggId 230914): "Muy recomendada", dos nuevos mapas e hitos.
  - Sinergias y receta de torneo.
- **Carcassonne (Devir):**
  - *Posadas y Catedrales* (BggId 2993): "Imprescindible, añade 6º jugador", meeple grande y losetas de riesgo.
  - *Constructores y Comerciantes* (BggId 8443): "Imprescindible, dinamiza la partida", cerdos y constructores (doble turno).
  - Sinergias y receta "Carcassonne Clásico Pro".

---

## 6. Plan de Verificación y Pruebas Unitarias
- Pruebas unitarias de Dominio: validación de la entidad `Game` con tipo expansión, cálculo de métricas modificadas, validación de `ExpansionSynergy`.
- Pruebas de Servicio: `ExpansionServiceTests` con SQLite en memoria, evaluador del mezclador de mesa (casos óptimos, advertencias y conflictos).
- Pruebas de Integración y Componentes Razor: renderizado del banner de juego base en la ficha de expansión, renderizado de la sección de expansiones en el juego base, y comportamiento del filtro de catálogo.
- Meta de verificación: Todos los tests nuevos y los 157 existentes en verde (0 fallos).
