# Propuesta Técnica — INC-102: Auto-vinculación Inmediata de Expansiones y Síntesis Asistida de Aporte Lúdico con IA

## 1. Motivación y Visión

Cuando los usuarios amplían su colección en Ludeka buscando títulos en BoardGameGeek, la experiencia debe ser fluida e integral. Si un usuario añade una expansión (como *Everdell: New Leaf*), el sistema debe:
1. Reconocerla como expansión y conectarla de inmediato con su juego base existente (*Everdell*), habilitando de inmediato el banner de juego padre y la navegación entre expansiones hermanas.
2. Proporcionar un aporte comprensible y de calidad sobre lo que añade al juego base (mecánicas, necesidad, impacto en jugadores y tiempo), evitando contenedores vacíos y permitiendo generar la síntesis mediante IA tanto en la ingesta como bajo demanda.

## 2. Decisiones Arquitectónicas

### D1. Auto-vinculación bidireccional en la ingesta individual (`BggSearchAssistedService`)
- Al añadir una expansión: se consulta el snapshot satélite para obtener el BGG ID del juego base (`inbound`). Si el juego base ya está en el catálogo, se asocia `BaseGameId` atómicamente antes o después de la inserción.
- Al añadir un juego base: se extraen sus enlaces de expansiones (`outbound`) del snapshot y se busca en catálogo si existen expansiones huérfanas esperando ser asociadas, asignándoles el nuevo `BaseGameId`.
- Idempotencia: si una expansión ya catalogada se añade posteriormente a la colección de otro usuario y seguía huérfana, se reintenta la vinculación de forma oportunista.

### D2. Extensión del servicio de IA para Aportes de Expansión (`IAiGameSummaryService`)
- Se define el DTO `ExpansionAporteAiDto` (`WhatItBringsSummary`, `Necessity`, `ImpactTags`, `ExtraPlayerCount`, `ExtraDurationMinutes`).
- Se amplía el contrato `IAiGameSummaryService` con `GenerateExpansionAporteAsync(Game expansion, Game? baseGame, CancellationToken ct)`.
- Se implementa en `GeminiGameSummaryService` mediante Google Gemini Flash con esquema JSON estructurado y respaldo heurístico determinista (`HeuristicExpansionAporteGenerator`).

### D3. Estado vacío y acción interactiva en `ExpansionAporteCard.razor`
- Si la expansión no cuenta con datos de aporte, en lugar de un bloque en blanco con título, se presenta un estado vacío informativo que explica que el aporte está pendiente de curación.
- Para administradores/moderadores (o bajo demanda), se añade un botón directo con estado de carga para disparar la generación asistida con IA, guardando el resultado en base de datos y refrescando la tarjeta de forma reactiva.

## 3. Impacto en Almacenamiento y Esquema
- Cero cambios en el esquema de base de datos (`Game` ya dispone de `WhatItBringsSummary`, `ExpansionNecessity`, `ImpactTags`, `ExtraPlayerCount`, `ExtraDurationMinutes`, `BaseGameId`).
- Compatible con SQLite y PostgreSQL en Supabase.
