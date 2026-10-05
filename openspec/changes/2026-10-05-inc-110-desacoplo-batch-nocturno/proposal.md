# Propuesta INC-110: Desacoplo en Segundo Plano del Batch Nocturno y Auto-Recuperación de Bloqueos en Cola

## 1. Motivación y Diagnóstico
En ejecuciones de catalogación nocturna desde el panel de administración (`/admin/cola-catalogacion`), el disparo del lote de 400 títulos se ejecutaba de forma síncrona dentro del circuito interactivo de Blazor Server (`InteractiveServer`).
Debido a las pausas de cortesía hacia BGG (2,5s por bloque), la consulta de versiones XML y la síntesis con Gemini Flash, procesar cientos de títulos requiere más de 5 minutos, superando el tiempo de espera máximo de conexión (timeout de 300 segundos en Google Cloud Run y proxies HTTP).

### Defectos Identificados
1. **Muerte por timeout de circuito:** Al alcanzarse los 300s, el circuito SignalR o Cloud Run aborta la petición, matando la tarea en curso.
2. **Bitácora histórica en `Running` permanente:** Al crearse el log con `Status = "Running"` y abortarse el proceso antes de `log.Complete(...)`, la bitácora histórica y la tarjeta KPI de «Última Ejecución» quedan atascadas en `Running` indefinidamente.
3. **Bloqueo silencioso de títulos en `Processing`:** El último bloque de hasta 20 títulos queda con estado `Processing`. Al no existir recuperación de elementos huérfanos, `GetTopPendingAsync` los ignora en subsiguientes ejecuciones, dejando esos juegos en un limbo permanente.
4. **Falta de granularidad en el cupo interactivo:** El botón web asume siempre el cupo máximo diario (400) sin permitir procesar bloques menores ni ajustar la cuota restante tras una interrupción.

---

## 2. Alcance Propuesto
1. **Auto-recuperación de bloqueos huérfanos en `PendingBggImports`:**
   - Incorporar en `IPendingBggImportRepository` y `SqlitePendingBggImportRepository` el método `RecoverStaleProcessingToPendingAsync()`.
   - Al iniciar `RunScheduledCatalogingAsync`, resetear automáticamente a `Pending` cualquier registro que haya quedado en `Processing`.
2. **Saneamiento de bitácoras huérfanas en `NightlyCatalogingExecutionLogs`:**
   - Marcar como `Failed` («Interrumpido por timeout o reinicio») cualquier log previo que haya quedado en `Running` al arrancar un nuevo ciclo.
3. **Desacoplo del botón a segundo plano en `CatalogQueueAdmin.razor`:**
   - Reemplazar la llamada bloqueante en el circuito por una tarea desacoplada en segundo plano con `Task.Run` y ámbito aislado (`ScopeFactory.CreateScope()`), provista de `CancellationTokenSource`, telemetría en tiempo real y sondeo periódico idéntico al patrón ya validado de `StartContinuousDrain`.
4. **Selector interactivo de cupo del lote:**
   - Permitir al moderador ejecutar el lote con el tamaño deseado (50, 100, 200 o los 400 por defecto) para control operativo directo.
