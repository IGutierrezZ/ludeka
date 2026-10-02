# Exploración de Arquitectura — INC-102: Auto-vinculación de Expansiones y Síntesis Asistida de Aporte con IA

## 1. Contexto y Diagnóstico del Problema

Un usuario que importa una expansión oficial desde el buscador asistido de BGG en su ludoteca personal (`/mi-ludoteca` -> `BggSearchModal`) observa dos anomalías críticas:

1. **La expansión no se vincula a su juego base:**  
   `BggXmlParser.ParseItem` detecta correctamente `item type="boardgameexpansion"` y marca el tipo como `GameType.Expansion`. Sin embargo, `BggSearchAssistedService.AddGameToCollectionAsync` descarga el juego y lo persiste directamente en el catálogo mediante `_gameRepo.AddRangeAsync([fetchedGame])` sin consultar el snapshot crudo satélite de BGG, sin extraer el identificador `inbound` del juego base (`BggRawSnapshotParser.ExtractInboundBaseGameBggIdFromJson`), y sin buscar si dicho juego base ya reside en el catálogo local para invocar `SetBaseGameId(baseGame.Id)`. Tampoco contempla el caso inverso (cuando se añade un juego base y ya existían expansiones huérfanas en el catálogo esperando ser asociadas).

2. **El apartado «¿Qué aporta al Juego Base?» en la ficha de expansión queda completamente vacío:**  
   En la ficha de juego (`GameDetail.razor`), cuando `Game.IsExpansion` es verdadero se renderiza el componente `ExpansionAporteCard.razor`. Este componente consume metadatos editoriales propios de Ludeka (`WhatItBringsSummary`, `ExpansionNecessity`, `ImpactTags`, `ExtraPlayerCount`, `ExtraDurationMinutes`). BGG no proporciona estos campos en su API. Como consecuencia, al no existir datos editoriales y carecer el componente de un diseño para estado vacío (*empty state*), la tarjeta se dibuja únicamente con la cabecera «¿Qué aporta al Juego Base?» y un contenedor en blanco sin información.

## 2. Puntos de Impacto en el Código

- **`Ludeka.Core`:**  
  - `Game.cs`: Asegurar método de conveniencia `SetExpansionAporte(necessity, impactTags, summary, extraPlayerCount, extraDurationMinutes)` para mutar los datos editoriales del aporte sin requerir `baseGameId` si ya está fijado.
  - Enums `ExpansionNecessity` y `ExpansionImpactTag`: ya definidos y probados.

- **`Ludeka.Application`:**  
  - `BggSearchAssistedService.cs`:
    - Inyectar `IBggRawSnapshotRepository?` opcional.
    - Al importar un juego nuevo:
      - Si es expansión, resolver `inboundBaseBggId` desde el snapshot crudo recién persistido y vincular `BaseGameId` si el juego base ya existe en catálogo.
      - Si es juego base, buscar si hay expansiones huérfanas en catálogo presentes en sus enlaces `outbound` y vincularlas.
    - Si el juego ya existía pero era una expansión huérfana (`BaseGameId == null`), intentar resolver la vinculación al asociarlo a la colección.
    - Al importar una expansión, si se dispone de servicio de IA, disparar la generación asistida del aporte.
  - `IAiGameSummaryService.cs` y DTOs (`ExpansionAporteAiDto`):
    - Añadir métodos para generar y asegurar el aporte de expansión: `GenerateExpansionAporteAsync` y `EnsureExpansionAporteAsync`.

- **`Ludeka.Infrastructure`:**  
  - `GeminiGameSummaryService.cs`:
    - Implementar `GenerateExpansionAporteAsync` con prompt especializado y estructurado (JSON Schema) para Gemini Flash.
    - Implementar generador heurístico `HeuristicExpansionAporteGenerator` como respaldo de fallo cero (*Zero-Crash Fallback*) y soporte en pruebas/simulación.

- **`Ludeka.Web`:**  
  - `ExpansionAporteCard.razor`:
    - Incorporar estado vacío visual (*empty state*) elegante con mensaje explicativo cuando no existan datos editoriales.
    - Añadir botón de acción interactivo para moderadores/usuarios con permiso para generar el aporte mediante IA bajo demanda.
  - `GameDetail.razor`:
    - Cablear la llamada de generación de aporte por IA en tiempo real y refresco reactivo de la ficha.

## 3. Plan de Verificación

1. Pruebas unitarias en `Ludeka.UnitTests/Application/BggSearchAssistedServiceTests.cs` validando:
   - Auto-vinculación de expansión a juego base existente al añadir a colección.
   - Auto-vinculación de expansiones huérfanas cuando se añade un juego base.
   - Resiliencia si el snapshot no está disponible o el juego base no existe aún en catálogo.
2. Pruebas unitarias para `GeminiGameSummaryService` y `HeuristicExpansionAporteGenerator` verificando generación determinista de resumen, necesidad, tags y deltas.
3. Pruebas de componentes Razor en `ExpansionAporteCardTests` verificando el estado vacío cuando los campos son nulos y la interacción con el disparador de IA.
