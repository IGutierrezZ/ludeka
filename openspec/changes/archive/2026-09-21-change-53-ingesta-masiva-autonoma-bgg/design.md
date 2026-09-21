# Documento de Diseño Técnico — INC-53: Ingesta Masiva Autónoma de Catálogo BGG (~8.000 Juegos) sin Manipulación Manual

> **Fase:** `sdd-design` · **Fecha:** 2026-09-21  
> **Worktree:** `C:\repos\ludeka-wt\ingesta-masiva-autonoma-bgg`, rama `inc/ingesta-masiva-autonoma-bgg`, base `main` en `6d3b510`  
> **Entrada:** `openspec/changes/2026-09-21-change-53-ingesta-masiva-autonoma-bgg/specs/autonomous-bgg-catalog-ingestion/spec.md`

---

## 1. Visión General de Arquitectura

El diseño desacopla y automatiza el aprovisionamiento masivo de la tabla intermedia `BggCatalogStaging`. En lugar de exigir a un operador humano que busque, descargue y cargue un archivo CSV local, el servidor toma la responsabilidad integral de:
1. Resolver la URL pública del dataset diario de clasificación de BoardGameGeek.
2. Descargar el flujo de datos mediante streaming HTTP continuo con memoria acotada (< 30 MB RAM).
3. Filtrar de forma eficiente los títulos con masa crítica comunitaria (`usersrated >= 30`).
4. Insertar e indexar en la base de datos relacional mediante lotes de 100 registros con idempotencia.
5. Permitir la ejecución interactiva desde el panel de control `/admin/cola-catalogacion` (con autorización INC-46) y la ejecución desatendida desde `Ludeka.Jobs` y `NightlyCatalogingService`.

```
                    ┌──────────────────────────────────────────────┐
                    │  Fastly CDN / GitHub Raw (Público y Diario)  │
                    │   beefsack/bgg-ranking-historicals           │
                    │   {yyyy-MM-dd}.csv (~7,1 MB, ~31.300 filas)  │
                    └──────────────────────┬───────────────────────┘
                                           │ HTTP GET Streaming (ResponseHeadersRead)
                                           ▼
┌──────────────────────────────────────────────────────────────────────────────────┐
│                             Ludeka.Application                                   │
│                                                                                  │
│  BggMassIngestionService.DownloadAndIngestLatestRanksAsync                       │
│    ├── 1. Resolución de fecha: DateTime.UtcNow -> Fallback hasta 5 días         │
│    ├── 2. BggDumpParser.ParseRanksDumpAsync (Stream continuo, CanSeek == false) │
│    ├── 3. Filtro: usersrated >= 30 (~8.000 títulos relevantes)                   │
│    └── 4. Inserción por lotes (100 ítems) -> IBggCatalogStagingRepository        │
└───────────────────────┬──────────────────────────────────┬───────────────────────┘
                        │                                  │
                        ▼                                  ▼
        ┌───────────────────────────────┐  ┌───────────────────────────────────────┐
        │          Ludeka.Web           │  │              Ludeka.Jobs              │
        │    /admin/cola-catalogacion   │  │   Cloud Run Jobs / Cloud Scheduler    │
        │   Botón + Progreso Reactivo   │  │   seed-staging | nightly-cataloging   │
        │  (Exige CanEditGames INC-46)  │  │         (Canal de Sistema)            │
        └───────────────────────────────┘  └───────────────────────────────────────┘
```

---

## 2. Contratos y Clases de Aplicación (`Ludeka.Application`)

### 2.1. Opciones de Configuración (`BggMassIngestionOptions`)
Ubicación: `src/Ludeka.Application/DTOs/BggMassIngestionDtos.cs`

Se incorporan nuevas propiedades para parametrizar la descarga remota:

```csharp
public class BggMassIngestionOptions
{
    public const string SectionName = "BggMassIngestion";

    public int MinUsersRated { get; set; } = 30;
    public int FetchBatchSize { get; set; } = 20;
    public int ImagesBatchSize { get; set; } = 10;
    public int AiBatchSize { get; set; } = 8;
    public int PromotionBatchSize { get; set; } = 50;
    public bool Simulate { get; set; } = false;

    /// <summary>
    /// Plantilla URL para descargar el volcado CSV de ranks de BGG. Formato string con marcador {0:yyyy-MM-dd}.
    /// Por defecto apunta al mirror diario público en GitHub Raw / Fastly CDN.
    /// </summary>
    public string RanksDumpUrlPattern { get; set; } =
        "https://raw.githubusercontent.com/beefsack/bgg-ranking-historicals/master/{0:yyyy-MM-dd}.csv";

    /// <summary>
    /// Número máximo de días a retroceder en caso de que la fecha actual no esté disponible (HTTP 404).
    /// </summary>
    public int MaxFallbackDays { get; set; } = 5;
}
```

### 2.2. Interfaz `IBggMassIngestionService`
Ubicación: `src/Ludeka.Application/Contracts/IBggMassIngestionService.cs`

Se añaden los dos puntos de entrada para la ingesta autónoma:

```csharp
public interface IBggMassIngestionService
{
    // ... métodos existentes ...

    /// <summary>
    /// Descarga y procesa de forma autónoma el volcado más reciente de clasificación de BGG.
    /// Exige sesión de usuario y permiso de edición de fichas (INC-46, W1).
    /// </summary>
    Task<int> DownloadAndIngestLatestRanksAsync(int? minUsersRated = null, CancellationToken ct = default);

    /// <summary>
    /// Versión de sistema para ejecutor en segundo plano (Ludeka.Jobs) sin la guarda de sesión de usuario.
    /// </summary>
    Task<int> RunScheduledDownloadAndIngestLatestRanksAsync(int? minUsersRated = null, CancellationToken ct = default);
}
```

### 2.3. Robustez de Streaming en `BggDumpParser`
Ubicación: `src/Ludeka.Application/Features/Bgg/BggDumpParser.cs`

En la línea 30, la comprobación de cabecera GZip asume `inputStream.CanSeek`. En streams HTTP, `CanSeek` es `false`.
El diseño ajusta la comprobación para que:
1. Si `inputStream.CanSeek` es verdadero y la longitud permite inspección, evalúa magic bytes `0x1F, 0x8B`.
2. Si `inputStream.CanSeek` es falso (flujo HTTP continuo), asume que es texto plano UTF-8 (formato estándar del CSV en GitHub raw), o envuelve en un lector que no intente posicionamiento (`Seek` / `Position = 0`).
3. El `StreamReader` lee el flujo directamente sin intentar operaciones de búsqueda (`CanSeek`).

### 2.4. Implementación en `BggMassIngestionService`
Ubicación: `src/Ludeka.Application/Features/Bgg/BggMassIngestionService.cs`

```csharp
public async Task<int> DownloadAndIngestLatestRanksAsync(int? minUsersRated = null, CancellationToken ct = default)
{
    await RequirePermissionAsync(ct);
    return await ExecuteDownloadAndIngestAsync(minUsersRated, ct);
}

public Task<int> RunScheduledDownloadAndIngestLatestRanksAsync(int? minUsersRated = null, CancellationToken ct = default)
{
    return ExecuteDownloadAndIngestAsync(minUsersRated, ct);
}

private async Task<int> ExecuteDownloadAndIngestAsync(int? minUsersRated, CancellationToken ct)
{
    int threshold = minUsersRated.HasValue && minUsersRated.Value > 0 ? minUsersRated.Value : _options.MinUsersRated;

    if (_options.Simulate)
    {
        _logger.LogInformation("Modo simulado activo: ingiriendo dataset sintético de catálogo BGG en staging.");
        return await IngestSyntheticSampleAsync(threshold, ct);
    }

    var (stream, resolvedDate) = await OpenLatestRanksStreamAsync(ct);
    using (stream)
    {
        _logger.LogInformation("Procesando volcado BGG correspondiente a la fecha {Date:yyyy-MM-dd} con umbral usersrated >= {MinVotes}",
            resolvedDate, threshold);

        return await IngestRanksDumpAsync(stream, threshold, ct);
    }
}
```

#### Algoritmo de Resolución de Fecha con Fallback (`OpenLatestRanksStreamAsync`):
1. Inicia en `currentDate = DateTime.UtcNow.Date`.
2. Itera desde `attempt = 0` hasta `_options.MaxFallbackDays`.
3. Construye la URL evaluando `string.Format(CultureInfo.InvariantCulture, _options.RanksDumpUrlPattern, currentDate)`.
4. Emite petición HTTP `HEAD` (o `GET` con `HttpCompletionOption.ResponseHeadersRead`).
5. Si responde `200 OK`, devuelve el `Stream` del cuerpo (`response.Content.ReadAsStreamAsync(ct)`) y la fecha resuelta.
6. Si responde `404 Not Found`, registra un log de información y decrementa `currentDate = currentDate.AddDays(-1)`.
7. Si se agotan todos los intentos sin éxito, lanza `InvalidOperationException("No se encontró ningún volcado disponible de BGG en el rango de fechas comprobado...")`.

---

## 3. Auto-Siembra en `NightlyCatalogingService` y `Ludeka.Jobs`

### 3.1. Auto-Siembra Inteligente en el Ciclo Nocturno
Ubicación: `src/Ludeka.Application/Features/Bgg/NightlyCatalogingService.cs`

En la Fase 3 del ciclo nocturno (`RunScheduledCatalogingAsync`):
```csharp
if (_massIngestionService != null)
{
    try
    {
        var metrics = await _massIngestionService.GetMetricsAsync(ct);
        if (metrics.TotalInStaging == 0)
        {
            _logger.LogInformation("Fase 3: Staging está vacío. Disparando auto-siembra inicial autónoma de catálogo BGG.");
            int seededCount = await _massIngestionService.RunScheduledDownloadAndIngestLatestRanksAsync(ct: ct);
            _logger.LogInformation("Auto-siembra completada: {Count} títulos incorporados a staging.", seededCount);
        }

        var drainResult = await _massIngestionService.RunScheduledDrainCycleAsync(ct);
        topBackfillCount += drainResult.PromotedToCatalogCount;
        _logger.LogInformation("Fase 3 (Staging): {Result}", drainResult.Message);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Advertencia durante la ingesta/drenaje de staging: {Message}", ex.Message);
    }
}
```

### 3.2. Subcomando `seed-staging` en `Ludeka.Jobs`
Ubicación: `src/Ludeka.Jobs`

1. **`JobNames.cs`**:
   ```csharp
   public const string SeedStaging = "seed-staging";

   public static readonly IReadOnlyList<string> All =
   [
       NightlyCataloging,
       PriceRadar,
       SocialCollector,
       NotificationOutbox,
       SeedStaging
   ];
   ```
2. **`SeedStagingJobRunner.cs`**:
   Implementa `IJobRunner`:
   - `Name => JobNames.SeedStaging`
   - `RunAsync(CancellationToken ct)`:
     Invoca `_massIngestionService.RunScheduledDownloadAndIngestLatestRanksAsync(ct: ct)` a través de `_coordinator.ExecuteWithWindowLeaseAsync` con ventana horaria o diaria para garantizar ejecución única si se invoca concurrentemente.

---

## 4. Interfaz de Usuario en `/admin/cola-catalogacion` (`CatalogQueueAdmin.razor`)

En la sección "Ingesta Masiva BGG & Staging (~8.000 títulos)" (líneas 215-246):
- Se añade el botón:
  ```razor
  <button type="button"
          @onclick="ExecuteAutonomousStagingSeedAsync"
          disabled="@(_isSeedingStaging || _isDrainingStaging)"
          class="px-3.5 py-2 rounded-xl bg-[var(--bg-surface-elevated)] border border-[var(--border-subtle)] hover:border-[var(--brand-primary)] text-xs font-bold text-[var(--text-primary)] transition-all flex items-center gap-1.5 shadow-sm disabled:opacity-50">
      @if (_isSeedingStaging)
      {
          <span class="animate-spin inline-block"><Icon Name="loader" Size="14" /></span>
          <span>Descargando y Poblando (~8.000)...</span>
      }
      else
      {
          <Icon Name="download" Size="14" class="text-sky-500" />
          <span>Descargar y Poblar Catálogo BGG</span>
      }
  </button>
  ```
- Al hacer clic:
  - Establece `_isSeedingStaging = true`.
  - Invoca `await MassIngestionService.DownloadAndIngestLatestRanksAsync()`.
  - Al completar, actualiza las métricas con `LoadDataAsync()` y muestra un mensaje informativo temporal:
    *«Catálogo BGG descargado e incorporado en staging: X títulos procesados.»*

---

## 5. Pruebas y Casos de Verificación

Se creará la suite `tests/Ludeka.UnitTests/Application/BggMassIngestionAutonomousDownloadTests.cs`:
1. `DownloadAndIngest_UsesTodayDate_WhenAvailable`: Simula respuesta HTTP 200 para la fecha de hoy; verifica inserción en staging.
2. `DownloadAndIngest_FallsBackToPreviousDate_WhenTodayIs404`: Simula 404 hoy y 200 ayer; verifica que resuelve la fecha de ayer y puebla staging.
3. `DownloadAndIngest_ThrowsException_WhenAllFallbackDaysFail`: Simula 404 en todos los días; verifica que lanza `InvalidOperationException`.
4. `DownloadAndIngest_FiltersByMinUsersRated`: Verifica que los títulos con `usersrated < 30` son descartados y no llegan al repositorio.
5. `DownloadAndIngest_RequiresPermission`: Verifica que la versión interactiva invoca `RequirePermissionAsync` de `ISessionPermissionGuard`.
6. `DownloadAndIngest_InSimulateMode_DoesNotUseNetwork`: Verifica que con `Simulate = true` no se realizan llamadas HTTP y se pueblan datos sintéticos.
7. `BggDumpParser_HandlesNonSeekableStream`: Verifica lectura desde un `Stream` con `CanSeek == false`.
8. `SeedStagingJobRunner_ExecutesServiceAndReturnsOutcome`: Verifica que el runner de `Ludeka.Jobs` ejecuta el método de sistema.
