# Especificación: autonomous-bgg-catalog-ingestion (Ingesta Masiva Autónoma de Catálogo BGG en Staging)

## Propósito

Define los requisitos funcionales, operativos, de rendimiento y de seguridad para la descarga y poblado 100% autónomo de la tabla intermedia `BggCatalogStaging` con el catálogo de BoardGameGeek (~8.000 juegos de mesa con relevancia comunitaria `usersrated >= 30`), eliminando cualquier dependencia de archivos CSV locales suministrados por el operador.

---

## Requirements

### Requirement: Descarga y resolución resiliente de dataset diario BGG

El sistema DEBE resolver y descargar automáticamente el archivo CSV diario de clasificación de BoardGameGeek desde el repositorio público `beefsack/bgg-ranking-historicals` servido a través de CDN. La URL DEBE resolverse dinámicamente a partir de una plantilla configurable (`RanksDumpUrlPattern`) utilizando la fecha actual UTC (`yyyy-MM-dd`).

Si la petición HTTP devuelve un código 404 (Not Found) debido a que el workflow diario aún no se ha ejecutado a primera hora UTC, el sistema DEBE retroceder día a día (`DateTime.UtcNow.AddDays(-1)`) hasta encontrar un volcado disponible, hasta un máximo configurable de días (`MaxFallbackDays`, por defecto 5). Si ninguna fecha dentro del margen responde con éxito, el sistema DEBE lanzar una excepción descriptiva `InvalidOperationException` y registrar el error.

Si la opción `Simulate` es verdadera, el sistema DEBE omitir la petición de red y utilizar un flujo de datos sintéticos representativos para pruebas locales y de integración.

#### Scenario: Descarga exitosa con la fecha de hoy UTC
- GIVEN una configuración de `RanksDumpUrlPattern` válida
- AND la fecha de hoy UTC contiene un volcado publicado en la fuente remota
- WHEN se solicita `DownloadAndIngestLatestRanksAsync`
- THEN el cliente HTTP realiza una petición a la URL de hoy
- AND recibe una respuesta HTTP 200 OK
- AND procesa el flujo de datos directamente hacia staging

#### Scenario: Fallback automático a fecha anterior ante 404 inicial
- GIVEN que la URL correspondiente a la fecha de hoy UTC responde con HTTP 404 Not Found
- AND la URL del día inmediatamente anterior (ayer UTC) responde con HTTP 200 OK
- WHEN se ejecuta `DownloadAndIngestLatestRanksAsync`
- THEN el cliente detecta el 404 de hoy, registra una advertencia informativa y solicita la URL de ayer
- AND procede a procesar el volcado de ayer sin interrumpir la operación

#### Scenario: Agotamiento del rango de fallback
- GIVEN que todas las fechas comprobadas desde hoy hasta `MaxFallbackDays` días atrás responden con HTTP 404 u otro error de red
- WHEN se ejecuta `DownloadAndIngestLatestRanksAsync`
- THEN la operación se cancela lanzando `InvalidOperationException`
- AND se registra en el log el detalle de las URLs comprobadas

#### Scenario: Ejecución en modo simulado
- GIVEN la opción `BggMassIngestionOptions.Simulate` configurada a `true`
- WHEN se invoca `DownloadAndIngestLatestRanksAsync`
- THEN no se realiza ninguna petición HTTP externa
- AND se procesa un conjunto sintético de títulos en memoria poblando staging de forma determinista

---

### Requirement: Streaming HTTP continuo en memoria acotada (< 30 MB RAM)

La lectura del volcado remoto DEBE realizarse en streaming directo (`HttpCompletionOption.ResponseHeadersRead` con `response.Content.ReadAsStreamAsync()`).
El sistema NO DEBE cargar el archivo completo en memoria (`ReadAsStringAsync`, `ReadAllBytes` o buffers masivos quedan estrictamente prohibidos).

`BggDumpParser` DEBE procesar correctamente flujos que no admitan posicionamiento (`CanSeek == false`), característicos de streams de respuesta HTTP. Cada registro emitido por `BggDumpParser` DEBE filtrarse aplicando el umbral de tracción comunitaria (`usersrated >= minUsersRated`, por defecto 30).

Los registros que superen el filtro DEBEN enviarse a la base de datos en lotes acotados (100 elementos) mediante `IBggCatalogStagingRepository.UpsertBatchAsync`, manteniendo el incremento de memoria RAM del proceso por debajo de 30 MB durante toda la ingesta.

#### Scenario: Procesamiento de stream HTTP no buscable (CanSeek == false)
- GIVEN un `Stream` de entrada proveniente de una conexión HTTP cuya propiedad `CanSeek` es `false`
- WHEN `BggDumpParser.ParseRanksDumpAsync` lee las líneas del flujo
- THEN el parser procesa los encabezados y cada fila sin lanzar `NotSupportedException`
- AND emite las filas correctamente estructuradas en `BggRanksDumpRowDto`

#### Scenario: Filtrado de títulos por debajo del umbral comunitario
- GIVEN un volcado que contiene títulos con diferentes cantidades de valoraciones (`usersrated`)
- WHEN se procesa con un umbral `minUsersRated = 30`
- THEN sólo aquellos títulos con `usersrated >= 30` son insertados en `BggCatalogStaging`
- AND los títulos con menos de 30 valoraciones son descartados en el pipeline de streaming

#### Scenario: Inserción por lotes e idempotencia en la persistencia
- GIVEN un lote de títulos procesados que incluye juegos ya presentes en `BggCatalogStaging` y juegos nuevos
- WHEN se ejecuta `_stagingRepo.UpsertBatchAsync`
- THEN los juegos nuevos son dados de alta con estado `FetchStatus = Pending`
- AND los juegos existentes actualizan sus estadísticas de ranking y votos sin alterar su progreso de fotos, IA ni promoción
- AND no se generan duplicados por `BggId`

---

### Requirement: Disparo interactivo desde el panel de administración con autorización

La sección de Staging en `/admin/cola-catalogacion` DEBE incluir un botón de acción interactivo para iniciar la descarga y poblado del catálogo BGG.

La ejecución interactiva DEBE exigir que el usuario cuente con sesión activa y con el permiso `ModeratorPermission.CanEditGames` a través de `ISessionPermissionGuard`. Si la sesión es anónima o el usuario no posee dicho permiso, la acción DEBE ser denegada.

Mientras la operación se encuentra en curso, el botón DEBE permanecer deshabilitado e informar visualmente el estado del proceso. Al concluir, la interfaz DEBE mostrar una notificación de éxito con el total de juegos procesados y refrescar los contadores KPI de Staging.

#### Scenario: Usuario moderador lanza la ingesta interactiva
- GIVEN un usuario autenticado con permiso `ModeratorPermission.CanEditGames` en `/admin/cola-catalogacion`
- WHEN pulsa el botón «Descargar y Poblar Catálogo BGG (~8.000 títulos)»
- THEN se activa el estado de procesamiento visual
- AND el servicio ejecuta la descarga e inserción en staging
- AND al terminar se muestra el mensaje de éxito con el recuento y se actualizan los contadores KPI

#### Scenario: Denegación de acceso para usuarios sin permiso
- GIVEN un usuario sin el permiso `ModeratorPermission.CanEditGames`
- WHEN intenta invocar el método de ingesta interactiva
- THEN el servicio aborta la llamada mediante `ISessionPermissionGuard`
- AND no se altera el estado de la base de datos

---

### Requirement: Modo autónomo y auto-siembra en Cloud Run Jobs

El ensamblado `Ludeka.Jobs` DEBE soportar la ingesta desatendida mediante dos vías:

1. **Subcomando específico `seed-staging`**: ejecutable directamente mediante `dotnet Ludeka.Jobs.dll seed-staging`, procesando la descarga y poblado autónomo de Staging con código de salida 0 ante éxito y código distinto de cero ante fallo no recuperable.
2. **Auto-siembra condicional en `nightly-cataloging`**: en el proceso de catalogación nocturna, al alcanzar la Fase 3, el servicio DEBE evaluar si `BggCatalogStaging` contiene 0 registros (`TotalInStaging == 0`). En caso afirmativo, DEBE invocar la descarga y sembrado inicial antes de ejecutar el drenaje, permitiendo que la catalogación nocturna del primer despliegue opere de forma completamente desatendida.

#### Scenario: Subcomando seed-staging en Ludeka.Jobs
- GIVEN la invocación del ejecutable con el argumento `seed-staging`
- WHEN el runner `SeedStagingJobRunner` ejecuta su lógica
- THEN se invoca la descarga autónoma por canal de sistema
- AND el proceso termina con código de salida 0 tras poblar Staging

#### Scenario: Auto-siembra condicional cuando Staging está vacío
- GIVEN una base de datos de producción recién desplegada donde `BggCatalogStaging` tiene 0 registros
- WHEN `NightlyCatalogingJobRunner` ejecuta el ciclo nocturno programado
- THEN la Fase 3 detecta que `TotalInStaging == 0`
- AND dispara automáticamente `DownloadAndIngestLatestRanksAsync`
- AND una vez poblado Staging, procede con el drenaje de los primeros lotes
