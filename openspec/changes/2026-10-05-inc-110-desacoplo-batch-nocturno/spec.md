# Especificación: Desacoplo en Segundo Plano del Batch Nocturno y Auto-Recuperación de Bloqueos en Cola

## Requerimientos Funcionales

### RF-01: Auto-Recuperación de Títulos Huérfanos en `Processing`
* **GIVEN** registros en `PendingBggImports` con estado `CatalogQueueStatus.Processing` procedentes de una ejecución anterior interrumpida abruptamente,
* **WHEN** se invoca el ciclo nocturno de catalogación (`RunScheduledCatalogingAsync`) o el método de recuperación explícito del repositorio,
* **THEN** todos los registros con estado `Processing` deben transicionar a `Pending` con `ErrorMessage = null`, quedando disponibles para `GetTopPendingAsync`.

### RF-02: Saneamiento de Bitácoras Huérfanas en `Running`
* **GIVEN** uno o más registros en `NightlyCatalogingExecutionLogs` con `Status == "Running"` con fecha anterior al inicio del ciclo actual,
* **WHEN** se inicia una nueva ejecución de catalogación nocturna,
* **THEN** dichos registros deben actualizarse a `Status = "Failed"` con el mensaje descriptivo `"Ejecución interrumpida (timeout o reinicio del host)"` y `CompletedAt = DateTimeOffset.UtcNow`.

### RF-03: Desacoplo del Lote Nocturno a Segundo Plano en el Panel Web
* **GIVEN** un moderador con permiso `CanEditGames` en `/admin/cola-catalogacion`,
* **WHEN** pulsa el botón «Ejecutar Batch Nocturno»,
* **THEN** la ejecución debe delegarse a una tarea de fondo (`Task.Run`) ejecutada en un `IServiceScope` independiente,
* **AND** el hilo del circuito SignalR no debe bloquearse en espera de la finalización del lote,
* **AND** la interfaz debe mostrar el progreso reactivo mediante sondeo periódico (`Timer`), permitiendo cancelar el proceso en segundo plano en cualquier momento mediante `CancellationTokenSource`.

### RF-04: Selector de Límite Interactivo
* **GIVEN** el formulario de disparo del lote en el panel,
* **WHEN** el moderador selecciona un cupo específico (ej. 40, 50, 100, 200, 400),
* **THEN** la invocación de `RunScheduledCatalogingAsync` debe recibir dicho valor como parámetro `customLimit`, respetándolo estrictamente en todas las fases del ciclo.
