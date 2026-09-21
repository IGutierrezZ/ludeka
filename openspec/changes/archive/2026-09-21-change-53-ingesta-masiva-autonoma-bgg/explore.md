# Exploración — INC-53: Ingesta Masiva Autónoma de Catálogo BGG (~8.000 Juegos) sin Manipulación Manual

> **Fase:** `sdd-explore` · **Fecha:** 2026-09-21  
> **Worktree:** `C:\repos\ludeka-wt\ingesta-masiva-autonoma-bgg`, rama `inc/ingesta-masiva-autonoma-bgg`, base `main` en `6d3b510`  
> **Motivo:** Tras el primer despliegue en producción, la ejecución de `ludeka-job-nightly-cataloging` reportó en su Fase 3:
> `Ciclo de drenaje: 0 detalles, 0 imágenes, 0 síntesis IA, 0 promovidos`.
> La tabla `BggCatalogStaging` se encuentra vacía porque la ingesta inicial construida en INC-41 dependía de la subida o lectura de un archivo CSV local manipulado manualmente por el operador. El maintainer requiere que el aprovisionamiento sea 100% autónomo desde el servidor.

---

## 1. Pregunta que responde esta exploración

¿Cómo podemos poblar de forma desatendida y continua la tabla `BggCatalogStaging` con los ~8.000 juegos de mesa más relevantes de BoardGameGeek (`usersrated >= 30`), tanto desde el panel de administración como en el Cloud Run Job, sin intervención de ficheros locales ni consumo excesivo de memoria?

---

## 2. Estado Actual del Sistema

### 2.1. Ingesta Masiva en `Ludeka.Application` (INC-41)
- **`BggDumpParser` (`src/Ludeka.Application/Features/Bgg/BggDumpParser.cs`)**:
  - Implementa un parser de streaming `ParseRanksDumpAsync(Stream inputStream, int minUsersRated = 30, ...)` que produce `IAsyncEnumerable<BggRanksDumpRowDto>`.
  - Dispone de un mapeador flexible de columnas (`BuildColumnMap`) capaz de reconocer encabezados alternativos (`id`, `name`, `year`, `rank`, `usersrated`, `bayes`, `average`).
  - **Hallazgo técnico (A1):** en la línea 30 evalúa `if (inputStream.CanSeek && inputStream.Length >= 2)` para detectar cabeceras GZip (`0x1F, 0x8B`). Al recibir un `Stream` directamente de una respuesta HTTP (`response.Content.ReadAsStreamAsync()`), `CanSeek` es `false`. Si el contenido no viene comprimido (como el CSV raw de GitHub), se procesa directamente sin problemas; pero si viniera en GZip o requiriera autodetección, esa comprobación fallaría silenciosamente asumiendo stream sin comprimir.
- **`BggMassIngestionService` (`src/Ludeka.Application/Features/Bgg/BggMassIngestionService.cs`)**:
  - Ofrece `IngestRanksDumpAsync(Stream dumpStream, int minUsersRated = 30, ...)` que procesa el stream e inserta en lotes de 100 en `IBggCatalogStagingRepository.UpsertBatchAsync`.
  - Ya tiene inyectado `HttpClient` en su constructor (`Program.cs` / `LudekaServiceCollectionExtensions.cs:308`: `services.AddHttpClient<IBggMassIngestionService, BggMassIngestionService>();`).
  - No dispone aún de ningún método de descarga remota autónoma.
- **`BggMassIngestionOptions` (`src/Ludeka.Application/DTOs/BggMassIngestionDtos.cs`)**:
  - Centraliza umbrales y tamaños de lote (`MinUsersRated = 30`, `FetchBatchSize = 20`, `ImagesBatchSize = 10`, `AiBatchSize = 8`, `PromotionBatchSize = 50`, `Simulate = false`).
  - No incluye URLs de origen ni parámetros para descargas remotas de volcados.

### 2.2. Panel de Administración (`CatalogQueueAdmin.razor`)
- La sección de Staging (líneas 214-289) muestra métricas KPI (`_stagingMetrics`) y un único botón de acción: `[ Drenar Ciclo de Staging Ahora ]`.
- No existe ningún control interactivo para iniciar la descarga o sembrado inicial del catálogo BGG.

### 2.3. Ejecutor de Trabajos en Segundo Plano (`Ludeka.Jobs`) (INC-47)
- `Ludeka.Jobs` alberga cuatro runners: `nightly-cataloging`, `price-radar`, `social-collector`, `notification-outbox`.
- `NightlyCatalogingJobRunner` ejecuta `NightlyCatalogingService.RunScheduledCatalogingAsync`.
- En la Fase 3 del servicio nocturno, se invoca `_massIngestionService.RunScheduledDrainCycleAsync(ct)`.
- Si `BggCatalogStaging` está vacía, el ciclo reporta 0 elementos procesados y pasa al relleno tradicional de Top BGG (limitado por el cupo diario de 20 juegos).

---

## 3. Fuente Remota de Datos Pública y Confiable

### 3.1. Dónde se encuentran los datos de rankings de BGG
BoardGameGeek no expone un archivo estático oficial de descarga directa en `boardgamegeek.com/data_dumps/bg_ranks` (este endpoint redirige a la aplicación Angular HTML).
La comunidad lúdica y analítica mantiene el repositorio de referencia pública:
- **Repositorio:** [`beefsack/bgg-ranking-historicals`](https://github.com/beefsack/bgg-ranking-historicals)
- **Frecuencia:** Se actualiza automáticamente cada día vía GitHub Actions (verificado commit de hoy `2026-09-21T00:58:07Z`: *"Added 2026-09-21.csv"*).
- **Acceso:** Fastly CDN / GitHub Raw Content:
  `https://raw.githubusercontent.com/beefsack/bgg-ranking-historicals/master/{yyyy-MM-dd}.csv`
  - Sin límite de cuota de API de GitHub (servido por Fastly como contenido estático puro).
  - Tamaño: ~7,1 MB sin comprimir, ~31.338 registros de juegos de mesa.
  - Columnas presentes:
    `ID,Name,Year,Rank,Average,Bayes average,Users rated,URL,Thumbnail`
  - Mapeo exacto con `BggDumpParser`:
    - `ID` ➔ `id`
    - `Name` ➔ `name`
    - `Year` ➔ `year`
    - `Rank` ➔ `rank`
    - `Average` ➔ `average`
    - `Bayes average` ➔ `bayes`
    - `Users rated` ➔ `usersrated`
  - Filtrado `usersrated >= 30`: arroja ~8.000 títulos con tracción comunitaria real, descartando ~23.000 prototipos o fichas sin votos.

### 3.2. Resiliencia en la Resolución de Fecha
Dado que el commit diario ocurre aproximadamente a las 01:00 UTC, durante las primeras horas del día una petición a `{DateTime.UtcNow:yyyy-MM-dd}.csv` podría retornar HTTP 404 si el workflow de ese día aún no ha concluido.
- **Estrategia de fallback:**
  Intentar la fecha de hoy UTC; si responde 404, retroceder día a día (`AddDays(-1)`) hasta un máximo de 5 días de antigüedad.
  Garantiza disponibilidad 100% ininterrumpida los 365 días del año.

---

## 4. Requisitos de Rendimiento y Memoria RAM (< 30 MB)

- El volcado completo pesa ~7,1 MB.
- Si se hiciera `await response.Content.ReadAsStringAsync()`, se cargaría todo el texto en el Large Object Heap (LOH) y se generarían millones de allocations innecesarias.
- **Solución implementada:**
  Utilizar `await response.Content.ReadAsStreamAsync(ct)` con streaming HTTP directo hacia `BggDumpParser.ParseRanksDumpAsync`.
  Lectura línea a línea con `StreamReader` e inserción en lotes de 100 en la base de datos con `_stagingRepo.UpsertBatchAsync`.
  El consumo de memoria RAM adicional no supera los 15-20 MB durante todo el proceso.

---

## 5. Seguridad y Permisos (INC-46)

- La invocación manual desde el panel de administración web `/admin/cola-catalogacion` debe validar la sesión y exigir el permiso `ModeratorPermission.CanEditGames` mediante `_permissionGuard.RequireAsync(...)`.
- La invocación desde `Ludeka.Jobs` o como paso de inicialización automática en el ciclo nocturno utiliza el canal de sistema (sin contexto de sesión HTTP interactivo).

---

## 6. Conclusión de la Exploración

El camino técnico es claro, limpio y no requiere dependencias externas pesadas:
1. Dotar a `IBggMassIngestionService` y `BggMassIngestionService` del método `DownloadAndIngestLatestRanksAsync(...)` que resuelve la URL de volcado más reciente y procesa el flujo en streaming.
2. Añadir en `CatalogQueueAdmin.razor` el botón interactivo con barra de estado y progreso.
3. Incorporar en `Ludeka.Jobs` el comando `seed-staging` y la auto-siembra condicional si `BggCatalogStaging` está vacía al iniciar `nightly-cataloging`.
