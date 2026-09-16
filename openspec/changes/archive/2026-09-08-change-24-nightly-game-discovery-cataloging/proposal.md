# Propuesta de Cambio: change-24-nightly-game-discovery-cataloging

## 1. Resumen Ejecutivo
El **Incremento 24** implementa el pipeline de detección de juegos en novedades editoriales y el orquestador inteligente de catalogación nocturna asistido por BGG y Gemini:
1. **Extractor de Juegos en Novedades (`INewsGameExtractor`):**
   - Extrae automáticamente el título del juego o expansión mencionado en titulares o notas de novedades editoriales (`WeeklyRelease`).
   - Comprueba su existencia en el catálogo local de Ludeka vinculándolo directamente si ya existe.
   - Si no existe en Ludeka, busca coincidencias en BGG Search API y encola el juego en `PendingBggImports` con origen `NewsDiscovery`.
2. **Orquestador Nocturno Inteligente de Ingesta (`INightlyCatalogingService`):**
   - Servicio programado para ejecutarse en segundo plano con cupo diario configurable (`DailyCatalogingLimit = 20`).
   - Procesa los juegos prioritarios encolados (usuarios y novedades).
   - Si los juegos en cola no alcanzan el cupo de 20 diarios, consulta los juegos con mejor ranking de BGG no catalogados y completa el cupo restante hasta alcanzar exactamente los 20 diarios.
   - Aplica pausas de cortesía (mínimo 2.5s entre llamadas) protegiendo las cuotas de BGG XMLAPI2 y Google Gemini.
3. **Panel de Supervisión en Administración (`/admin/cola-catalogacion`):**
   - Panel accesible para moderadores (`CanEditGames`) y Mesa Fundadora con métricas de la cola, histórico de ejecuciones nocturnas, desglose por origen y ejecución manual bajo demanda.

---

## 2. Justificación y Valor para el Ecosistema
1. **Catálogo Autónomo y Siempre Actualizado:** Ludeka no depende de que los usuarios soliciten manualmente cada juego nuevo anunciado por editoriales como Devir o Maldito Games. El sistema detecta el anuncio, verifica BGG y prepara la ficha automáticamente.
2. **Crecimiento Orgánico del Catálogo de Referencia:** El relleno automático nocturno hasta 20 juegos garantiza que los títulos más aclamados del ranking mundial de BGG se incorporen de forma constante y ordenada al catálogo en español.
3. **Máxima Resiliencia y Respeto a Cuotas Externas:** Al controlar estrictamente el volumen diario (20 títulos) y espaciar las peticiones con 2.5s de delay, se eliminan los riesgos de baneos por IP (HTTP 429) en BGG y se gestionan eficientemente los tokens de Gemini.
4. **Transparencia y Control para la Mesa Fundadora:** La bandeja de supervisión `/admin/cola-catalogacion` permite auditar cada ingesta, revisar juegos originados en noticias y forzar ejecuciones en caliente si se precisa.

---

## 3. Alcance de la Propuesta por Capas

### 3.1 Dominio (`Ludeka.Core`)
- **`CatalogQueueOrigin`:** Nuevo enum en `Ludeka.Core/Enums/CatalogQueueOrigin.cs`:
  - `UserImport = 0`: Juego solicitado por un usuario en su importación de BGG.
  - `NewsDiscovery = 1`: Juego detectado automáticamente a partir de una novedad editorial.
  - `TopBggBackfill = 2`: Juego incorporado en el relleno automático nocturno desde el Top de BGG.
- **`PendingBggImport`:**
  - Nueva propiedad `Origin`: `public CatalogQueueOrigin Origin { get; private set; }`.
  - Nueva propiedad `ExtractedTitle`: `public string? ExtractedTitle { get; private set; }`.
  - Constructores y métodos auxiliares preservando compatibilidad hacia atrás.
- **`WeeklyRelease`:**
  - Método de mutación de dominio: `public void LinkGame(Guid gameId)`.
- **`NightlyCatalogingExecutionLog`:**
  - Nueva entidad en `Ludeka.Core/Entities/NightlyCatalogingExecutionLog.cs` para auditar ejecuciones del batch (`Id`, `StartedAt`, `CompletedAt`, `QueueProcessedCount`, `NewsDiscoveryCount`, `TopBackfillCount`, `TotalCatalogedCount`, `FailedCount`, `CatalogedTitles`, `Status`, `ErrorMessage`).

### 3.2 Aplicación (`Ludeka.Application`)
- **`INewsGameExtractor` y `NewsGameExtractor`:**
  - Métodos para analizar títulos de novedades editoriales con patrones de expresiones regulares y coincidencia en catálogo / BGG.
  - Método `DiscoverAndEnqueueFromReleasesAsync` para procesar publicaciones sin vincular.
- **`INightlyCatalogingService` y `NightlyCatalogingService`:**
  - Orquestador del ciclo completo nocturno:
    1. Extracción en novedades pendientes.
    2. Ingesta de cola prioritaria (`PendingBggImports`) hasta el cupo configurado.
    3. Relleno con Top de BGG si el procesado es menor al cupo.
    4. Guardado de log de ejecución en `INightlyCatalogingLogRepository`.
  - Pausa configurable entre peticiones (`MinDelaySecondsBetweenCalls = 2.5`).
- **`IBggClient`:**
  - Nueva firma: `Task<IReadOnlyList<BggTopGameDto>> FetchTopGamesAsync(int limit = 50, CancellationToken ct = default);`.
- **DTOs y Opciones:**
  - `NightlyCatalogingOptions` (`DailyCatalogingLimit`, `MinDelaySecondsBetweenCalls`, `ExecutionHourUtc`, `Enabled`).
  - `BggTopGameDto`, `NightlyCatalogingResultDto`, `NightlyCatalogingExecutionLogDto`.
  - Actualización de `CatalogQueueItemDto` incorporando `Origin` y `ExtractedTitle`.
- **`INightlyCatalogingLogRepository`:**
  - Contrato para consulta e inserción de la bitácora de ejecuciones nocturnas.

### 3.3 Infraestructura (`Ludeka.Infrastructure`)
- **`BggSimulationDataset` y `SimulatedBggClient`:**
  - Implementación de `FetchTopGamesAsync` extrayendo los juegos ordenados por `BggRank`.
- **`BggXmlApiClient`:**
  - Implementación de `FetchTopGamesAsync` consultando `/xmlapi2/hot?type=boardgame`.
- **`LudekaDbContext`:**
  - Nuevo `DbSet<NightlyCatalogingExecutionLog> NightlyCatalogingExecutionLogs`.
- **`SqliteSchemaMigrator`:**
  - Migración idempotente para `Origin` y `ExtractedTitle` en `PendingBggImports` y creación de tabla `NightlyCatalogingExecutionLogs`.
- **`SqliteNightlyCatalogingLogRepository`:**
  - Implementación EF Core / SQLite del repositorio de logs.
- **`NightlyCatalogingHostedService`:**
  - `BackgroundService` que programa la ejecución a la hora configurada (03:00 UTC) o periodicidad configurada con token de cancelación.

### 3.4 Presentación Web (`Ludeka.Web`)
- **`/admin/cola-catalogacion` (`CatalogQueueAdmin.razor`):**
  - Vista exclusiva para moderadores (`CanEditGames`) y Mesa Fundadora.
  - Métricas clave en tarjetas: Cupo diario, Juegos en cola, Descubrimientos de novedades, Último lote procesado.
  - Botón interactivo para ejecutar la catalogación nocturna manual bajo demanda.
  - Tabla de la cola de catalogación con filtro por estado y origen (`Usuario`, `Novedad`, `Top BGG`).
  - Tabla de historial de ejecuciones nocturnas.
- **Navegación en `MainLayout.razor`:**
  - Enlace rápido "Cola BGG" para moderadores autorizados.

---

## 4. Estrategia de Pruebas
- Pruebas unitarias de dominio para `PendingBggImport` con origen y `WeeklyRelease.LinkGame`.
- Pruebas unitarias para `NewsGameExtractor` verificando patrones editoriales ("Devir anuncia la edición en castellano de Apiary", comillas, titulares directos, etc.), vinculación a juegos locales y encolado en BGG.
- Pruebas unitarias para `NightlyCatalogingService`:
  - Escenario 1: Procesa elementos pendientes de la cola.
  - Escenario 2: Rellena el cupo restante hasta 20 con el Top de BGG.
  - Escenario 3: Respeta el límite total sin duplicar juegos ya catalogados.
  - Escenario 4: Auditoría en log de ejecución nocturna.
- Pruebas de integración para `SimulatedBggClient.FetchTopGamesAsync`.
