# 10. Módulo de Síntesis con IA (Google Gemini / Heurística)

## 1. Visión General y Propósito
El Módulo de Síntesis con IA proporciona a Ludeka resúmenes inteligentes, objetivos y estructurados en español (`🤖 Resumen generado por IA`) para juegos recién catalogados o pendientes de análisis por la Mesa Fundadora. Responde a los tres ejes críticos del jugador moderno: escalabilidad y comensales ideales, accesibilidad y edad real, y huella en mesa/ritmo de juego.

---

## 2. Modelo de Dominio (`Ludeka.Core`)

### 2.1 Value Object `AiGameSummary`
Ubicación: [`src/Ludeka.Core/ValueObjects/AiGameSummary.cs`](file:///c:/repos/Ludeka/src/Ludeka.Core/ValueObjects/AiGameSummary.cs)

- `GeneralVerdict`: Texto narrativo en español objetivo y conciso (2-3 oraciones).
- `ScalabilitySummary`: Rango de comensales óptimo y comportamiento del entreturno.
- `AgeSummary`: Comparativa entre edad legal de componentes (caja) y edad real observada.
- `FootprintSummary`: Superficie de salón requerida (cafetería, mesa estándar o monstruo de mesa) y duración por comensal.
- `Model`: Nombre del modelo o motor que produjo la síntesis (`"Google Gemini (gemini-2.5-flash)"`, `"Heurística Editorial"`, `"Heurística Editorial (Fallback)"`).
- `GeneratedAt`: Marca de tiempo UTC de generación.

### 2.2 Entidad `Game`
- Propiedad: `AiSummary` (`AiGameSummary?`).
- Mutador: `SetAiSummary(AiGameSummary summary)`.

---

## 3. Capa de Aplicación (`Ludeka.Application`)

### 3.1 Contrato `IAiGameSummaryService`
Ubicación: [`src/Ludeka.Application/Contracts/IAiGameSummaryService.cs`](file:///c:/repos/Ludeka/src/Ludeka.Application/Contracts/IAiGameSummaryService.cs)

- `GenerateSummaryAsync(Game game, CancellationToken ct)`: Genera la síntesis estructurada.
- `EnsureSummaryForGameAsync(Guid gameId, CancellationToken ct)`: Recupera la síntesis persistida o la genera y persiste bajo demanda en el catálogo.
- `ProcessPendingSummariesBatchAsync(int batchSize, CancellationToken ct)`: Procesa por lotes (carga nocturna) juegos del catálogo sin resumen.

### 3.2 Desacoplo de la Ficha y Ejecución en Descarga BGG / Cargas Nocturnas
- **Navegación Ordinaria en Ficha (`FoundingVerdictService.GetAiSummaryForGameAsync`):** La carga de fichas es 100% libre de llamadas externas a la IA; únicamente lee de SQLite el `AiSummary` pre-existente. Si no existe, devuelve `null` sin latencia.
- **Botón de Moderación Bajo Demanda (`FoundingVerdictService.RequestAiSummaryGenerationAsync`):** Reservado para usuarios con rol `Moderator` o `FoundingTeam`. Permite generar o regenerar la síntesis de IA para una ficha específica bajo demanda.
- **Descargas desde BGG:**
  - [`BggCatalogQueueService.cs`](file:///c:/repos/Ludeka/src/Ludeka.Application/Features/Bgg/BggCatalogQueueService.cs): Al procesar la cola comunitaria nocturna, genera y almacena la síntesis de IA de forma atómica.
  - [`BggSearchAssistedService.cs`](file:///c:/repos/Ludeka/src/Ludeka.Application/Features/Bgg/BggSearchAssistedService.cs): Al incorporar un juego descargado en vivo de BGG, ejecuta la síntesis de IA y la persiste en el catálogo.
- **Cargas Nocturnas en Lote:** [`GeminiGameSummaryService.ProcessPendingSummariesBatchAsync`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/Services/GeminiGameSummaryService.cs) itera por lotes sobre títulos no sintetizados en el catálogo.

---

## 4. Capa de Infraestructura (`Ludeka.Infrastructure`)

### 4.1 Opciones de Configuración (`GeminiOptions`) y Resolutor Dinámico (`IGeminiModelResolver`)
Ubicación: [`src/Ludeka.Infrastructure/Services/GeminiOptions.cs`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/Services/GeminiOptions.cs), [`IGeminiModelResolver.cs`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/Services/IGeminiModelResolver.cs) y [`GeminiModelResolver.cs`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/Services/GeminiModelResolver.cs)
- `Simulate`: Conmutador booleano (por defecto `true` para desarrollo seguro).
- `ApiKey`: Clave de API de Google AI Studio.
- `Model`: Configurado en `"auto"` por defecto (o vacío) para delegar la selección al autodescubrimiento dinámico empezando por el modelo más barato (`DefaultModel = "gemini-3.5-flash-lite"`). Si se especifica un identificador explícito distinto de `"auto"`, dicho modelo encabeza la cascada.
- `DefaultFallbackModels`: Cadena estática de respaldo de emergencia con los modelos vigentes ordenados de menor a mayor coste (`["gemini-3.5-flash-lite", "gemini-3.8-flash", "gemini-3.7-flash", "gemini-3.6-flash"]`).
- `BaseUrl`: Endpoint REST de Google Gemini (`https://generativelanguage.googleapis.com/v1beta`).
- `ShouldSimulate`: Propiedad evaluada (`Simulate || string.IsNullOrWhiteSpace(ApiKey)`).
- **Autodescubrimiento Dinámico (`GeminiModelResolver`, INC-150):**
  - Consulta `GET {BaseUrl}/models?key={ApiKey}` y cachea el catálogo en memoria durante **6 horas** (o 15 minutos de enfriamiento si la consulta de listado falla, cayendo a `DefaultFallbackModels`).
  - Filtra exclusivamente modelos generativos (`supportedGenerationMethods` contiene `"generateContent"`) que cumplan el patrón estable `^gemini-(?<version>\d+(?:\.\d+)?)-(?<tier>flash-lite|flash)$` con `version >= 3.5`, descartando automáticamente modelos obsoletos (`1.5`, `2.0`, `2.5`, `3.1-flash-lite`), variantes `-preview`/`-exp`, audio/imagen/vídeo (`-tts`, `-live`, `-image`, `-transcribe`) y la familia `pro` (para evitar picos de facturación).
  - **Ordenación por Escalón de Precio:** Clasifica siempre primero la familia `flash-lite` (\$0.30 / \$2.50 por 1M tokens) y a continuación la familia `flash` (\$0.75 / \$3.75 en 3.8/3.7/3.6), ordenando de versión más reciente a más antigua dentro de cada escalón.
  - **Estado de Salud y *Cooldown* en Memoria:**
    - `HTTP 404 NotFound` / `400 BadRequest`: Excluye el modelo durante 6 horas para no reintentar modelos retirados.
    - `HTTP 429 TooManyRequests` / `503 ServiceUnavailable`: Penaliza el modelo durante 5 minutos relegándolo al final de la cola (detrás de todos los modelos sanos) sin dejar la cadena vacía.

### 4.2 Motor Heurístico Resiliente (`HeuristicGameSummaryGenerator`)
- Genera resúmenes deterministas en español analizando las características del juego sin depender de internet ni de tokens.

### 4.3 Cascada Unificada de Modelos y Tolerancia a Fallos (`GeminiGameSummaryService`, `GeminiSocialAnalysisService`, `GeminiReleaseMatcherService`)
- Los tres servicios de IA consumen `IGeminiModelResolver` (registrado como `Singleton` en DI):
  - [`GeminiGameSummaryService`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/Services/GeminiGameSummaryService.cs): Síntesis editorial individual, síntesis por lotes (`CallGeminiBatchApiAsync`) y análisis de aporte de expansiones (`CallGeminiExpansionAporteApiAsync`).
  - [`GeminiSocialAnalysisService`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/Services/GeminiSocialAnalysisService.cs): Extracción de texto y visión multimodal para sorteos, novedades y eventos.
  - [`GeminiReleaseMatcherService`](file:///c:/repos/Ludeka/src/Ludeka.Infrastructure/Services/GeminiReleaseMatcherService.cs): Inferencia asistida de correspondencias BGG para lanzamientos editoriales.
- Ante cualquier fallo HTTP (`404`, `429`, `503`), el servicio invoca `_modelResolver.ReportModelFailure(model, statusCode)` y salta inmediatamente al siguiente modelo candidato de la cascada en la misma operación; al completarse con éxito, invoca `_modelResolver.ReportModelSuccess(model)`.
- **Zero-Crash Fallback:** Si `ShouldSimulate == true` o si toda la cascada de modelos falla en flujos con respaldo heurístico, conmuta de forma segura al generador heurístico determinista.

### 4.4 Persistencia EF Core y SQLite
- `LudekaDbContext.cs`: Mapeo propio JSON `game.OwnsOne(g => g.AiSummary, b => b.ToJson());`.
- `SqliteSchemaMigrator.cs`: Reconciliación defensiva de la columna `AiSummary` (`TEXT NULL`) en la tabla `Games`.

---

## 5. Interfaz de Usuario (`Ludeka.Web`)

- Componente [`AiSummaryCard.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/AiSummaryCard.razor):
  - Badge principal `🤖 Síntesis generada por IA`.
  - Chip con el modelo utilizado (`Google Gemini (gemini-3.6-flash)` o `Heurística Editorial`).
  - Tres paneles visuales limpios para escalabilidad, edad y huella/tiempo.
  - Veredicto general y aviso editorial transparente.
- En [`GameDetail.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Pages/GameDetail.razor):
  - Carga inmediata sin peticiones de IA para usuarios generales.
  - Botón de moderación `🤖 Generar con IA` / `🔄 Regenerar con IA` exclusivo para moderadores/fundadores.
  - Bloque editorial informativo con disparador manual cuando el juego carece de veredicto y de síntesis.
- En [`CatalogQueuePanel.razor`](file:///c:/repos/Ludeka/src/Ludeka.Web/Components/Shared/CatalogQueuePanel.razor):
  - Botón administrativo `🌙 Carga Nocturna IA` para ejecutar el lote de enriquecimiento de catálogo bajo demanda con reporte de resultados en tiempo real.
